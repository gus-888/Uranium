using System.Text;

namespace native_browser;

// ============================================================================
// DOM (Document Object Model)
// ============================================================================
public enum NodeType { Element, Text, Comment }

public class DomNode
{
    public NodeType Type;
    public string TagName = "";
    public string TextContent = "";
    public readonly Dictionary<string, string> Attributes = new(StringComparer.OrdinalIgnoreCase);
    public DomNode? Parent;
    public readonly List<DomNode> Children = new();

    /// <summary>Shadow root anexado a este elemento (attachShadow), se houver.</summary>
    public DomNode? ShadowRoot;

    /// <summary>Se este nó É um shadow root, o elemento host correspondente.</summary>
    public DomNode? ShadowHost;

    /// <summary>
    /// `<template>`: o conteúdo vive num DocumentFragment separado (.content),
    /// e NÃO é renderizado como filho direto.
    /// </summary>
    public DomNode? TemplateContent;

    /// <summary>Slots deste shadow root: nome → nó &lt;slot&gt;.</summary>
    public Dictionary<string, DomNode>? Slots;

    /// <summary>Modo do shadow root: "open" | "closed".</summary>
    public string ShadowMode = "open";

    /// <summary>Já passou por customElements upgrade (connectedCallback chamado)?</summary>
    public bool Upgraded;

    public DomNode(NodeType type) { Type = type; }

    public string Id => Attributes.TryGetValue("id", out var v) ? v.Trim() : "";

    public string[] GetClasses() => Attributes.TryGetValue("class", out var v)
        ? v.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : Array.Empty<string>();

    public string GetAttr(string name) => Attributes.TryGetValue(name, out var v) ? v : "";

    /// <summary>Concatena o texto descendente renderizável (pula script/style/noscript/template).</summary>
    public string CollectText()
    {
        var sb = new StringBuilder();
        void Walk(DomNode n)
        {
            if (n.Type == NodeType.Text) sb.Append(n.TextContent);
            else if (n.TagName is "script" or "style" or "noscript" or "template") return;
            foreach (var c in n.Children) Walk(c);
        }
        foreach (var c in Children) Walk(c);
        return sb.ToString();
    }

    public DomNode? FindTag(string tag)
    {
        if (Type == NodeType.Element && TagName == tag) return this;
        foreach (var c in Children)
        {
            var f = c.FindTag(tag);
            if (f != null) return f;
        }
        return null;
    }
}

// ============================================================================
// Entidades HTML (&amp; &#231; &ccedil; ...)
// ============================================================================
public static class HtmlEntities
{
    private static readonly Dictionary<string, char> Named = new()
    {
        ["quot"] = '"', ["amp"] = '&', ["lt"] = '<', ["gt"] = '>', ["apos"] = '\'',
        ["nbsp"] = '\u00A0', ["copy"] = '©', ["reg"] = '®', ["trade"] = '™',
        ["hellip"] = '…', ["mdash"] = '—', ["ndash"] = '–', ["bull"] = '•',
        ["dagger"] = '†', ["Dagger"] = '‡', ["prime"] = '′', ["Prime"] = '″',
        ["lsquo"] = '‘', ["rsquo"] = '’', ["ldquo"] = '“', ["rdquo"] = '”',
        ["sbquo"] = '‚', ["bdquo"] = '„', ["lsaquo"] = '‹', ["rsaquo"] = '›',
        ["euro"] = '€', ["pound"] = '£', ["yen"] = '¥', ["cent"] = '¢',
        ["sect"] = '§', ["para"] = '¶', ["middot"] = '·', ["laquo"] = '«', ["raquo"] = '»',
        ["deg"] = '°', ["plusmn"] = '±', ["times"] = '×', ["divide"] = '÷',
        ["frac12"] = '½', ["frac14"] = '¼', ["frac34"] = '¾', ["sup1"] = '¹', ["sup2"] = '²', ["sup3"] = '³',
        ["micro"] = 'µ', ["shy"] = '\u00AD', ["cedil"] = '¸', ["ordf"] = 'ª', ["ordm"] = 'º',
        ["iexcl"] = '¡', ["iquest"] = '¿', ["brvbar"] = '¦', ["curren"] = '¤', ["uml"] = '¨', ["macr"] = '¯',
        ["acute"] = '´',
        // Latin-1 letras acentuadas
        ["Agrave"] = 'À', ["Aacute"] = 'Á', ["Acirc"] = 'Â', ["Atilde"] = 'Ã', ["Auml"] = 'Ä', ["Aring"] = 'Å', ["AElig"] = 'Æ',
        ["Ccedil"] = 'Ç', ["Egrave"] = 'È', ["Eacute"] = 'É', ["Ecirc"] = 'Ê', ["Euml"] = 'Ë',
        ["Igrave"] = 'Ì', ["Iacute"] = 'Í', ["Icirc"] = 'Î', ["Iuml"] = 'Ï', ["ETH"] = 'Ð', ["Ntilde"] = 'Ñ',
        ["Ograve"] = 'Ò', ["Oacute"] = 'Ó', ["Ocirc"] = 'Ô', ["Otilde"] = 'Õ', ["Ouml"] = 'Ö', ["Oslash"] = 'Ø',
        ["Ugrave"] = 'Ù', ["Uacute"] = 'Ú', ["Ucirc"] = 'Û', ["Uuml"] = 'Ü', ["Yacute"] = 'Ý', ["THORN"] = 'Þ', ["szlig"] = 'ß',
        ["agrave"] = 'à', ["aacute"] = 'á', ["acirc"] = 'â', ["atilde"] = 'ã', ["auml"] = 'ä', ["aring"] = 'å', ["aelig"] = 'æ',
        ["ccedil"] = 'ç', ["egrave"] = 'è', ["eacute"] = 'é', ["ecirc"] = 'ê', ["euml"] = 'ë',
        ["igrave"] = 'ì', ["iacute"] = 'í', ["icirc"] = 'î', ["iuml"] = 'ï', ["eth"] = 'ð', ["ntilde"] = 'ñ',
        ["ograve"] = 'ò', ["oacute"] = 'ó', ["ocirc"] = 'ô', ["otilde"] = 'õ', ["ouml"] = 'ö', ["oslash"] = 'ø',
        ["ugrave"] = 'ù', ["uacute"] = 'ú', ["ucirc"] = 'û', ["uuml"] = 'ü', ["yacute"] = 'ý', ["thorn"] = 'þ', ["yuml"] = 'ÿ',
        ["OElig"] = 'Œ', ["oelig"] = 'œ', ["Scaron"] = 'Š', ["scaron"] = 'š', ["Yuml"] = 'Ÿ', ["fnof"] = 'ƒ',
        ["circ"] = 'ˆ', ["tilde"] = '˜', ["ensp"] = '\u2002', ["emsp"] = '\u2003', ["thinsp"] = '\u2009',
        ["zwnj"] = '\u200C', ["zwj"] = '\u200D', ["lrm"] = '\u200E', ["rlm"] = '\u200F', ["minus"] = '−',
    };

