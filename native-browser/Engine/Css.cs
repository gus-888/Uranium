using System.Globalization;
using System.Text;

namespace native_browser;

// ============================================================================
// Cores CSS: nomes, hex (#fff, #ffffff, #fffffff0), rgb()/rgba()
// ============================================================================
public static class CssColors
{
    public static readonly Dictionary<string, Color> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = Color.FromArgb(240, 248, 255), ["antiquewhite"] = Color.FromArgb(250, 235, 215),
        ["aqua"] = Color.FromArgb(0, 255, 255), ["aquamarine"] = Color.FromArgb(127, 255, 212),
        ["azure"] = Color.FromArgb(240, 255, 255), ["beige"] = Color.FromArgb(245, 245, 220),
        ["bisque"] = Color.FromArgb(255, 228, 196), ["black"] = Color.FromArgb(0, 0, 0),
        ["blanchedalmond"] = Color.FromArgb(255, 235, 205), ["blue"] = Color.FromArgb(0, 0, 255),
        ["blueviolet"] = Color.FromArgb(138, 43, 226), ["brown"] = Color.FromArgb(165, 42, 42),
        ["burlywood"] = Color.FromArgb(222, 184, 135), ["cadetblue"] = Color.FromArgb(95, 158, 160),
        ["chartreuse"] = Color.FromArgb(127, 255, 0), ["chocolate"] = Color.FromArgb(210, 105, 30),
        ["coral"] = Color.FromArgb(255, 127, 80), ["cornflowerblue"] = Color.FromArgb(100, 149, 237),
        ["cornsilk"] = Color.FromArgb(255, 248, 220), ["crimson"] = Color.FromArgb(220, 20, 60),
        ["cyan"] = Color.FromArgb(0, 255, 255), ["darkblue"] = Color.FromArgb(0, 0, 139),
        ["darkcyan"] = Color.FromArgb(0, 139, 139), ["darkgoldenrod"] = Color.FromArgb(184, 134, 11),
        ["darkgray"] = Color.FromArgb(169, 169, 169), ["darkgreen"] = Color.FromArgb(0, 100, 0),
        ["darkgrey"] = Color.FromArgb(169, 169, 169), ["darkkhaki"] = Color.FromArgb(189, 183, 107),
        ["darkmagenta"] = Color.FromArgb(139, 0, 139), ["darkolivegreen"] = Color.FromArgb(85, 107, 47),
        ["darkorange"] = Color.FromArgb(255, 140, 0), ["darkorchid"] = Color.FromArgb(153, 50, 204),
        ["darkred"] = Color.FromArgb(139, 0, 0), ["darksalmon"] = Color.FromArgb(233, 150, 122),
        ["darkseagreen"] = Color.FromArgb(143, 188, 143), ["darkslateblue"] = Color.FromArgb(72, 61, 139),
        ["darkslategray"] = Color.FromArgb(47, 79, 79), ["darkslategrey"] = Color.FromArgb(47, 79, 79),
        ["darkturquoise"] = Color.FromArgb(0, 206, 209), ["darkviolet"] = Color.FromArgb(148, 0, 211),
        ["deeppink"] = Color.FromArgb(255, 20, 147), ["deepskyblue"] = Color.FromArgb(0, 191, 255),
        ["dimgray"] = Color.FromArgb(105, 105, 105), ["dimgrey"] = Color.FromArgb(105, 105, 105),
        ["dodgerblue"] = Color.FromArgb(30, 144, 255), ["firebrick"] = Color.FromArgb(178, 34, 34),
        ["floralwhite"] = Color.FromArgb(255, 250, 240), ["forestgreen"] = Color.FromArgb(34, 139, 34),
        ["fuchsia"] = Color.FromArgb(255, 0, 255), ["gainsboro"] = Color.FromArgb(220, 220, 220),
        ["ghostwhite"] = Color.FromArgb(248, 248, 255), ["gold"] = Color.FromArgb(255, 215, 0),
        ["goldenrod"] = Color.FromArgb(218, 165, 32), ["gray"] = Color.FromArgb(128, 128, 128),
        ["green"] = Color.FromArgb(0, 128, 0), ["greenyellow"] = Color.FromArgb(173, 255, 47),
        ["grey"] = Color.FromArgb(128, 128, 128), ["honeydew"] = Color.FromArgb(240, 255, 240),
        ["hotpink"] = Color.FromArgb(255, 105, 180), ["indianred"] = Color.FromArgb(205, 92, 92),
        ["indigo"] = Color.FromArgb(75, 0, 130), ["ivory"] = Color.FromArgb(255, 255, 240),
        ["khaki"] = Color.FromArgb(240, 230, 140), ["lavender"] = Color.FromArgb(230, 230, 250),
        ["lavenderblush"] = Color.FromArgb(255, 240, 245), ["lawngreen"] = Color.FromArgb(124, 252, 0),
        ["lemonchiffon"] = Color.FromArgb(255, 250, 205), ["lightblue"] = Color.FromArgb(173, 216, 230),
        ["lightcoral"] = Color.FromArgb(240, 128, 128), ["lightcyan"] = Color.FromArgb(224, 255, 255),
        ["lightgoldenrodyellow"] = Color.FromArgb(250, 250, 210), ["lightgray"] = Color.FromArgb(211, 211, 211),
        ["lightgreen"] = Color.FromArgb(144, 238, 144), ["lightgrey"] = Color.FromArgb(211, 211, 211),
        ["lightpink"] = Color.FromArgb(255, 182, 193), ["lightsalmon"] = Color.FromArgb(255, 160, 122),
        ["lightseagreen"] = Color.FromArgb(32, 178, 170), ["lightskyblue"] = Color.FromArgb(135, 206, 250),
        ["lightslategray"] = Color.FromArgb(119, 136, 153), ["lightslategrey"] = Color.FromArgb(119, 136, 153),
        ["lightsteelblue"] = Color.FromArgb(176, 196, 222), ["lightyellow"] = Color.FromArgb(255, 255, 224),
        ["lime"] = Color.FromArgb(0, 255, 0), ["limegreen"] = Color.FromArgb(50, 205, 50),
        ["linen"] = Color.FromArgb(250, 240, 230), ["magenta"] = Color.FromArgb(255, 0, 255),
        ["maroon"] = Color.FromArgb(128, 0, 0), ["mediumaquamarine"] = Color.FromArgb(102, 205, 170),
        ["mediumblue"] = Color.FromArgb(0, 0, 205), ["mediumorchid"] = Color.FromArgb(186, 85, 211),
        ["mediumpurple"] = Color.FromArgb(147, 112, 219), ["mediumseagreen"] = Color.FromArgb(60, 179, 113),
        ["mediumslateblue"] = Color.FromArgb(123, 104, 238), ["mediumspringgreen"] = Color.FromArgb(0, 250, 154),
        ["mediumturquoise"] = Color.FromArgb(72, 209, 204), ["mediumvioletred"] = Color.FromArgb(199, 21, 133),
        ["midnightblue"] = Color.FromArgb(25, 25, 112), ["mintcream"] = Color.FromArgb(245, 255, 250),
        ["mistyrose"] = Color.FromArgb(255, 228, 225), ["moccasin"] = Color.FromArgb(255, 228, 181),
        ["navajowhite"] = Color.FromArgb(255, 222, 173), ["navy"] = Color.FromArgb(0, 0, 128),
        ["oldlace"] = Color.FromArgb(253, 245, 230), ["olive"] = Color.FromArgb(128, 128, 0),
        ["olivedrab"] = Color.FromArgb(107, 142, 35), ["orange"] = Color.FromArgb(255, 165, 0),
        ["orangered"] = Color.FromArgb(255, 69, 0), ["orchid"] = Color.FromArgb(218, 112, 214),
        ["palegoldenrod"] = Color.FromArgb(238, 232, 170), ["palegreen"] = Color.FromArgb(152, 251, 152),
        ["paleturquoise"] = Color.FromArgb(175, 238, 238), ["palevioletred"] = Color.FromArgb(219, 112, 147),
        ["papayawhip"] = Color.FromArgb(255, 239, 213), ["peachpuff"] = Color.FromArgb(255, 218, 185),
        ["peru"] = Color.FromArgb(205, 133, 63), ["pink"] = Color.FromArgb(255, 192, 203),
        ["plum"] = Color.FromArgb(221, 160, 221), ["powderblue"] = Color.FromArgb(176, 224, 230),
        ["purple"] = Color.FromArgb(128, 0, 128), ["rebeccapurple"] = Color.FromArgb(102, 51, 153),
        ["red"] = Color.FromArgb(255, 0, 0), ["rosybrown"] = Color.FromArgb(188, 143, 143),
        ["royalblue"] = Color.FromArgb(65, 105, 225), ["saddlebrown"] = Color.FromArgb(139, 69, 19),
        ["salmon"] = Color.FromArgb(250, 128, 114), ["sandybrown"] = Color.FromArgb(244, 164, 96),
        ["seagreen"] = Color.FromArgb(46, 139, 87), ["seashell"] = Color.FromArgb(255, 245, 238),
        ["sienna"] = Color.FromArgb(160, 82, 45), ["silver"] = Color.FromArgb(192, 192, 192),
        ["skyblue"] = Color.FromArgb(135, 206, 235), ["slateblue"] = Color.FromArgb(106, 90, 205),
        ["slategray"] = Color.FromArgb(112, 128, 144), ["slategrey"] = Color.FromArgb(112, 128, 144),
        ["snow"] = Color.FromArgb(255, 250, 250), ["springgreen"] = Color.FromArgb(0, 255, 127),
        ["steelblue"] = Color.FromArgb(70, 130, 180), ["tan"] = Color.FromArgb(210, 180, 140),
        ["teal"] = Color.FromArgb(0, 128, 128), ["thistle"] = Color.FromArgb(216, 191, 216),
        ["tomato"] = Color.FromArgb(255, 99, 71), ["turquoise"] = Color.FromArgb(64, 224, 208),
        ["violet"] = Color.FromArgb(238, 130, 238), ["wheat"] = Color.FromArgb(245, 222, 179),
        ["white"] = Color.FromArgb(255, 255, 255), ["whitesmoke"] = Color.FromArgb(245, 245, 245),
        ["yellow"] = Color.FromArgb(255, 255, 0), ["yellowgreen"] = Color.FromArgb(154, 205, 50),
    };

    public static Color? Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = CssValues.ResolveVar(s);
        if (s == null) return null;
        s = s.Trim();
        if (s.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return Color.Transparent;
        if (Named.TryGetValue(s, out var named)) return named;
        if (s[0] == '#')
        {
            string h = s.Substring(1);
            try
            {
                if (h.Length == 3)
                    return Color.FromArgb(To2(h[0]), To2(h[1]), To2(h[2]));
                if (h.Length == 4)
                    return Color.FromArgb(To2(h[3]), To2(h[0]), To2(h[1]), To2(h[2]));
                if (h.Length == 6)
                    return Color.FromArgb(255, To(h, 0), To(h, 2), To(h, 4));
                if (h.Length == 8)
                    return Color.FromArgb(To(h, 6), To(h, 0), To(h, 2), To(h, 4));
            }
            catch { return null; }
            return null;
        }
        if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) && s.Contains('(') && s.Contains(')'))
        {
            int open = s.IndexOf('('), close = s.IndexOf(')');
            var parts = s.Substring(open + 1, close - open - 1).Split(',');
            if (parts.Length >= 3 &&
                int.TryParse(parts[0].Trim().TrimEnd('%'), out int r) &&
                int.TryParse(parts[1].Trim().TrimEnd('%'), out int g) &&
                int.TryParse(parts[2].Trim().TrimEnd('%'), out int b))
            {
                int a = 255;
                if (parts.Length == 4 && float.TryParse(parts[3].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float af))
                    a = Math.Clamp((int)Math.Round(af * 255), 0, 255);
                return Color.FromArgb(a, Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
            }
        }
        return null;
    }

    private static byte To(string h, int i) => byte.Parse(h.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static byte To2(char c) => byte.Parse(new string(c, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

public enum LengthOp { None, Min, Max, Clamp }

// ============================================================================
// Comprimentos CSS (px, em, %, pt, vw/vh, calc/min/max/clamp...)
// ============================================================================
public sealed class CssLength
{
    public float Px;       // parte absoluta em px
    public float Percent;  // parte percentual (0-100+)
    public bool IsPercent; // tem parte percentual
    public bool IsAuto;
    public LengthOp Op;
    public List<CssLength> Args = new();

    public float Resolve(float hundredPercent)
    {
        if (IsAuto) return 0;
        switch (Op)
        {
            case LengthOp.Min:
            {
                float m = float.MaxValue;
                foreach (var a in Args) m = Math.Min(m, a.Resolve(hundredPercent));
                return m == float.MaxValue ? 0 : m;
            }
            case LengthOp.Max:
            {
                float m = float.MinValue;
                foreach (var a in Args) m = Math.Max(m, a.Resolve(hundredPercent));
                return m == float.MinValue ? 0 : m;
            }
            case LengthOp.Clamp:
            {
                if (Args.Count != 3) return 0;
                float lo = Args[0].Resolve(hundredPercent);
                float v = Args[1].Resolve(hundredPercent);
                float hi = Args[2].Resolve(hundredPercent);
                return Math.Clamp(v, Math.Min(lo, hi), Math.Max(lo, hi));
            }
        }
        return Px + (IsPercent ? Percent / 100f * hundredPercent : 0);
    }

    public static CssLength? Parse(string? s, float emContext) => Parse(s, emContext, 1280, 800, 16f);

    public static CssLength? Parse(string? s, float emContext, float viewportW, float viewportH, float rootPx)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = CssValues.ResolveVar(s);
        if (s == null) return null;
        s = s.Trim().ToLowerInvariant();
        if (s == "auto") return new CssLength { IsAuto = true };
        if (s == "0") return new CssLength { Px = 0 };
        if (s == "thin") return new CssLength { Px = 1 };
        if (s == "medium") return new CssLength { Px = 3 };
        if (s == "thick") return new CssLength { Px = 5 };

        // funções: calc / min / max / clamp (podem aninhar)
        if (s.StartsWith("calc(") && s.EndsWith(")"))
            return ParseCalc(s.Substring(5, s.Length - 6), emContext, viewportW, viewportH, rootPx);
        if (s.StartsWith("min(") && s.EndsWith(")"))
            return MakeFn(LengthOp.Min, s.Substring(4, s.Length - 5), emContext, viewportW, viewportH, rootPx);
        if (s.StartsWith("max(") && s.EndsWith(")"))
            return MakeFn(LengthOp.Max, s.Substring(4, s.Length - 5), emContext, viewportW, viewportH, rootPx);
        if (s.StartsWith("clamp(") && s.EndsWith(")"))
            return MakeFn(LengthOp.Clamp, s.Substring(6, s.Length - 7), emContext, viewportW, viewportH, rootPx);

        return ParseSingle(s, emContext, viewportW, viewportH, rootPx);
    }

    private static CssLength? MakeFn(LengthOp op, string inner, float em, float vw, float vh, float root)
    {
        var parts = SplitFnArgs(inner);
        if (parts.Count == 0) return null;
        if (op == LengthOp.Clamp && parts.Count != 3) return null;
        var args = new List<CssLength>();
        foreach (var p in parts)
        {
            var l = Parse(p, em, vw, vh, root);
            if (l == null) return null;
            args.Add(l);
        }
        // se tudo absoluto, dobra para px direto
        bool anyPct = false;
        foreach (var a in args) if (a.IsPercent || a.Op != LengthOp.None) anyPct = true;
        if (!anyPct)
        {
            var tmp = new CssLength { Op = op, Args = args };
            float v = tmp.Resolve(0);
            return new CssLength { Px = v };
        }
        // com %: mantém lazy para resolver com containingWidth no layout
        if (op == LengthOp.Clamp && args.Count != 3) return null;
        return new CssLength { Op = op, Args = args };
    }

    internal static List<string> SplitFnArgs(string inner)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        int depth = 0;
        foreach (char c in inner)
        {
            if (c == '(') depth++;
            if (c == ')') depth = Math.Max(0, depth - 1);
            if (c == ',' && depth == 0) { parts.Add(cur.ToString().Trim()); cur.Clear(); }
            else cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString().Trim());
        return parts.FindAll(p => p.Length > 0);
    }

    private static CssLength? ParseSingle(string s, float emContext, float vw, float vh, float root)
    {
        int end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-' || s[end] == '+')) end++;
        if (end == 0) return null;
        if (!float.TryParse(s.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return null;
        string unit = s.Substring(end).Trim();

        return unit switch
        {
            "" or "px" => new CssLength { Px = v },
            "%" => new CssLength { Percent = v, IsPercent = true },
            "em" or "ex" => new CssLength { Px = v * (unit == "ex" ? emContext * 0.5f : emContext) },
            "rem" => new CssLength { Px = v * (root <= 0 ? 16f : root) },
            "vw" => new CssLength { Px = v * vw / 100f },
            "vh" => new CssLength { Px = v * vh / 100f },
            "vmin" => new CssLength { Px = v * Math.Min(vw, vh) / 100f },
            "vmax" => new CssLength { Px = v * Math.Max(vw, vh) / 100f },
            "pt" => new CssLength { Px = v * 4f / 3f },
            "pc" => new CssLength { Px = v * 16f },
            "in" => new CssLength { Px = v * 96f },
            "cm" => new CssLength { Px = v * 37.795f },
            "mm" => new CssLength { Px = v * 3.7795f },
            "q" => new CssLength { Px = v * 0.945f },
            _ => null,
        };
    }

    // ---- calc(): avaliador simbólico Px + Percent ----
    private struct CalcVal { public double Px; public double Pct; public bool IsNum; public double Num; public bool Bad; }

    private static CssLength? ParseCalc(string expr, float em, float vw, float vh, float root)
    {
        var p = new CalcParser(expr, em, vw, vh, root);
        var v = p.ParseExpr();
        if (v.Bad || !p.Eof()) return null;
        if (v.IsNum) return new CssLength { Px = (float)v.Num };
        return new CssLength { Px = (float)v.Px, Percent = (float)v.Pct, IsPercent = Math.Abs(v.Pct) > 1e-9 };
    }

    private sealed class CalcParser
    {
        private readonly string s; private int i;
        private readonly float em, vw, vh, root;
        public CalcParser(string s, float em, float vw, float vh, float root) { this.s = s; this.em = em; this.vw = vw; this.vh = vh; this.root = root; }
        public bool Eof() { Ws(); return i >= s.Length; }
        private void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        public CalcVal ParseExpr()
        {
            var v = ParseTerm();
            while (true)
            {
                Ws();
                if (i >= s.Length || (s[i] != '+' && s[i] != '-')) return v;
                char op = s[i++];
                var r = ParseTerm();
                if (v.Bad || r.Bad) return new CalcVal { Bad = true };
                if (v.IsNum && r.IsNum) v = new CalcVal { IsNum = true, Num = op == '+' ? v.Num + r.Num : v.Num - r.Num };
                else
                {
                    double vp = v.IsNum ? v.Num : v.Px, pp = v.IsNum ? 0 : v.Pct;
                    double vq = r.IsNum ? r.Num : r.Px, pq = r.IsNum ? 0 : r.Pct;
                    v = op == '+'
                        ? new CalcVal { Px = vp + vq, Pct = pp + pq }
                        : new CalcVal { Px = vp - vq, Pct = pp - pq };
                }
            }
        }
        private CalcVal ParseTerm()
        {
            var v = ParseFactor();
            while (true)
            {
                Ws();
                if (i >= s.Length || (s[i] != '*' && s[i] != '/')) return v;
                char op = s[i++];
                var r = ParseFactor();
                if (v.Bad || r.Bad) return new CalcVal { Bad = true };
                if (op == '*')
                {
                    if (v.IsNum && r.IsNum) v = new CalcVal { IsNum = true, Num = v.Num * r.Num };
                    else if (v.IsNum) v = new CalcVal { Px = r.Px * v.Num, Pct = r.Pct * v.Num };
                    else if (r.IsNum) v = new CalcVal { Px = v.Px * r.Num, Pct = v.Pct * r.Num };
                    else return new CalcVal { Bad = true }; // comprimento * comprimento
                }
                else
                {
                    if (!r.IsNum || Math.Abs(r.Num) < 1e-12) return new CalcVal { Bad = true };
                    if (v.IsNum) v = new CalcVal { IsNum = true, Num = v.Num / r.Num };
                    else v = new CalcVal { Px = v.Px / r.Num, Pct = v.Pct / r.Num };
                }
            }
        }
        private CalcVal ParseFactor()
        {
            Ws();
            if (i >= s.Length) return new CalcVal { Bad = true };
            if (s[i] == '(') { i++; var v = ParseExpr(); Ws(); if (i >= s.Length || s[i] != ')') return new CalcVal { Bad = true }; i++; return v; }
            // funções aninhadas
            foreach (var fn in new[] { "calc(", "min(", "max(", "clamp(" })
            {
                if (s.AsSpan(i).StartsWith(fn, StringComparison.OrdinalIgnoreCase))
                {
                    int open = s.IndexOf('(', i);
                    int close = FindClose(i);
                    if (close < 0) return new CalcVal { Bad = true };
                    string inner = s.Substring(open + 1, close - open - 1);
                    i = close + 1;
                    string low = fn[..^1].ToLowerInvariant();
                    if (low == "calc") { var p2 = new CalcParser(inner, em, vw, vh, root); var v2 = p2.ParseExpr(); if (v2.Bad || !p2.Eof()) return new CalcVal { Bad = true }; return v2; }
                    LengthOp op = low == "min" ? LengthOp.Min : low == "max" ? LengthOp.Max : LengthOp.Clamp;
                    var l = MakeFn(op, inner, em, vw, vh, root);
                    if (l == null) return new CalcVal { Bad = true };
                    if (l.Op == LengthOp.None) return new CalcVal { Px = l.Px, Pct = l.IsPercent ? l.Percent : 0 };
                    // com % adiado: aproxima com containingWidth = viewport para dentro do calc
                    return new CalcVal { Px = l.Resolve(vw), Pct = 0 };
                }
            }
            // número com unidade
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == '+' || s[i] == '-'))
            {
                if ((s[i] == '+' || s[i] == '-') && i != st) break;
                i++;
            }
            if (i == st) return new CalcVal { Bad = true };
            if (!double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) return new CalcVal { Bad = true };
            int us = i;
            while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '%')) i++;
            string unit = s.Substring(us, i - us).Trim().ToLowerInvariant();
            if (unit.Length == 0) return new CalcVal { IsNum = true, Num = n };
            var single = ParseSingle(n.ToString(CultureInfo.InvariantCulture) + unit, em, vw, vh, root);
            if (single == null) return new CalcVal { Bad = true };
            if (single.Op != LengthOp.None)
            {
                // min/max/clamp com % dentro do calc: resolve contra viewport como aproximação
                return new CalcVal { Px = single.Resolve(vw), Pct = 0 };
            }
            return new CalcVal { Px = single.Px, Pct = single.IsPercent ? single.Percent : 0 };
        }
        private int FindClose(int from)
        {
            int open = s.IndexOf('(', from);
            if (open < 0) return -1;
            int d = 0;
            for (int k = open; k < s.Length; k++)
            {
                if (s[k] == '(') d++;
                else if (s[k] == ')') { d--; if (d == 0) return k; }
            }
            return -1;
        }
    }
}

