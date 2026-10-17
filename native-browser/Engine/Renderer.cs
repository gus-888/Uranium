namespace native_browser;

/// <summary>Estado de interação passado ao renderer (foco, caret, botão pressionado).</summary>
public class ControlContext
{
    public IDictionary<DomNode, ControlState>? Inputs;
    public DomNode? Focused;
    public bool CaretOn;
    public DomNode? Pressed;
    public DomNode? OpenSelect;
    public int OpenSelectHover = -1;
    public List<string> OpenSelectOptions = new();
    public RectangleF OpenSelectRect;
}

/// <summary>Diagnóstico leve de pintura.</summary>
internal static class Diag
{
    public static long VideoPaints;
    public static long VideoPaintsWithFrame;
    public static long Draws;
    public static long DrawErrors;
}

/// <summary>Pinta a árvore de layout com GDI+ (fundos, bordas, texto, imagens, controles).</summary>
public static class Renderer
{
    public static readonly StringFormat TypoFormat = CreateTypo();

    private static StringFormat CreateTypo()
    {
        var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
        sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return sf;
    }

    public static void Draw(Graphics g, LayoutResult page, float scrollY, int viewW, int viewH, ControlContext? ctx = null)
    {
        Diag.Draws++;
        if (Diag.Draws % 150 == 1) VideoManager.Log($"draw: n={Diag.Draws} scrollY={scrollY:0} view={viewW}x{viewH} pecas-video={page.Videos.Count} links={(page.Links?.Count ?? 0)} altura={page.Height:0}");
        try
        {
            DrawInner(g, page, scrollY, viewW, viewH, ctx);
        }
        catch (Exception ex)
        {
            Diag.DrawErrors++;
            if (Diag.DrawErrors <= 3) VideoManager.Log($"draw EX ({Diag.DrawErrors}): {ex.Message} :: {ex.StackTrace?.Split('\n')[0]}");
        }
    }

    private static void DrawInner(Graphics g, LayoutResult page, float scrollY, int viewW, int viewH, ControlContext? ctx)
    {
        g.Clear(page.PageBackground);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.TranslateTransform(0, -scrollY);
        DrawBoxChildren(g, page.Root, scrollY, viewH, ctx);
        g.ResetTransform();
    }

    private static Color Fade(Color c, float o) =>
        o >= 0.999f ? c : Color.FromArgb((int)Math.Round(Math.Clamp(o, 0, 1) * c.A), c.R, c.G, c.B);

