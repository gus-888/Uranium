using System.Text;
using System.Collections.Generic;

namespace native_browser;

// ============================================================================
// Estruturas do layout
// ============================================================================
public enum PieceKind { Text, Image, Box, Break, Control, Video }

/// <summary>Estado interativo de um controle de formulário (sobrevive a re-layouts).</summary>
public class ControlState
{
    public bool Touched;        // o usuário interagiu com este controle
    public string Value = "";   // texto digitado / opção escolhida
    public bool Checked;        // checkbox/radio marcado
    public int SelectedIndex = -1; // opção do select
}

public class LayoutPiece
{
    public PieceKind Kind;
    public string Text = "";
    public ComputedStyle Style = null!;
    public float X, Y, W, H;        // posição absoluta na página
    public float Asc, Desc;        // métricas relativos à linha-base
    public float RelX;             // offset dentro da linha (antes do alinhamento)
    public bool IsSpace;
    public Image? Image;
    public bool ImageBroken;
    public string Alt = "";
    public BlockBox? Box;          // inline-block
    public string? Href;           // link resolvido (absoluto)
    public string ControlTag = "";
    public DomNode? VideoNode;     // <video>/<audio> dono da peça
    public bool VideoIsAudio;
}

public class LineBox
{
    public float Y, Height, Ascent, Width;
    public float Lx, AvailW;   // geometria da linha considerando floats vizinhos
    public List<LayoutPiece> Pieces = new();
}

public class BlockBox
{
    public DomNode? Node;
    public ComputedStyle Style = null!;
    public float X, Y, W, H;       // caixa de borda (border-box), coordenadas absolutas
    public List<BlockBox> Children = new();
    public List<LineBox> Lines = new();
    public float NaturalWidth;     // largura "shrink-to-fit" do conteúdo
    public string? Marker;         // marcador de item de lista ("•", "1.")
    public float MarkerY;
    public bool IsPositioned;      // absolute/fixed: fora do fluxo
    public bool Fixed;             // fixed: relativo ao viewport (compensa scroll na pintura)
    public int ZIndex;
    public float PosDx, PosDy;     // deslocamento relative
    public bool HasSticky;
    public float StickyTop;
    public bool ControlChrome;   // item flex/grid que é controle: pinta como controle
    public Image? BgImage;       // background-image resolvida
    public RectangleF BgRect;
    public bool BgTiled;
}

public class LayoutResult
{
    public BlockBox Root = null!;
    public float Height;
    public Color PageBackground = Color.White;
    public List<(RectangleF Rect, string Href, bool Fixed)> Links = new();
    public List<(RectangleF Rect, DomNode Node, string Kind, bool Fixed)> Controls = new();
    public List<(RectangleF Rect, DomNode Node, bool Fixed)> Videos = new();
    public Dictionary<string, float> Anchors = new(StringComparer.OrdinalIgnoreCase);
}

// ============================================================================
// Motor de layout: blocos empilhados + fluxo inline com quebra de linha
// ============================================================================
public class LayoutEngine
{
    private readonly Graphics g;
    private readonly StyleResolver styles;
    private readonly ImageLoader? images;
    private readonly Uri? baseUri;
    private readonly Dictionary<DomNode, ControlState>? inputState;
    private readonly Dictionary<(string, Font), float> measureCache = new();
    private LayoutResult result = new();
    private bool probing;   // medições de largura natural (shrink-to-fit): sem efeitos colaterais
    private float viewportW = 1280;

    public LayoutEngine(Graphics measureGraphics, StyleResolver resolver, ImageLoader? imageLoader, Uri? documentBase,
        Dictionary<DomNode, ControlState>? inputs = null)
    {
        g = measureGraphics;
        styles = resolver;
        images = imageLoader;
        baseUri = documentBase;
        inputState = inputs;
    }

    public LayoutResult Run(DomNode document, float viewportWidth)
    {
        result = new LayoutResult();
        viewportW = viewportWidth;
        var body = document.FindTag("body") ?? document;
        if (!_holderVisto)
        {
            try
            {
                foreach (var c in body.Children)
                {
                    if (c.TagName == "div" && c.GetAttr("class").Contains("html5-video-holder"))
                    {
                        _holderVisto = true;
                        var vd = c.FindTag("video");
                        var hcs = styles.Get(c);
                        var vcs = vd != null ? styles.Get(vd) : null;
                        VideoManager.Log($"layout-trace: holder pos={body.Children.IndexOf(c)} video={(vd != null ? "sim src=" + vd.GetAttr("src").Length : "não")} holder-display={hcs.Display} holder-pos={hcs.Position} holder-hidden={hcs.Hidden} video-display={(vcs != null ? vcs.Display.ToString() : "?")} video-pos={(vcs != null ? vcs.Position.ToString() : "?")}");
                    }
                }
            }
            catch { }
        }
        else
        {
            try
            {
                _runsPosHolder++;
                if (_runsPosHolder == 2 || _runsPosHolder == 40)
                {
                    DomNode? app = null;
                    void AchaApp(DomNode n)
                    {
                        if (app != null) return;
                        if (n.Type == NodeType.Element && n.TagName == "ytd-app") { app = n; return; }
                        foreach (var c in n.Children) AchaApp(c);
                        if (app == null && n.ShadowRoot != null) AchaApp(n.ShadowRoot);
                    }
                    AchaApp(document);
                    if (app != null)
                    {
                        var attrs = string.Join(",", Array.ConvertAll(app.Attributes.Keys.ToArray(), k => k + "=" + (app.Attributes[k].Length > 18 ? app.Attributes[k].Substring(0, 18) + "…" : app.Attributes[k])));
                        var acs = styles.Get(app);
                        var vholder = document.FindTag("body")?.Children.Count ?? -1;
                        VideoManager.Log($"layout-trace3 run#{_runsPosHolder}: app-attrs=[{attrs}] app-display={acs.Display} body-n={vholder}");
                    }
                    else VideoManager.Log($"layout-trace3 run#{_runsPosHolder}: sem ytd-app");
                }
            }
            catch { }
        }
        var bodyCs = styles.Get(body);
        var html = document.FindTag("html");
        var htmlCs = html != null ? styles.Get(html) : null;

        Color bg = Color.White;
        if (bodyCs.Background.A > 0) bg = bodyCs.Background;
        else if (htmlCs != null && htmlCs.Background.A > 0) bg = htmlCs.Background;
        result.PageBackground = bg;

        var root = new BlockBox { Node = document, Style = bodyCs, X = 0, Y = 0, W = viewportWidth };
        var bodyBox = LayoutBlock(body, bodyCs, viewportWidth, 0, 0, 0, viewportWidth, 0);
        root.Children.Add(bodyBox);
        root.H = bodyBox.Y + bodyBox.H;
        result.Root = root;
        result.Height = Math.Max(root.H, 1f);
        try
        {
            _resultadoRuns++;
            if (_resultadoRuns <= 6 || _resultadoRuns % 300 == 0)
                VideoManager.Log($"layout-run#{_resultadoRuns}: links={result.Links.Count} videos={result.Videos.Count} altura={result.Height:0} bodyBoxH={bodyBox.H:0}");
        }
        catch { }
        return result;
    }

    /// <summary>
    /// href herdado para os textos do bloco corrente: <a> em nível de bloco
    /// (os cards do watch) precisam propagar o link para os descendentes de
    /// texto (o caminho inline só cobre <a> dentro de linha).
    /// </summary>
    private string? _hrefCorrente;
    private static bool _holderVisto;
    private static long _runsPosHolder;
    private static long _resultadoRuns;

    private BlockBox LayoutBlock(DomNode node, ComputedStyle cs, float containingWidth, float x, float y, float cbX, float cbW, float cbY)
    {
        string? hrefAntigo = _hrefCorrente;
        bool ehLink = node.Type == NodeType.Element && node.GetAttr("href").Length > 0;
        if (ehLink)
        {
            var h = node.GetAttr("href");
            try { _hrefCorrente = ResolveUrl(h) ?? h; } catch { _hrefCorrente = h; }
        }
        try { return LayoutBlockInterno(node, cs, containingWidth, x, y, cbX, cbW, cbY); }
        finally { _hrefCorrente = hrefAntigo; }
    }

    /// <summary>Lay out um elemento em nível de bloco. (x, y) é o cursor de conteúdo do pai.</summary>
    private BlockBox LayoutBlockInterno(DomNode node, ComputedStyle cs, float containingWidth, float x, float y, float cbX, float cbW, float cbY)
    {
        bool mlAuto = cs.Margin[3].IsAuto, mrAuto = cs.Margin[1].IsAuto;
        float ml = mlAuto ? 0 : cs.Margin[3].Resolve(containingWidth);
        float mr = mrAuto ? 0 : cs.Margin[1].Resolve(containingWidth);
        float w = cs.Width?.Resolve(containingWidth) ?? (containingWidth - ml - mr);
        if (cs.MaxWidth != null) w = Math.Min(w, cs.MaxWidth.Resolve(containingWidth));
        if (cs.MinWidth != null) w = Math.Max(w, Math.Max(0, cs.MinWidth.Resolve(containingWidth)));
        w = Math.Max(0, w);

        if (mlAuto || mrAuto)
        {
            // margin:auto centraliza (ou empurra) a caixa dentro do espaço disponível
            float leftover = Math.Max(0, containingWidth - w);
            if (mlAuto && mrAuto) { ml = mr = leftover / 2f; }
            else if (mlAuto) { ml = leftover; mr = 0; }
            else { mr = leftover; ml = 0; }
        }

        var box = new BlockBox { Node = node, Style = cs, X = x + ml, Y = y, W = w };
        LayoutInside(box, containingWidth, cbX, cbW, cbY);

        if (cs.Height != null)
        {
            float h = (cs.Height.IsPercent || cs.Height.Op != LengthOp.None) ? 0 : cs.Height.Px;
            if (h > 0) box.H = Math.Max(box.H, h);
        }
        if (cs.MinHeight != null && !cs.MinHeight.IsPercent && cs.MinHeight.Op == LengthOp.None && cs.MinHeight.Px > 0)
            box.H = Math.Max(box.H, cs.MinHeight.Px);
        // relative/sticky: deslocamento só na pintura (mantém o espaço no fluxo)
        if (cs.Position == CssPosition.Relative || cs.Position == CssPosition.Sticky)
        {
            if (cs.Left != null) box.PosDx = cs.Left.Resolve(containingWidth);
            else if (cs.Right != null) box.PosDx = -cs.Right.Resolve(containingWidth);
            if (cs.Top != null) box.PosDy = cs.Top.IsPercent ? 0 : cs.Top.Resolve(0);
            else if (cs.Bottom != null) box.PosDy = -(cs.Bottom.IsPercent ? 0 : cs.Bottom.Resolve(0));
        }
        if (cs.Position == CssPosition.Sticky)
        {
            box.HasSticky = true;
            box.StickyTop = cs.Top != null && !cs.Top.IsPercent ? cs.Top.Resolve(0) : 0;
        }
        return box;
    }