// ============================================================================
// Seletores e regras CSS
// ============================================================================
public enum Combinator { Descendant, Child, Adjacent, Sibling }

public class AttrSel
{
    public string Name = "";
    public string Op = ""; // "", "=", "~=", "|=", "^=", "$=", "*="
    public string Value = "";
}

public class SimpleSelector
{
    public string? Tag;
    public string? Id;
    public List<string> Classes = new();
    public List<AttrSel> Attrs = new();
    public List<SimpleSelector> Not = new();
    public List<string> Langs = new();
    public string? Dir; // "ltr" | "rtl"
    public bool FirstChild, LastChild;

    public bool Matches(DomNode n)
    {
        if (n.Type != NodeType.Element) return false;
        // Tag vazio = seletor universal (`*`), que casa com QUALQUER elemento.
        // Sem esta guarda `querySelectorAll('*')` devolvia 0 e o Polymer não
        // enxergava a árvore (usava a lista vazia para decidir o que estilizar).
        if (Tag != null && Tag.Length > 0 && Tag != n.TagName) return false;
        if (Id != null && !string.Equals(Id, n.Id, StringComparison.OrdinalIgnoreCase)) return false;
        var classes = n.GetClasses();
        foreach (var c in Classes)
            if (!classes.Contains(c, StringComparer.Ordinal)) return false;
        foreach (var a in Attrs)
        {
            string v = n.GetAttr(a.Name);
            bool has = n.Attributes.ContainsKey(a.Name);
            switch (a.Op)
            {
                case "": if (!has) return false; break;
                case "=": if (!string.Equals(v, a.Value, StringComparison.OrdinalIgnoreCase)) return false; break;
                case "~=":
                {
                    var toks = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (!toks.Contains(a.Value, StringComparer.OrdinalIgnoreCase)) return false;
                    break;
                }
                case "|=":
                    if (!(string.Equals(v, a.Value, StringComparison.OrdinalIgnoreCase) || v.StartsWith(a.Value + "-", StringComparison.OrdinalIgnoreCase))) return false;
                    break;
                case "^=": if (!v.StartsWith(a.Value, StringComparison.OrdinalIgnoreCase)) return false; break;
                case "$=": if (!v.EndsWith(a.Value, StringComparison.OrdinalIgnoreCase)) return false; break;
                case "*=": if (v.IndexOf(a.Value, StringComparison.OrdinalIgnoreCase) < 0) return false; break;
            }
        }
        foreach (var nn in Not)
            if (nn.Matches(n)) return false;
        if (Langs.Count > 0)
        {
            string? lang = null;
            for (var e = n; e != null && e.Type == NodeType.Element; e = e.Parent)
            {
                string l = e.GetAttr("lang");
                if (l.Length == 0) l = e.GetAttr("xml:lang");
                if (l.Length > 0) { lang = l; break; }
            }
            if (lang == null) return false;
            bool ok = false;
            foreach (var want in Langs)
            {
                if (lang.Equals(want, StringComparison.OrdinalIgnoreCase) ||
                    lang.StartsWith(want + "-", StringComparison.OrdinalIgnoreCase)) { ok = true; break; }
            }
            if (!ok) return false;
        }
        if (Dir != null)
        {
            string dir = "ltr";
            for (var e = n; e != null && e.Type == NodeType.Element; e = e.Parent)
            {
                string d = e.GetAttr("dir");
                if (d.Length > 0) { dir = d.ToLowerInvariant(); break; }
            }
            if (dir != Dir) return false;
        }
        if (FirstChild || LastChild)
        {
            var p = n.Parent;
            if (p == null) return false;
            DomNode? first = null, last = null;
            foreach (var c in p.Children)
            {
                if (c.Type != NodeType.Element) continue;
                first ??= c;
                last = c;
            }
            if (FirstChild && !ReferenceEquals(first, n)) return false;
            if (LastChild && !ReferenceEquals(last, n)) return false;
        }
        return true;
    }
}

