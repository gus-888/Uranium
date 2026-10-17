using System.Globalization;

namespace native_browser;

public enum CssDisplay { Block, Inline, InlineBlock, ListItem, Row, Table, TableSection, TableRow, TableCell, None }
public enum CssWhiteSpace { Normal, Pre, NoWrap, PreWrap }
public enum CssTextAlign { Left, Center, Right }
public enum CssListStyle { None, Disc, Circle, Square, Decimal }
public enum CssFloat { None, Left, Right, Both }
public enum CssPosition { Static, Relative, Absolute, Fixed, Sticky }
public enum CssOverflow { Visible, Hidden, Auto, Scroll }
public enum FlexDir { Row, Column }
public enum CssJustify { Start, Center, End, Between, Around, Evenly }
public enum CssAlign { Stretch, Start, Center, End }

public class GridTrack
{
    public bool IsFr;
    public float Fr;
    public CssLength? Len;
}

public class FloatBox
{
    public CssFloat Side;
    public float X0, X1;    // faixa horizontal ocupada
    public float YTop, YBottom;
}

/// <summary>Estilo final calculado para um nó (após cascade + herança).</summary>
public class ComputedStyle
{
    public CssDisplay Display = CssDisplay.Inline;
    public Color Color = Color.Black;
    public Color Background = Color.Transparent;
    public string FontFamily = "Segoe UI";
    public float FontSize = 16f;
    public bool Bold, Italic;
    public bool Underline, Strike;
    public CssTextAlign Align = CssTextAlign.Left;
    public CssWhiteSpace WS = CssWhiteSpace.Normal;
    public float LineHeight = 19f;
    public CssLength[] Margin = NewLengths();    // topo, direita, baixo, esquerda
    public CssLength[] Padding = NewLengths();
    public float[] BorderW = new float[4];
    public Color[] BorderC = { Color.Black, Color.Black, Color.Black, Color.Black };
    public CssLength? Width, Height, MaxWidth;
    public CssLength? MinWidth, MinHeight;
    public CssListStyle ListStyle = CssListStyle.Disc;
    public string TagName = "";
    public bool Hidden;   // visibility:hidden ou opacity:0 — ocupa layout, mas não pinta
    public CssFloat Float = CssFloat.None;
    public CssFloat Clear = CssFloat.None;
    public float Opacity = 1f;
    public CssAnimation? Animation;
    public Dictionary<string, string> Vars = new();
    public CssPosition Position = CssPosition.Static;
    public CssLength? Top, Right, Bottom, Left;
    public int ZIndex;
    public bool ZAuto = true;
    public CssOverflow OverflowX = CssOverflow.Visible, OverflowY = CssOverflow.Visible;
    public bool Clip => OverflowX != CssOverflow.Visible || OverflowY != CssOverflow.Visible;
    public bool IsGrid;
    public FlexDir Direction = FlexDir.Row;
    public bool Wrap;
    public CssJustify JustifyContent = CssJustify.Start;
    public CssAlign AlignItems = CssAlign.Stretch;
    public CssLength? GapRow, GapCol;
    public CssLength? BorderSpacingH, BorderSpacingV;
    public bool CollapseBorders;
    public CssLength?[] BorderRadius = new CssLength?[4]; // TL TR BR BL
    public bool HasRadius => BorderRadius[0] != null || BorderRadius[1] != null || BorderRadius[2] != null || BorderRadius[3] != null;
    public string BgImageUrl = "";       // background-image: url() (primeira camada)
    public bool BgNoRepeat = true;      // repeat só se explícito
    public string BgPosX = "left", BgPosY = "top";
    public string BgSize = "";          // "", cover, contain ou "WxH"
    public float FlexGrow, FlexShrink = 1f;
    public CssLength? FlexBasis;
    public List<GridTrack> GridCols = new();

    private static CssLength[] NewLengths() => new[] { new CssLength(), new CssLength(), new CssLength(), new CssLength() };

    /// <summary>Cópia rasa para overrides de animação (arrays copiados, comprimentos compartilhados).</summary>
    public ComputedStyle Clone() => new()
    {
        Display = Display, Color = Color, Background = Background,
        FontFamily = FontFamily, FontSize = FontSize, Bold = Bold, Italic = Italic,
        Underline = Underline, Strike = Strike, Align = Align, WS = WS, LineHeight = LineHeight,
        Margin = (CssLength[])Margin.Clone(), Padding = (CssLength[])Padding.Clone(),
        BorderW = (float[])BorderW.Clone(), BorderC = (Color[])BorderC.Clone(),
        Width = Width, Height = Height, MaxWidth = MaxWidth,
        MinWidth = MinWidth, MinHeight = MinHeight,
        ListStyle = ListStyle, TagName = TagName, Hidden = Hidden,
        Float = Float, Clear = Clear, Opacity = Opacity, Animation = Animation,
        Vars = Vars, Position = Position, Top = Top, Right = Right, Bottom = Bottom, Left = Left,
        ZIndex = ZIndex, ZAuto = ZAuto, OverflowX = OverflowX, OverflowY = OverflowY,
        IsGrid = IsGrid, Direction = Direction, Wrap = Wrap,
        JustifyContent = JustifyContent, AlignItems = AlignItems,
        GapRow = GapRow, GapCol = GapCol, FlexGrow = FlexGrow, FlexShrink = FlexShrink,
        FlexBasis = FlexBasis, GridCols = GridCols,
        BorderSpacingH = BorderSpacingH, BorderSpacingV = BorderSpacingV,
        CollapseBorders = CollapseBorders,
        BorderRadius = (CssLength?[])BorderRadius.Clone(),
        BgImageUrl = BgImageUrl, BgNoRepeat = BgNoRepeat,
        BgPosX = BgPosX, BgPosY = BgPosY, BgSize = BgSize,
    };