    /// <summary>Processa os filhos da caixa (blocos empilhados + conteúdo inline em linhas).</summary>
    private void LayoutInside(BlockBox box, float containingWidth, float cbX, float cbW, float cbY)
    {
        var cs = box.Style;
        float bt = cs.BorderW[0], br = cs.BorderW[1], bb = cs.BorderW[2], bl = cs.BorderW[3];
        float pt = Math.Max(0, cs.Padding[0].Resolve(containingWidth));
        float pr = Math.Max(0, cs.Padding[1].Resolve(containingWidth));
        float pb = Math.Max(0, cs.Padding[2].Resolve(containingWidth));
        float pl = Math.Max(0, cs.Padding[3].Resolve(containingWidth));

        float cx = box.X + bl + pl;
        float cw = Math.Max(0, box.W - bl - pl - br - pr);
        float y = box.Y + bt + pt;

        float lastMarginBottom = 0;
        bool firstBlockChild = true;
        float natural = 0;
        var pendingInline = new List<DomNode>();
        var floats = new List<FloatBox>();
        float floatsBottom = 0;
        var positioned = new List<(DomNode node, ComputedStyle cs, float sx, float sy)>();

        // containing block para os descendentes absolutos = padding box (sem o padding,
        // só bordas), como no CSS: left/top contam a partir da borda interna
        bool iEstablish = cs.Position != CssPosition.Static;
        float kidCbX = iEstablish ? box.X + bl : cbX;
        float kidCbW = iEstablish ? Math.Max(1, box.W - bl - br) : cbW;
        float kidCbY = iEstablish ? box.Y + bt : cbY;
        float KidX(ComputedStyle s, BlockBox b) => s.Position != CssPosition.Static ? b.X : kidCbX;
        float KidW(ComputedStyle s, BlockBox b) => s.Position != CssPosition.Static ? b.W : kidCbW;
        float KidY(ComputedStyle s, BlockBox b) => s.Position != CssPosition.Static ? b.Y : kidCbY;

        float BottomOfSide(CssFloat side)
        {
            float b = 0;
            foreach (var f in floats)
                if (f.Side == side || side == CssFloat.Both || f.Side == CssFloat.Both)
                    b = Math.Max(b, f.YBottom);
            return b;
        }

        void FlushInline()
        {
            if (pendingInline.Count == 0) return;
            var builder = new LineBuilder(this, box, cs, cx, cw, y) { Floats = floats.Count > 0 ? floats : null };
            foreach (var n in pendingInline)
                WalkInline(n, styles.Get(n), builder, n.TagName == "a" ? null : _hrefCorrente);
            builder.Finish();
            box.Lines.AddRange(builder.Lines);
            y += builder.TotalHeight;
            natural = Math.Max(natural, builder.MaxLineWidth);
            pendingInline.Clear();
        }

        // flex/grid reais (Fase C): linha/coluna/wrap/gap/justify/align + grade px/%/fr
        // (NaturalWidth = conteúdo, medido pelos métodos — nunca largura cheia)
        if (box.Node != null && cs.Display == CssDisplay.Row)
        {
            if (cs.IsGrid && cs.GridCols.Count > 0)
                LayoutGridChildren(box, cx, cw, ref y, kidCbX, kidCbW, kidCbY);
            else
                LayoutFlexChildren(box, cx, cw, ref y, kidCbX, kidCbW, kidCbY);
            float fgBottom = Math.Max(y, floatsBottom);
            box.H = Math.Max(0, fgBottom - box.Y) + pb + bb;
            box.NaturalWidth += pl + pr + bl + br;
            return;
        }

        if (box.Node != null)
        {
            // SHADOW ROOT: Polymer estampa o conteúdo no shadow root do host
            // (ytd-app, ytd-page-manager, ytd-watch-flexy). Sem descer nele a
            // página inteira some do layout. Reaproveita o loop: primeiro os
            // filhos leves, depois os filhos do SR (o <slot> não é simulado).
            foreach (var child in NodeComSombra(box.Node))
            {
                var ccs = styles.Get(child);
                if (child.Type == NodeType.Element && ccs.Display == CssDisplay.None) continue;

                // absolute/fixed: fora do fluxo — posiciona depois com o containing block
                if (child.Type == NodeType.Element &&
                    (ccs.Position == CssPosition.Absolute || ccs.Position == CssPosition.Fixed))
                {
                    FlushInline();
                    positioned.Add((child, ccs, cx, y));
                    continue;
                }

                // tabelas: grade real (Fase D)
                if (child.Type == NodeType.Element &&
                    (ccs.Display == CssDisplay.Table || ccs.Display == CssDisplay.TableSection ||
                     ccs.Display == CssDisplay.TableRow || ccs.Display == CssDisplay.TableCell))
                {
                    FlushInline();
                    if (ccs.Clear != CssFloat.None)
                        y = Math.Max(y, BottomOfSide(ccs.Clear));
                    float tmt = Math.Max(0, ccs.Margin[0].Resolve(cw));
                    float tmb = Math.Max(0, ccs.Margin[2].Resolve(cw));
                    float teff = firstBlockChild ? tmt : Math.Max(tmt, lastMarginBottom);
                    var tb = LayoutFlowTable(child, ccs, cw, cx, y + teff, kidCbX, kidCbW, kidCbY);
                    y = tb.Y + tb.H + tmb;
                    lastMarginBottom = tmb;
                    firstBlockChild = false;
                    natural = Math.Max(natural, tb.W);
                    box.Children.Add(tb);
                    continue;
                }

                bool blockLike = child.Type == NodeType.Element &&
                    (ccs.Display == CssDisplay.Block || ccs.Display == CssDisplay.ListItem || ccs.Display == CssDisplay.Row);

                if (blockLike && ccs.Float != CssFloat.None && ccs.Float != CssFloat.Both)
                {
                    // ---- float: caixa fora do fluxo, encostada no lado; o texto contorna ----
                    FlushInline();

                    float maxW = Math.Max(40, cw * 0.7f);
                    float fw;
                    if (ccs.Width != null)
                    {
                        fw = Math.Min(ccs.Width.Resolve(cw), maxW);
                        var fb0 = new BlockBox { Node = child, Style = ccs, X = cx, Y = y, W = Math.Max(1, fw) };
                        LayoutInside(fb0, cw, KidX(ccs, fb0), KidW(ccs, fb0), KidY(ccs, fb0));
                        var fb = fb0;
                        fb.Marker = ccs.Display == CssDisplay.ListItem ? MakeMarker(child) : null;
                        fb.MarkerY = fb.Marker != null ? FirstLineTop(fb) : -1;
                        PositionFloat(fb, floats, cx, cw, ref floatsBottom);
                        box.Children.Add(fb);
                    }
                    else
                    {
                        // shrink-to-fit: mede com a largura máxima e refaz com a largura natural
                        bool wasProbing = probing; probing = true;
                        var probe = new BlockBox { Node = child, Style = ccs, X = cx, Y = y, W = maxW };
                        LayoutInside(probe, cw, KidX(ccs, probe), KidW(ccs, probe), KidY(ccs, probe));
                        probing = wasProbing;
                        fw = Math.Clamp(probe.NaturalWidth, 40, maxW);
                        var fb = new BlockBox { Node = child, Style = ccs, X = cx, Y = y, W = fw };
                        LayoutInside(fb, cw, KidX(ccs, fb), KidW(ccs, fb), KidY(ccs, fb));
                        fb.Marker = ccs.Display == CssDisplay.ListItem ? MakeMarker(child) : null;
                        fb.MarkerY = fb.Marker != null ? FirstLineTop(fb) : -1;
                        PositionFloat(fb, floats, cx, cw, ref floatsBottom);
                        box.Children.Add(fb);
                    }
                    continue;
                }

                // vídeo/áudio em nível de bloco: o MakeVideoPiece vive no caminho
                // inline (WalkInline) — empurra para a linha para ganhar a peça
                // (posicionável e pintável); display:block puro virava caixa vazia
                if (child.Type == NodeType.Element &&
                    (child.TagName == "video" || child.TagName == "audio"))
                {
                    bool nosso = ((child.Parent?.GetAttr("class")) ?? "").Contains("html5-video-holder");
                    if (nosso || (ccs.Position == CssPosition.Static && ccs.Float == CssFloat.None))
                    {
                        FlushInline();
                        pendingInline.Add(child);
                        continue;
                    }
                }

                if (blockLike)
                {
                    FlushInline();

                    if (ccs.Clear != CssFloat.None)
                        y = Math.Max(y, BottomOfSide(ccs.Clear));

                    float mt = Math.Max(0, ccs.Margin[0].Resolve(cw));
                    float mb = Math.Max(0, ccs.Margin[2].Resolve(cw));
                    float eff = firstBlockChild ? mt : Math.Max(mt, lastMarginBottom);
                    var cb = LayoutBlock(child, ccs, cw, cx, y + eff, kidCbX, kidCbW, kidCbY);
                    y = cb.Y + cb.H + mb;
                    lastMarginBottom = mb;
                    firstBlockChild = false;
                    natural = Math.Max(natural, cb.W);

                    if (ccs.Display == CssDisplay.ListItem)
                    {
                        cb.Marker = MakeMarker(child);
                        cb.MarkerY = FirstLineTop(cb);
                    }
                    box.Children.Add(cb);
                }
                else
                {
                    pendingInline.Add(child);
                }
            }
        }
        FlushInline();

        LayoutPositionedList(box, positioned, kidCbX, kidCbW, kidCbY, ref natural);

        // a altura do container precisa cobrir também os floats pendentes
        // (posicionados não contam — estão fora do fluxo)
        float contentBottom = Math.Max(y, floatsBottom);
        box.H = Math.Max(0, contentBottom - box.Y) + pb + bb;
        box.NaturalWidth = natural + pl + pr + bl + br;
        ResolveBgImage(box);
    }