public class CssSelector
{
    public SimpleSelector[] Chain = Array.Empty<SimpleSelector>(); // [ancestral mais externo ... alvo]
    public Combinator[] Combs = Array.Empty<Combinator>(); // Combs[i] liga Chain[i] -> Chain[i+1]

    private static DomNode? PrevElement(DomNode n)
    {
        var p = n.Parent;
        if (p == null) return null;
        DomNode? prev = null;
        foreach (var c in p.Children)
        {
            if (ReferenceEquals(c, n)) return prev;
            if (c.Type == NodeType.Element) prev = c;
        }
        return null;
    }

    public bool Matches(DomNode n)
    {
        int i = Chain.Length - 1;
        DomNode? cur = n;
        if (cur == null || !Chain[i].Matches(cur)) return false;
        while (i > 0)
        {
            var comb = Combs[i - 1];
            var want = Chain[i - 1];
            if (comb == Combinator.Child)
            {
                cur = cur.Parent;
                if (cur == null || cur.Type != NodeType.Element || !want.Matches(cur)) return false;
                i--;
            }
            else if (comb == Combinator.Descendant)
            {
                cur = cur.Parent;
                bool found = false;
                while (cur != null)
                {
                    if (cur.Type == NodeType.Element && want.Matches(cur)) { found = true; break; }
                    cur = cur.Parent;
                }
                if (!found) return false;
                i--;
            }
            else if (comb == Combinator.Adjacent)
            {
                if (cur == null) return false;
                cur = PrevElement(cur);
                if (cur == null || !want.Matches(cur)) return false;
                i--;
            }
            else // Sibling (~)
            {
                if (cur == null) return false;
                var p = cur.Parent;
                if (p == null) return false;
                bool found = false;
                foreach (var c in p.Children)
                {
                    if (ReferenceEquals(c, cur)) break;
                    if (c.Type == NodeType.Element && want.Matches(c)) found = true;
                }
                if (!found) return false;
                // para ~, qualquer irmão anterior que casa basta; continua com aquele? usa o parent walk simplificado:
                // encontra o irmão mais próximo que casa e segue dele
                DomNode? best = null;
                foreach (var c in p.Children)
                {
                    if (ReferenceEquals(c, cur)) break;
                    if (c.Type == NodeType.Element && want.Matches(c)) best = c;
                }
                cur = best;
                i--;
            }
        }
        return true;
    }