    /// <summary>Retângulo arredondado por canto (TL TR BR BL).</summary>
    private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(float x, float y, float w, float h, float[] r)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        if (w <= 0 || h <= 0) return path;
        float maxR = Math.Min(w, h) / 2f;
        float tl = Math.Min(r[0], maxR), tr = Math.Min(r[1], maxR);
        float br = Math.Min(r[2], maxR), bl = Math.Min(r[3], maxR);
        path.StartFigure();
        path.AddArc(x, y, tl * 2, tl * 2, 180, 90);
        path.AddArc(x + w - tr * 2, y, tr * 2, tr * 2, 270, 90);
        path.AddArc(x + w - br * 2, y + h - br * 2, br * 2, br * 2, 0, 90);
        path.AddArc(x, y + h - bl * 2, bl * 2, bl * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static float[] ResolveRadius(ComputedStyle cs, float w, float h)
    {
        var r = new float[4];
        float m = Math.Min(w, h);
        for (int i = 0; i < 4; i++)
            r[i] = cs.BorderRadius[i] == null ? 0 : Math.Max(0, cs.BorderRadius[i]!.Resolve(m));
        return r;
    }

    private static void DrawBoxChildren(Graphics g, BlockBox root, float scrollY, int viewH, ControlContext? ctx)
    {
        foreach (var child in root.Children)
            DrawBox(g, child, scrollY, viewH, ctx);
    }

    private static void DrawBox(Graphics g, BlockBox box, float scrollY, int viewH, ControlContext? ctx, bool noCullParent = false, float parentOpacity = 1f)
    {
        if (box.Style.Hidden) return;
        float opacity = parentOpacity * box.Style.Opacity;
        if (opacity < 0.01f) return;

        float top = scrollY - 600, bottom = scrollY + viewH + 600;
        // fixed/sticky ignoram o culling vertical (posição pintada != posição de layout);
        // subárvores atômicas (inline-block) também, pois usam coords relativas
        bool noCull = noCullParent || box.Fixed || box.HasSticky;

        var st = g.Save();
        if (box.PosDx != 0 || box.PosDy != 0) g.TranslateTransform(box.PosDx, box.PosDy);
        if (box.Fixed) g.TranslateTransform(0, scrollY); // cancela o scroll: gruda no viewport
        else if (box.HasSticky)
        {
            float dy = scrollY + box.StickyTop - box.Y;
            if (dy > 0) g.TranslateTransform(0, dy);
        }

        // item flex/grid que é controle: só o chrome do controle
        if (box.ControlChrome && box.Node != null)
        {
            if (!noCull && (box.Y + box.H < top || box.Y > bottom)) { g.Restore(st); return; }
            DrawControl(g, box, ctx);
            g.Restore(st);
            return;
        }

        bool rounded = box.Style.HasRadius;
        float[] radii = rounded ? ResolveRadius(box.Style, box.W, box.H) : Array.Empty<float>();
        bool useRound = rounded && radii.Any(v => v > 0.5f);

        // fundo (pinta a caixa de borda inteira, como CSS manda)
        var bg = Fade(box.Style.Background, opacity);
        if (bg.A > 0)
        {
            using var brush = new SolidBrush(bg);
            if (useRound) { using var path = RoundedPath(box.X, box.Y, box.W, box.H, radii); g.FillPath(brush, path); }
            else g.FillRectangle(brush, box.X, box.Y, box.W, box.H);
        }

        // background-image (ícones etc.): recortada na caixa de borda
        if (box.BgImage != null)
        {
            var bgSt = g.Save();
            if (useRound) { using var path = RoundedPath(box.X, box.Y, box.W, box.H, radii); g.IntersectClip(new Region(path)); }
            else g.IntersectClip(new RectangleF(box.X, box.Y, box.W, box.H));
            var r = box.BgRect;
            if (box.BgTiled)
            {
                for (float ty = r.Y; ty < box.Y + box.H; ty += Math.Max(1, r.Height))
                    for (float tx = r.X; tx < box.X + box.W; tx += Math.Max(1, r.Width))
                        g.DrawImage(box.BgImage, new RectangleF(tx, ty, r.Width, r.Height));
            }
            else if (r.Width > 0 && r.Height > 0)
            {
                g.DrawImage(box.BgImage, r);
            }
            g.Restore(bgSt);
        }

        // bordas por lado (arredondadas: contorno único quando uniformes)
        var bw = box.Style.BorderW;
        var bc = box.Style.BorderC;
        if (useRound && bw[0] > 0 && bw[0] == bw[1] && bw[1] == bw[2] && bw[2] == bw[3])
        {
            using var pen = new Pen(Fade(bc[0], opacity), bw[0]);
            using var path = RoundedPath(box.X + bw[0] / 2, box.Y + bw[0] / 2, box.W - bw[0], box.H - bw[0], radii);
            if (box.W > bw[0] && box.H > bw[0]) g.DrawPath(pen, path);
        }
        else
        {
            if (bw[0] > 0) { using var p = new SolidBrush(Fade(bc[0], opacity)); g.FillRectangle(p, box.X, box.Y, box.W, bw[0]); }
            if (bw[1] > 0) { using var p = new SolidBrush(Fade(bc[1], opacity)); g.FillRectangle(p, box.X + box.W - bw[1], box.Y, bw[1], box.H); }
            if (bw[2] > 0) { using var p = new SolidBrush(Fade(bc[2], opacity)); g.FillRectangle(p, box.X, box.Y + box.H - bw[2], box.W, bw[2]); }
            if (bw[3] > 0) { using var p = new SolidBrush(Fade(bc[3], opacity)); g.FillRectangle(p, box.X, box.Y, bw[3], box.H); }
        }

        // marcador de lista
        if (box.Marker != null && box.MarkerY >= 0)
        {
            var font = box.Style.GetFont();
            using var brush = new SolidBrush(Fade(box.Style.Color, opacity));
            var size = g.MeasureString(box.Marker, font, int.MaxValue, TypoFormat);
            g.DrawString(box.Marker, font, brush, box.X - size.Width - 4, box.MarkerY);
        }

        // overflow: recorta conteúdo (linhas + filhos) na caixa de borda
        var clipSt = g.Save();
        if (box.Style.Clip && box.W > 0 && box.H > 0)
        {
            if (useRound) { using var path = RoundedPath(box.X, box.Y, box.W, box.H, radii); g.IntersectClip(new Region(path)); }
            else g.IntersectClip(new RectangleF(box.X, box.Y, box.W, box.H));
        }

        // linhas de texto
        foreach (var line in box.Lines)
        {
            if (!noCull && (line.Y + line.Height < top || line.Y > bottom)) continue;
            foreach (var piece in line.Pieces)
                DrawPiece(g, piece, ctx, opacity);
        }

        // blocos filhos: primeiro os normais, depois os posicionados por z-index
        foreach (var child in box.Children)
        {
            if (child.IsPositioned) continue;
            if (!noCull && (child.Y + child.H < top || child.Y > bottom)) continue;
            DrawBox(g, child, scrollY, viewH, ctx, noCull, opacity);
        }
        foreach (var child in box.Children
                     .Where(c => c.IsPositioned)
                     .OrderBy(c => c.ZIndex))
        {
            if (!noCull && !child.Fixed && !child.HasSticky && (child.Y + child.H < top || child.Y > bottom)) continue;
            DrawBox(g, child, scrollY, viewH, ctx, noCull, opacity);
        }

        g.Restore(clipSt);
        g.Restore(st);
    }

    private static void DrawPiece(Graphics g, LayoutPiece p, ControlContext? ctx, float opacity = 1f)
    {
        static string FmtTime(float s)
        {
            int t = Math.Max(0, (int)s);
            int hh = t / 3600, mm = (t % 3600) / 60, ss = t % 60;
            return hh > 0 ? $"{hh}:{mm:00}:{ss:00}" : $"{mm}:{ss:00}";
        }
        float op = opacity * p.Style.Opacity;
        if (op < 0.01f) return;
        switch (p.Kind)
        {
            case PieceKind.Text:
                {
                    var font = p.Style.GetFont();
                    using var brush = new SolidBrush(Fade(p.Style.Color, op));
                    g.DrawString(p.Text, font, brush, p.X, p.Y, TypoFormat);

                    float baseline = p.Y + p.Asc;
                    if (p.Style.Underline)
                    {
                        using var ub = new SolidBrush(Fade(p.Style.Color, op));
                        g.FillRectangle(ub, p.X, baseline + 1.2f, p.W, Math.Max(1f, p.Style.FontSize / 14f));
                    }
                    if (p.Style.Strike)
                    {
                        using var sb = new SolidBrush(Fade(p.Style.Color, op));
                        g.FillRectangle(sb, p.X, p.Y + p.Asc * 0.62f, p.W, Math.Max(1f, p.Style.FontSize / 14f));
                    }
                    break;
                }
            case PieceKind.Image:
                {
                    if (p.Image != null)
                    {
                        ImageAnimator.UpdateFrames(p.Image); // GIFs animados avançam quadro a quadro
                        var dest = new RectangleF(p.X, p.Y, p.W, p.H);
                        if (op >= 0.999f) g.DrawImage(p.Image, dest);
                        else
                        {
                            using var ia = new System.Drawing.Imaging.ImageAttributes();
                            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = op });
                            g.DrawImage(p.Image, new Rectangle((int)dest.X, (int)dest.Y, (int)dest.Width, (int)dest.Height),
                                0, 0, p.Image.Width, p.Image.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    else
                    {
                        using var pen = new Pen(Color.FromArgb(150, 150, 150), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                        g.DrawRectangle(pen, p.X + 0.5f, p.Y + 0.5f, p.W - 1, p.H - 1);
                        if (p.ImageBroken && p.Alt.Length > 0)
                        {
                            using var font = new Font("Segoe UI", Math.Min(12f, Math.Max(9f, p.H / 3f)), FontStyle.Italic, GraphicsUnit.Pixel);
                            using var brush = new SolidBrush(Color.FromArgb(120, 120, 120));
                            g.DrawString(p.Alt, font, brush, new RectangleF(p.X + 2, p.Y + 2, p.W - 4, p.H - 4), TypoFormat);
                        }
                    }
                    break;
                }
            case PieceKind.Video:
                {
                    System.Threading.Interlocked.Increment(ref Diag.VideoPaints);
                    var rect = new RectangleF(p.X, p.Y, p.W, p.H);
                    var vm = VideoManager.Global;
                    var slot = p.VideoNode != null ? vm?.Find(p.VideoNode) : null;
                    var frame = slot?.Frame ?? p.Image;      // quadro vivo vence poster
                    if (frame != null) System.Threading.Interlocked.Increment(ref Diag.VideoPaintsWithFrame);
                    if (Diag.VideoPaints % 30 == 1)
                        VideoManager.Log($"paint: kind=video rect=({p.X:0},{p.Y:0} {p.W:0}x{p.H:0}) frame={(frame != null ? "sim" : "nao")} slotFrames={slot?.Frames ?? -1}");

                    if (frame != null)
                    {
                        // quadro de vídeo nunca estica para além do retângulo
                        var src = new RectangleF(0, 0, frame.Width, frame.Height);
                        float scale = Math.Min(rect.Width / src.Width, rect.Height / src.Height);
                        float dw = src.Width * scale, dh = src.Height * scale;
                        var dst = new RectangleF(rect.X + (rect.Width - dw) / 2, rect.Y + (rect.Height - dh) / 2, dw, dh);
                        bool drew = false;
                        if (slot != null) { try { lock (slot.Gate) { g.DrawImage(frame, dst); } drew = true; } catch { drew = false; } }
                        if (!drew)
                        {
                            try { g.DrawImage(frame, dst); } catch { }
                        }
                    }
                    else
                    {
                        using var bg = new SolidBrush(Color.FromArgb(17, 24, 39));
                        g.FillRectangle(bg, rect);
                    }

                    bool isAudio = p.VideoIsAudio;
                    bool playing = vm != null && p.VideoNode != null && vm.IsPlaying(p.VideoNode);
                    bool paused = vm != null && p.VideoNode != null && vm.IsPaused(p.VideoNode);
                    float posSec = (float)(vm?.GetTime(p.VideoNode!) ?? 0);
                    float durSec = (float)(vm?.GetDuration(p.VideoNode!) ?? 0);

                    // ======= chrome do player (barra de progresso + botão) =======
                    float pad = isAudio ? 4 : 10;
                    float barH = isAudio ? 4 : 5;
                    float barY = rect.Bottom - barH - pad;
                    float barX = rect.Left + pad;
                    float barW = rect.Width - pad * 2;
                    float frac = durSec > 0 ? Math.Clamp(posSec / durSec, 0, 1) : (playing || paused ? 0f : 0f);

                    using (var track = new SolidBrush(isAudio ? Color.FromArgb(70, 70, 80) : Color.FromArgb(160, 60, 60, 70)))
                        g.FillRectangle(track, barX, barY, barW, barH);
                    if (durSec > 0)
                    {
                        using var prog = new SolidBrush(Color.FromArgb(230, 200, 30, 40));
                        g.FillRectangle(prog, barX, barY, Math.Max(2, barW * frac), barH);
                        float knobR = barH + 2.5f;
                        using var knob = new SolidBrush(Color.White);
                        g.FillEllipse(knob, barX + barW * frac - knobR, barY + barH / 2 - knobR, knobR * 2, knobR * 2);
                    }

                    // tempo 0:12 / 3:33
                    if (durSec > 0 && !isAudio)
                    {
                        string tmp = $"{FmtTime(posSec)} / {FmtTime(durSec)}";
                        using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
                        var sz = g.MeasureString(tmp, font);
                        using var txtBg = new SolidBrush(Color.FromArgb(150, 20, 20, 30));
                        g.FillRectangle(txtBg, rect.Right - pad - sz.Width - 6, barY - sz.Height - 2, sz.Width + 6, sz.Height + 2);
                        using var txt = new SolidBrush(Color.White);
                        g.DrawString(tmp, font, txt, rect.Right - pad - sz.Width - 3, barY - sz.Height - 1);
                    }

                    // botão central ▶/❚❚ (vídeo) ou ▶/❚❚ pequeno à esquerda (áudio)
                    if (isAudio)
                    {
                        float t = Math.Min(12, rect.Height * 0.4f);
                        float ax = rect.Left + 10, ay = rect.Y + (rect.Height - t) / 2;
                        using var wp = new SolidBrush(playing ? Color.FromArgb(230, 60, 60, 70) : Color.FromArgb(235, 90, 90, 100));
                        if (playing)
                        {
                            g.FillRectangle(wp, ax, ay, t / 3, t);
                            g.FillRectangle(wp, ax + t * 2 / 3, ay, t / 3, t);
                        }
                        else
                        {
                            g.FillPolygon(wp, new[] { new PointF(ax, ay - t / 4), new PointF(ax, ay + t / 4), new PointF(ax + t * 0.8f, ay) });
                        }
                    }
                    else if (!playing)
                    {
                        float tri = Math.Min(30, p.H * 0.32f);
                        float cx = p.X + (p.W - tri) / 2, cy = p.Y + (p.H - tri) / 2;
                        using var halo = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                        g.FillEllipse(halo, cx - 16, cy - 16, tri + 32, tri + 32);
                        using var play = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                        if (playing)
                        {
                            float bw = tri / 2.4f;
                            g.FillRectangle(play, cx - bw - 2, cy - tri / 2, bw, tri);
                            g.FillRectangle(play, cx + 2, cy - tri / 2, bw, tri);
                        }
                        else
                        {
                            g.FillPolygon(play, new[] {
                                new PointF(cx - tri / 4, cy - tri / 2), new PointF(cx - tri / 4, cy + tri / 2),
                                new PointF(cx + tri * 0.75f, cy) });
                        }
                    }
                    break;
                }
            case PieceKind.Box:
                if (p.Box != null)
                {
                    // caixas inline-block têm coords relativas (0,0); translada para a posição da peça na linha
                    var st = g.Save();
                    g.TranslateTransform(p.X - p.Box.X, p.Y - p.Box.Y);
                    DrawBox(g, p.Box, scrollY: 0, viewH: 0, ctx, noCullParent: true, parentOpacity: opacity);
                    g.Restore(st);
                }
                break;
            case PieceKind.Control:
                if (p.Box != null)
                {
                    var st = g.Save();
                    g.TranslateTransform(p.X - p.Box.X, p.Y - p.Box.Y);
                    DrawControl(g, p.Box, ctx);
                    g.Restore(st);
                }
                break;
        }
    }

    private static void DrawControl(Graphics g, BlockBox box, ControlContext? ctx)
    {
        var node = box.Node!;
        var cs = box.Style;
        var rect = new RectangleF(box.X, box.Y, box.W, box.H);
        ControlState? st = null;
        if (ctx?.Inputs != null && ctx.Inputs.TryGetValue(node, out var s)) st = s;
        bool focused = ctx?.Focused == node;
        bool pressed = ctx?.Pressed == node;
        bool isButton = node.TagName == "button" ||
            (node.TagName == "input" && node.GetAttr("type").ToLowerInvariant() is "button" or "submit" or "reset");
        string type = node.TagName == "input" ? node.GetAttr("type").ToLowerInvariant() : node.TagName;

        if (type == "checkbox" || type == "radio")
        {
            using var border = new Pen(Color.FromArgb(90, 90, 95));
            bool check = st?.Touched == true ? st.Checked : node.Attributes.ContainsKey("checked");
            if (type == "checkbox")
            {
                using var bg = new SolidBrush(check ? Color.FromArgb(59, 130, 246) : Color.White);
                g.FillRectangle(bg, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                g.DrawRectangle(border, rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1, rect.Height - 1);
                if (check)
                {
                    using var pen = new Pen(Color.White, 2f);
                    g.DrawLines(pen, new[] {
                        new PointF(rect.X + 3, rect.Y + rect.Height / 2),
                        new PointF(rect.X + rect.Width / 2 - 1, rect.Y + rect.Height - 4),
                        new PointF(rect.X + rect.Width - 3, rect.Y + 4) });
                }
            }
            else
            {
                g.FillEllipse(focused ? Brushes.LightSkyBlue : Brushes.White, rect);
                g.DrawEllipse(border, rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1, rect.Height - 1);
                if (check)
                    g.FillEllipse(Brushes.DodgerBlue, rect.X + 3.5f, rect.Y + 3.5f, rect.Width - 7, rect.Height - 7);
            }
            return;
        }

        if (isButton)
        {
            if (box.BgImage != null)
            {
                // sprite/imagem de fundo do botão (ex. .lsb do Google)
                var bst = g.Save();
                g.IntersectClip(rect);
                if (box.BgTiled)
                {
                    var r = box.BgRect;
                    for (float ty = r.Y; ty < rect.Bottom; ty += Math.Max(1, r.Height))
                        for (float tx = r.X; tx < rect.Right; tx += Math.Max(1, r.Width))
                            g.DrawImage(box.BgImage, new RectangleF(tx, ty, r.Width, r.Height));
                }
                else g.DrawImage(box.BgImage, box.BgRect);
                g.Restore(bst);
            }
            else
            {
                var bgc = pressed ? Color.FromArgb(193, 200, 214) : focused ? Color.FromArgb(228, 236, 250) : Color.FromArgb(225, 225, 228);
                using var bg = new SolidBrush(bgc);
                g.FillRectangle(bg, rect);
            }
            using var border = new Pen(pressed ? Color.FromArgb(90, 105, 160) : Color.FromArgb(160, 160, 165));
            g.DrawRectangle(border, rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1, rect.Height - 1);
            var offset = pressed ? 1f : 0f;
            foreach (var line in box.Lines)
                foreach (var piece in line.Pieces)
                {
                    if (piece.Text.Length == 0) continue;
                    using var brush = new SolidBrush(Color.FromArgb(30, 30, 30));
                    g.DrawString(piece.Text, piece.Style.GetFont(), brush, box.X + 12 + offset, box.Y + (box.H - piece.H) / 2, TypoFormat);
                }
            return;
        }

        // input de texto / password / select / textarea (usa border do CSS quando definido)
        using var fieldBg = new SolidBrush(Color.White);
        g.FillRectangle(fieldBg, rect);
        // bordas do CSS por lado; sem borda estilizada, o padrão cinza 1px
        float[] sw = cs.BorderW;
        bool anyStyled = sw[0] > 0 || sw[1] > 0 || sw[2] > 0 || sw[3] > 0;
        if (focused || !anyStyled)
        {
            float bw = 1f;
            Color bc = focused ? Color.FromArgb(59, 130, 246) : Color.FromArgb(118, 118, 118);
            using var fieldBorder = new Pen(bc, bw);
            g.DrawRectangle(fieldBorder, rect.X + bw / 2, rect.Y + bw / 2, rect.Width - bw, rect.Height - bw);
        }
        else
        {
            if (sw[0] > 0) { using var p = new SolidBrush(cs.BorderC[0]); g.FillRectangle(p, rect.X, rect.Y, rect.Width, sw[0]); }
            if (sw[1] > 0) { using var p = new SolidBrush(cs.BorderC[1]); g.FillRectangle(p, rect.X + rect.Width - sw[1], rect.Y, sw[1], rect.Height); }
            if (sw[2] > 0) { using var p = new SolidBrush(cs.BorderC[2]); g.FillRectangle(p, rect.X, rect.Y + rect.Height - sw[2], rect.Width, sw[2]); }
            if (sw[3] > 0) { using var p = new SolidBrush(cs.BorderC[3]); g.FillRectangle(p, rect.X, rect.Y, sw[3], rect.Height); }
        }

        string display = "";
        foreach (var line in box.Lines)
            foreach (var piece in line.Pieces)
                display = piece.Text;

        if (type == "password") display = new string('•', Math.Min(display.Length, 30));

        if (node.TagName == "textarea" && display.Contains('\n'))
        {
            var font = box.Style.GetFont();
            int row = 0;
            foreach (var ln in display.Split('\n'))
            {
                using var brush = new SolidBrush(Color.FromArgb(40, 40, 40));
                g.DrawString(ln, font, brush, box.X + 6, box.Y + 4 + row * (font.GetHeight(g) + 1), TypoFormat);
                row++;
                if (row > 12) break;
            }
            return;
        }

        if (display.Length > 0)
        {
            var font = box.Style.GetFont();
            using var brush = new SolidBrush(Color.FromArgb(40, 40, 40));
            g.DrawString(display, font, brush, box.X + 6, box.Y + (box.H - (AscentH(font) + DescentH(font))) / 2, TypoFormat);
        }

        // setinha do select
        if (node.TagName == "select")
        {
            using var arrow = new Pen(Color.FromArgb(70, 70, 75));
            float ax = rect.Right - 14, ay = rect.Y + rect.Height / 2;
            g.DrawLines(arrow, new[] { new PointF(ax, ay - 3), new PointF(ax + 5, ay + 3), new PointF(ax + 10, ay - 3) });
        }

        // caret piscando no controle com foco
        if (focused && (ctx?.CaretOn ?? false))
        {
            var f = box.Style.GetFont();
            float tw = display.Length > 0 ? g.MeasureString(display, f, int.MaxValue, TypoFormat).Width : 0;
            float caretX = Math.Min(box.X + 6 + tw, box.X + box.W - 4);
            using var caret = new SolidBrush(Color.FromArgb(20, 20, 25));
            g.FillRectangle(caret, caretX, box.Y + 2, 1.4f, box.H - 4);
        }
    }

    private static float AscentH(Font f)
    {
        try { var fam = f.FontFamily; return f.Size * fam.GetCellAscent(f.Style) / fam.GetEmHeight(f.Style); }
        catch { return f.Size * 0.8f; }
    }

    private static float DescentH(Font f)
    {
        try { var fam = f.FontFamily; return f.Size * fam.GetCellDescent(f.Style) / fam.GetEmHeight(f.Style); }
        catch { return f.Size * 0.25f; }
    }
}