    /// <summary>Resolve background-image (url) para a caixa: baixa, mede e posiciona.</summary>
    private void ResolveBgImage(BlockBox box)
    {
        box.BgImage = null;
        var cs = box.Style;
        if (cs.BgImageUrl.Length == 0 || box.W <= 0 || box.H <= 0) return;
        Uri? abs = null;
        try { abs = ResolveUri(cs.BgImageUrl); } catch { }
        if (abs == null) return;
        images?.Request(abs);
        var img = images?.GetLoaded(abs);
        if (img == null || img.Width <= 0 || img.Height <= 0) return;

        float dw = img.Width, dh = img.Height;
        string size = (cs.BgSize ?? "").Trim();
        if (size is "cover" or "contain")
        {
            float s = size == "cover"
                ? Math.Max(box.W / dw, box.H / dh)
                : Math.Min(box.W / dw, box.H / dh);
            if (float.IsFinite(s) && s > 0 && s < 100) { dw *= s; dh *= s; }
        }
        else if (size.Length > 0)
        {
            var parts = CssValues.SplitTopLevel(size);
            float? pw = parts.Count > 0 ? BgSizeLen(parts[0], box.W) : null;
            float? ph = parts.Count > 1 ? BgSizeLen(parts[1], box.H) : null;
            if (pw is float wpx && wpx > 0)
            {
                if (ph is float hpx && hpx > 0) { dw = wpx; dh = hpx; }
                else { dh = dh * (wpx / dw); dw = wpx; }
            }
            else if (ph is float hpx2 && hpx2 > 0) { dw = dw * (hpx2 / dh); dh = hpx2; }
        }
        if (dw <= 0 || dh <= 0 || dw > 4096 || dh > 4096) return;
        float dx = box.X + BgPos(cs.BgPosX, box.W, dw);
        float dy = box.Y + BgPos(cs.BgPosY, box.H, dh);
        box.BgImage = img;
        box.BgRect = new RectangleF(dx, dy, dw, dh);
        box.BgTiled = !cs.BgNoRepeat;
    }

    private static float? BgSizeLen(string s, float basis)
    {
        s = s.Trim().ToLowerInvariant();
        if (s == "auto") return null;
        var l = CssLength.Parse(s, 16f);
        if (l == null) return null;
        if (l.IsPercent) return Math.Max(0, l.Percent / 100f * basis);
        if (l.Op != LengthOp.None) return Math.Max(0, l.Resolve(basis));
        return Math.Max(0, l.Px);
    }

    private static float BgPos(string pos, float area, float size)
    {
        // offsets negativos permitidos (sprites: background-position: 0 -261px)
        pos = (pos ?? "").Trim().ToLowerInvariant();
        if (pos is "center" or "middle") return (area - size) / 2;
        if (pos is "right" or "bottom" or "end") return area - size;
        var l = CssLength.Parse(pos, 16f);
        if (l == null) return 0;
        if (l.IsPercent) return l.Percent / 100f * (area - size);
        return l.Resolve(0);
    }

    /// <summary>Lay out caixas absolute/fixed coletadas (fora do fluxo).</summary>
    private void LayoutPositionedList(BlockBox box, List<(DomNode node, ComputedStyle cs, float sx, float sy)> positioned, float kidCbX, float kidCbW, float kidCbY, ref float natural)
    {
        // ---- absolute/fixed: fora do fluxo, posicionados pelo containing block ----
        foreach (var (pnode, pccs, psx, psy) in positioned)
        {
            bool isFixed = pccs.Position == CssPosition.Fixed;
            float pcbX = isFixed ? 0 : kidCbX;
            float pcbW = Math.Max(1, isFixed ? viewportW : kidCbW);
            float pcbY = isFixed ? 0 : kidCbY;

            float pw;
            if (pccs.Width != null) pw = Math.Max(1, Math.Min(pccs.Width.Resolve(pcbW), pcbW));
            else
            {
                bool wasProbing = probing; probing = true;
                var pprobe = new BlockBox { Node = pnode, Style = pccs, X = psx, Y = psy, W = Math.Max(40, pcbW) };
                LayoutInside(pprobe, pcbW, pprobe.X + pccs.BorderW[3], Math.Max(1, pprobe.W - pccs.BorderW[3] - pccs.BorderW[1]), pprobe.Y + pccs.BorderW[0]);
                probing = wasProbing;
                pw = Math.Clamp(pprobe.NaturalWidth, 1f, Math.Max(40, pcbW));
            }
            float fx = psx, fy = psy;
            if (pccs.Left != null) fx = pcbX + pccs.Left.Resolve(pcbW);
            else if (pccs.Right != null) fx = pcbX + pcbW - pccs.Right.Resolve(pcbW) - pw;
            if (pccs.Top != null) fy = pcbY + (pccs.Top.IsPercent || pccs.Top.Op != LengthOp.None ? pccs.Top.Px : pccs.Top.Resolve(0));
            else if (pccs.Bottom != null && !pccs.Bottom.IsPercent && pccs.Bottom.Op == LengthOp.None)
                fy = Math.Max(psy, pcbY - pccs.Bottom.Resolve(0)); // sem altura do CB: aproxima
            var fb = new BlockBox
            {
                Node = pnode, Style = pccs, X = fx, Y = fy, W = Math.Max(1, pw),
                IsPositioned = true, Fixed = isFixed, ZIndex = pccs.ZIndex,
            };
            if (isFixed) fixedDepth++;
            LayoutInside(fb, pcbW, fb.X + pccs.BorderW[3], Math.Max(1, fb.W - pccs.BorderW[3] - pccs.BorderW[1]), fb.Y + pccs.BorderW[0]);
            if (isFixed) fixedDepth--;
            if (pccs.Height != null && !pccs.Height.IsPercent && pccs.Height.Op == LengthOp.None && pccs.Height.Px > 0)
                fb.H = Math.Max(fb.H, pccs.Height.Px);
            if (pccs.Display == CssDisplay.ListItem)
            {
                fb.Marker = MakeMarker(pnode);
                fb.MarkerY = FirstLineTop(fb);
            }
            box.Children.Add(fb);
            natural = Math.Max(natural, pw);
        }
    }
    private class FlexItem
    {
        public DomNode Node = null!;
        public ComputedStyle Style = null!;
        public float Ml, Mr, Mt, Mb;
        public float BaseW, FinalW;
        public float Grow, Shrink = 1f;
        public bool Anon;
    }

    /// <summary>
    /// Filhos para layout: os leves + os do shadow root (Polymer estampa no SR;
    /// sem isto ytd-app/page-manager/watch-flexy não aparecem).
    /// </summary>
    private static IEnumerable<DomNode> NodeComSombra(DomNode n)
    {
        foreach (var c in n.Children) yield return c;
        if (n.ShadowRoot != null)
        {
            foreach (var c in n.ShadowRoot.Children) yield return c;
        }
    }

    /// <summary>Coleta itens flex/grid: elementos visíveis + texto não-branco (anônimo).</summary>
    private void CollectItems(BlockBox box, float cw, float cx, float y, List<FlexItem> items,
        List<(DomNode node, ComputedStyle cs, float sx, float sy)> positioned)
    {
        foreach (var child in NodeComSombra(box.Node!))
        {
            var ccs = styles.Get(child);
            if (child.Type == NodeType.Text)
            {
                if (string.IsNullOrWhiteSpace(child.TextContent)) continue;
                items.Add(new FlexItem { Node = child, Style = ccs, Anon = true, Shrink = 1 });
                continue;
            }
            if (ccs.Display == CssDisplay.None) continue;
            if (ccs.Position == CssPosition.Absolute || ccs.Position == CssPosition.Fixed)
            {
                positioned.Add((child, ccs, cx, y));
                continue;
            }
            float baseW;
            if (ccs.Width != null) baseW = Math.Min(ccs.Width.Resolve(cw), cw);
            else if (ccs.FlexBasis != null && !ccs.FlexBasis.IsAuto) baseW = Math.Min(ccs.FlexBasis.Resolve(cw), cw);
            else if (child.TagName is "input" or "button" or "select" or "textarea")
            {
                // controles medem pelo tamanho natural do controle (não pelo conteúdo interno)
                bool wasProbing = probing; probing = true;
                var cprobe = MakeControlBox(child, ccs, Math.Max(1, cw));
                probing = wasProbing;
                baseW = Math.Clamp(cprobe.W, 1f, Math.Max(1, cw));
            }
            else
            {
                bool wasProbing = probing; probing = true;
                var probe = new BlockBox { Node = child, Style = ccs, X = cx, Y = y, W = Math.Max(1, cw) };
                LayoutInside(probe, cw, cx, cw, y);
                probing = wasProbing;
                baseW = Math.Clamp(probe.NaturalWidth, 1f, Math.Max(1, cw));
            }
            items.Add(new FlexItem
            {
                Node = child, Style = ccs,
                Ml = Math.Max(0, ccs.Margin[3].Resolve(cw)), Mr = Math.Max(0, ccs.Margin[1].Resolve(cw)),
                Mt = Math.Max(0, ccs.Margin[0].Resolve(cw)), Mb = Math.Max(0, ccs.Margin[2].Resolve(cw)),
                BaseW = Math.Max(1, baseW), Grow = ccs.FlexGrow, Shrink = ccs.FlexShrink,
            });
        }
        // texto anônimo: mede para a base
        foreach (var it in items)
        {
            if (!it.Anon) continue;
            var font = it.Style.GetFont();
            float w = Measure(it.Node.TextContent.Trim(), font);
            it.BaseW = Math.Clamp(w + 1, 1f, Math.Max(1, cw));
        }
    }