    public (int, int, int) Specificity()
    {
        int ids = 0, classes = 0, tags = 0;
        foreach (var part in Chain)
        {
            if (part.Id != null) ids++;
            classes += part.Classes.Count + part.Attrs.Count + part.Langs.Count;
            if (part.FirstChild || part.LastChild) classes++;
            if (part.Dir != null) classes++;
            foreach (var nn in part.Not)
            {
                if (nn.Id != null) ids++;
                classes += nn.Classes.Count + nn.Attrs.Count;
                if (nn.Tag != null) tags++;
            }
            if (part.Tag != null) tags++;
        }
        return (ids, classes, tags);
    }
}

public class CssRule
{
    public List<CssSelector> Selectors = new();
    public Dictionary<string, string> Declarations = new(StringComparer.OrdinalIgnoreCase);
    public int Origin;  // 0 = user-agent (padrão do navegador), 1 = autor da página
    public int Order;

    public bool Matches(DomNode n)
    {
        foreach (var s in Selectors) if (s.Matches(n)) return true;
        return false;
    }
}

// ============================================================================
// @keyframes: armazena keyframes de uma animação CSS
// ============================================================================
public class CssKeyframes
{
    public string Name = "";
    /// <summary>Lista de (porcentagem 0..100, declarações CSS) em ordem.</summary>
    public List<(float Percent, Dictionary<string, string> Declarations)> Stops = new();
}