    public static string Decode(string s)
    {
        if (!s.Contains('&')) return s;
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c != '&') { sb.Append(c); i++; continue; }
            int semi = s.IndexOf(';', i + 1, Math.Min(12, s.Length - i - 1));
            if (semi < 0) { sb.Append(c); i++; continue; }
            string ent = s.Substring(i + 1, semi - i - 1);
            if (ent.Length > 1 && ent[0] == '#')
            {
                bool isHex = ent[1] == 'x' || ent[1] == 'X';
                string num = isHex ? ent.Substring(2) : ent.Substring(1);
                var style = isHex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer;
                if (num.Length > 0 && int.TryParse(num, style, null, out int code) && code > 0 && code <= 0x10FFFF)
                    sb.Append(char.ConvertFromUtf32(code));
                else
                    sb.Append('&');
                i = semi + 1;
                continue;
            }
            if (Named.TryGetValue(ent, out var ch)) { sb.Append(ch); i = semi + 1; continue; }
            sb.Append(c); i++;
        }
        return sb.ToString();
    }
}

// ============================================================================
// Parser HTML tolerante a HTML do mundo real (tags sem fechar, aninhamento
// implícito, comentários, DOCTYPE, tags raw-text como <script>/<style>).
// ============================================================================
public class HtmlParser
{
    private static readonly HashSet<string> VoidTags = new()
    { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr", "circle", "ellipse", "line", "path", "polygon", "polyline", "rect", "stop", "use" };

    private static readonly HashSet<string> RawTextTags = new() { "script", "style", "textarea", "title" };

    private static readonly HashSet<string> BlockTags = new()
    {
        "address", "article", "aside", "blockquote", "center", "details", "div", "dl", "dt", "dd",
        "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section", "summary", "table",
        "tbody", "thead", "tfoot", "tr", "td", "th", "ul"
    };

    private readonly string input;
    private int pos;

    public HtmlParser(string html) { input = html ?? ""; }

    public DomNode Parse()
    {
        var root = new DomNode(NodeType.Element) { TagName = "#document" };
        var stack = new List<DomNode> { root };

        while (pos < input.Length)
        {
            char c = input[pos];
            if (c != '<')
            {
                int start = pos;
                while (pos < input.Length && input[pos] != '<') pos++;
                if (pos > start)
                    stack[^1].Children.Add(new DomNode(NodeType.Text)
                    {
                        TextContent = HtmlEntities.Decode(input.Substring(start, pos - start)),
                        Parent = stack[^1]
                    });
                continue;
            }

            if (StartsWith("<!--")) { SkipUntil("-->", 4); continue; }
            if (StartsWith("<!") || StartsWith("<?")) { SkipUntil(">", 2); continue; }
            if (StartsWith("</"))
            {
                pos += 2;
                string name = ParseName().ToLowerInvariant();
                SkipUntil(">", 0);
                // fecha a tag aberta correspondente mais interna (tolera </b> solto etc.)
                for (int i = stack.Count - 1; i > 0; i--)
                {
                    if (stack[i].TagName == name) { stack.RemoveRange(i, stack.Count - i); break; }
                }
                continue;
            }

            // Start tag
            pos++; // consome '<'
            string tag = ParseName().ToLowerInvariant();
            if (tag.Length == 0) continue;
            var attrs = ParseAttributes(out bool selfClosing);

            ImplicitClose(tag, stack);

            var el = new DomNode(NodeType.Element) { TagName = tag, Parent = stack[^1] };
            foreach (var kv in attrs) el.Attributes[kv.Key] = kv.Value;
            stack[^1].Children.Add(el);

            if (RawTextTags.Contains(tag))
            {
                string raw = ReadRawText(tag);
                // textarea/title são raw-text mas com entities decodificadas (script/style não)
                if (tag is "textarea" or "title") raw = HtmlEntities.Decode(raw);
                if (raw.Length > 0)
                    el.Children.Add(new DomNode(NodeType.Text) { TextContent = raw, Parent = el });
                continue; // conteúdo raw, não faz parse nem empilha
            }
            if (!selfClosing && !VoidTags.Contains(tag))
                stack.Add(el);
        }
        return root;
    }

    // Fecha tags implicitamente, como navegadores reais fazem
    // (ex.: <li> sem </li>, <p> fechado por qualquer bloco).
    private static void ImplicitClose(string tag, List<DomNode> stack)
    {
        void PopWhile(params string[] names)
        {
            while (stack.Count > 1 && names.Contains(stack[^1].TagName))
                stack.RemoveAt(stack.Count - 1);
        }
        switch (tag)
        {
            case "li": PopWhile("li", "p"); break;
            case "dt":
            case "dd": PopWhile("dt", "dd", "p"); break;
            case "td":
            case "th": PopWhile("td", "th", "p"); break;
            case "tr": PopWhile("td", "th", "tr", "p"); break;
            case "option": PopWhile("option"); break;
            case "p": PopWhile("p"); break;
            default:
                if (BlockTags.Contains(tag)) PopWhile("p");
                break;
        }
    }

    private string ParseName()
    {
        int start = pos;
        while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '-' || input[pos] == '_' || input[pos] == ':'))
            pos++;
        return input.Substring(start, pos - start);
    }

    private Dictionary<string, string> ParseAttributes(out bool selfClosing)
    {
        selfClosing = false;
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (pos < input.Length)
        {
            SkipWs();
            if (pos >= input.Length) break;
            char c = input[pos];
            if (c == '>') { pos++; break; }
            if (c == '/') { pos++; selfClosing = true; continue; }
            if (c == '<') break; // HTML quebrado

            int nameStart = pos;
            while (pos < input.Length && !char.IsWhiteSpace(input[pos]) && input[pos] != '=' && input[pos] != '>' && input[pos] != '/')
                pos++;
            if (pos == nameStart) { pos++; continue; } // guarda anti-travamento
            string name = input.Substring(nameStart, pos - nameStart).ToLowerInvariant();

            SkipWs();
            string value = "";
            if (pos < input.Length && input[pos] == '=')
            {
                pos++;
                SkipWs();
                if (pos < input.Length && (input[pos] == '"' || input[pos] == '\''))
                {
                    char q = input[pos]; pos++;
                    int vStart = pos;
                    while (pos < input.Length && input[pos] != q) pos++;
                    value = input.Substring(vStart, pos - vStart);
                    if (pos < input.Length) pos++;
                }
                else
                {
                    int vStart = pos;
                    while (pos < input.Length && !char.IsWhiteSpace(input[pos]) && input[pos] != '>') pos++;
                    value = input.Substring(vStart, pos - vStart);
                }
                value = HtmlEntities.Decode(value);
            }
            attrs[name] = value;
        }
        return attrs;
    }

    private string ReadRawText(string tag)
    {
        string close = "</" + tag;
        int i = FindIgnoreCase(close, pos);
        string raw;
        if (i < 0) { raw = input.Substring(pos); pos = input.Length; }
        else { raw = input.Substring(pos, i - pos); pos = i; }
        return raw;
    }

    private void SkipUntil(string s, int already)
    {
        pos += already;
        int i = FindIgnoreCase(s, pos);
        pos = i < 0 ? input.Length : i + s.Length;
    }

    private int FindIgnoreCase(string s, int from)
    {
        for (int i = from; i <= input.Length - s.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < s.Length; j++)
                if (char.ToLowerInvariant(input[i + j]) != s[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    private bool StartsWith(string s)
    {
        if (pos + s.Length > input.Length) return false;
        for (int i = 0; i < s.Length; i++)
            if (input[pos + i] != s[i]) return false;
        return true;
    }

    private void SkipWs()
    {
        while (pos < input.Length && char.IsWhiteSpace(input[pos])) pos++;
    }
}