    /// <summary>Lay out um item já com largura final (elemento ou texto anônimo).</summary>
    private BlockBox LayoutFlexItem(BlockBox parent, FlexItem it, float x, float y, float w, float cwFlex, float cbX, float cbW, float cbY)
    {
        // controles (input/button/select/textarea) usam a caixa de controle real
        if (!it.Anon && it.Node.TagName is "input" or "button" or "select" or "textarea")
        {
            var cbox = MakeControlBox(it.Node, it.Style, Math.Max(1, w), Math.Max(1, w));
            cbox.X = x; cbox.Y = y;
            cbox.ControlChrome = true;
            parent.Children.Add(cbox);
            AddControl(new RectangleF(x, y, cbox.W, cbox.H), it.Node);
            return cbox;
        }
        if (it.Anon)
        {
            var b = new BlockBox { Node = it.Node, Style = it.Style, X = x, Y = y, W = Math.Max(1, w) };
            var lb = new LineBuilder(this, b, it.Style, x, Math.Max(1, w), y);
            WalkInline(it.Node, styles.Get(it.Node), lb, null);
            lb.Finish();
            b.Lines.AddRange(lb.Lines);
            b.H = lb.TotalHeight;
            b.NaturalWidth = lb.MaxLineWidth;
            parent.Children.Add(b);
            return b;
        }
        var cb = new BlockBox { Node = it.Node, Style = it.Style, X = x, Y = y, W = Math.Max(1, w) };
        float ikx = it.Style.Position != CssPosition.Static ? cb.X : cbX;
        float ikw = it.Style.Position != CssPosition.Static ? cb.W : cbW;
        float iky = it.Style.Position != CssPosition.Static ? cb.Y : cbY;
        // href herdado: cards <a display:flex> do watch tornam os textos clicáveis
        string? hrefAnt = _hrefCorrente;
        bool itLink = it.Node.Type == NodeType.Element && it.Node.GetAttr("href").Length > 0;
        if (itLink)
        {
            var h0 = it.Node.GetAttr("href");
            try { _hrefCorrente = ResolveUrl(h0) ?? h0; } catch { _hrefCorrente = h0; }
        }
        try { LayoutInside(cb, cwFlex, ikx, ikw, iky); }
        finally { _hrefCorrente = hrefAnt; }
        if (it.Style.Display == CssDisplay.ListItem)
        {
            cb.Marker = MakeMarker(it.Node);
            cb.MarkerY = FirstLineTop(cb);
        }
        parent.Children.Add(cb);
        return cb;
    }

    /// <summary>display:flex — linha/coluna, wrap, gap, justify, align, grow/shrink.</summary>
    private void LayoutFlexChildren(BlockBox box, float cx, float cw, ref float y, float cbX, float cbW, float cbY)
    {
        var cs = box.Style;
        var positioned = new List<(DomNode node, ComputedStyle cs, float sx, float sy)>();
        var items = new List<FlexItem>();
        CollectItems(box, cw, cx, y, items, positioned);

        if (cs.Direction == FlexDir.Column)
        {
            float colGap = (cs.GapRow ?? cs.GapCol)?.Resolve(cw) ?? 0;
            float colNatural = 0;
            float maxItem = 0;
            foreach (var it in items)
            {
                float w = it.Anon ? Math.Min(it.BaseW, cw)
                    : it.Style.Width != null ? Math.Min(it.Style.Width.Resolve(cw), cw)
                    : cs.AlignItems == CssAlign.Stretch ? Math.Max(1, cw - it.Ml - it.Mr)
                    : Math.Min(it.BaseW, Math.Max(1, cw - it.Ml - it.Mr));
                float x = cx + it.Ml;
                if (!it.Anon && cs.AlignItems == CssAlign.Center) x = cx + Math.Max(0, (cw - w) / 2);
                else if (!it.Anon && cs.AlignItems == CssAlign.End) x = cx + Math.Max(0, cw - w - it.Mr);
                var b = LayoutFlexItem(box, it, x, y + it.Mt, w, cw, cbX, cbW, cbY);
                maxItem = Math.Max(maxItem, w + it.Ml + it.Mr);
                y = b.Y + b.H + it.Mb + colGap;
            }
            if (items.Count > 0) y -= colGap;
            LayoutPositionedList(box, positioned, cbX, cbW, cbY, ref colNatural);
            box.NaturalWidth = Math.Max(maxItem, colNatural);
            return;
        }

        // ---- row ----
        float mainGap = (cs.GapCol ?? cs.GapRow)?.Resolve(cw) ?? 0;
        float crossGap = (cs.GapRow ?? cs.GapCol)?.Resolve(cw) ?? 0;
        var line = new List<FlexItem>();
        float lineW = 0; // soma baseW+ml+mr
        float lineTop = y;

        float maxUsed = 0;
        void CommitLine(bool last)
        {
            if (line.Count == 0) return;
            float used = lineW; // lineW já inclui os gaps entre itens
            float free = cw - used;
            // grow
            float totGrow = 0;
            foreach (var t in line) totGrow += t.Grow;
            if (free > 0 && totGrow > 0)
            {
                foreach (var t in line) t.FinalW = t.BaseW + free * t.Grow / totGrow;
                used = cw; free = 0;
            }
            else foreach (var t in line) t.FinalW = t.BaseW;
            // shrink (nowrap estourado)
            if (!cs.Wrap && used > cw + 1)
            {
                float totShrink = 0, totBase = 0;
                foreach (var t in line) { totShrink += t.Shrink * t.BaseW; totBase += t.BaseW; }
                if (totShrink > 0)
                {
                    float over = used - cw;
                    foreach (var t in line)
                        t.FinalW = Math.Max(1, t.BaseW - over * (t.Shrink * t.BaseW) / totShrink);
                    used = cw; free = 0;
                }
            }
            float startX = cx, between = 0;
            if (free > 0)
            {
                switch (cs.JustifyContent)
                {
                    case CssJustify.Center: startX = cx + free / 2; break;
                    case CssJustify.End: startX = cx + free; break;
                    case CssJustify.Between: between = line.Count > 1 ? free / (line.Count - 1) : 0; break;
                    case CssJustify.Around: startX = cx + free / (line.Count * 2); between = free / line.Count; break;
                    case CssJustify.Evenly: startX = cx + free / (line.Count + 1); between = free / (line.Count + 1); break;
                }
            }
            float x = startX, maxH = 0;
            var placed = new List<(FlexItem it, BlockBox b)>();
            foreach (var t in line)
            {
                var b = LayoutFlexItem(box, t, x + t.Ml, lineTop + t.Mt, t.FinalW, cw, cbX, cbW, cbY);
                placed.Add((t, b));
                x += t.Ml + t.FinalW + t.Mr + mainGap + between;
                maxH = Math.Max(maxH, b.H + t.Mt + t.Mb);
            }
            // align-items no eixo cruzado
            foreach (var (t, b) in placed)
            {
                if (cs.AlignItems == CssAlign.Stretch) b.H = Math.Max(b.H, maxH - t.Mt - t.Mb);
                else if (cs.AlignItems == CssAlign.Center) b.Y += (maxH - t.Mt - t.Mb - b.H) / 2;
                else if (cs.AlignItems == CssAlign.End) b.Y += maxH - t.Mt - t.Mb - b.H;
            }
            lineTop += maxH + (cs.Wrap && !last ? crossGap : 0);
            maxUsed = Math.Max(maxUsed, lineW);
            line.Clear(); lineW = 0;
        }

        foreach (var it in items)
        {
            float need = it.BaseW + it.Ml + it.Mr;
            if (cs.Wrap && line.Count > 0 && lineW + mainGap + need > cw + 1)
                CommitLine(false);
            lineW += (line.Count > 0 ? mainGap : 0) + need;
            line.Add(it);
        }
        CommitLine(true);
        y = lineTop;
        float flexNatural = 0;
        LayoutPositionedList(box, positioned, cbX, cbW, cbY, ref flexNatural);
        box.NaturalWidth = Math.Max(maxUsed, flexNatural);
    }

    /// <summary>display:grid — colunas px/%/fr (+repeat), linhas automáticas.</summary>
    private void LayoutGridChildren(BlockBox box, float cx, float cw, ref float y, float cbX, float cbW, float cbY)
    {
        var cs = box.Style;
        int n = cs.GridCols.Count;
        var widths = new float[n];
        float fixedSum = 0, frTotal = 0;
        for (int i = 0; i < n; i++)
        {
            var t = cs.GridCols[i];
            if (t.IsFr) frTotal += t.Fr;
            else { widths[i] = Math.Max(1, t.Len != null ? t.Len.Resolve(cw) : 0); fixedSum += widths[i]; }
        }
        float gap = (cs.GapCol ?? cs.GapRow)?.Resolve(cw) ?? 0;
        float rowGap = (cs.GapRow ?? cs.GapCol)?.Resolve(cw) ?? 0;
        float frUnit = frTotal > 0 ? Math.Max(0, (cw - fixedSum - gap * (n - 1)) / frTotal) : 0;
        for (int i = 0; i < n; i++)
            if (cs.GridCols[i].IsFr) widths[i] = Math.Max(1, frUnit * cs.GridCols[i].Fr);

        var positioned = new List<(DomNode node, ComputedStyle cs, float sx, float sy)>();
        var items = new List<FlexItem>();
        CollectItems(box, cw, cx, y, items, positioned);

        float rowTop = y, rowH = 0, xCursor = cx;
        int col = 0;
        int rowStart = box.Children.Count;
        foreach (var it in items)
        {
            float colW = widths[col];
            float w = Math.Max(1, colW - it.Ml - it.Mr);
            var b = LayoutFlexItem(box, it, xCursor + it.Ml, rowTop + it.Mt, w, cw, cbX, cbW, cbY);
            rowH = Math.Max(rowH, b.H + it.Mt + it.Mb);
            it.FinalW = w;
            xCursor += colW + gap;
            col++;
            if (col >= n)
            {
                if (cs.AlignItems == CssAlign.Stretch)
                    for (int k = rowStart; k < box.Children.Count; k++)
                        box.Children[k].H = Math.Max(box.Children[k].H, rowH);
                rowStart = box.Children.Count;
                col = 0; xCursor = cx;
                rowTop += rowH + rowGap; rowH = 0;
            }
        }
        y = rowTop + rowH;
        float gridNatural = 0;
        LayoutPositionedList(box, positioned, cbX, cbW, cbY, ref gridNatural);
        float tracksSum = 0;
        for (int i = 0; i < n; i++) tracksSum += widths[i];
        box.NaturalWidth = Math.Max(tracksSum + gap * Math.Max(0, n - 1), gridNatural);
    }