/// <summary>Dados de animação resolvidos para um elemento.</summary>
public class CssAnimation
{
    public string Name = "";
    public float Duration = 0;       // segundos (0 = instantâneo)
    public float Delay = 0;
    public int IterationCount = 1;   // 0 = infinito
    public string TimingFunction = "ease"; // ease, linear, ease-in, ease-out, ease-in-out
    public string FillMode = "none";       // none, forwards, backwards, both
    public string Direction = "normal";     // normal, reverse, alternate, alternate-reverse
}

// ============================================================================
// Parser de folhas de estilo
// ============================================================================
public static class CssParser
{
    /// <summary>Keyframes coletados de todas as folhas de estilo (nome → keyframes).</summary>
    public static readonly Dictionary<string, CssKeyframes> KeyframesMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adiciona regras de uma folha de estilo CSS à lista.</summary>
    public static void Parse(string css, List<CssRule> rules, int origin, int viewportWidth = 1280)
    {
        if (string.IsNullOrEmpty(css)) return;
        int pos = 0, order = rules.Count;

        while (pos < css.Length)
        {
            SkipWsAndComments(css, ref pos);
            if (pos >= css.Length) break;

            if (css[pos] == '@')
            {
                int semi = css.IndexOf(';', pos);
                int brace = css.IndexOf('{', pos);
                bool blockRule = brace >= 0 && (semi < 0 || brace < semi);

                if (blockRule)
                {
                    int blockEnd = MatchBrace(css, brace);
                    string prelude = css.Substring(pos, brace - pos);
                    string atName = ReadAtName(prelude);

                    // @media: entra no bloco quando a query casa com a janela;
                    // @supports: entra sempre (aproximação de navegador moderno)
                    if (atName == "@keyframes")
                    {
                        ParseKeyframes(prelude, css.Substring(brace + 1, blockEnd - brace - 1));
                    }
                    else if ((atName == "@media" && MediaMatches(prelude, viewportWidth)) || atName == "@supports")
                    {
                        string inner = css.Substring(brace + 1, blockEnd - brace - 1);
                        Parse(inner, rules, origin, viewportWidth);
                    }
                    pos = blockEnd + 1;
                }
                else if (semi >= 0) pos = semi + 1;
                else break;
                continue;
            }

            int selStart = pos;
            int open = FindTopLevel(css, ref pos, '{');
            if (open == -1) break;
            if (open == -2) { pos++; continue; } // '}' órfã — pula e segue
            string selText = css.Substring(selStart, open - selStart).Trim();

            pos = open + 1;
            int declStart = pos;
            int close = FindTopLevel(css, ref pos, '}');
            string declText = close < 0 ? css.Substring(declStart) : css.Substring(declStart, close - declStart);
            pos = close < 0 ? css.Length : close + 1;

            if (selText.Length == 0) continue;
            var decls = ParseDeclarations(declText);
            if (decls.Count == 0) continue;

            var rule = new CssRule { Origin = origin, Order = order++ };
            foreach (var selRaw in SplitSelectors(selText))
            {
                var sel = ParseSelector(selRaw);
                if (sel != null) rule.Selectors.Add(sel);
            }
            if (rule.Selectors.Count == 0) continue;
            rule.Declarations = decls;
            rules.Add(rule);
        }
    }

    public static Dictionary<string, string> ParseDeclarations(string text)
    {
        var decls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in SplitDeclarations(text))
        {
            int colon = IndexOfTopColon(raw);
            if (colon <= 0) continue;
            string propRaw = raw.Substring(0, colon).Trim();
            string prop = propRaw.StartsWith("--") ? propRaw : propRaw.ToLowerInvariant();
            string val = raw.Substring(colon + 1).Trim();
            if (val.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
                val = val.Substring(0, val.Length - "!important".Length).Trim();
            if (prop.Length == 0 || val.Length == 0) continue;
            int cstart = val.IndexOf("/*", StringComparison.Ordinal);
            if (cstart >= 0) val = val.Substring(0, cstart).Trim();
            if (val.Length == 0) continue;
            decls[prop] = val;
        }
        return decls;
    }

