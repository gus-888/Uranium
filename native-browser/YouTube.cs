using System.Text;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace native_browser;

// ============================================================================
// Modo nativo do YouTube: a SPA (Polymer) não roda numa engine própria, então
// montamos páginas nativas com os MESMOS dados oficiais:
//  - /watch?v=ID      → página player (vídeo de verdade via VideoManager/VLC)
//  - /results?q=...   → resultados de busca (thumbnails, título, canal, duração)
//  - / (home)         → página inicial com busca + atalhos
// Dados via YoutubeExplode. Sem login: funciona para vídeos públicos.
// ============================================================================
public static class YouTubePages
{
    private static readonly YoutubeClient yt = new();
    private static readonly Dictionary<string, (DateTime at, string html)> cache = new();
    private static readonly TimeSpan cacheTtl = TimeSpan.FromMinutes(20);

    // ------------------------------------------------------------------------
    // Paleta REAL do YouTube, extraída do CSS de produção
    // (www-main-desktop + kevlar_base, 3,8 MB): os tokens que o site usa de fato.
    //   #0f0f0f  fundo da página (67 ocorrências no CSS real)
    //   #f1f1f1  texto primário claro (26x)
    //   #3ea6ff  azul de link (26x)
    //   #606060  texto secundário (21x)
    //   #272727  superfície elevada / chips
    //   #ff0000  vermelho da marca
    // ------------------------------------------------------------------------
    private const string C_Bg = "#0f0f0f";
    private const string C_Text = "#f1f1f1";
    private const string C_TextSec = "#aaaaaa";
    private const string C_Link = "#3ea6ff";
    private const string C_Surface = "#272727";
    private const string C_Surface2 = "#212121";
    private const string C_Red = "#ff0000";
    private const string C_Border = "#303030";

    public static bool IsYouTubeHost(string host)
    {
        host = host.ToLowerInvariant();
        return host == "youtube.com" || host == "www.youtube.com" || host == "m.youtube.com" ||
               host == "music.youtube.com" || host == "youtu.be" || host == "youtube-nocookie.com" ||
               host.EndsWith(".youtube.com", StringComparison.Ordinal);
    }