    /// <summary>Lay out tabela/seção/linha/célula em fluxo: grade com colunas px/%/auto.</summary>
    private BlockBox LayoutFlowTable(DomNode node, ComputedStyle cs, float containingWidth, float x, float y, float cbX, float cbW, float cbY)
    {
        bool mlAuto = cs.Margin[3].IsAuto, mrAuto = cs.Margin[1].IsAuto;
        float ml = mlAuto ? 0 : cs.Margin[3].Resolve(containingWidth);
        float mr = mrAuto ? 0 : cs.Margin[1].Resolve(containingWidth);
        float avail = Math.Max(1, containingWidth - (mlAuto ? 0 : ml) - (mrAuto ? 0 : mr));

        // coleta linhas: cada linha = lista de células (nó + estilo)
        var captions = new List<DomNode>();
        var rows = new List<List<(DomNode node, ComputedStyle st)>>();
        void AddRow(List<(DomNode, ComputedStyle)>? pending)
        {
            if (pending != null && pending.Count > 0) rows.Add(pending);
        }
        void CollectCells(DomNode parent, List<(DomNode, ComputedStyle)> sink)
        {
            foreach (var ch in parent.Children)
            {
                if (ch.Type == NodeType.Text) continue; // texto solto em tabela: ignora
                var s = styles.Get(ch);
                if (s.Display == CssDisplay.None) continue;
                if (ch.TagName is "col" or "colgroup") continue;
                if (ch.TagName == "caption") { captions.Add(ch); continue; }
                sink.Add((ch, s)); // td/th ou conteúdo malformado vira célula
            }
        }
        if (cs.Display == CssDisplay.Table)
        {
            List<(DomNode, ComputedStyle)>? pending = null;
            foreach (var ch in node.Children)
            {
                if (ch.Type == NodeType.Text) continue;
                var s = styles.Get(ch);
                if (s.Display == CssDisplay.None) continue;
                if (ch.TagName is "col" or "colgroup") continue;
                if (ch.TagName == "caption") { captions.Add(ch); continue; }
                if (s.Display == CssDisplay.TableSection)
                {
                    AddRow(pending); pending = null;
                    foreach (var r in ch.Children)
                    {
                        if (r.Type == NodeType.Text) continue;
                        var rs = styles.Get(r);
                        if (rs.Display == CssDisplay.None) continue;
                        if (rs.Display == CssDisplay.TableRow)
                        {
                            var cur = new List<(DomNode, ComputedStyle)>();
                            CollectCells(r, cur);
                            AddRow(cur);
                        }
                        else
                        {
                            var cur = new List<(DomNode, ComputedStyle)>();
                            CollectCells(ch, cur);
                            AddRow(cur);
                            break;
                        }
                    }
                }
                else if (s.Display == CssDisplay.TableRow)
                {
                    AddRow(pending); pending = null;
                    var cur = new List<(DomNode, ComputedStyle)>();
                    CollectCells(ch, cur);
                    AddRow(cur);
                }
                else if (ch.Type == NodeType.Element)
                {
                    pending ??= new List<(DomNode, ComputedStyle)>();
                    pending.Add((ch, s));
                }
            }
            AddRow(pending);
        }
        else if (cs.Display == CssDisplay.TableSection)
        {
            foreach (var r in node.Children)
            {
                if (r.Type == NodeType.Text) continue;
                var rs = styles.Get(r);
                if (rs.Display == CssDisplay.None) continue;
                if (rs.Display == CssDisplay.TableRow)
                {
                    var cur = new List<(DomNode, ComputedStyle)>();
                    CollectCells(r, cur);
                    AddRow(cur);
                }
                else if (r.Type == NodeType.Element)
                {
                    var cur = new List<(DomNode, ComputedStyle)>();
                    CollectCells(node, cur);
                    AddRow(cur);
                    break;
                }
            }
        }
        else if (cs.Display == CssDisplay.TableRow)
        {
            var cur = new List<(DomNode, ComputedStyle)>();
            CollectCells(node, cur);
            AddRow(cur);
        }
        else // célula solta: grade 1x1
        {
            rows.Add(new List<(DomNode, ComputedStyle)> { (node, cs) });
        }

        int ncols = 0;
        foreach (var r in rows) ncols = Math.Max(ncols, r.Count);
        if (ncols == 0 && captions.Count == 0)
            return new BlockBox { Node = node, Style = cs, X = x + ml, Y = y, W = 0, H = 0 };

        float spH = cs.CollapseBorders ? 0 : cs.BorderSpacingH?.Resolve(avail) ?? 2;
        float spV = cs.CollapseBorders ? 0 : cs.BorderSpacingV?.Resolve(avail) ?? 2;

        var explicitW = new CssLength?[Math.Max(1, ncols)];
        var naturalW = new float[Math.Max(1, ncols)];
        for (int j = 0; j < ncols; j++)
            foreach (var r in rows)
            {
                if (j >= r.Count) continue;
                if (explicitW[j] == null && r[j].st.Width != null) explicitW[j] = r[j].st.Width;
            }
        for (int j = 0; j < ncols; j++)
        {
            if (explicitW[j] != null) continue;
            float best = 40;
            foreach (var r in rows)
            {
                if (j >= r.Count) continue;
                bool wasProbing = probing; probing = true;
                var pprobe = new BlockBox { Node = r[j].node, Style = r[j].st, X = 0, Y = 0, W = Math.Max(40, avail) };
                LayoutInside(pprobe, avail, 0, avail, 0);
                probing = wasProbing;
                best = Math.Max(best, pprobe.NaturalWidth);
            }
            naturalW[j] = Math.Min(best, avail);
        }
        float tableW0 = cs.Width?.Resolve(avail) ?? avail;
        var expPx = new float[Math.Max(1, ncols)];
        float expSum = 0;
        int nAuto = 0;
        for (int j = 0; j < ncols; j++)
        {
            if (explicitW[j] != null) { expPx[j] = Math.Max(1, explicitW[j]!.Resolve(tableW0)); expSum += expPx[j]; }
            else nAuto++;
        }
        float gaps = spH * (ncols + 1);
        float autoSum = 0;
        for (int j = 0; j < ncols; j++) if (explicitW[j] == null) autoSum += naturalW[j];
        float prefTotal = expSum + autoSum + gaps;
        float tableW = cs.Width != null ? Math.Max(1, Math.Min(cs.Width.Resolve(avail), avail)) : Math.Min(avail, Math.Max(1, prefTotal));

        float bt = cs.BorderW[0], br = cs.BorderW[1], bb = cs.BorderW[2], bl = cs.BorderW[3];
        float pt = Math.Max(0, cs.Padding[0].Resolve(avail));
        float pr = Math.Max(0, cs.Padding[1].Resolve(avail));
        float pb2 = Math.Max(0, cs.Padding[2].Resolve(avail));
        float pl = Math.Max(0, cs.Padding[3].Resolve(avail));
        float contentW = Math.Max(1, tableW - bl - pl - br - pr);

        var colW = new float[Math.Max(1, ncols)];
        float remain = contentW - expSum - gaps;
        if (nAuto > 0 && remain >= autoSum)
        {
            float extra = (remain - autoSum) / nAuto;
            for (int j = 0; j < ncols; j++)
                colW[j] = explicitW[j] != null ? Math.Min(expPx[j], contentW) : naturalW[j] + extra;
        }
        else if (prefTotal > 0)
        {
            float totNat = Math.Max(1, expSum + autoSum);
            float budget = Math.Max(1, contentW - gaps);
            for (int j = 0; j < ncols; j++)
                colW[j] = Math.Max(1, ((explicitW[j] != null ? expPx[j] : naturalW[j]) / totNat) * budget);
        }
        else
        {
            for (int j = 0; j < ncols; j++) colW[j] = Math.Max(1, (contentW - gaps) / Math.Max(1, ncols));
        }

        var box = new BlockBox { Node = node, Style = cs, X = x + ml, Y = y, W = tableW };
        if (mlAuto || mrAuto)
        {
            float leftover = Math.Max(0, avail - tableW);
            box.X = x + (mlAuto && mrAuto ? leftover / 2f : mlAuto ? leftover : 0);
        }
        float ty = box.Y + bt + pt;

        // legendas primeiro (caption-side ignorado: sempre no topo)
        foreach (var cap in captions)
        {
            var ccs = styles.Get(cap);
            var cb = LayoutBlock(cap, ccs, contentW, box.X + bl + pl, ty, cbX, cbW, cbY);
            box.Children.Add(cb);
            ty = cb.Y + cb.H;
        }

        float rowTop = ty + (rows.Count > 0 ? spV : 0);
        foreach (var r in rows)
        {
            float xc = box.X + bl + pl + spH;
            float rowH = 0;
            for (int j = 0; j < ncols; j++)
            {
                if (j < r.Count)
                {
                    var (cn, cst) = r[j];
                    var cell = new BlockBox { Node = cn, Style = cst, X = xc, Y = rowTop, W = Math.Max(1, colW[j]) };
                    float ikx = cst.Position != CssPosition.Static ? cell.X : cbX;
                    float ikw = cst.Position != CssPosition.Static ? cell.W : cbW;
                    float iky = cst.Position != CssPosition.Static ? cell.Y : cbY;
                    LayoutInside(cell, contentW, ikx, ikw, iky);
                    box.Children.Add(cell);
                    rowH = Math.Max(rowH, cell.H);
                }
                xc += colW[j] + spH;
            }
            rowTop += rowH + spV;
        }
        box.H = Math.Max(0, rowTop - box.Y) + pb2 + bb;
        box.NaturalWidth = tableW + pl + pr + bl + br;
        ResolveBgImage(box);
        return box;
    }

    /// <summary>Posiciona um float no lado escolhido, empilhando abaixo de floats anteriores do mesmo lado.</summary>
    private static void PositionFloat(BlockBox fb, List<FloatBox> floats, float cx, float cw, ref float floatsBottom)
    {
        var side = fb.Style.Float;
        float stackY = 0;
        foreach (var f in floats)
            if (f.Side == side) stackY = Math.Max(stackY, f.YBottom);

        fb.Y = Math.Max(fb.Y, stackY);
        fb.X = side == CssFloat.Left ? cx : cx + cw - fb.W;
        floats.Add(new FloatBox
        {
            Side = side,
            X0 = fb.X,
            X1 = fb.X + fb.W,
            YTop = fb.Y,
            YBottom = fb.Y + fb.H,
        });
        floatsBottom = Math.Max(floatsBottom, fb.Y + fb.H);
    }

