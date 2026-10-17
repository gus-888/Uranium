using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace native_browser;

/// <summary>
/// Fontes: resolve famílias CSS para fontes reais — primeiro as carregadas via
/// @font-face (TTF/OTF), depois as instaladas no Windows, e por fim os
/// mapeamentos genéricos (serif/sans-serif/monospace).
/// </summary>
public static class FontLoader
{
    private static readonly PrivateFontCollection Private = new();
    private static readonly Dictionary<string, string> CustomFamilies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Installed = new(StringComparer.OrdinalIgnoreCase);
    private static bool installedScanned;

    private static void ScanInstalled()
    {
        if (installedScanned) return;
        installedScanned = true;
        try
        {
            foreach (var f in FontFamily.Families) Installed.Add(f.Name);
        }
        catch { }
    }

    /// <summary>Registra uma fonte a partir dos bytes de um TTF/OTF; mapeia o
    /// apelido CSS para o nome real da família dentro do arquivo.</summary>
    public static void Add(string cssAlias, byte[] fontData)
    {
        try
        {
            var gch = GCHandle.Alloc(fontData, GCHandleType.Pinned);
            Private.AddMemoryFont(gch.AddrOfPinnedObject(), fontData.Length);
            if (Private.Families.Length > 0)
            {
                var real = Private.Families[^1].Name;
                CustomFamilies[cssAlias] = real;
                CustomFamilies[real] = real;
            }
        }
        catch { }
    }

    /// <summary>Nome real da família a usar no Font, ou null se não conhecer.</summary>
    public static string? Resolve(string cssFamily)
    {
        ScanInstalled();
        var first = cssFamily.Split(',')[0].Trim().Trim('"', '\'');
        if (first.Length == 0) return null;

        if (CustomFamilies.TryGetValue(first, out var custom)) return custom;
        if (Installed.Contains(first)) return first;

        // apelidos comuns
        if (first.Contains("mono") || first.Contains("consol") || first.Contains("courier")) return "Consolas";
        if (first.Contains("serif") && !first.Contains("sans")) return Installed.Contains("Georgia") ? "Georgia" : "Times New Roman";
        return null; // deixa o chamador decidir o fallback sans
    }
}