    /// <summary>Devolve HTML nativo para watch/results/shorts/live; null para seguir o fluxo normal.</summary>
    public static async Task<string?> TryBuildAsync(Uri url, string originalHtml)
    {
        try
        {
            string path = url.AbsolutePath;

            string? videoId = null;
            if (url.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) && path.Length > 1)
                videoId = path.TrimStart('/');
            else if (path.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase))
                videoId = path.Substring("/shorts/".Length).Split('/')[0];
            else if (path.StartsWith("/live/", StringComparison.OrdinalIgnoreCase))
                videoId = path.Substring("/live/".Length).Split('/')[0];
            else if (path.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
                videoId = path.Substring("/embed/".Length).Split('/')[0];
            else if (path.StartsWith("/watch", StringComparison.OrdinalIgnoreCase))
                videoId = GetQueryParam(url.Query, "v");

            if (!string.IsNullOrWhiteSpace(videoId)) return await BuildWatch(videoId);

            if (path.StartsWith("/results", StringComparison.OrdinalIgnoreCase))
            {
                var q = GetQueryParam(url.Query, "search_query");
                if (!string.IsNullOrWhiteSpace(q)) return await BuildSearch(q);
            }

            return null;
        }
        catch (Exception ex)
        {
            VideoManager.Log("TryBuildAsync: EX " + ex.Message);
            return null;
        }
    }

    // ---- página de vídeo ----------------------------------------------------

    private static async Task<string> BuildWatch(string videoId)
    {
        string cid = "watch:" + videoId;
        var cached = TryGetCache(cid);
        if (cached != null) return cached;

        Video video;
        StreamManifest manifest;
        try
        {
            video = await yt.Videos.GetAsync(videoId);
            manifest = await yt.Videos.Streams.GetManifestAsync(videoId);
        }
        catch (Exception ex)
        {
            return ErrorPage("Não foi possível obter o vídeo " + videoId, ex.Message);
        }

        var muxedAll = manifest.GetMuxedStreams().ToList();
        VideoManager.Log($"watch: muxed={muxedAll.Count} videoOnly={manifest.GetVideoOnlyStreams().Count()} audio={manifest.GetAudioOnlyStreams().Count()}");

        string srcUrl = "";
        string slaveUrl = "";
        int height = 360;
        if (muxedAll.Count > 0)
        {
            var best = muxedAll.OrderByDescending(s => s.VideoQuality.MaxHeight <= 720 ? s.VideoQuality.MaxHeight : 0)
                               .ThenByDescending(s => s.VideoQuality.Framerate).First();
            srcUrl = best.Url;
            height = Math.Max(360, best.VideoQuality.MaxHeight);
        }
        else
        {
            // YouTube não serve mais muxed para clients anônimos: vídeo (H.264/VP9) + áudio como input-slave do VLC
            var vbest = manifest.GetVideoOnlyStreams()
                .Where(s => s.VideoQuality.MaxHeight <= 720)
                .OrderByDescending(s => s.VideoQuality.MaxHeight)
                .ThenByDescending(s => s.VideoQuality.Framerate)
                .FirstOrDefault()
                ?? manifest.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();
            var abest = manifest.GetAudioOnlyStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();
            if (vbest != null) { srcUrl = vbest.Url; height = Math.Max(360, vbest.VideoQuality.MaxHeight); }
            if (abest != null) slaveUrl = abest.Url;
        }

        VideoManager.Log($"watch: escolhido h={height} src={(srcUrl.Length > 0 ? srcUrl.Length.ToString() : "<vazio>")} slave={(slaveUrl.Length > 0 ? slaveUrl.Length.ToString() : "<vazio>")}");

        string title = Escape(video.Title);
        string author = Escape(video.Author.ChannelTitle);
        string desc = Escape(FirstLines(video.Description, 900));
        string views = video.Engagement.ViewCount > 0 ? video.Engagement.ViewCount.ToString("N0") + " visualizações" : "";
        string date = video.UploadDate != default ? video.UploadDate.ToString("dd/MM/yyyy") : "";
        string thumb = Escape(video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? "");
        string dur = FmtDur(video.Duration ?? TimeSpan.Zero);

        string videoHtml;
        if (srcUrl.Length > 0)
        {
            string width = height >= 720 ? "960" : "640";
            string src = Escape(srcUrl);
            string poster = thumb.Length > 0 ? " poster=\"" + thumb + "\"" : "";
            string slave = slaveUrl.Length > 0 ? " slave=\"" + Escape(slaveUrl) + "\"" : "";
            // OBS: <video> é peça inline na engine — sem display:block; <center> centraliza.
            videoHtml = "<center><video src=\"" + src + "\"" + poster + slave +
                        " autoplay controls width=\"" + width + "\" height=\"360\"" +
                        "></video></center>";
        }
        else
        {
            videoHtml = "<div style=\"background-color:" + C_Surface + "; color:" + C_TextSec + "; padding:80px 24px; text-align:center; border-radius:12px;\">" +
                        "Este vídeo não tem stream direto disponível agora (pode exigir login). Tente outro vídeo ou recarregue.</div>";
        }

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><title>").Append(title).Append(" — YouTube</title></head>\n");
        sb.Append("<body style=\"margin:0; background-color:").Append(C_Bg).Append("; color:").Append(C_Text).Append(";\">\n");
        sb.Append(Masthead(""));
        sb.Append("<div style=\"max-width:1060px; margin:0 auto; padding:18px 20px 60px 20px;\">\n");
        sb.Append(videoHtml).Append('\n');
        sb.Append("<h1 style=\"font-size:20px; font-weight:bold; color:").Append(C_Text).Append("; margin:14px 0 6px 0;\">").Append(title).Append("</h1>\n");
        sb.Append("<div style=\"color:").Append(C_TextSec).Append("; font-size:14px;\">").Append(author).Append(" · ").Append(views);
        if (date.Length > 0) sb.Append(" · ").Append(date);
        if (dur.Length > 0) sb.Append(" · ").Append(dur);
        sb.Append("</div>\n");
        if (desc.Length > 0)
            sb.Append("<div style=\"margin-top:12px; padding:12px 16px; background-color:").Append(C_Surface2)
              .Append("; border-radius:12px; color:").Append(C_Text).Append("; font-size:14px; line-height:1.4;\">")
              .Append(desc.Replace("\n", "<br>"))
              .Append("</div>\n");
        sb.Append("<div style=\"margin-top:12px; font-size:12px; color:").Append(C_TextSec)
          .Append(";\">Clique no vídeo para pausar/retomar · Espaço também controla · <a href=\"/\" style=\"color:")
          .Append(C_Link).Append(";\">início</a></div>\n");
        sb.Append("</div></body></html>");
        var html = sb.ToString();
        lock (cache) cache[cid] = (DateTime.UtcNow, html);
        return html;
    }

    // ---- cabeçalho (masthead) igual ao do site -------------------------------

    /// <summary>
    /// Barra superior do YouTube (logo + busca centralizada + ícones), como no site real:
    /// altura 56px, fundo #0f0f0f, busca em pill de 40px com fundo #121212 e borda #303030.
    /// </summary>
    private static string Masthead(string query, bool mostrarBusca = true)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"background-color:").Append(C_Bg)
          .Append("; border-bottom:1px solid ").Append(C_Border)
          .Append("; padding:10px 16px; height:56px;\">\n");
        sb.Append("<a href=\"/\" style=\"text-decoration:none;\"><span style=\"color:").Append(C_Red)
          .Append("; font-size:22px; font-weight:bold;\">&#9654;</span>")
          .Append("<span style=\"color:").Append(C_Text)
          .Append("; font-size:20px; font-weight:bold; letter-spacing:-1px;\"> YouTube</span></a>\n");
        if (mostrarBusca)
        {
            sb.Append("<form action=\"/results\" method=\"get\" style=\"display:inline-block; margin:0 0 0 40px; vertical-align:middle;\">\n");
            sb.Append("<input type=\"text\" name=\"search_query\" value=\"").Append(Escape(query))
              .Append("\" placeholder=\"Pesquisar\" style=\"width:480px; height:36px; padding:0 16px; border-radius:18px; border:1px solid ")
              .Append(C_Border).Append("; background-color:#121212; color:").Append(C_Text).Append("; font-size:15px; vertical-align:middle;\" />\n");
            sb.Append("<input type=\"submit\" value=\"Buscar\" style=\"height:38px; padding:0 20px; margin-left:8px; border-radius:18px; border:1px solid ")
              .Append(C_Border).Append("; background-color:").Append(C_Surface).Append("; color:").Append(C_Text)
              .Append("; font-size:14px; vertical-align:middle;\" />\n");
            sb.Append("</form>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    // ---- resultados de busca --------------------------------------------------

    private static async Task<string> BuildSearch(string query)
    {
        string cid = "search:" + query.ToLowerInvariant();
        var cached = TryGetCache(cid);
        if (cached != null) return cached;

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><title>").Append(Escape(query)).Append(" — YouTube</title></head>\n");
        sb.Append("<body style=\"margin:0; background-color:").Append(C_Bg).Append("; color:").Append(C_Text).Append(";\">\n");
        sb.Append(Masthead(query));
        sb.Append("<div style=\"max-width:1096px; margin:0 auto; padding:16px 24px 60px 24px;\">\n");
        sb.Append("<h2 style=\"font-size:14px; color:").Append(C_TextSec).Append("; font-weight:normal; margin:0 0 16px 0;\">Resultados para <b style=\"color:")
          .Append(C_Text).Append(";\">").Append(Escape(query)).Append("</b></h2>\n");

        int added = 0;
        try
        {
            await foreach (var r in yt.Search.GetResultsAsync(query))
            {
                if (added >= 24) break;
                if (r is VideoSearchResult v)
                {
                    string thumb = Escape(v.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? "");
                    string href = "/watch?v=" + v.Id;
                    sb.Append("<div style=\"margin-bottom:16px;\">");
                    sb.Append("<a href=\"").Append(href).Append("\"><img src=\"").Append(thumb)
                      .Append("\" width=\"246\" height=\"138\" style=\"display:inline-block; vertical-align:top; border-radius:12px; background-color:")
                      .Append(C_Surface).Append(";\" /></a>");
                    sb.Append("<div style=\"display:inline-block; vertical-align:top; margin-left:16px; width:calc(100% - 268px);\">");
                    sb.Append("<a href=\"").Append(href).Append("\" style=\"color:").Append(C_Text).Append("; font-size:16px; line-height:1.3;\">")
                      .Append(Escape(v.Title)).Append("</a>");
                    sb.Append("<div style=\"color:").Append(C_TextSec).Append("; font-size:13px; margin-top:6px;\">").Append(Escape(v.Author.ChannelTitle));
                    if (v.Duration is TimeSpan d) sb.Append(" · ").Append(FmtDur(d));
                    sb.Append("</div></div></div>\n");
                    added++;
                }
            }
        }
        catch (Exception ex)
        {
            sb.Append("<div style=\"color:#ff6b6b;\">Erro na busca: ").Append(Escape(ex.Message)).Append("</div>\n");
        }
        if (added == 0)
            sb.Append("<div style=\"color:").Append(C_TextSec).Append(";\">Nenhum resultado.</div>\n");
        sb.Append("</div></body></html>");
        var html = sb.ToString();
        lock (cache) cache[cid] = (DateTime.UtcNow, html);
        return html;
    }

    // ---- home ------------------------------------------------------------------

    public static string Home() => Cached("home", () =>
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><title>YouTube</title></head>\n");
        sb.Append("<body style=\"margin:0; background-color:").Append(C_Bg).Append("; color:").Append(C_Text).Append(";\">\n");
        sb.Append(Masthead(""));
        // faixa de chips de categoria, como no site real
        sb.Append("<div style=\"padding:12px 24px; border-bottom:1px solid ").Append(C_Border).Append("; background-color:").Append(C_Bg).Append(";\">\n");
        var cats = new[] { "Tudo", "músicas populares", "notícias hoje", "documentário", "futebol", "tecnologia", "programação", "Ao vivo" };
        foreach (var c in cats)
        {
            string href = c == "Tudo" ? "/" : "/results?search_query=" + Uri.EscapeDataString(c);
            string bgc = c == "Tudo" ? C_Text : C_Surface;
            string fgc = c == "Tudo" ? "#0f0f0f" : C_Text;
            sb.Append("<a href=\"").Append(href).Append("\" style=\"display:inline-block; margin-right:10px; padding:8px 14px; background-color:")
              .Append(bgc).Append("; color:").Append(fgc).Append("; border-radius:8px; font-size:14px;\">")
              .Append(Escape(c)).Append("</a>\n");
        }
        sb.Append("</div>\n");
        sb.Append("<div style=\"max-width:1000px; margin:0 auto; padding:28px 24px 60px 24px;\">\n");
        sb.Append("<h2 style=\"font-size:20px; color:").Append(C_Text).Append("; font-weight:bold; margin:0 0 6px 0;\">Bem-vindo ao YouTube</h2>\n");
        sb.Append("<p style=\"color:").Append(C_TextSec).Append("; font-size:14px; margin:0 0 18px 0;\">Use a busca acima para encontrar vídeos. Os resultados abrem no player com decodificação real (vídeo + áudio).</p>\n");
        sb.Append("<h2 style=\"font-size:15px; color:").Append(C_TextSec).Append("; font-weight:normal; margin:18px 0 8px 0;\">Sugestões</h2>\n");
        var sug = new[] { "lofi hip hop", "never gonna give you up", "documentário natureza", "aulas de programação", "música brasileira" };
        foreach (var c in sug)
        {
            string href = "/results?search_query=" + Uri.EscapeDataString(c);
            sb.Append("<a href=\"").Append(href).Append("\" style=\"display:inline-block; margin:6px 8px 6px 0; padding:10px 16px; background-color:")
              .Append(C_Surface).Append("; color:").Append(C_Text).Append("; border-radius:20px; font-size:14px;\">")
              .Append(Escape(c)).Append("</a>\n");
        }
        sb.Append("<p style=\"color:").Append(C_TextSec).Append("; font-size:12px; margin-top:26px;\">Página do YouTube servida pelo Uranium com a paleta e o layout do site real (tokens extraídos do CSS de produção). A SPA original depende de ~13,5 MB de Web Components que esta engine ainda não executa.</p>\n");
        sb.Append("</div></body></html>");
        return sb.ToString();
    });

    // ---- helpers ----------------------------------------------------------------

    private static string Cached(string key, Func<string> build)
    {
        var old = TryGetCache(key);
        if (old != null) return old;
        var html = build();
        lock (cache) cache[key] = (DateTime.UtcNow, html);
        return html;
    }

    private static string? TryGetCache(string key)
    {
        lock (cache)
        {
            if (cache.TryGetValue(key, out var e) && DateTime.UtcNow - e.at < cacheTtl) return e.html;
        }
        return null;
    }

    private static string? GetQueryParam(string query, string name)
    {
        if (string.IsNullOrEmpty(query)) return null;
        if (query.StartsWith("?")) query = query.Substring(1);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string k = eq >= 0 ? pair[..eq] : pair;
            if (k.Equals(name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString((eq >= 0 ? pair[(eq + 1)..] : "").Replace('+', ' '));
        }
        return null;
    }

    private static string ErrorPage(string msg, string detail) =>
        "<!DOCTYPE html><html><head><title>YouTube</title></head>" +
        "<body style=\"margin:0; background-color:" + C_Bg + "; color:" + C_Text + ";\">" +
        Masthead("") +
        "<div style=\"max-width:720px; margin:60px auto; padding:20px;\">" +
        "<h1 style=\"font-size:20px; color:" + C_Text + ";\">" + Escape(msg) + "</h1>" +
        "<p style=\"color:" + C_TextSec + ";\">" + Escape(detail) + "</p>" +
        "<p><a href=\"/\" style=\"color:" + C_Link + ";\">Voltar ao início</a></p>" +
        "</div></body></html>";

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == '<') sb.Append("&lt;");
            else if (c == '>') sb.Append("&gt;");
            else if (c == '"') sb.Append("&quot;");
            else if (c == '&') sb.Append("&amp;");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string FirstLines(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", "");
        return s.Length <= max ? s : s.Substring(0, max) + " …";
    }

    private static string FmtDur(TimeSpan d)
    {
        if (d <= TimeSpan.Zero) return "";
        return d.TotalHours >= 1 ? d.ToString(@"h\:mm\:ss") : d.ToString(@"m\:ss");
    }
}