    private Font? font;
    public Font GetFont()
    {
        if (font != null) return font;
        string family = FontLoader.Resolve(FontFamily) ?? MapFontFamily(FontFamily);
        var style = FontStyle.Regular;
        if (Bold) style |= FontStyle.Bold;
        if (Italic) style |= FontStyle.Italic;
        try { font = new Font(family, FontSize, style, GraphicsUnit.Pixel); }
        catch { font = new Font("Segoe UI", FontSize, style, GraphicsUnit.Pixel); }
        return font;
    }

    private static string MapFontFamily(string css)
    {
        css = css.ToLowerInvariant();
        if (css.Contains("mono") || css.Contains("consol") || css.Contains("courier")) return "Consolas";
        if (css.Contains("serif") && !css.Contains("sans")) return css.Contains("georgia") ? "Georgia" : "Times New Roman";
        return "Segoe UI"; // sans-serif genérico e a maioria das fontes modernas
    }

    /// <summary>Copia as propriedades herdáveis a partir do estilo do pai.</summary>
    public static ComputedStyle Inherit(ComputedStyle parent) => new()
    {
        Color = parent.Color,
        FontFamily = parent.FontFamily,
        FontSize = parent.FontSize,
        Bold = parent.Bold,
        Italic = parent.Italic,
        Underline = parent.Underline,
        Strike = parent.Strike,
        Align = parent.Align,
        WS = parent.WS,
        LineHeight = parent.LineHeight,
        ListStyle = parent.ListStyle,
        Vars = new Dictionary<string, string>(parent.Vars),
    };
}

/// <summary>Aplica a cascade (UA → autor → style inline) e calcula estilos computados.</summary>
public class StyleResolver
{
    private readonly List<CssRule> rules;
    private readonly Dictionary<DomNode, ComputedStyle> cache = new();
    public float ViewportW = 1280, ViewportH = 800, RootPx = 16f;

    public StyleResolver(List<CssRule> cssRules, float viewportW = 1280, float viewportH = 800)
    {
        ViewportW = viewportW; ViewportH = viewportH;
        // origem (UA < autor), depois especificidade, depois ordem de aparecimento
        rules = cssRules
            .OrderBy(r => r.Origin)
            .ThenBy(r => r.Selectors.Max(s => s.Specificity()))
            .ThenBy(r => r.Order)
            .ToList();
    }

    public ComputedStyle Get(DomNode n)
    {
        if (cache.TryGetValue(n, out var cached)) return cached;
        // Nó criado pelo JS DEPOIS do Resolve inicial (SPA/fallback): computa
        // sob demanda subindo ao ancestral mais próximo já estilizado — um
        // KeyNotFoundException aqui derruba o layout inteiro (links=0).
        var host = n.Parent ?? (n.Type == NodeType.Element ? null : null);
        ComputedStyle parentCs;
        try { parentCs = host != null ? Get(host) : new ComputedStyle(); }
        catch { parentCs = new ComputedStyle(); }
        var made = n.Type == NodeType.Text ? ComputedStyle.Inherit(parentCs) : ComputeElement(n, parentCs);
        cache[n] = made;
        return made;
    }

    public bool TryGet(DomNode n, out ComputedStyle? cs) => cache.TryGetValue(n, out cs);

    /// <summary>Substitui o estilo em cache (usado por overrides de animação).</summary>
    public void SetOverride(DomNode n, ComputedStyle cs) => cache[n] = cs;

    public void Resolve(DomNode root)
    {
        RootPx = 16f;
        var initial = new ComputedStyle();
        Walk(root, initial);
    }

    private void Walk(DomNode n, ComputedStyle parent)
    {
        ComputedStyle cs;
        if (n.Type == NodeType.Text)
        {
            cs = ComputedStyle.Inherit(parent);
        }
        else
        {
            cs = ComputeElement(n, parent);
            if (n.TagName == "html")
                RootPx = cs.FontSize;
        }
        cache[n] = cs;
        if (cs.Display != CssDisplay.None)
        {
            foreach (var child in n.Children)
                Walk(child, cs);
            // SHADOW ROOT: conteúdo estampado pelo Polymer (ou importado) precisa
            // de estilo computado também — sem isto o layout não pinta nada
            if (n.ShadowRoot != null)
                Walk(n.ShadowRoot, cs);
        }
    }