    /// <summary>Busca descendentes (e o próprio escopo) que casam com o seletor (para querySelector).</summary>
    public static List<DomNode> QueryAll(DomNode scope, string selector, bool includeSelf = true)
    {
        var found = new List<DomNode>();
        foreach (var part in SplitSelectors(selector))
        {
            var sel = ParseSelector(part);
            if (sel == null) continue;
            void Walk(DomNode n, bool self)
            {
                if ((self || !ReferenceEquals(n, scope)) && n.Type == NodeType.Element && sel.Matches(n) && !found.Contains(n))
                    found.Add(n);
                foreach (var c in n.Children) Walk(c, false);
                // shadow roots participam da busca: Polymer consulta component a
                // component nos próprios mundos — sem isto o page-manager importado
                // era invisível para document.querySelector no ciclo de SPA
                if (n.ShadowRoot != null) Walk(n.ShadowRoot, false);
            }
            Walk(scope, includeSelf);
        }
        return found;
    }

    /// <summary>Testa se um nó casa com um seletor (usado por Element.matches/closest).</summary>
    public static bool MatchesSelector(DomNode n, string selector)
    {
        if (n == null || n.Type != NodeType.Element || string.IsNullOrWhiteSpace(selector)) return false;
        foreach (var part in SplitSelectors(selector))
        {
            var sel = ParseSelector(part);
            if (sel != null && sel.Matches(n)) return true;
        }
        return false;
    }

