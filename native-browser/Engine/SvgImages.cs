using Svg;

namespace native_browser;

// ============================================================================
// SVG: rasterização via Svg.NET (GDI+) para <img src=.svg> e <svg> inline
// ============================================================================
public static class SvgRaster
{
    /// <summary>Detecta SVG pelo caminho ou pelo conteúdo.</summary>
    public static bool LooksLikeSvg(string urlPath, byte[] bytes)
    {
        string p = urlPath.Split('?', '#')[0];
        if (p.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return true;
        int n = Math.Min(bytes.Length, 600);
        string head = System.Text.Encoding.UTF8.GetString(bytes, 0, n).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)) return true;
        return head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) &&
               head.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static Bitmap? FromBytes(byte[] data, int widthHint = 0, int heightHint = 0)
    {
        try
        {
            string text = System.Text.Encoding.UTF8.GetString(data);
            return FromText(text, widthHint, heightHint);
        }
        catch { return null; }
    }

    public static Bitmap? FromText(string svg, int widthHint = 0, int heightHint = 0)
    {
        try
        {
            var doc = SvgDocument.FromSvg<SvgDocument>(svg);
            ApplySize(doc, widthHint, heightHint);
            var bmp = doc.Draw();
            if (bmp.Width <= 0 || bmp.Height <= 0 || bmp.Width > 4096 || bmp.Height > 4096) return null;
            return bmp;
        }
        catch { return null; }
    }

    private static void ApplySize(SvgDocument doc, int wHint, int hHint)
    {
        try
        {
            float vbW = 0, vbH = 0;
            if (doc.ViewBox != null) { vbW = doc.ViewBox.Width; vbH = doc.ViewBox.Height; }
            float ratio = (vbW > 0 && vbH > 0) ? vbW / vbH : 0;
            if (wHint > 0 && hHint > 0) { doc.Width = wHint; doc.Height = hHint; }
            else if (wHint > 0 && ratio > 0) { doc.Width = wHint; doc.Height = Math.Max(1, (int)Math.Round(wHint / ratio)); }
            else if (hHint > 0 && ratio > 0) { doc.Height = hHint; doc.Width = Math.Max(1, (int)Math.Round(hHint * ratio)); }
            // sem hints: mantém tamanho do documento (width/height ou viewBox)
        }
        catch { }
    }

    /// <summary>Serializa um nó &lt;svg&gt; do DOM para texto SVG.</summary>
    public static string Serialize(DomNode svg)
    {
        var sb = new System.Text.StringBuilder();
        WriteNode(svg, sb);
        return sb.ToString();
    }

    private static void WriteNode(DomNode n, System.Text.StringBuilder sb)
    {
        if (n.Type == NodeType.Text) { sb.Append(Escape(n.TextContent)); return; }
        sb.Append('<').Append(n.TagName);
        foreach (var kv in n.Attributes)
            sb.Append(' ').Append(kv.Key).Append("=\"").Append(EscapeAttr(kv.Value)).Append('"');
        if (n.Children.Count == 0 && IsVoid(n.TagName)) { sb.Append("/>"); return; }
        sb.Append('>');
        foreach (var c in n.Children) WriteNode(c, sb);
        sb.Append("</").Append(n.TagName).Append('>');
    }

    private static bool IsVoid(string tag) => tag is "path" or "rect" or "circle" or "ellipse"
        or "line" or "polyline" or "polygon" or "stop" or "use" or "image" or "br" or "hr"
        or "img" or "input" or "link" or "meta";

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    private static string EscapeAttr(string s) => Escape(s).Replace("\"", "&quot;");
}