    // ---- fluxo inline -----------------------------------------------------

    private void WalkInline(DomNode node, ComputedStyle cs, LineBuilder lb, string? href)
    {
        if (cs.Hidden) return;

        if (node.Type == NodeType.Text)
        {
            EmitText(node.TextContent, cs, lb, href);
            return;
        }

        // vídeo/áudio em QUALQUER display: peça de vídeo (o MakeVideoPiece vive
        // no caminho inline; block puro virava Box sem registro — sem pintura)
        if ((node.TagName == "video" || node.TagName == "audio") && node.Type == NodeType.Element)
        {
            lb.Add(MakeVideoPiece(node, cs, lb.AvailableWidth));
            return;
        }

        switch (cs.Display)
        {
            case CssDisplay.None:
                return;
            case CssDisplay.Inline:
                if (node.TagName == "img")
                {
                    lb.Add(MakeImagePiece(node, cs, lb.AvailableWidth));
                    return;
                }
                if (node.TagName == "svg")
                {
                    lb.Add(MakeSvgPiece(node, cs, lb.AvailableWidth));
                    return;
                }
                if (node.TagName == "br")
                {
                    lb.AddBreak();
                    return;
                }
                if (node.TagName == "video" || node.TagName == "audio")
                {
                    lb.Add(MakeVideoPiece(node, cs, lb.AvailableWidth));
                    return;
                }
                {
                    string? nextHref = href;
                    if (node.TagName == "a")
                    {
                        var raw = node.GetAttr("href");
                        if (raw.Length > 0)
                            nextHref = ResolveUrl(raw) ?? href;
                        string anchorId = node.Id.Length > 0 ? node.Id : node.GetAttr("name");
                        if (anchorId.Length > 0) lb.MarkAnchorNext(anchorId);
                    }
                    else if (node.Id.Length > 0)
                    {
                        lb.MarkAnchorNext(node.Id);
                    }
                    foreach (var child in node.Children)
                        WalkInline(child, styles.Get(child), lb, nextHref);
                    break;
                }
            case CssDisplay.InlineBlock:
                {
                    var piece = LayoutInlineBlock(node, cs, lb.AvailableWidth);
                    lb.Add(piece);
                    break;
                }
            default:
                // bloco dentro de contexto inline (HTML malformado): vira atômico
                {
                    var piece = LayoutInlineBlock(node, cs, lb.AvailableWidth);
                    lb.Add(piece);
                    break;
                }
        }
    }