    private static List<string> SplitSelectors(string text)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        int depthB = 0, depthP = 0;
        bool sq = false, dq = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !dq) sq = !sq;
            else if (c == '"' && !sq) dq = !dq;
            if (!sq && !dq)
            {
                if (c == '[') depthB++;
                else if (c == ']') depthB = Math.Max(0, depthB - 1);
                else if (c == '(') depthP++;
                else if (c == ')') depthP = Math.Max(0, depthP - 1);
                else if (c == ',' && depthB == 0 && depthP == 0) { parts.Add(cur.ToString().Trim()); cur.Clear(); continue; }
            }
            cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString().Trim());
        return parts.FindAll(p => p.Length > 0);
    }

    private static List<string> SplitDeclarations(string text)
    {
        var parts = new List<string>();
        var cur = new StringBuilder();
        int depth = 0;
        bool sq = false, dq = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !dq) sq = !sq;
            else if (c == '"' && !sq) dq = !dq;
            if (!sq && !dq)
            {
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                else if (c == ';' && depth == 0) { parts.Add(cur.ToString()); cur.Clear(); continue; }
            }
            cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }

    private static int IndexOfTopColon(string s)
    {
        int depth = 0;
        bool sq = false, dq = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !dq) sq = !sq;
            else if (c == '"' && !sq) dq = !dq;
            if (sq || dq) continue;
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == ':' && depth == 0) return i;
        }
        return -1;
    }

    private static CssSelector? ParseSelector(string s)
    {
        // Divide em simples + combinadores (espaço, >, +, ~), respeitando [] e ().
        var chain = new List<SimpleSelector>();
        var combs = new List<Combinator>();
        var cur = new StringBuilder();
        int depthB = 0, depthP = 0;
        Combinator pending = Combinator.Descendant;
        bool first = true;
        for (int i = 0; i <= s.Length; i++)
        {
            char c = i < s.Length ? s[i] : '\0';
            if (i < s.Length)
            {
                if (c == '[') depthB++;
                else if (c == ']') depthB = Math.Max(0, depthB - 1);
                else if (c == '(') depthP++;
                else if (c == ')') depthP = Math.Max(0, depthP - 1);
            }
            bool top = depthB == 0 && depthP == 0;
            bool isComb = i < s.Length && top && (c == '>' || c == '+' || c == '~');
            bool isSpace = i >= s.Length || (top && (char.IsWhiteSpace(c) || isComb));
            if (isComb)
            {
                string t = cur.ToString().Trim();
                cur.Clear();
                if (t.Length > 0)
                {
                    var simple = ParseSimple(t);
                    if (simple == null) return null;
                    if (!first) combs.Add(pending);
                    chain.Add(simple);
                    first = false;
                }
                pending = c == '>' ? Combinator.Child : c == '+' ? Combinator.Adjacent : Combinator.Sibling;
                // pula espaços após combinador
                while (i + 1 < s.Length && char.IsWhiteSpace(s[i + 1])) i++;
                continue;
            }
            if (isSpace)
            {
                string t = cur.ToString().Trim();
                cur.Clear();
                if (t.Length > 0)
                {
                    var simple = ParseSimple(t);
                    if (simple == null) return null;
                    if (!first) combs.Add(pending);
                    chain.Add(simple);
                    first = false;
                }
                pending = Combinator.Descendant;
                continue;
            }
            if (i < s.Length) cur.Append(c);
        }
        if (chain.Count == 0) return null;
        while (combs.Count < chain.Count - 1) combs.Add(Combinator.Descendant);
        return new CssSelector { Chain = chain.ToArray(), Combs = combs.ToArray() };
    }

    private static SimpleSelector? ParseSimple(string part)
    {
        var simple = new SimpleSelector();
        for (int i = 0; i < part.Length; i++)
        {
            char c = part[i];
            // `*` é o seletor universal: casa com qualquer elemento. Marcar Tag como
            // string vazia (em vez de deixar null) faz a validação final aceitar e o
            // Matches tratar como "qualquer tag" — antes `querySelectorAll('*')`
            // devolvia 0 e o Polymer não enxergava a árvore.
            if (c == '*')
            {
                if (simple.Tag == null) simple.Tag = "";
                continue;
            }
            if (c == '#')
            {
                int st = ++i;
                while (i < part.Length && IsNameChar(part[i])) i++;
                simple.Id = part.Substring(st, i - st);
                i--;
            }
            else if (c == '.')
            {
                int st = ++i;
                while (i < part.Length && IsNameChar(part[i])) i++;
                string cls = part.Substring(st, i - st);
                if (cls.Length == 0) return null;
                simple.Classes.Add(cls);
                i--;
            }
            else if (c == '[')
            {
                int close = part.IndexOf(']', i);
                if (close < 0) return null;
                string inner = part.Substring(i + 1, close - i - 1).Trim();
                i = close;
                var attr = ParseAttr(inner);
                if (attr == null) return null;
                simple.Attrs.Add(attr);
            }
            else if (c == ':')
            {
                bool pseudoEl = i + 1 < part.Length && part[i + 1] == ':';
                int st = i + (pseudoEl ? 2 : 1);
                int j = st;
                while (j < part.Length && (char.IsLetterOrDigit(part[j]) || part[j] == '-')) j++;
                string name = part.Substring(st, j - st).ToLowerInvariant();
                string arg = "";
                if (j < part.Length && part[j] == '(')
                {
                    int d = 1, k = j + 1;
                    while (k < part.Length && d > 0) { if (part[k] == '(') d++; else if (part[k] == ')') d--; k++; }
                    if (d != 0) return null;
                    arg = part.Substring(j + 1, k - j - 2);
                    j = k;
                }
                i = j - 1;
                if (pseudoEl || name is "before" or "after" or "first-line" or "first-letter" or "marker" or "selection" or "placeholder" or "backdrop")
                    return null; // pseudo-elemento: não casa com o elemento
                switch (name)
                {
                    case "not":
                    {
                        var inner = ParseSimple(arg.Trim());
                        if (inner == null) return null;
                        simple.Not.Add(inner);
                        break;
                    }
                    case "lang":
                    {
                        bool any = false;
                        foreach (var piece in CssLength.SplitFnArgs(arg))
                        {
                            string w = piece.Trim().Trim('"', '\'').ToLowerInvariant();
                            if (w is "*" or "") continue;
                            simple.Langs.Add(w);
                            any = true;
                        }
                        if (!any) return null; // :lang() vazio: ignora a regra
                        break;
                    }
                    case "dir":
                    {
                        string d = arg.Trim().Trim('"', '\'').ToLowerInvariant();
                        if (d is not ("ltr" or "rtl")) break; // valor inválido: ignora o pseudo
                        simple.Dir = d;
                        break;
                    }
                    case "first-child": simple.FirstChild = true; break;
                    case "last-child": simple.LastChild = true; break;
                    case "only-child": simple.FirstChild = simple.LastChild = true; break;
                    case "empty": break; // aproxima como match (raro)
                    case "root": simple.Tag ??= "html"; break;
                    case "hover" or "active" or "focus" or "focus-visible" or "focus-within" or "visited" or "link" or "target":
                        return null; // estado dinâmico: ignora a regra no layout estático
                    default:
                        break; // pseudo desconhecida: ignora (match)
                }
            }
            else
            {
                int st = i;
                while (i < part.Length && (char.IsLetterOrDigit(part[i]) || part[i] == '-' || part[i] == '|')) i++;
                if (i == st) return null;
                string tag = part.Substring(st, i - st).ToLowerInvariant();
                int pipe = tag.IndexOf('|');
                if (pipe >= 0) tag = tag.Substring(pipe + 1);
                if (tag.Length > 0) simple.Tag = tag;
                i--;
            }
        }
        if (simple.Tag == null && simple.Id == null && simple.Classes.Count == 0 && simple.Attrs.Count == 0 && simple.Not.Count == 0 && simple.Langs.Count == 0 && simple.Dir == null && !simple.FirstChild && !simple.LastChild)
            return null;
        return simple;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '-' || c == '_';

    private static AttrSel? ParseAttr(string inner)
    {
        foreach (var op in new[] { "~=", "|=", "^=", "$=", "*=", "=" })
        {
            int idx = inner.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0)
            {
                string name = inner.Substring(0, idx).Trim().ToLowerInvariant();
                string val = inner.Substring(idx + op.Length).Trim().Trim('"', '\'');
                if (name.Length == 0) return null;
                return new AttrSel { Name = name, Op = op, Value = val };
            }
        }
        string n = inner.Trim().ToLowerInvariant();
        if (n.Length == 0) return null;
        return new AttrSel { Name = n, Op = "" };
    }

    private static string ReadAtName(string prelude)
    {
        var sb = new StringBuilder();
        foreach (char c in prelude)
        {
            if (c == '@') { sb.Append(c); continue; }
            if (char.IsLetter(c) || c == '-') sb.Append(c);
            else break;
        }
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>Extrai @keyframes nome { ... } e registra em KeyframesMap.</summary>
    private static void ParseKeyframes(string prelude, string inner)
    {
        // "@keyframes nome" → extrai "nome"
        string raw = prelude.IndexOf(' ') >= 0 ? prelude.Substring(prelude.IndexOf(' ')).Trim() : "";
        if (raw.Length == 0) return;
        // remove tudo após espaço (se houver "nome extra")
        int sp = raw.IndexOf(' ');
        if (sp >= 0) raw = raw.Substring(0, sp);
        raw = raw.Trim('"', '\'');

        var kf = new CssKeyframes { Name = raw };
        int pos = 0;
        while (pos < inner.Length)
        {
            SkipWsAndComments(inner, ref pos);
            if (pos >= inner.Length) break;
            // lê selector (porcentagem ou from/to) até '{'
            int brace = inner.IndexOf('{', pos);
            if (brace < 0) break;
            string selText = inner.Substring(pos, brace - pos).Trim();
            pos = brace + 1;
            int close = inner.IndexOf('}', pos);
            if (close < 0) close = inner.Length;
            string declText = inner.Substring(pos, close - pos);
            pos = close + 1;

            float pct = selText.ToLowerInvariant() switch
            {
                "from" => 0f,
                "to" => 100f,
                _ => CssValues.TryNumber(selText.TrimEnd('%')) ?? -1f,
            };
            if (pct < 0 || pct > 100) continue;
            var decls = ParseDeclarations(declText);
            if (decls.Count > 0) kf.Stops.Add((pct, decls));
        }
        if (kf.Stops.Count > 0) KeyframesMap[kf.Name] = kf;
    }

    /// <summary>Tema escuro do Windows (para prefers-color-scheme). Definido pelo BrowserForm.</summary>
    public static bool SystemDark;

    /// <summary>
    /// Avalia uma media query: screen/all passam, print/speech/handheld e recursos
    /// desconhecidos rejeitam; prefers-color-scheme segue o tema do Windows.
    /// </summary>
    private static bool MediaMatches(string prelude, int viewportWidth)
    {
        foreach (var clauseRaw in prelude.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string clause = clauseRaw.ToLowerInvariant();
            if (clause.StartsWith("@media")) clause = clause.Substring(6).Trim();
            if (clause.StartsWith("only")) clause = clause.Substring(4).Trim();

            if (clause.Contains("print") || clause.Contains("speech") || clause.Contains("handheld") ||
                clause.Contains("forced-colors") || clause.Contains("resolution") ||
                clause.Contains("dynamic-range") || clause.Contains("update-rate"))
                continue;

            // prefers-color-scheme: dark/light conforme o Windows; outros prefers-* rejeitam
            if (clause.Contains("prefers-color-scheme"))
            {
                bool wantsDark = clause.Contains("dark");
                bool wantsLight = clause.Contains("light");
                if (wantsDark == wantsLight) continue; // ambíguo ou estranho
                if (wantsDark != SystemDark) continue;
                clause = StripFeature(clause, "prefers-color-scheme");
            }
            else if (clause.Contains("prefers-"))
            {
                continue;
            }

            if (clause.StartsWith("not ")) clause = clause.Substring(4).Trim();

            bool ok = true;
            int i = 0;
            while (i < clause.Length && ok)
            {
                int open = clause.IndexOf('(', i);
                if (open < 0) break;
                int close = clause.IndexOf(')', open);
                if (close < 0) break;
                string feat = clause.Substring(open + 1, close - open - 1).Trim();
                i = close + 1;
                ok = FeatureMatches(feat, viewportWidth);
            }
            if (ok) return true;
        }
        return false;
    }

    /// <summary>Remove todos os "(nome: ...)" da clause (para features já avaliadas).</summary>
    private static string StripFeature(string clause, string name)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < clause.Length)
        {
            int open = clause.IndexOf('(', i);
            if (open < 0) { sb.Append(clause.Substring(i)); break; }
            int close = clause.IndexOf(')', open);
            if (close < 0) { sb.Append(clause.Substring(i)); break; }
            string feat = clause.Substring(open + 1, close - open - 1).Trim();
            if (feat.StartsWith(name, StringComparison.Ordinal))
            {
                sb.Append(clause.Substring(i, open - i));
                sb.Append(' ');
            }
            else
            {
                sb.Append(clause.Substring(i, close + 1 - i));
            }
            i = close + 1;
        }
        return sb.ToString();
    }

    private static bool FeatureMatches(string feat, int viewportWidth)
    {
        // ex: "min-width: 640px", "max-width: 720px", "min-resolution: 2dppx"
        int colon = feat.IndexOf(':');
        if (colon < 0)
        {
            // consulta só de recurso, ex "(hover)" — sem como avaliar, rejeita
            return false;
        }
        string name = feat.Substring(0, colon).Trim();
        string value = feat.Substring(colon + 1).Trim();
        if (value.Contains("calc") || value.Length == 0) return false;

        float? px = null;
        if (value.EndsWith("px")) px = CssValues.TryNumber(value.TrimEnd('x', 'p'));
        else if (value.EndsWith("em") || value.EndsWith("rem")) px = (CssValues.TryNumber(value.TrimEnd('m', 'e', 'r')) ?? 0) * 16f;
        if (px == null) return false;

        return name switch
        {
            "min-width" => viewportWidth >= px.Value,
            "max-width" => viewportWidth <= px.Value,
            _ => false,
        };
    }

    private static void SkipWsAndComments(string css, ref int pos)
    {
        while (pos < css.Length)
        {
            if (char.IsWhiteSpace(css[pos])) { pos++; continue; }
            if (css[pos] == '/' && pos + 1 < css.Length && css[pos + 1] == '*')
            {
                int end = css.IndexOf("*/", pos + 2);
                pos = end < 0 ? css.Length : end + 2;
                continue;
            }
            break;
        }
    }

    private static int FindTopLevel(string css, ref int start, char target)
    {
        SkipWsAndComments(css, ref start);
        int pos = start;
        while (pos < css.Length)
        {
            char c = css[pos];
            if (c == '/' && pos + 1 < css.Length && css[pos + 1] == '*')
            {
                int end = css.IndexOf("*/", pos + 2);
                pos = end < 0 ? css.Length : end + 2;
                continue;
            }
            if (c == target) { start = pos; return pos; }
            if (c == '}' ) { start = pos; return -2; } // fim prematuro
            pos++;
        }
        return -1;
    }

    private static int MatchBrace(string css, int open)
    {
        int depth = 0;
        for (int i = open; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}') { depth--; if (depth == 0) return i; }
        }
        return css.Length - 1;
    }
}

