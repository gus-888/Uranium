namespace native_browser;

// ============================================================================
// Animações CSS: interpola @keyframes (opacity, color, background) com clock.
// Propriedades de layout (width, transform...) não animam — aplicam no fim.
// ============================================================================
public class ActiveAnim
{
    public DomNode Node = null!;
    public CssKeyframes Kf = null!;
    public CssAnimation Anim = null!;
    public DateTime Start;
}

public class Animator
{
    private readonly List<ActiveAnim> items = new();
    private readonly Dictionary<DomNode, ComputedStyle> originals = new();

    public bool HasItems => items.Count > 0;

    /// <summary>Coleta elementos com animation-name válido após o load.</summary>
    public void Collect(StyleResolver resolver, DomNode root)
    {
        items.Clear();
        originals.Clear();
        var now = DateTime.UtcNow;
        void Walk(DomNode n)
        {
            if (n.Type == NodeType.Element && resolver.TryGet(n, out var cs) && cs?.Animation != null)
            {
                var a = cs.Animation;
                if (a.Name.Length > 0 && a.Duration > 0 &&
                    CssParser.KeyframesMap.TryGetValue(a.Name, out var kf) && kf.Stops.Count > 0)
                {
                    items.Add(new ActiveAnim { Node = n, Kf = kf, Anim = a, Start = now });
                    originals[n] = cs;
                }
            }
            foreach (var c in n.Children) Walk(c);
        }
        Walk(root);
    }

    /// <summary>Avança o clock; retorna true se algo mudou (precisa repintar).</summary>
    public bool Tick(DateTime now, StyleResolver resolver)
    {
        if (items.Count == 0) return false;
        bool changed = false;
        var done = new List<ActiveAnim>();
        foreach (var it in items)
        {
            float elapsed = (float)(now - it.Start).TotalSeconds - it.Anim.Delay;
            if (elapsed < 0)
            {
                // durante o delay: fill backwards/both mostra o 1º quadro
                if (it.Anim.FillMode is "backwards" or "both") { ApplyFrame(resolver, it, 0); changed = true; }
                continue;
            }
            float dur = Math.Max(0.001f, it.Anim.Duration);
            int iter = (int)(elapsed / dur);
            bool infinite = it.Anim.IterationCount <= 0;
            if (!infinite && iter >= it.Anim.IterationCount)
            {
                // fim: fill forwards/both segura o último quadro, senão restaura
                if (it.Anim.FillMode is "forwards" or "both") { ApplyFrame(resolver, it, 100); }
                else if (originals.TryGetValue(it.Node, out var orig)) resolver.SetOverride(it.Node, orig);
                done.Add(it);
                changed = true;
                continue;
            }
            float frac = (elapsed % dur) / dur;
            string dir = it.Anim.Direction;
            bool rev = dir == "reverse" || (dir == "alternate" && iter % 2 == 1) || (dir == "alternate-reverse" && iter % 2 == 0);
            if (rev) frac = 1 - frac;
            ApplyFrame(resolver, it, Eased(frac, it.Anim.TimingFunction) * 100f);
            changed = true;
        }
        foreach (var d in done) { items.Remove(d); originals.Remove(d.Node); }
        return changed;
    }

    private static float Eased(float t, string fn) => fn switch
    {
        "linear" => t,
        "ease-in" => t * t,
        "ease-out" => 1 - (1 - t) * (1 - t),
        "ease-in-out" => t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2,
        _ => t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2, // ease
    };

    private static void ApplyFrame(StyleResolver resolver, ActiveAnim it, float pct)
    {
        if (!resolver.TryGet(it.Node, out var cur) || cur == null) return;
        var stops = it.Kf.Stops.OrderBy(s => s.Percent).ToList();
        var lo = stops[0];
        var hi = stops[^1];
        foreach (var s in stops)
        {
            if (s.Percent <= pct) lo = s;
            if (s.Percent >= pct) { hi = s; break; }
        }
        var patched = cur.Clone();
        float span = Math.Max(0.0001f, hi.Percent - lo.Percent);
        float f = Math.Clamp((pct - lo.Percent) / span, 0, 1);
        if (LerpOpacity(lo.Declarations, hi.Declarations, f) is float op) patched.Opacity = op;
        if (LerpColor(lo.Declarations, hi.Declarations, f, "color") is Color c) patched.Color = c;
        string? bgKey = lo.Declarations.ContainsKey("background-color") || hi.Declarations.ContainsKey("background-color") ? "background-color"
            : lo.Declarations.ContainsKey("background") || hi.Declarations.ContainsKey("background") ? "background" : null;
        if (bgKey != null && LerpColor(lo.Declarations, hi.Declarations, f, bgKey, true) is Color bg) patched.Background = bg;
        resolver.SetOverride(it.Node, patched);
    }

    private static float? LerpOpacity(Dictionary<string, string> a, Dictionary<string, string> b, float f)
    {
        a.TryGetValue("opacity", out string? sa);
        b.TryGetValue("opacity", out string? sb);
        sa ??= sb; sb ??= sa;
        if (sa == null || sb == null) return null;
        if (CssValues.TryNumber(sa) is not float va || CssValues.TryNumber(sb) is not float vb) return null;
        return Math.Clamp(va + (vb - va) * f, 0, 1);
    }

    private static Color? LerpColor(Dictionary<string, string> a, Dictionary<string, string> b, float f, string key, bool bg = false)
    {
        a.TryGetValue(key, out string? sa);
        b.TryGetValue(key, out string? sb);
        sa ??= sb; sb ??= sa;
        if (sa == null || sb == null) return null;
        if (bg && (sa.Contains("url(") || sb.Contains("url("))) return null;
        var ca = CssColors.Parse(sa);
        var cb = CssColors.Parse(sb);
        if (ca == null || cb == null) return null;
        return Color.FromArgb(
            (int)Math.Round(ca.Value.A + (cb.Value.A - ca.Value.A) * f),
            (int)Math.Round(ca.Value.R + (cb.Value.R - ca.Value.R) * f),
            (int)Math.Round(ca.Value.G + (cb.Value.G - ca.Value.G) * f),
            (int)Math.Round(ca.Value.B + (cb.Value.B - ca.Value.B) * f));
    }
}