    private void EmitText(string text, ComputedStyle cs, LineBuilder lb, string? href)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        switch (cs.WS)
        {
            case CssWhiteSpace.Pre:
                {
                    var lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Replace("\t", "    ");
                        if (line.Length > 0) lb.AddWord(line, cs, href, false);
                        if (i < lines.Length - 1) lb.AddBreak();
                    }
                    break;
                }
            case CssWhiteSpace.PreWrap:
                {
                    var lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        EmitPreservedLine(lines[i].Replace("\t", "    "), cs, lb, href);
                        if (i < lines.Length - 1) lb.AddBreak();
                    }
                    break;
                }
            default: // Normal, NoWrap — colapsa espaços
                {
                    int i = 0, n = text.Length;
                    while (i < n)
                    {
                        // pula espaços colapsáveis (mas não NBSP)
                        bool hadSpace = false;
                        while (i < n && (text[i] == ' ' || text[i] == '\t' || text[i] == '\n'))
                        {
                            hadSpace = true;
                            i++;
                        }
                        int start = i;
                        while (i < n && text[i] != ' ' && text[i] != '\t' && text[i] != '\n') i++;
                        if (i > start)
                        {
                            if (hadSpace) lb.AddWord(" ", cs, href, true);
                            lb.AddWord(text.Substring(start, i - start), cs, href, false);
                        }
                        else if (hadSpace)
                        {
                            lb.AddWord(" ", cs, href, true);
                        }
                    }
                    break;
                }
        }
    }

    private void EmitPreservedLine(string line, ComputedStyle cs, LineBuilder lb, string? href)
    {
        int i = 0, n = line.Length;
        while (i < n)
        {
            int start = i;
            while (i < n && line[i] == ' ') i++;
            if (i > start) lb.AddWord(new string(' ', i - start), cs, href, true);
            start = i;
            while (i < n && line[i] != ' ') i++;
            if (i > start) lb.AddWord(line.Substring(start, i - start), cs, href, false);
        }
    }

    private LayoutPiece LayoutInlineBlock(DomNode node, ComputedStyle cs, float availableWidth)
    {
        BlockBox box;
        if (node.TagName is "input" or "button" or "select" or "textarea")
        {
            box = MakeControlBox(node, cs, availableWidth);
        }
        else if (cs.Width != null)
        {
            float w = Math.Min(cs.Width.Resolve(availableWidth), availableWidth);
            box = new BlockBox { Node = node, Style = cs, X = 0, Y = 0, W = Math.Max(0, w) };
            LayoutInside(box, availableWidth, cs.BorderW[3], Math.Max(1, availableWidth - cs.BorderW[3] - cs.BorderW[1]), cs.BorderW[0]);
        }
        else
        {
            // shrink-to-fit: layout uma vez com a largura disponível, mede o
            // conteúdo natural e refaz o layout com a largura final
            bool wasProbing = probing; probing = true;
            var probe = new BlockBox { Node = node, Style = cs, X = 0, Y = 0, W = availableWidth };
            LayoutInside(probe, availableWidth, 0, availableWidth, 0);
            probing = wasProbing;
            float w = Math.Clamp(probe.NaturalWidth, 1f, availableWidth);
            box = new BlockBox { Node = node, Style = cs, X = 0, Y = 0, W = w };
            LayoutInside(box, availableWidth, cs.BorderW[3], Math.Max(1, availableWidth - cs.BorderW[3] - cs.BorderW[1]), cs.BorderW[0]);
        }
        bool isControl = node.TagName is "input" or "button" or "select" or "textarea";
        // baseline: controle desce um pouco abaixo da linha (como no Chrome);
        // inline-block com texto usa a baseline da última linha
        float asc = box.H, desc = 0;
        if (isControl)
        {
            desc = Math.Max(2f, cs.FontSize * 0.2f);
            asc = Math.Max(0, box.H - desc);
        }
        else if (box.Lines.Count > 0)
        {
            var last = box.Lines[^1];
            asc = Math.Clamp(last.Y + last.Ascent - box.Y, 0, box.H);
            desc = box.H - asc;
        }
        return new LayoutPiece
        {
            Kind = isControl ? PieceKind.Control : PieceKind.Box,
            Style = cs,
            W = box.W,
            H = box.H,
            Asc = asc,
            Desc = desc,
            Box = box,
            ControlTag = node.TagName,
        };
    }

    private BlockBox MakeControlBox(DomNode node, ComputedStyle cs, float availableWidth, float? forceW = null)
    {
        string type = node.GetAttr("type").ToLowerInvariant();
        bool isButton = node.TagName == "button" || type is "button" or "submit" or "reset";
        ControlState? st = null;
        if (inputState != null && inputState.TryGetValue(node, out var s)) st = s;

        var font = cs.GetFont();
        float lineH = Ascent(font) + Descent(font);

        // checkbox/radio: quadradinho/circulinho de 14px — o rótulo é texto irmão no HTML
        if (type is "checkbox" or "radio")
        {
            return new BlockBox { Node = node, Style = cs, X = 0, Y = 0, W = 14, H = 14 };
        }

        string label;
        if (node.TagName == "input")
        {
            label = st?.Touched == true ? st.Value : node.GetAttr("value");
            if (isButton && label.Length == 0) label = type == "submit" ? "Enviar" : type == "reset" ? "Limpar" : "";
        }
        else if (node.TagName == "select")
        {
            var options = OptionTexts(node);
            int sel = st?.Touched == true ? st.SelectedIndex : InitialOptionIndex(node);
            label = sel >= 0 && sel < options.Count ? options[sel] : "";
        }
        else // button / textarea
        {
            label = st?.Touched == true ? st.Value : node.CollectText().Replace("\n", " ").Trim();
        }

        float textW = label.Length > 0 ? Measure(label.Length > 200 ? label.Substring(0, 200) : label, font) : 0;

        float minH = cs.MinHeight != null && !cs.MinHeight.IsPercent && cs.MinHeight.Op == LengthOp.None ? Math.Max(0, cs.MinHeight.Px) : 0;
        float w, h;
        if (isButton)
        {
            w = forceW ?? textW + 24;
            h = Math.Max(Math.Max(lineH + 8, 24), minH);
        }
        else if (forceW != null)
        {
            w = forceW.Value;
            h = node.TagName == "textarea" ? lineH * (int.TryParse(node.GetAttr("rows"), out int fr) && fr > 0 ? fr : 3) + 8 : lineH + 8;
        }
        else if (cs.Width != null)
        {
            w = Math.Min(cs.Width.Resolve(availableWidth), availableWidth);
            h = lineH + 8;
        }
        else if (node.TagName == "textarea")
        {
            w = 220;
            int rows = int.TryParse(node.GetAttr("rows"), out int r) && r > 0 ? r : 3;
            h = lineH * rows + 8;
        }
        else
        {
            w = Math.Max(140, textW + 14);
            h = lineH + 8;
        }
        var box = new BlockBox { Node = node, Style = cs, X = 0, Y = 0, W = w, H = h };
        ResolveBgImage(box);
        box.Lines.Add(new LineBox { Y = 0, Height = h, Ascent = 4 + Ascent(font), Width = textW });
        box.Lines[0].Pieces.Add(new LayoutPiece
        {
            Kind = PieceKind.Text,
            Text = label,
            Style = cs,
            W = textW,
            H = lineH,
            Asc = Ascent(font),
            Desc = Descent(font),
        });
        return box;
    }

    public static List<string> OptionTexts(DomNode select)
    {
        var options = new List<string>();
        foreach (var c in select.Children)
            if (c.TagName == "option") options.Add(c.CollectText().Trim());
        return options;
    }

    private static int InitialOptionIndex(DomNode select)
    {
        int i = 0, sel = 0;
        foreach (var c in select.Children)
        {
            if (c.TagName != "option") continue;
            if (c.Attributes.ContainsKey("selected")) return i;
            i++;
        }
        return sel;
    }

    private LayoutPiece MakeVideoPiece(DomNode node, ComputedStyle cs, float availW)
    {
        bool isAudio = node.TagName == "audio";
        float attrW = ParsePx(node.GetAttr("width"));
        float attrH = ParsePx(node.GetAttr("height"));

        // CSS vence atributos (width/height px explícitos); % vira largura da linha
        float w = 0, h = 0;
        try
        {
            if (cs.Width is CssLength lw && !lw.IsAuto)
                w = lw.IsPercent ? Math.Max(0, lw.Resolve(availW)) : lw.Resolve(availW);
            if (cs.Height is CssLength lh && !lh.IsAuto)
                h = lh.IsPercent ? 0 : lh.Resolve(availW);
        }
        catch { }
        if (w <= 0) w = attrW;
        if (w <= 0) w = isAudio ? 240 : 320;
        if (w > availW && availW > 40) w = availW; // max-width:100% implícito
        if (h <= 0) h = attrH;

        Image? poster = null;
        var posterSrc = node.GetAttr("poster");
        if (posterSrc.Length > 0)
        {
            try
            {
                var abs = ResolveUri(posterSrc);
                poster = abs != null ? images?.GetLoaded(abs) : null;
                images?.Request(abs);
            }
            catch { }
        }
        return new LayoutPiece { Kind = PieceKind.Video, Style = cs, W = w, H = h, Asc = h, Desc = 0, Image = poster, VideoNode = node, VideoIsAudio = isAudio };
    }

    // ---- imagens ------------------------------------------------------------

    /// <summary>Escolhe a melhor URL: picture/source srcset, srcset/sizes, data-src*, src.</summary>
    private string PickImageSource(DomNode img, float availW)
    {
        // <picture><source srcset> — primeiro source com srcset vence
        var parent = img.Parent;
        if (parent != null && parent.TagName == "picture")
        {
            foreach (var s in parent.Children)
            {
                if (s.TagName != "source") continue;
                string ss = s.GetAttr("srcset");
                if (ss.Length > 0) return PickFromSrcset(ss, availW) ?? "";
            }
        }
        string set = img.GetAttr("srcset");
        if (set.Length > 0) return PickFromSrcset(set, availW) ?? FallbackSrc(img);
        return FallbackSrc(img);
    }

    private static string FallbackSrc(DomNode img)
    {
        foreach (var a in new[] { "src", "data-src", "data-lazy-src", "data-original", "data-srcset" })
        {
            string v = img.GetAttr(a);
            if (v.Length == 0) continue;
            if (a == "data-srcset") return PickFromSrcset(v, 800) ?? "";
            return v.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        }
        return "";
    }

    private static string? PickFromSrcset(string srcset, float effW)
    {
        string? best = null, biggest = null;
        float bestW = 0, bigW = 0, bestD = 0;
        bool hasW = false;
        foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var toks = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (toks.Length == 0) continue;
            string url = toks[0];
            biggest ??= url;
            if (toks.Length == 1) { best ??= url; continue; }
            string d = toks[^1].ToLowerInvariant();
            if (d.EndsWith("w") && float.TryParse(d[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float w))
            {
                hasW = true;
                if (w > bigW) { bigW = w; biggest = url; }
                if (w >= effW && (best == null || !hasW || w < bestW)) { best = url; bestW = w; }
            }
            else if (d.EndsWith("x") && float.TryParse(d[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x))
            {
                if (x >= bestD) { bestD = x; if (x <= 1.5f) best = url; biggest = url; }
            }
        }
        if (hasW) return best ?? biggest;
        return best ?? biggest;
    }

    private LayoutPiece MakeImagePiece(DomNode node, ComputedStyle cs, float availW)
    {
        string src = PickImageSource(node, availW);
        Uri? abs = null;
        try { abs = ResolveUri(src); } catch { }

        Image? img = abs != null ? images?.GetLoaded(abs) : null;
        images?.Request(abs);

        float attrW = ParsePx(node.GetAttr("width"));
        float attrH = ParsePx(node.GetAttr("height"));
        // CSS vence atributos (como no Chrome); % resolve contra a linha
        float cssW = cs.Width != null && !cs.Width.IsAuto ? cs.Width.Resolve(availW) : -1;
        float cssH = cs.Height != null && !cs.Height.IsAuto && !cs.Height.IsPercent && cs.Height.Op == LengthOp.None ? cs.Height.Px : -1;
        float w = cssW > 0 ? cssW : attrW;
        float h = cssH > 0 ? cssH : attrH;
        if (cs.MaxWidth != null)
        {
            float mw = cs.MaxWidth.Resolve(availW);
            if (mw > 0 && (w <= 0 || w > mw))
            {
                if (img != null && w > 0) h = h > 0 ? h * (mw / w) : img.Height * (mw / img.Width);
                else if (h > 0) h = h * (mw / Math.Max(1, w));
                w = mw;
            }
        }
        if (img != null)
        {
            if (w <= 0 && h <= 0) { w = img.Width; h = img.Height; }
            else if (w <= 0) w = img.Width * (h / img.Height);
            else if (h <= 0) h = img.Height * (w / img.Width);
        }
        float ph = abs == null ? 16 : 32; // sem URL: placeholder discreto; carregando: 32
        if (w <= 0) w = h > 0 ? h : ph;
        if (h <= 0) h = ph; // placeholder enquanto carrega

        return new LayoutPiece
        {
            Kind = PieceKind.Image,
            Style = cs,
            W = w,
            H = h,
            Asc = h,
            Desc = 0,
            Image = img,
            ImageBroken = (abs == null || (images != null && images.IsFailed(abs))) && img == null,
            Alt = node.GetAttr("alt"),
        };
    }

    /// <summary>Renderiza &lt;svg&gt; inline rasterizando o XML via Svg.NET.</summary>
    private LayoutPiece MakeSvgPiece(DomNode node, ComputedStyle cs, float availW)
    {
        float attrW = ParsePx(node.GetAttr("width"));
        float attrH = ParsePx(node.GetAttr("height"));
        float cssW = cs.Width != null && !cs.Width.IsAuto ? cs.Width.Resolve(availW) : -1;
        float cssH = cs.Height != null && !cs.Height.IsAuto && !cs.Height.IsPercent && cs.Height.Op == LengthOp.None ? cs.Height.Px : -1;
        float w = cssW > 0 ? cssW : attrW;
        float h = cssH > 0 ? cssH : attrH;
        if (w <= 0 || h <= 0)
        {
            // viewBox dá a proporção (e o tamanho padrão)
            float vbW = 0, vbH = 0;
            var vb = node.GetAttr("viewBox").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (vb.Length == 4 && float.TryParse(vb[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a)
                && float.TryParse(vb[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b) && a > 0 && b > 0)
            { vbW = a; vbH = b; }
            if (w <= 0 && h <= 0) { w = vbW > 0 ? Math.Min(vbW, availW) : 24; h = vbH > 0 && vbW > 0 ? w * vbH / vbW : w; }
            else if (w <= 0) w = vbW > 0 && vbH > 0 ? h * vbW / vbH : h;
            else h = vbW > 0 && vbH > 0 ? w * vbH / vbW : w;
            w = Math.Clamp(w, 1, Math.Max(1, availW));
            h = Math.Clamp(h, 1, 2048);
        }

        Image? bmp = null;
        try
        {
            string xml = SvgRaster.Serialize(node);
            // currentColor não existe fora do navegador: usa a cor computada
            string hex = $"#{cs.Color.R:X2}{cs.Color.G:X2}{cs.Color.B:X2}";
            xml = xml.Replace("currentColor", hex).Replace("currentcolor", hex);
            bmp = SvgRaster.FromText(xml, (int)Math.Round(w), (int)Math.Round(h));
            if (bmp != null) { w = bmp.Width; h = bmp.Height; }
        }
        catch { bmp = null; }

        return new LayoutPiece
        {
            Kind = PieceKind.Image,
            Style = cs,
            W = Math.Max(1, w),
            H = Math.Max(1, h),
            Asc = Math.Max(1, h),
            Desc = 0,
            Image = bmp,
            ImageBroken = bmp == null,
            Alt = node.GetAttr("aria-label"),
        };
    }

    private static float ParsePx(string s)
    {
        s = s.Trim().TrimEnd('p', 'x', 'P', 'X');
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0 ? v : -1;
    }

    // ---- utilidades -----------------------------------------------------------

    public float Measure(string text, Font font)
    {
        var key = (text, font);
        if (measureCache.TryGetValue(key, out float w)) return w;
        var size = g.MeasureString(text, font, int.MaxValue, Renderer.TypoFormat);
        w = size.Width;
        measureCache[key] = w;
        return w;
    }

    private static readonly Dictionary<Font, (float asc, float desc)> fontMetrics = new();
    public static float Ascent(Font f)
    {
        if (fontMetrics.TryGetValue(f, out var m)) return m.asc;
        float asc, desc;
        try
        {
            var fam = f.FontFamily;
            float em = fam.GetEmHeight(f.Style);
            asc = f.Size * fam.GetCellAscent(f.Style) / em;
            desc = f.Size * fam.GetCellDescent(f.Style) / em;
        }
        catch { asc = f.Size * 0.8f; desc = f.Size * 0.25f; }
        fontMetrics[f] = (asc, desc);
        return asc;
    }

    public static float Descent(Font f)
    {
        if (fontMetrics.TryGetValue(f, out var m)) return m.desc;
        Ascent(f);
        return fontMetrics[f].desc;
    }

    /// <summary>Conta descendentes com corte antecipado (para decidir se vale medir).</summary>
    private static int CountDescendants(DomNode n, int cap = 2000)
    {
        int count = 0;
        var stack = new Stack<DomNode>();
        stack.Push(n);
        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            foreach (var c in cur.Children)
            {
                if (++count >= cap) return cap;
                stack.Push(c);
            }
        }
        return count;
    }

    private int fixedDepth;

    internal void AddLink(RectangleF rect, string href)
    {
        if (!probing) result.Links.Add((rect, href, fixedDepth > 0));
    }

    internal void AddControl(RectangleF rect, DomNode node)
    {
        if (probing) return;
        string kind = node.TagName switch
        {
            "button" => "button",
            "select" => "select",
            "textarea" => "textarea",
            _ => node.GetAttr("type").ToLowerInvariant() switch
            {
                "checkbox" => "checkbox",
                "radio" => "radio",
                "button" or "submit" or "reset" => "button",
                _ => "text",
            },
        };
        result.Controls.Add((rect, node, kind, fixedDepth > 0));
    }

    internal void AddVideo(RectangleF rect, DomNode node)
    {
        if (probing) return;
        result.Videos.Add((rect, node, fixedDepth > 0));
        try { VideoManager.Log($"add-video: w={rect.Width:0} h={rect.Height:0} y={rect.Y:0} tag={node.TagName}"); } catch { }
    }

    internal void RecordAnchor(string id, float y)
    {
        if (!probing && !result.Anchors.ContainsKey(id)) result.Anchors[id] = y;
    }

    private string? ResolveUrl(string raw) => ResolveUri(raw)?.AbsoluteUri;

    private Uri? ResolveUri(string raw)
    {
        try
        {
            if (baseUri != null) return new Uri(baseUri, raw);
            if (Uri.TryCreate(raw, UriKind.Absolute, out var u)) return u;
        }
        catch { }
        return null;
    }

    private string? MakeMarker(DomNode li)
    {
        var cs = styles.Get(li);
        switch (cs.ListStyle)
        {
            case CssListStyle.None: return null;
            case CssListStyle.Disc: return "•";
            case CssListStyle.Circle: return "○";
            case CssListStyle.Square: return "▪";
            case CssListStyle.Decimal:
                {
                    int index = 1;
                    var parent = li.Parent;
                    if (parent != null)
                    {
                        foreach (var sib in parent.Children)
                        {
                            if (ReferenceEquals(sib, li)) break;
                            if (sib.TagName == "li") index++;
                        }
                        if (parent.TagName == "ol" && int.TryParse(parent.GetAttr("start"), out int start) && start != 0)
                            index += start - 1;
                    }
                    return index.ToString() + ".";
                }
        }
        return null;
    }

    private static float FirstLineTop(BlockBox box)
    {
        if (box.Lines.Count > 0) return box.Lines[0].Y;
        foreach (var c in box.Children)
        {
            float y = FirstLineTop(c);
            if (y >= 0) return y;
        }
        return -1;
    }
}

// ============================================================================
// Construtor de linhas (word wrap, alinhamento, posicionamento das peças)
// ============================================================================
public class LineBuilder
{
    private readonly LayoutEngine engine;
    private readonly ComputedStyle blockStyle;
    private readonly float x, width, startY;
    private readonly List<LineBox> done = new();
    private readonly List<(string id, LayoutPiece piece)> anchorPieces = new();
    private LineBox cur = new();
    private float curX;
    private float ascent, descent;
    private bool lastWasSpace = true;
    private bool lastFlushWasBr;
    private string? pendingAnchor;

    public float AvailableWidth => width;
    public IReadOnlyList<LineBox> Lines => done;
    public float TotalHeight { get; private set; }
    public float MaxLineWidth { get; private set; }
    public List<FloatBox>? Floats { get; init; }

    private float curLx, curAvailW;
    private bool lineGeomSet;

    /// <summary>Geometria da linha corrente: começa depois dos floats da esquerda e
    /// termina antes dos da direita que ocupam a faixa vertical desta linha.</summary>
    private void ComputeLineGeometry()
    {
        lineGeomSet = true;
        curLx = x;
        curAvailW = width;
        if (Floats == null || Floats.Count == 0) return;

        float lineY = startY + TotalHeight;
        float lh = Math.Max(blockStyle.LineHeight, 12f);
        float left = x, right = x + width;
        foreach (var f in Floats)
        {
            if (f.YBottom <= lineY || f.YTop >= lineY + lh) continue;
            if (f.Side == CssFloat.Left) left = Math.Max(left, f.X1);
            else if (f.Side == CssFloat.Right) right = Math.Min(right, f.X0);
        }
        curLx = left;
        curAvailW = Math.Max(30f, right - left);
    }

    public LineBuilder(LayoutEngine e, BlockBox containerBox, ComputedStyle style, float contentX, float contentWidth, float startOffsetY)
    {
        engine = e;
        blockStyle = style;
        x = contentX;
        width = Math.Max(1, contentWidth);
        startY = startOffsetY;
    }

    public void MarkAnchorNext(string id) => pendingAnchor = id;

    public void AddWord(string text, ComputedStyle cs, string? href, bool isSpace)
    {
        var font = cs.GetFont();
        float w = engine.Measure(text, font);
        Add(new LayoutPiece
        {
            Kind = PieceKind.Text,
            Text = text,
            Style = cs,
            W = w,
            H = LayoutEngine.Ascent(font) + LayoutEngine.Descent(font),
            Asc = LayoutEngine.Ascent(font),
            Desc = LayoutEngine.Descent(font),
            Href = href,
            IsSpace = isSpace,
        });
    }

    public void AddBreak() => Add(new LayoutPiece { Kind = PieceKind.Break });

    public void Add(LayoutPiece piece)
    {
        if (piece.Kind == PieceKind.Break)
        {
            FlushLine(forcedByBr: true);
            return;
        }

        bool wrapAllowed = blockStyle.WS is CssWhiteSpace.Normal or CssWhiteSpace.PreWrap;

        if (!lineGeomSet) ComputeLineGeometry();

        if (piece.IsSpace)
        {
            // colapsa espaços no início da linha e espaços duplicados
            if (curX == 0 || lastWasSpace) return;
        }

        if (wrapAllowed && curX > 0 && curX + piece.W > curAvailW)
            FlushLine(forcedByBr: false);

        if (wrapAllowed && curX == 0 && piece.W > curAvailW && piece.Kind == PieceKind.Text && !piece.IsSpace)
        {
            HardSplit(piece);
            return;
        }

        Place(piece);
    }

    private void Place(LayoutPiece piece)
    {
        piece.RelX = curX;
        cur.Pieces.Add(piece);
        curX += piece.W;
        ascent = Math.Max(ascent, piece.Asc);
        descent = Math.Max(descent, piece.Desc);
        lastWasSpace = piece.IsSpace;

        if (pendingAnchor != null)
        {
            anchorPieces.Add((pendingAnchor, piece));
            pendingAnchor = null;
        }
    }

    private void HardSplit(LayoutPiece piece)
    {
        // palavra maior que a linha inteira: quebra caractere a caractere
        var font = piece.Style.GetFont();
        string text = piece.Text;
        var chunk = new StringBuilder();
        float chunkW = 0;
        foreach (char ch in text)
        {
            float cw = engine.Measure(ch.ToString(), font);
            if (chunkW + cw > curAvailW && chunk.Length > 0)
            {
                var p = ClonePiece(piece, chunk.ToString(), chunkW);
                Place(p);
                FlushLine(false);
                chunk.Clear();
                chunkW = 0;
            }
            chunk.Append(ch);
            chunkW += cw;
        }
        if (chunk.Length > 0)
            Place(ClonePiece(piece, chunk.ToString(), chunkW));
    }

    private static LayoutPiece ClonePiece(LayoutPiece src, string text, float w) => new()
    {
        Kind = src.Kind,
        Text = text,
        Style = src.Style,
        W = w,
        H = src.H,
        Asc = src.Asc,
        Desc = src.Desc,
        Href = src.Href,
        IsSpace = src.IsSpace,
    };

    private void FlushLine(bool forcedByBr)
    {
        // remove espaços no fim da linha
        while (cur.Pieces.Count > 0 && cur.Pieces[^1].IsSpace)
        {
            curX -= cur.Pieces[^1].W;
            cur.Pieces.RemoveAt(cur.Pieces.Count - 1);
        }

        if (cur.Pieces.Count == 0 && !forcedByBr && !lastFlushWasBr && done.Count == 0)
        {
            // linha vazia no começo (espaço colapsado) — ignora
            curX = 0;
            return;
        }

        float contentH = ascent + descent;
        cur.Height = Math.Max(contentH, blockStyle.LineHeight);
        // se line-height > altura do conteúdo, o extra é repartido (half-leading)
        float halfLead = (cur.Height - contentH) / 2f;
        cur.Ascent = ascent + halfLead;
        cur.Width = curX;
        cur.Lx = lineGeomSet ? curLx : x;
        cur.AvailW = lineGeomSet ? curAvailW : width;
        done.Add(cur);
        TotalHeight += cur.Height;
        MaxLineWidth = Math.Max(MaxLineWidth, curX);

        cur = new LineBox();
        curX = 0;
        ascent = descent = 0;
        lastWasSpace = true;
        lastFlushWasBr = forcedByBr;
        lineGeomSet = false;
    }

    /// <summary>Posiciona todas as linhas (alinhamento horizontal + coordenadas absolutas).</summary>
    public void Finish()
    {
        if (cur.Pieces.Count > 0) FlushLine(false);
        // remove última linha vazia (não forçada por <br>)
        if (done.Count > 0 && done[^1].Pieces.Count == 0 && !lastFlushWasBr)
        {
            TotalHeight -= done[^1].Height;
            done.RemoveAt(done.Count - 1);
        }

        float y = startY;
        foreach (var line in done)
        {
            line.Y = y;
            float offset = blockStyle.Align switch
            {
                CssTextAlign.Center => Math.Max(0, (line.AvailW - line.Width) / 2f),
                CssTextAlign.Right => Math.Max(0, line.AvailW - line.Width),
                _ => 0f,
            };
            foreach (var p in line.Pieces)
            {
                p.X = line.Lx + offset + p.RelX;
                p.Y = line.Y + line.Ascent - p.Asc;
                if (p.Href != null && !p.IsSpace)
                    engine.AddLink(new RectangleF(p.X, p.Y, p.W, p.H), p.Href);
                if (p.Kind == PieceKind.Control && p.Box?.Node != null)
                    engine.AddControl(new RectangleF(p.X, p.Y, p.W, p.H), p.Box.Node);
                if (p.Kind == PieceKind.Video && p.VideoNode != null)
                    engine.AddVideo(new RectangleF(p.X, p.Y, p.W, p.H), p.VideoNode);
            }
            y += line.Height;
        }
        TotalHeight = y - startY;

        foreach (var (id, piece) in anchorPieces)
            engine.RecordAnchor(id, piece.Y);
    }
}