// ============================================================================
// Utilidades para valores CSS
// ============================================================================
public static class CssValues
{
    /// <summary>
    /// Resolve var(--nome, fallback) de token único (legado: sem contexto de vars,
    /// usa o fallback). Mantido para parse de cores/comprimentos isolados.
    /// </summary>
    public static string? ResolveVar(string value)
    {
        var v = value.Trim();
        if (!v.StartsWith("var(", StringComparison.OrdinalIgnoreCase) || !v.EndsWith(")")) return value;
        int depth = 0, comma = -1;
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i] == '(') depth++;
            else if (v[i] == ')') depth--;
            else if (v[i] == ',' && depth == 1) { comma = i; break; }
        }
        if (comma < 0) return null;
        var fallback = v.Substring(comma + 1, v.Length - 1 - comma - 1).Trim();
        return fallback.Length > 0 ? fallback : null;
    }

    /// <summary>
    /// Resolve todas as ocorrências de var() dentro de um valor, usando lookup de
    /// custom properties (inclui herança). Retorna null se algum var() não tem
    /// valor nem fallback — a declaração deve ser ignorada.
    /// </summary>
    public static string? ResolveAllVars(string value, Func<string, string?> lookup, int depth = 0)
    {
        if (depth > 10) return null;
        if (value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) < 0) return value;
        var sb = new StringBuilder();
        int i = 0;
        while (i < value.Length)
        {
            int idx = value.IndexOf("var(", i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { sb.Append(value.Substring(i)); break; }
            sb.Append(value.Substring(i, idx - i));
            int open = value.IndexOf('(', idx);
            int close = MatchParen(value, open);
            if (close < 0) return null;
            string inner = value.Substring(open + 1, close - open - 1);
            SplitVarInner(inner, out string name, out string? fallback);
            name = name.Trim();
            string? repl = lookup(name);
            if (repl != null)
            {
                var r = ResolveAllVars(repl, lookup, depth + 1);
                if (r == null) return null;
                sb.Append(r);
            }
            else if (fallback != null)
            {
                var r = ResolveAllVars(fallback, lookup, depth + 1);
                if (r == null) return null;
                sb.Append(r);
            }
            else return null;
            i = close + 1;
        }
        string out0 = sb.ToString();
        // var() aninhado gerado pela substituição
        if (out0.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0 && depth < 10)
            return ResolveAllVars(out0, lookup, depth + 1);
        return out0;
    }

    private static int MatchParen(string s, int open)
    {
        int d = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '(') d++;
            else if (s[i] == ')') { d--; if (d == 0) return i; }
        }
        return -1;
    }

    private static void SplitVarInner(string inner, out string name, out string? fallback)
    {
        int depth = 0;
        for (int i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '(') depth++;
            else if (inner[i] == ')') depth--;
            else if (inner[i] == ',' && depth == 0)
            {
                name = inner.Substring(0, i).Trim();
                fallback = inner.Substring(i + 1).Trim();
                if (fallback.Length == 0) fallback = null;
                return;
            }
        }
        name = inner.Trim();
        fallback = null;
    }

    /// <summary>Divide um valor em tokens de nível superior (não quebra dentro de parênteses).</summary>
    public static List<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var cur = new System.Text.StringBuilder();
        int depth = 0;
        foreach (char c in value)
        {
            if (c == '(') depth++;
            if (c == ')') depth = Math.Max(0, depth - 1);
            if (depth == 0 && char.IsWhiteSpace(c))
            {
                if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); }
            }
            else cur.Append(c);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts;
    }

    public static bool LooksLikeColor(string token)
    {
        if (token.Length == 0) return false;
        if (token[0] == '#' || token.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return true;
        if (char.IsLetter(token[0]))
        {
            var colors = CssColors.Named;
            if (colors.ContainsKey(token)) return true;
            return token is "transparent" or "currentcolor" or "inherit";
        }
        return false;
    }

    public static bool LooksLikeLength(string token) => CssLength.Parse(token, 16f) != null;

    public static float? TryNumber(string s)
    {
        if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
        return null;
    }
}