    private ComputedStyle ComputeElement(DomNode n, ComputedStyle parent)
    {
        var decls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
            if (rule.Matches(n))
                foreach (var kv in rule.Declarations)
                    decls[kv.Key] = kv.Value;

        if (n.Attributes.TryGetValue("style", out var inline))
            foreach (var kv in CssParser.ParseDeclarations(inline))
                decls[kv.Key] = kv.Value;

        var cs = ComputedStyle.Inherit(parent);
        cs.TagName = n.TagName;

        // ---- custom properties (--*): herda + resolve var() entre si ----
        {
            var merged = new Dictionary<string, string>(parent.Vars, StringComparer.Ordinal);
            foreach (var kv in decls)
                if (kv.Key.StartsWith("--")) merged[kv.Key] = kv.Value;
            var resolvedVars = new Dictionary<string, string>(parent.Vars, StringComparer.Ordinal);
            foreach (var kv in decls)
            {
                if (!kv.Key.StartsWith("--")) continue;
                string prop = kv.Key;
                string raw = kv.Value;
                string? r = CssValues.ResolveAllVars(raw, nm =>
                {
                    if (nm == prop) return parent.Vars.TryGetValue(nm, out var pv) ? pv : null;
                    return merged.TryGetValue(nm, out var vv) ? vv : null;
                });
                if (r != null) resolvedVars[prop] = r;
            }
            cs.Vars = resolvedVars;
        }
        string? ResolveVal(string v)
        {
            if (v.IndexOf("var(", StringComparison.OrdinalIgnoreCase) < 0) return v;
            return CssValues.ResolveAllVars(v, nm => cs.Vars.TryGetValue(nm, out var vv) ? vv : null);
        }

        // posicionamento: Fase B trata absolute/fixed como fora do fluxo posicionado
        // (antes era display:none e sumia com dropdowns/headers)
        if (decls.TryGetValue("position", out var posRaw) && ResolveVal(posRaw) is string posv)
        {
            var p = posv.Trim().ToLowerInvariant();
            // valor resolvido pode vir com "!important" residual ou espaços: pega 1a palavra
            p = p.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            cs.Position = p switch
            {
                "relative" => CssPosition.Relative,
                "absolute" => CssPosition.Absolute,
                "fixed" => CssPosition.Fixed,
                "sticky" => CssPosition.Sticky,
                _ => CssPosition.Static,
            };
        }
        if (decls.TryGetValue("visibility", out var visRaw) && ResolveVal(visRaw) is string vis)
        {
            var v = vis.Trim().ToLowerInvariant();
            if (v == "hidden" || v == "collapse") cs.Hidden = true;
        }
        if (decls.TryGetValue("opacity", out var opRaw) && ResolveVal(opRaw) is string op)
        {
            if (CssValues.TryNumber(op) is float ov) cs.Opacity = Math.Clamp(ov, 0f, 1f);
            if (cs.Opacity < 0.01f) cs.Hidden = true;
        }

        // font-size primeiro: em/% de outras propriedades dependem do tamanho da própria fonte
        float ownEm = parent.FontSize;
        if (decls.TryGetValue("font-size", out var fsValRaw) && ResolveVal(fsValRaw) is string fsVal)
        {
            if (IsKeyword(fsVal, out var kw))
            {
                if (kw == "inherit") { cs.FontSize = parent.FontSize; ownEm = cs.FontSize; }
            }
            else
            {
                float? fs = ParseFontSize(fsVal, parent.FontSize, ViewportW, ViewportH, RootPx);
                if (fs != null) { cs.FontSize = Math.Clamp(fs.Value, 3f, 200f); ownEm = cs.FontSize; }
            }
        }
        cs.LineHeight = cs.FontSize * 1.2f;

        CssLength? Len(string v, float em) => CssLength.Parse(v, em, ViewportW, ViewportH, RootPx);

        foreach (var (prop, rawVal) in decls)
        {
            if (prop.StartsWith("--")) continue;
            string? resolved = ResolveVal(rawVal);
            if (resolved == null) continue; // var() sem valor nem fallback: declaração inválida
            string val = resolved.Trim();
            if (IsKeyword(val, out var keyword))
            {
                ApplyKeyword(cs, parent, prop, keyword);
                continue;
            }
            switch (prop)
            {
                case "display":
                    switch (val.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault())
                    {
                        case "block": case "flow-root": cs.Display = CssDisplay.Block; cs.IsGrid = false; break;
                        case "table": cs.Display = CssDisplay.Table; cs.IsGrid = false; break;
                        case "table-header-group": case "table-row-group": case "table-footer-group": cs.Display = CssDisplay.TableSection; break;
                        case "table-row": cs.Display = CssDisplay.TableRow; break;
                        case "table-cell": cs.Display = CssDisplay.TableCell; break;
                        case "table-column": case "table-column-group": case "table-caption": cs.Display = CssDisplay.Block; break;
                        case "flex": case "inline-flex": cs.Display = CssDisplay.Row; cs.IsGrid = false; break;
                        case "grid": case "inline-grid": cs.Display = CssDisplay.Row; cs.IsGrid = true; break;
                        case "inline": cs.Display = CssDisplay.Inline; break;
                        case "inline-block": case "inline-table": cs.Display = CssDisplay.InlineBlock; break;
                        case "list-item": cs.Display = CssDisplay.ListItem; break;
                        case "none": cs.Display = CssDisplay.None; break;
                    }
                    break;
                case "color":
                    { var c = CssColors.Parse(val); if (c != null) cs.Color = c.Value; }
                    break;
                case "background-color":
                    { var c = CssColors.Parse(val); if (c != null) cs.Background = c.Value; }
                    break;
                case "background":
                {
                    string? url = ExtractUrl(val);
                    if (url != null) { cs.BgImageUrl = url; ParseBgRest(val, cs, ownEm); }
                    if (!val.Contains("url(")) { var c = CssColors.Parse(val); if (c != null) cs.Background = c.Value; }
                    else { var c = CssColors.Parse(StripUrls(val)); if (c != null) cs.Background = c.Value; }
                    break;
                }
                case "background-image":
                {
                    string? url = ExtractUrl(val);
                    cs.BgImageUrl = url ?? "";
                    if (val.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) cs.BgImageUrl = "";
                    break;
                }
                case "background-repeat":
                {
                    string t = val.Trim().ToLowerInvariant();
                    cs.BgNoRepeat = t is "no-repeat" or "repeat-x" or "repeat-y";
                    break;
                }
                case "background-position":
                {
                    var tp = CssValues.SplitTopLevel(val.Trim().ToLowerInvariant());
                    if (tp.Count >= 1) cs.BgPosX = tp[0];
                    if (tp.Count >= 2) cs.BgPosY = tp[1];
                    break;
                }
                case "background-size":
                    cs.BgSize = val.Trim().ToLowerInvariant();
                    break;
                case "font-family":
                    cs.FontFamily = BestFamily(val);
                    break;
                case "font-weight":
                    cs.Bold = val is "bold" or "bolder" || (CssValues.TryNumber(val) >= 600f);
                    break;
                case "font-style":
                    cs.Italic = val is "italic" or "oblique";
                    break;
                case "text-decoration" or "text-decoration-line":
                    cs.Underline = val.Contains("underline");
                    cs.Strike = val.Contains("line-through");
                    break;
                case "text-align":
                    cs.Align = val switch
                    {
                        "center" => CssTextAlign.Center,
                        "right" or "end" => CssTextAlign.Right,
                        _ => CssTextAlign.Left,
                    };
                    break;
                case "white-space":
                    cs.WS = val switch
                    {
                        "pre" => CssWhiteSpace.Pre,
                        "nowrap" => CssWhiteSpace.NoWrap,
                        "pre-wrap" or "pre-line" => CssWhiteSpace.PreWrap,
                        _ => CssWhiteSpace.Normal,
                    };
                    break;
                case "line-height":
                    {
                        if (val == "normal") { cs.LineHeight = cs.FontSize * 1.2f; break; }
                        var len = Len(val, cs.FontSize);
                        if (len != null)
                        {
                            float lh = len.IsPercent ? len.Percent / 100f * cs.FontSize : len.Resolve(0);
                            if (len.Op != LengthOp.None) lh = len.Resolve(cs.FontSize);
                            cs.LineHeight = Math.Max(4f, lh > 0 ? lh : cs.FontSize * 1.2f);
                        }
                        else if (CssValues.TryNumber(val) is float mult) cs.LineHeight = mult * cs.FontSize;
                    }
                    break;
                case "margin": ApplyBox(cs.Margin, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "padding": ApplyBox(cs.Padding, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "margin-top": SetSide(cs.Margin, 0, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "margin-right": SetSide(cs.Margin, 1, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "margin-bottom": SetSide(cs.Margin, 2, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "margin-left": SetSide(cs.Margin, 3, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "padding-top": SetSide(cs.Padding, 0, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "padding-right": SetSide(cs.Padding, 1, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "padding-bottom": SetSide(cs.Padding, 2, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "padding-left": SetSide(cs.Padding, 3, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border":
                    { var (w, col) = ParseBorder(val, ownEm, ViewportW, ViewportH, RootPx); for (int i = 0; i < 4; i++) { cs.BorderW[i] = w; if (col != null) cs.BorderC[i] = col.Value; } }
                    break;
                case "border-top": SetBorderSide(cs, 0, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-right": SetBorderSide(cs, 1, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-bottom": SetBorderSide(cs, 2, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-left": SetBorderSide(cs, 3, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-width":
                    { var lens = Split4(val); for (int i = 0; i < 4 && i < lens.Count; i++) { var l = Len(lens[i], ownEm); if (l != null) cs.BorderW[i] = Math.Max(0, l.Resolve(0)); } }
                    break;
                case "border-top-width": SetBorderWidth(cs, 0, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-right-width": SetBorderWidth(cs, 1, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-bottom-width": SetBorderWidth(cs, 2, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-left-width": SetBorderWidth(cs, 3, val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-color":
                    { var cols = Split4(val); for (int i = 0; i < 4 && i < cols.Count; i++) { var c = CssColors.Parse(cols[i]); if (c != null) cs.BorderC[i] = c.Value; } }
                    break;
                case "border-top-color": { var c = CssColors.Parse(val); if (c != null) cs.BorderC[0] = c.Value; } break;
                case "border-right-color": { var c = CssColors.Parse(val); if (c != null) cs.BorderC[1] = c.Value; } break;
                case "border-bottom-color": { var c = CssColors.Parse(val); if (c != null) cs.BorderC[2] = c.Value; } break;
                case "border-left-color": { var c = CssColors.Parse(val); if (c != null) cs.BorderC[3] = c.Value; } break;
                case "width": cs.Width = Len(val, ownEm); break;
                case "height": cs.Height = Len(val, ownEm); break;
                case "max-width": cs.MaxWidth = Len(val, ownEm); break;
                case "min-width": cs.MinWidth = Len(val, ownEm); break;
                case "min-height": cs.MinHeight = Len(val, ownEm); break;
                case "top": cs.Top = Len(val, ownEm); break;
                case "right": cs.Right = Len(val, ownEm); break;
                case "bottom": cs.Bottom = Len(val, ownEm); break;
                case "left": cs.Left = Len(val, ownEm); break;
                case "position":
                    cs.Position = val.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() switch
                    {
                        "relative" => CssPosition.Relative,
                        "absolute" => CssPosition.Absolute,
                        "fixed" => CssPosition.Fixed,
                        "sticky" => CssPosition.Sticky,
                        _ => CssPosition.Static,
                    };
                    break;
                case "z-index":
                    if (val.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)) { cs.ZIndex = 0; cs.ZAuto = true; }
                    else if (int.TryParse(val.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int zi)) { cs.ZIndex = zi; cs.ZAuto = false; }
                    break;
                case "overflow": cs.OverflowX = cs.OverflowY = ParseOverflow(val); break;
                case "overflow-x": cs.OverflowX = ParseOverflow(val); break;
                case "overflow-y": cs.OverflowY = ParseOverflow(val); break;
                case "flex-direction":
                    cs.Direction = val.Trim().ToLowerInvariant().StartsWith("column") ? FlexDir.Column : FlexDir.Row;
                    break;
                case "flex-wrap":
                    cs.Wrap = val.Trim().ToLowerInvariant().StartsWith("wrap");
                    break;
                case "gap":
                {
                    var gp = CssValues.SplitTopLevel(val);
                    float ge = ownEm;
                    if (gp.Count == 1) cs.GapRow = cs.GapCol = CssLength.Parse(gp[0], ge, ViewportW, ViewportH, RootPx);
                    else if (gp.Count >= 2) { cs.GapRow = CssLength.Parse(gp[0], ge, ViewportW, ViewportH, RootPx); cs.GapCol = CssLength.Parse(gp[1], ge, ViewportW, ViewportH, RootPx); }
                    break;
                }
                case "row-gap": cs.GapRow = Len(val, ownEm); break;
                case "column-gap": cs.GapCol = Len(val, ownEm); break;
                case "justify-content":
                    cs.JustifyContent = val.Trim().ToLowerInvariant() switch
                    {
                        "center" => CssJustify.Center,
                        "flex-end" or "end" or "right" => CssJustify.End,
                        "space-between" => CssJustify.Between,
                        "space-around" => CssJustify.Around,
                        "space-evenly" => CssJustify.Evenly,
                        _ => CssJustify.Start,
                    };
                    break;
                case "align-items":
                    cs.AlignItems = val.Trim().ToLowerInvariant() switch
                    {
                        "flex-start" or "start" => CssAlign.Start,
                        "center" => CssAlign.Center,
                        "flex-end" or "end" => CssAlign.End,
                        _ => CssAlign.Stretch,
                    };
                    break;
                case "flex":
                {
                    var ft = CssValues.SplitTopLevel(val.Trim().ToLowerInvariant());
                    if (ft.Count == 1 && ft[0] == "none") { cs.FlexGrow = 0; cs.FlexShrink = 0; }
                    else if (ft.Count == 1 && ft[0] == "auto") { cs.FlexGrow = 1; cs.FlexShrink = 1; }
                    else
                    {
                        int numSeen = 0;
                        foreach (var t in ft)
                        {
                            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
                            {
                                if (numSeen == 0) cs.FlexGrow = num;
                                else if (numSeen == 1) cs.FlexShrink = num;
                                numSeen++;
                            }
                            else { var b = Len(t, ownEm); if (b != null) cs.FlexBasis = b; }
                        }
                    }
                    break;
                }
                case "flex-grow":
                    if (float.TryParse(val.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fg)) cs.FlexGrow = fg;
                    break;
                case "flex-shrink":
                    if (float.TryParse(val.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fs)) cs.FlexShrink = fs;
                    break;
                case "flex-basis": cs.FlexBasis = Len(val, ownEm); break;
                case "grid-template-columns": cs.GridCols = ParseGridTracks(val, ownEm, ViewportW, ViewportH, RootPx); break;
                case "border-spacing":
                {
                    var sp = CssValues.SplitTopLevel(val);
                    if (sp.Count >= 1) cs.BorderSpacingH = cs.BorderSpacingV = Len(sp[0], ownEm);
                    if (sp.Count >= 2) cs.BorderSpacingV = Len(sp[1], ownEm);
                    break;
                }
                case "border-collapse":
                    cs.CollapseBorders = val.Trim().ToLowerInvariant().StartsWith("collapse");
                    break;
                case "border-radius":
                {
                    // ignora a parte elíptica após '/'; 1-4 valores
                    string first = val.Split('/', 2)[0];
                    var parts = CssValues.SplitTopLevel(first);
                    CssLength?[] rr = new CssLength?[4];
                    if (parts.Count == 1) rr[0] = rr[1] = rr[2] = rr[3] = Len(parts[0], ownEm);
                    else if (parts.Count == 2) { rr[0] = rr[2] = Len(parts[0], ownEm); rr[1] = rr[3] = Len(parts[1], ownEm); }
                    else if (parts.Count == 3) { rr[0] = Len(parts[0], ownEm); rr[1] = rr[3] = Len(parts[1], ownEm); rr[2] = Len(parts[2], ownEm); }
                    else if (parts.Count >= 4) { rr[0] = Len(parts[0], ownEm); rr[1] = Len(parts[1], ownEm); rr[2] = Len(parts[2], ownEm); rr[3] = Len(parts[3], ownEm); }
                    cs.BorderRadius = rr;
                    break;
                }
                case "list-style-type":
                    cs.ListStyle = val switch
                    {
                        "none" => CssListStyle.None,
                        "circle" => CssListStyle.Circle,
                        "square" => CssListStyle.Square,
                        "decimal" or "decimal-leading-zero" => CssListStyle.Decimal,
                        _ => CssListStyle.Disc,
                    };
                    break;
                case "list-style":
                    cs.ListStyle = val.Contains("none") ? CssListStyle.None
                                  : val.Contains("circle") ? CssListStyle.Circle
                                  : val.Contains("square") ? CssListStyle.Square
                                  : val.Contains("decimal") ? CssListStyle.Decimal
                                  : cs.ListStyle;
                    break;
                case "float":
                    cs.Float = val.Trim() switch
                    {
                        "left" => CssFloat.Left,
                        "right" => CssFloat.Right,
                        _ => CssFloat.None,
                    };
                    break;
                case "clear":
                    cs.Clear = val.Trim() switch
                    {
                        "left" => CssFloat.Left,
                        "right" => CssFloat.Right,
                        "both" => CssFloat.Both,
                        _ => CssFloat.None,
                    };
                    break;
                case "font":
                    ParseFontShorthand(cs, val, parent.FontSize, ViewportW, ViewportH, RootPx);
                    break;
                case "animation":
                case "animation-name":
                case "animation-duration":
                case "animation-timing-function":
                case "animation-iteration-count":
                case "animation-delay":
                case "animation-fill-mode":
                case "animation-direction":
                    cs.Animation = ParseAnimation(prop, val, cs.Animation);
                    break;
            }
        }

        // position:absolute/fixed "blockifica" o elemento (como no CSS real)
        if ((cs.Position == CssPosition.Absolute || cs.Position == CssPosition.Fixed) &&
            (cs.Display == CssDisplay.Inline || cs.Display == CssDisplay.InlineBlock))
            cs.Display = CssDisplay.Block;
        return cs;
    }

    private static List<GridTrack> ParseGridTracks(string val, float em, float vw, float vh, float root)
    {
        var tracks = new List<GridTrack>();
        foreach (var tok in CssValues.SplitTopLevel(val))
        {
            string t = tok.Trim().ToLowerInvariant();
            if (t.StartsWith("repeat(") && t.EndsWith(")"))
            {
                string inner = t.Substring(7, t.Length - 8);
                var args = CssLength.SplitFnArgs(inner);
                if (args.Count == 2 && int.TryParse(args[0].Trim(), out int n) && n > 0 && n <= 24)
                    for (int i = 0; i < n; i++)
                        tracks.AddRange(ParseGridTracks(args[1], em, vw, vh, root));
                continue;
            }
            if (t is "auto" or "min-content" or "max-content" or "auto-fill" or "auto-fit")
            { tracks.Add(new GridTrack { IsFr = true, Fr = 1 }); continue; }
            if (t.EndsWith("fr"))
            {
                if (float.TryParse(t[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float fr) && fr > 0)
                    tracks.Add(new GridTrack { IsFr = true, Fr = fr });
                continue;
            }
            if (t.StartsWith("minmax(") && t.EndsWith(")"))
            {
                var args = CssLength.SplitFnArgs(t.Substring(7, t.Length - 8));
                string use = args.Count == 2 ? args[1] : (args.Count == 1 ? args[0] : "");
                if (use.Length > 0) tracks.AddRange(ParseGridTracks(use, em, vw, vh, root));
                else tracks.Add(new GridTrack { IsFr = true, Fr = 1 });
                continue;
            }
            var len = CssLength.Parse(t, em, vw, vh, root);
            if (len != null) tracks.Add(new GridTrack { Len = len });
        }
        return tracks;
    }

    private static string? ExtractUrl(string val)
    {
        int i = val.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        i += 4;
        int j = val.IndexOf(')', i);
        if (j < 0) return null;
        return val.Substring(i, j - i).Trim().Trim('"', '\'');
    }

    private static string StripUrls(string val)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < val.Length)
        {
            int u = val.IndexOf("url(", i, StringComparison.OrdinalIgnoreCase);
            if (u < 0) { sb.Append(val.Substring(i)); break; }
            sb.Append(val.Substring(i, u - i));
            int j = val.IndexOf(')', u);
            i = j < 0 ? val.Length : j + 1;
        }
        return sb.ToString();
    }

    private static void ParseBgRest(string val, ComputedStyle cs, float em)
    {
        // background: <color?> url() <position>? / <size>? <repeat>? — melhor esforço
        string t = StripUrls(val).Trim().ToLowerInvariant();
        string sizePart = "";
        int slash = t.IndexOf('/');
        if (slash >= 0) { sizePart = t.Substring(slash + 1).Trim(); t = t.Substring(0, slash).Trim(); }
        var toks = CssValues.SplitTopLevel(t);
        var pos = new List<string>();
        foreach (var tok in toks)
        {
            if (tok is "no-repeat" or "repeat-x" or "repeat-y") cs.BgNoRepeat = true;
            else if (tok == "repeat") cs.BgNoRepeat = false;
            else if (CssColors.Parse(tok) != null) { /* cor tratada fora */ }
            else pos.Add(tok);
        }
        // "center/cover": size pode vir grudado no último token de posição
        if (sizePart.Length == 0 && pos.Count > 0)
        {
            string last = pos[^1];
            int s2 = last.IndexOf('/');
            if (s2 >= 0) { sizePart = last.Substring(s2 + 1).Trim(); pos[^1] = last.Substring(0, s2).Trim(); }
        }
        if (pos.Count >= 1) cs.BgPosX = pos[0];
        if (pos.Count >= 2) cs.BgPosY = pos[1];
        if (sizePart.Length > 0) cs.BgSize = sizePart;
    }

    private static CssOverflow ParseOverflow(string val)
    {
        string t = val.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return t switch
        {
            "hidden" => CssOverflow.Hidden,
            "auto" => CssOverflow.Auto,
            "scroll" => CssOverflow.Scroll,
            _ => CssOverflow.Visible,
        };
    }

    // ---- helpers ----------------------------------------------------------

    private static bool IsKeyword(string val, out string kw)
    {
        string t = val.Trim().ToLowerInvariant();
        if (t is "inherit" or "initial" or "unset") { kw = t; return true; }
        kw = "";
        return false;
    }

    private static void ApplyKeyword(ComputedStyle cs, ComputedStyle parent, string prop, string kw)
    {
        var def = new ComputedStyle();
        bool inherit = kw == "inherit" || (kw == "unset" && IsInheritedProp(prop));
        if (inherit)
        {
            switch (prop)
            {
                case "color": cs.Color = parent.Color; break;
                case "font-family": cs.FontFamily = parent.FontFamily; break;
                case "font-size": cs.FontSize = parent.FontSize; break;
                case "font-weight": cs.Bold = parent.Bold; break;
                case "font-style": cs.Italic = parent.Italic; break;
                case "text-align": cs.Align = parent.Align; break;
                case "white-space": cs.WS = parent.WS; break;
                case "line-height": cs.LineHeight = parent.LineHeight; break;
                case "visibility": cs.Hidden = parent.Hidden; break;
            }
            return;
        }
        // initial / unset (não-herdável): reseta para padrão
        switch (prop)
        {
            case "color": cs.Color = def.Color; break;
            case "background-color": case "background": cs.Background = def.Background; break;
            case "font-family": cs.FontFamily = def.FontFamily; break;
            case "font-size": cs.FontSize = parent.FontSize; break;
            case "font-weight": cs.Bold = false; break;
            case "font-style": cs.Italic = false; break;
            case "display": cs.Display = CssDisplay.Inline; break;
            case "visibility": cs.Hidden = false; break;
            case "opacity": cs.Opacity = 1f; break;
        }
    }

    private static bool IsInheritedProp(string prop) => prop is "color" or "font-family" or "font-size"
        or "font-weight" or "font-style" or "text-align" or "white-space" or "line-height"
        or "list-style" or "list-style-type" or "visibility";

    private static float? ParseFontSize(string val, float parentEm, float vw, float vh, float root)
    {
        val = val.Trim().ToLowerInvariant();
        float? kw = val switch
        {
            "xx-small" => 9f, "x-small" => 10f, "small" => 13f, "medium" => 16f,
            "large" => 18f, "x-large" => 24f, "xx-large" => 32f,
            "smaller" => parentEm * 0.83f, "larger" => parentEm * 1.2f,
            _ => null,
        };
        if (kw != null) return kw;
        if (val.EndsWith('%'))
        {
            if (float.TryParse(val.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
                return parentEm * p / 100f;
            return null;
        }
        var l = CssLength.Parse(val, parentEm, vw, vh, root);
        if (l == null) return null;
        if (l.Op != LengthOp.None) return l.Resolve(parentEm);
        if (l.IsPercent) return parentEm * l.Percent / 100f;
        return l.Px;
    }

    private static string FirstFamily(string val)
    {
        foreach (var f in val.Split(','))
        {
            var name = f.Trim().Trim('"', '\'');
            if (name.Length > 0) return name;
        }
        return val;
    }

    /// <summary>Escolhe a primeira família utilizável da pilha (como o Chrome):
    /// instalada/customizada vence; senão, genérico serif→Georgia, mono→Consolas.</summary>
    private static string BestFamily(string val)
    {
        string serifFallback = "", monoFallback = "";
        foreach (var f in val.Split(','))
        {
            var name = f.Trim().Trim('"', '\'');
            if (name.Length == 0) continue;
            string low = name.ToLowerInvariant();
            bool isMono = low is "monospace" or "ui-monospace" || low.Contains("mono") || low.Contains("consol") || low.Contains("courier");
            bool isSerif = low is "serif" || (low.Contains("serif") && !low.Contains("sans"));
            if (isMono) { monoFallback = "Consolas"; continue; }
            if (isSerif) { serifFallback = "Georgia"; continue; }
            if (low is "sans-serif" or "system-ui" or "ui-sans-serif" or "cursive" or "fantasy") continue; // genérico sans: usa padrão
            var resolved = FontLoader.Resolve(name);
            if (resolved != null) return resolved;
            // nome desconhecido e não-genérico: continua procurando na pilha
        }
        if (serifFallback.Length > 0) return serifFallback;
        if (monoFallback.Length > 0) return monoFallback;
        return "Segoe UI";
    }

    private static void ApplyBox(CssLength[] sides, string val, float em, float vw, float vh, float root)
    {
        var lens = Split4(val);
        if (lens.Count == 0) return;
        CssLength?[] parsed = new CssLength?[4];
        if (lens.Count == 1) parsed[0] = parsed[1] = parsed[2] = parsed[3] = CssLength.Parse(lens[0], em, vw, vh, root);
        else if (lens.Count == 2) { parsed[0] = parsed[2] = CssLength.Parse(lens[0], em, vw, vh, root); parsed[1] = parsed[3] = CssLength.Parse(lens[1], em, vw, vh, root); }
        else if (lens.Count == 3) { parsed[0] = CssLength.Parse(lens[0], em, vw, vh, root); parsed[1] = parsed[3] = CssLength.Parse(lens[1], em, vw, vh, root); parsed[2] = CssLength.Parse(lens[2], em, vw, vh, root); }
        else { parsed[0] = CssLength.Parse(lens[0], em, vw, vh, root); parsed[1] = CssLength.Parse(lens[1], em, vw, vh, root); parsed[2] = CssLength.Parse(lens[2], em, vw, vh, root); parsed[3] = CssLength.Parse(lens[3], em, vw, vh, root); }
        for (int i = 0; i < 4; i++)
            if (parsed[i] != null) sides[i] = parsed[i]!;
    }

    private static void SetSide(CssLength[] sides, int i, string val, float em, float vw, float vh, float root)
    {
        var l = CssLength.Parse(val, em, vw, vh, root);
        if (l != null) sides[i] = l;
    }

    private static (float W, Color? C) ParseBorder(string val, float em, float vw, float vh, float root)
    {
        float w = 0; Color? col = null;
        foreach (var token in CssValues.SplitTopLevel(val))
        {
            if (token is "none" or "hidden") w = 0;
            else if (token is "thin") w = 1;
            else if (token is "medium") w = 3;
            else if (token is "thick") w = 5;
            else if (CssValues.LooksLikeColor(token)) col = CssColors.Parse(token) ?? col;
            else if (token.Length > 0 && (char.IsDigit(token[0]) || token[0] == '.' || token[0] == '(' || token[0] == '-')) { var l = CssLength.Parse(token, em, vw, vh, root); if (l != null) w = Math.Max(0, l.Resolve(0)); }
        }
        return (w, col);
    }

    private static void SetBorderSide(ComputedStyle cs, int i, string val, float em, float vw, float vh, float root)
    {
        var (w, col) = ParseBorder(val, em, vw, vh, root);
        cs.BorderW[i] = w;
        if (col != null) cs.BorderC[i] = col.Value;
    }

    private static void SetBorderWidth(ComputedStyle cs, int i, string val, float em, float vw, float vh, float root)
    {
        var l = CssLength.Parse(val, em, vw, vh, root);
        if (l != null) cs.BorderW[i] = Math.Max(0, l.Resolve(0));
    }

    private static List<string> Split4(string val) => CssValues.SplitTopLevel(val);

    private static void ParseFontShorthand(ComputedStyle cs, string val, float parentEm, float vw, float vh, float root)
    {
        // [style] [weight] size[/line-height] family — número sem unidade é line-height, não size
        var tokens = CssValues.SplitTopLevel(val.Replace('/', ' '));
        string family = "";
        foreach (var t in tokens)
        {
            var lt = t.ToLowerInvariant();
            if (lt is "italic" or "oblique") { cs.Italic = true; continue; }
            if (lt is "bold" or "bolder") { cs.Bold = true; continue; }
            if (lt is "normal" or "small-caps") continue;
            // line-height primeiro: número puro (ex. o "1.6" de "16px/1.6") nunca é font-size
            if (float.TryParse(lt, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) continue;
            var size = CssLength.Parse(lt, parentEm, vw, vh, root);
            if (size != null)
            {
                float px = size.Op != LengthOp.None ? size.Resolve(parentEm)
                    : size.IsPercent ? parentEm * size.Percent / 100f : size.Px;
                if (px > 0) { cs.FontSize = Math.Clamp(px, 3f, 200f); cs.LineHeight = cs.FontSize * 1.2f; }
                continue;
            }
            family = family.Length == 0 ? t : family + " " + t;
        }
        if (family.Length > 0) cs.FontFamily = BestFamily(family);
    }

    private static CssAnimation? ParseAnimation(string prop, string val, CssAnimation? cur)
    {
        cur ??= new CssAnimation();
        string v = val.Trim().ToLowerInvariant();
        switch (prop)
        {
            case "animation-name": cur.Name = val.Trim(); break;
            case "animation-duration": case "animation-delay":
            {
                float secs = ParseTimeSecs(v);
                if (prop == "animation-duration") cur.Duration = secs;
                else cur.Delay = secs;
                break;
            }
            case "animation-timing-function": cur.TimingFunction = v; break;
            case "animation-iteration-count":
                cur.IterationCount = v == "infinite" ? 0 : (int.TryParse(v, out int n) ? n : 1);
                break;
            case "animation-fill-mode": cur.FillMode = v; break;
            case "animation-direction": cur.Direction = v; break;
            case "animation":
            {
                foreach (var tok in CssValues.SplitTopLevel(v))
                {
                    if (tok.EndsWith("ms") || tok.EndsWith("s")) { if (cur.Duration == 0) cur.Duration = ParseTimeSecs(tok); }
                    else if (tok is "infinite" || int.TryParse(tok, out _)) { cur.IterationCount = tok == "infinite" ? 0 : 1; }
                    else if (tok is "linear" or "ease" or "ease-in" or "ease-out" or "ease-in-out") cur.TimingFunction = tok;
                    else if (tok is "none" or "forwards" or "backwards" or "both") cur.FillMode = tok;
                    else if (tok is "normal" or "reverse" or "alternate" or "alternate-reverse") cur.Direction = tok;
                    else cur.Name = tok;
                }
                break;
            }
        }
        return cur;
    }

    private static float ParseTimeSecs(string v)
    {
        v = v.Trim().ToLowerInvariant();
        if (v.EndsWith("ms") && float.TryParse(v[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ms)) return ms / 1000f;
        if (v.EndsWith("s") && float.TryParse(v[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s)) return s;
        return 0;
    }
}

/// <summary>Stylesheet padrão do navegador (equivalente ao CSS default do Chrome).</summary>
public static class UserAgentCss
{
    public const string Text = @"
head, title, base, meta, link, script, style, noscript, template { display: none; }
canvas, iframe, object, embed, dialog { display: none; }
video { display: inline; }
svg { display: inline; }
audio { display: inline; }
html { display: block; }
/* O UA stylesheet dos browsers reais NÃO define background do body: a tela é
   branca por padrão, mas qualquer regra de autor vence. Forçar #ffffff aqui
   apagava o tema escuro do YouTube (body #0f0f0f) e deixava a página branca. */
body { display: block; margin: 8px; color: #000000; }
/* COMPONENTES CUSTOM (Polymer): sem regra UA viram inline e o layout não
   empilha nada (foi o caso do ytd-page-manager inteiro). O display real deles
   é block em todos os hosts estampados. */
ytd-app, ytd-page-manager, ytd-watch-flexy, ytd-watch-metadata, ytd-watch-info-text,
ytd-channel-name, ytd-subscribe-button, ytd-compact-video-renderer, ytd-masthead,
ytd-guide-renderer, ytd-mini-guide-renderer, ytd-guide-section-renderer,
ytd-popup-container, ytd-topbar-logo-renderer, ytd-searchbox, ytd-hero-playlist-title-renderer,
tp-yt-app-drawer, tp-yt-paper-tabs, yt-icon, yt-icon-button, yt-img-shadow,
tp-yt-iron-overlay-backdrop, yt-interaction, ytd-item-section-renderer, ytd-rich-grid-renderer { display: block; }
ytd-searchbox { display: flex; }
yt-icon-button { display: inline-block; }
div, p, section, article, header, footer, nav, aside, main, figure, figcaption,
details, summary, form, fieldset, legend, blockquote, dl, dt, dd, address, center { display: block; }
center { text-align: center; }
p { margin: 1em 0; }
h1, h2, h3, h4, h5, h6 { display: block; font-weight: bold; margin: 1em 0 0.5em 0; }
h1 { font-size: 2em; } h2 { font-size: 1.5em; } h3 { font-size: 1.17em; }
h4 { font-size: 1em; } h5 { font-size: 0.83em; } h6 { font-size: 0.67em; }
ul, ol { display: block; margin: 1em 0; padding-left: 40px; }
li { display: list-item; }
ul { list-style-type: disc; }
ol { list-style-type: decimal; }
a { color: #0000ee; text-decoration: underline; }
b, strong, th { font-weight: bold; }
i, em, cite, var, dfn { font-style: italic; }
u, ins { text-decoration: underline; }
s, strike, del { text-decoration: line-through; }
small, sub, sup { font-size: 0.83em; }
pre { display: block; white-space: pre; font-family: monospace; margin: 1em 0; }
code, kbd, samp, tt { font-family: monospace; }
blockquote { margin: 1em 40px; }
hr { display: block; height: 0; border-top: 1px solid #b0b0b0; margin: 0.5em 0; }
table { display: table; border-collapse: separate; border-spacing: 2px; }
thead, tbody, tfoot { display: table-row-group; }
tr { display: table-row; }
td, th { display: table-cell; padding: 1px 4px; }
caption { display: block; text-align: center; }
img { display: inline; }
br { display: inline; }
input, select, textarea, button { display: inline-block; }
input[type=""hidden""] { display: none; }
button { background-color: #e1e1e1; border: 1px solid #adadad; padding: 2px 12px; }
";
}
