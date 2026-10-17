using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;

namespace native_browser;

// ============================================================================
// Carregador assíncrono de imagens com cache
// ============================================================================
public class ImageLoader
{
    private readonly HttpClient http;
    public Action? OnImageReady;
    public Action? OnFrameReady; // GIF animado: novo quadro disponível
    private readonly Dictionary<Uri, Image?> cache = new();
    private readonly HashSet<Uri> queued = new();
    private readonly List<MemoryStream> liveStreams = new(); // Image.FromStream precisa do stream vivo
    public Action<Uri>? OnImageFailed;
    private int _falhasLogadas;

    public ImageLoader(HttpClient client) { http = client; }

    public Image? GetLoaded(Uri? u)
    {
        if (u == null) return null;
        if (cache.TryGetValue(u, out var img)) return img;
        // o layout pode pedir com a URL original (sqp/webp); o cache guarda a
        // normalizada — tenta também a forma ajustada
        var a = AjustaYtimg(u);
        if (a != null && a != u && cache.TryGetValue(a, out var img2)) return img2;
        return null;
    }

    public bool IsFailed(Uri? u) => u != null && cache.TryGetValue(u, out var img) && img == null;

    /// <summary>Normaliza URLs de imagem do ytimg para o formato que o GDI+ decodifica.</summary>
    public static Uri? AjustaYtimg(Uri? u)
    {
        if (u == null || !u.Host.Contains("ytimg")) return u;
        var s = u.ToString();
        if (u.AbsolutePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            s = s.Replace(".webp", ".jpg", StringComparison.OrdinalIgnoreCase);
        // a assinatura sqp= faz o ytimg devolver RIFF/WEBP mesmo em .jpg —
        // sem a query vem JPEG puro (a assinatura só ajusta recorte)
        if (s.Contains("?") && s.Contains("sqp="))
            s = s.Substring(0, s.IndexOf('?'));
        return s != u.ToString() && Uri.TryCreate(s, UriKind.Absolute, out var ju) ? ju : u;
    }

    public void Request(Uri? u)
    {
        u = AjustaYtimg(u);
        if (u == null || cache.ContainsKey(u) || !queued.Add(u)) return;
        if (u.Scheme == "data") { _ = LoadData(u); return; }
        if (!u.IsFile && u.Scheme is not ("http" or "https")) { cache[u] = null; queued.Remove(u); return; }
        _ = Load(u);
    }

    /// <summary>Decodifica data:[mime][;base64],payload inline (ícones pequenos).</summary>
    private async Task LoadData(Uri u)
    {
        try
        {
            await Task.Yield();
            string s = u.OriginalString;
            int comma = s.IndexOf(',');
            if (comma < 0) throw new Exception("data sem vírgula");
            string head = s.Substring(0, comma);
            string data = s.Substring(comma + 1);
            byte[] bytes = head.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(data)
                : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
            if (bytes.Length == 0 || bytes.Length > 2_000_000) throw new Exception("data inválido");
            Image img;
            if (SvgRaster.LooksLikeSvg("", bytes))
                img = SvgRaster.FromBytes(bytes) ?? throw new Exception("svg inválido");
            else
            {
                var ms = new MemoryStream(bytes);
                img = Image.FromStream(ms);
                lock (liveStreams) liveStreams.Add(ms);
            }
            cache[u] = img;
            TryAnimate(img);
        }
        catch
        {
            cache[u] = null;
        }
        finally
        {
            queued.Remove(u);
            OnImageReady?.Invoke();
        }
    }

    private async Task Load(Uri u)
    {
        try
        {
            Image img;
            if (u.IsFile)
            {
                var fbytes = await File.ReadAllBytesAsync(u.LocalPath);
                img = SvgRaster.LooksLikeSvg(u.LocalPath, fbytes)
                    ? SvgRaster.FromBytes(fbytes) ?? throw new Exception("svg inválido")
                    : Image.FromFile(u.LocalPath);
            }
            else
            {
                var bytes = await http.GetByteArrayAsync(u);
                if (SvgRaster.LooksLikeSvg(u.AbsolutePath, bytes))
                {
                    img = SvgRaster.FromBytes(bytes) ?? throw new Exception("svg inválido");
                }
                else
                {
                    var ms = new MemoryStream(bytes); // precisa ficar vivo enquanto a imagem viver
                    img = Image.FromStream(ms);
                    lock (liveStreams) liveStreams.Add(ms);
                }
            }
            cache[u] = img;
            TryAnimate(img);
            // diagnóstico watch: o ytimg carregou e chega ao cache?
            if (u.Host.Contains("ytimg") && _falhasLogadas < 14)
            {
                _falhasLogadas++;
                VideoManager.Log("img-ok: " + u.ToString().Substring(0, Math.Min(90, u.ToString().Length)));
            }
        }
        catch (Exception ex)
        {
            cache[u] = null;
            // diagnose: por que as thumbnails do watch ficam pretas
            if (_falhasLogadas < 8)
            {
                _falhasLogadas++;
                VideoManager.Log("img-falhou: " + ex.Message + " | " + u);
            }
        }
        finally
        {
            queued.Remove(u);
            OnImageReady?.Invoke();
        }
    }

    private void TryAnimate(Image img)
    {
        try
        {
            if (img.RawFormat.Equals(ImageFormat.Gif) && img.GetFrameCount(FrameDimension.Time) > 1)
                ImageAnimator.Animate(img, (s, e) => OnFrameReady?.Invoke());
        }
        catch { }
    }
}

// ============================================================================
// Estado de uma aba (histórico + documento + layout)
// ============================================================================
public class BrowserTab
{
    public List<string> History = new();
    public int HistoryIndex = -1;
    public string Url = "";
    public string Title = "Nova aba";
    public LayoutResult? Page;
    public DomNode? Document;
    public StyleResolver? Styles;
    public Uri? BaseUri;
    public float ScrollY;
    public int LoadSeq;
    public bool Loading;
    public string? PendingPostBody;      // submissão POST aguardando o LoadAsync da vez
    public int NoscriptHops;             // auto-redirect <noscript><meta refresh> já seguidos (teto 3)

    // stream real da watch page via Innertube ANDROID_VR (URL já assinada pelo servidor)
    public string? VrStreamUrl;          // vídeo muxed (itag 18) ou video-only master
    public string? VrAudioSlaveUrl;      // áudio para sincronia como slave do VLC
    public bool VrStreamRequested;

    // interação
    public Dictionary<DomNode, ControlState> Inputs = new();
    public DomNode? FocusedInput;
    public Animator Anims = new();       // animações CSS da aba
    public JsEngine? Js;                 // motor JS da aba (Fase F)
    public int DynScripts;               // scripts dinâmicos executados (teto anti-loop)
    public DomNode? OpenSelect;          // select com dropdown aberto
    public List<string> SelectOptions = new();
    public RectangleF SelectPopupRect;   // em coordenadas de tela (canvas)
    public int SelectHover = -1;

    public bool CanGoBack => HistoryIndex > 0;
    public bool CanGoForward => HistoryIndex < History.Count - 1;
}

// ============================================================================
// Páginas internas (about:home, erro)
// ============================================================================
public static class Pages
{
    public const string Home = @"<!DOCTYPE html>
<html>
<head><title>Início — Uranium</title></head>
<body style=""margin:0; background:#f1f5f9;"">
  <div style=""background:linear-gradient(#0f172a); background-color:#0f172a; padding:48px 40px; text-align:center;"">
    <h1 style=""color:#ffffff; margin:0; font-size:42px;"">⚛ Uranium Browser</h1>
    <p style=""color:#94a3b8; margin:8px 0 0 0; font-size:16px;"">
      Motor de renderização próprio — HTML, CSS e layout escritos do zero em C#
    </p>
  </div>
  <div style=""max-width:680px; margin:32px auto; padding:0 16px;"">
    <h2 style=""color:#0f172a; border-bottom:2px solid #3b82f6;"">Experimente</h2>
    <ul>
      <li><a href=""https://example.com"">example.com</a> — página simples e limpa</li>
      <li><a href=""https://pt.wikipedia.org/wiki/Navegador_web"">Wikipédia: Navegador web</a> — página grande, texto e links</li>
      <li><a href=""https://news.ycombinator.com"">Hacker News</a> — lista de links (layout clássico)</li>
    </ul>
    <h2 style=""color:#0f172a; border-bottom:2px solid #3b82f6;"">O que este motor já suporta</h2>
    <table>
      <tr><td style=""padding:6px 10px; background:#e2e8f0;""><b>HTML</b></td><td style=""padding:6px 10px;"">parser tolerante, entidades (&amp;ccedil; &amp;euro; &#9824;), listas, tabelas simples, formul&aacute;rios visuais</td></tr>
      <tr><td style=""padding:6px 10px; background:#e2e8f0;""><b>CSS</b></td><td style=""padding:6px 10px;"">seletores de tag/classe/id, &lt;style&gt; e folhas externas, cores hex/rgb/nomeadas, box model, em/%</td></tr>
      <tr><td style=""padding:6px 10px; background:#e2e8f0;""><b>Layout</b></td><td style=""padding:6px 10px;"">fluxo inline com quebra de linha, inline-block, alinhamento, imagens, rolagem</td></tr>
      <tr><td style=""padding:6px 10px; background:#e2e8f0;""><b>Navegação</b></td><td style=""padding:6px 10px;"">abas, hist&oacute;rico (voltar/avan&ccedil;ar), links clic&aacute;veis, HTTPS, atalhos de teclado</td></tr>
    </table>
    <blockquote style=""margin:24px 40px; color:#475569; font-style:italic;"">
      Nota: JavaScript n&atilde;o &eacute; executado — sites muito dependentes de scripts
      renderizam apenas o conte&uacute;do est&aacute;tico.
    </blockquote>
    <p style=""color:#64748b; font-size:13px;"">
      Atalhos: <b>Ctrl+L</b> barra de endere&ccedil;os &middot; <b>F5</b> recarregar &middot;
      <b>Alt+&larr;/&rarr;</b> voltar/avan&ccedil;ar &middot; <b>Ctrl+T</b> nova aba &middot; <b>Ctrl+W</b> fechar aba
    </p>
  </div>
</body>
</html>";

    public static string Error(string url, string message) => $@"<!DOCTYPE html>
<html>
<head><title>Erro — Uranium</title></head>
<body style=""margin:0; background:#fef2f2;"">
  <div style=""background-color:#dc2626; padding:24px 32px;"">
    <h1 style=""color:#ffffff; margin:0; font-size:24px;"">Não foi possível carregar a página</h1>
  </div>
  <div style=""padding:24px 32px; max-width:640px;"">
    <p style=""font-size:15px;"">O Uranium não conseguiu acessar:</p>
    <p style=""font-family:monospace; background-color:#fee2e2; padding:8px 12px; border-left:4px solid #dc2626;"">{EscapeHtml(url)}</p>
    <p style=""color:#7f1d1d;"">Detalhe: <i>{EscapeHtml(message)}</i></p>
    <ul>
      <li>Verifique o endereço e a conexão com a internet.</li>
      <li>Alguns sites bloqueiam navegadores não identificados.</li>
      <li>Lembre que este motor não executa JavaScript.</li>
    </ul>
  </div>
</body>
</html>";

    private static string EscapeHtml(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

// ============================================================================
// Faixa de abas customizada (desenhada à mão, tema escuro)
// ============================================================================
public class TabStrip : Panel
{
    private readonly BrowserForm owner;
    private int hoverTab = -1;
    private Rectangle plusRect;

    private const int TabWidth = 190;
    private const int CloseSize = 18;

    public TabStrip(BrowserForm form)
    {
        owner = form;
        Dock = DockStyle.Top;
        Height = 34;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 23, 42);
    }

    private Rectangle TabRect(int i) => new(8 + i * TabWidth, 3, TabWidth - 4, Height - 6);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(15, 23, 42));
        var tabs = owner.Tabs;
        for (int i = 0; i < tabs.Count; i++)
        {
            var r = TabRect(i);
            bool active = i == owner.ActiveTabIndex;
            using var bg = new SolidBrush(active ? Color.FromArgb(51, 65, 85) : hoverTab == i ? Color.FromArgb(30, 41, 59) : Color.FromArgb(22, 32, 51));
            g.FillRectangle(bg, r);
            if (active)
            {
                using var accent = new SolidBrush(Color.FromArgb(59, 130, 246));
                g.FillRectangle(accent, r.X, r.Bottom - 2, r.Width, 2);
            }

            string title = tabs[i].Loading ? "Carregando…" : tabs[i].Title;
            if (string.IsNullOrEmpty(title)) title = "Nova aba";
            using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(active ? Color.White : Color.FromArgb(148, 163, 184));
            var textRect = new RectangleF(r.X + 10, r.Y, r.Width - CloseSize - 22, r.Height);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(title, font, brush, textRect, sf);

            // botão fechar
            var close = new Rectangle(r.Right - CloseSize - 6, r.Y + (r.Height - CloseSize) / 2, CloseSize, CloseSize);
            if (hoverTab == i)
            {
                using var cbg = new SolidBrush(Color.FromArgb(71, 85, 105));
                g.FillRectangle(cbg, close);
            }
            using var cf = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var cbr = new SolidBrush(Color.FromArgb(203, 213, 225));
            g.DrawString("✕", cf, cbr, close.X + 3, close.Y + 0);
        }

        // botão "+"
        plusRect = new Rectangle(12 + tabs.Count * TabWidth, 6, 22, 22);
        using var plusBg = new SolidBrush(hoverTab == -2 ? Color.FromArgb(51, 65, 85) : Color.FromArgb(30, 41, 59));
        g.FillRectangle(plusBg, plusRect);
        using var pf = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var pbr = new SolidBrush(Color.FromArgb(226, 232, 240));
        g.DrawString("+", pf, pbr, plusRect.X + 5, plusRect.Y + 2);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int newHover = HitTest(e.X, e.Y);
        if (newHover != hoverTab)
        {
            hoverTab = newHover;
            Invalidate();
        }
        Cursor = newHover >= -2 && newHover != -1 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hoverTab = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitTest(e.X, e.Y);
        if (hit == -2) { owner.NewTab(); return; }
        if (hit >= 0)
        {
            // clique no X fecha, no corpo seleciona
            var r = TabRect(hit);
            var close = new Rectangle(r.Right - CloseSize - 6, 0, CloseSize + 8, Height);
            if (close.Contains(e.X, e.Y)) { owner.CloseTab(hit); return; }
            owner.ActiveTabIndex = hit;
            Invalidate();
        }
    }

    private int HitTest(int x, int y)
    {
        if (plusRect.Contains(x, y)) return -2;
        for (int i = 0; i < owner.Tabs.Count; i++)
            if (TabRect(i).Contains(x, y)) return i;
        return -1;
    }
}

// ============================================================================
// Painel de conteúdo da página (rolagem, links, scrollbar custom)
// ============================================================================
public class CanvasPanel : Panel
{
    private readonly BrowserForm owner;
    public bool DraggingScrollbar;
    public float DragStartScroll, DragStartY;

    public CanvasPanel(BrowserForm form)
    {
        owner = form;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        BackColor = Color.White;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public const int ScrollbarWidth = 12;

    public RectangleF ScrollBarThumbRect
    {
        get
        {
            var page = owner.CurrentTab?.Page;
            if (page == null) return RectangleF.Empty;
            int viewH = ClientSize.Height;
            float docH = page.Height;
            if (docH <= viewH || viewH <= 0) return RectangleF.Empty;
            float thumbH = Math.Max(30f, viewH * viewH / docH);
            float maxScroll = docH - viewH;
            float thumbY = (owner.CurrentTab!.ScrollY / maxScroll) * (viewH - thumbH);
            return new RectangleF(Width - ScrollbarWidth, thumbY, ScrollbarWidth - 2, thumbH);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var tab = owner.CurrentTab;
        if (tab?.Page != null)
        {
            Renderer.Draw(e.Graphics, tab.Page, tab.ScrollY, Width, Height, owner.BuildControlContext());
        }
        else
        {
            e.Graphics.Clear(Color.FromArgb(248, 250, 252));
        }

        // dropdown do <select> aberto (overlay, coordenadas de tela)
        if (tab?.OpenSelect != null && tab.SelectOptions.Count > 0)
        {
            var g = e.Graphics;
            var r = tab.SelectPopupRect;
            float rowH = 24;
            var popup = new RectangleF(r.X, r.Bottom, Math.Max(r.Width, 140), rowH * Math.Min(tab.SelectOptions.Count, 12) + 2);
            g.FillRectangle(Brushes.White, popup);
            using var border = new Pen(Color.FromArgb(120, 120, 128));
            g.DrawRectangle(border, popup.X + 0.5f, popup.Y + 0.5f, popup.Width - 1, popup.Height - 1);
            using var font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
            int selIdx = tab.Inputs.TryGetValue(tab.OpenSelect, out var st) && st.SelectedIndex >= 0 ? st.SelectedIndex : 0;
            for (int i = 0; i < tab.SelectOptions.Count && i < 12; i++)
            {
                var row = new RectangleF(popup.X + 1, popup.Y + 1 + i * rowH, popup.Width - 2, rowH);
                if (i == selIdx || i == tab.SelectHover)
                    g.FillRectangle(i == selIdx ? new SolidBrush(Color.FromArgb(219, 234, 254)) : new SolidBrush(Color.FromArgb(241, 245, 249)), row);
                using var brush = new SolidBrush(Color.FromArgb(30, 35, 45));
                g.DrawString(tab.SelectOptions[i], font, brush, row.X + 8, row.Y + 4);
            }
        }

        // scrollbar overlay
        var thumb = ScrollBarThumbRect;
        if (!thumb.IsEmpty)
        {
            var g = e.Graphics;
            using var track = new SolidBrush(Color.FromArgb(30, 240, 240, 245));
            g.FillRectangle(track, Width - ScrollbarWidth, 0, ScrollbarWidth, Height);
            using var t = new SolidBrush(DraggingScrollbar ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184));
            g.FillRectangle(t, thumb);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        owner.HandleCanvasMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        owner.HandleCanvasMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        owner.HandleCanvasMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        owner.HandleCanvasMouseLeave();
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        owner.HandleCanvasChar(e.KeyChar);
        e.Handled = true;
        base.OnKeyPress(e);
    }
}

// ============================================================================
// Formulário principal do navegador
// ============================================================================
public class BrowserForm : Form
{
    internal readonly List<BrowserTab> Tabs = new();
    internal int ActiveTabIndex;

    private readonly HttpClient http;
    private readonly ImageLoader images;
    private readonly Bitmap measureBitmap = new(8, 8);
    private readonly Graphics measureG;

    private TabStrip tabStrip = null!;
    private Panel toolbar = null!;
    private Button btnBack = null!, btnForward = null!, btnReload = null!, btnHome = null!;
    private TextBox txtUrl = null!;
    private CanvasPanel canvas = null!;
    private Panel statusBar = null!;
    private string statusText = "Pronto";

    private readonly System.Windows.Forms.Timer resizeTimer;
    private readonly System.Windows.Forms.Timer imageTimer;
    private readonly System.Windows.Forms.Timer caretTimer;
    private readonly System.Windows.Forms.Timer animTimer;
    private readonly System.Windows.Forms.Timer jsTimer;
    private readonly System.Windows.Forms.Timer videoTimer;   // quadros do <video>/<audio>
    private readonly VideoManager videos;
    private readonly ToolTip navTips = new();
    private readonly MouseWheelFilter wheelFilter;
    private string? hoverHref;
    private bool linkClickArmed;
    private Point linkClickPos;
    private DomNode? pressedControl;   // botão pressionado (visual)
    private bool caretOn = true;

    internal BrowserTab CurrentTab => Tabs[ActiveTabIndex];
    internal VideoManager VideoHost => videos;

    public BrowserForm(string startUrl)
    {
        wheelFilter = new MouseWheelFilter(this);
        measureG = Graphics.FromImage(measureBitmap);
        measureG.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            CookieContainer = new System.Net.CookieContainer(), // sessão/consentimento sobrevivem entre páginas
            UseCookies = true,
        };
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Uranium/2.0 Chrome/124.0 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en;q=0.8");

        // decodificação real de <video>/<audio> (LibVLC)
        videos = new VideoManager();
        VideoManager.Global = videos;
        videoTimer = new System.Windows.Forms.Timer { Interval = 66 };
        int vTickN = 0;
        videoTimer.Tick += (s, e) =>
        {
            try
            {
                bool any = false;
                foreach (var t in Tabs)
                {
                    if (t.Document == null) continue;
                    try { videos.Prune(t.Document); } catch { }
                    if (videos.HasActivePlayer(t.Document)) any = true; // player aberto (mesmo em Opening)
                    if (videos.Poll()) any = true;
                }
                if (!any) videoTimer.Stop(); // nada tocando: volta a dormir
                if (any) canvas.Invalidate();
                if (++vTickN % 100 == 1) VideoManager.Log($"tick {vTickN} any={any} tabs={Tabs.Count}");
            }
            catch (Exception exTick)
            {
                VideoManager.Log($"tick EX: {exTick.Message}");
            }
        };

        images = new ImageLoader(http);
        images.OnImageReady = () =>
        {
            // Load() completa em thread de fundo: OnImageReady chega FORA da UI —
            // mexer em System.Windows.Forms.Timer de outra thread lançava
            // (engolido pelo try) e o relayout pós-download NUNCA rodava — as
            // imagens ficavam presas no cache sem pintar.
            try { BeginInvoke(() => RelayoutCurrent()); } catch { }
        };
        images.OnFrameReady = () =>
        {
            // GIF animado: quadro novo pronto — só repinta (chamado de outra thread)
            try { BeginInvoke(() => canvas.Invalidate()); } catch { }
        };

        caretTimer = new System.Windows.Forms.Timer { Interval = 530 };
        caretTimer.Tick += (s, e) => { caretOn = !caretOn; if (CurrentTab.FocusedInput != null) canvas.Invalidate(); };

        resizeTimer = new System.Windows.Forms.Timer { Interval = 140 };
        resizeTimer.Tick += (s, e) => { resizeTimer.Stop(); RelayoutCurrent(); };
        imageTimer = new System.Windows.Forms.Timer { Interval = 120 };
        imageTimer.Tick += (s, e) =>
        {
            imageTimer.Stop();
            try
            {
                var t = CurrentTab;
                if (t?.Js != null && t.Js.FireImageEvents()) { /* handlers podem ter sujado */ }
                if (t?.Js != null && t.Js.Dirty) { JsDirty(t); return; }
            }
            catch { }
            RelayoutCurrent();
        };
        animTimer = new System.Windows.Forms.Timer { Interval = 50 };
        animTimer.Tick += (s, e) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                bool any = false;
                foreach (var t in Tabs)
                {
                    if (!t.Anims.HasItems || t.Styles == null || t.Document == null) continue;
                    any = true;
                    if (t.Anims.Tick(now, t.Styles) && t == CurrentTab) canvas.Invalidate();
                }
                if (!any) animTimer.Stop();
            }
            catch { }
        };
        jsTimer = new System.Windows.Forms.Timer { Interval = 40 };
        jsTimer.Tick += (s, e) =>
        {
            try
            {
                bool any = false;
                foreach (var t in Tabs)
                {
                    if (t.Js == null) continue;
                    any = true;
                    bool ran = false;
                    try { ran = t.Js.ProcessTimers(); } catch { }
                    if (t == CurrentTab && (ran || t.Js.Dirty))
                    {
                        if (t.Js.Dirty) JsDirty(t);
                        else canvas.Invalidate();
                    }
                }
                if (!any) jsTimer.Stop();
            }
            catch { }
        };

        BuildUi();

        var first = new BrowserTab();
        Tabs.Add(first);
        ActiveTabIndex = 0;
        Navigate(first, string.IsNullOrWhiteSpace(startUrl) ? "about:home" : startUrl);

        Application.AddMessageFilter(wheelFilter);
    }

    private void BuildUi()
    {
        Text = "Uranium Browser";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(560, 400);
        KeyPreview = true;
        BackColor = Color.FromArgb(15, 23, 42);
        Font = new Font("Segoe UI", 9f);

        // barra de ferramentas
        toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(30, 41, 59), Padding = new Padding(6, 5, 6, 5) };

        var MakeNavButton = (string glyph) => new Button
        {
            Text = glyph,
            Width = 38,
            Dock = DockStyle.Left,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Segoe UI Symbol", 12f, FontStyle.Regular, GraphicsUnit.Point),
            TabStop = false,
        };

        btnBack = MakeNavButton("◀");
        btnForward = MakeNavButton("▶");
        btnReload = MakeNavButton("⟳");
        btnHome = MakeNavButton("⌂");

        void NavClick(Button b, Action fn) { b.Click += (s, e) => fn(); b.MouseEnter += (s, e) => b.BackColor = Color.FromArgb(51, 65, 85); b.MouseLeave += (s, e) => b.BackColor = Color.FromArgb(30, 41, 59); }
        NavClick(btnBack, GoBack);
        NavClick(btnForward, GoForward);
        NavClick(btnReload, Reload);
        NavClick(btnHome, () => Navigate(CurrentTab, "about:home"));
        navTips.SetToolTip(btnBack, "Voltar (Alt+←)");
        navTips.SetToolTip(btnForward, "Avançar (Alt+→)");
        navTips.SetToolTip(btnReload, "Recarregar (F5)");
        navTips.SetToolTip(btnHome, "Página inicial");

        txtUrl = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f),
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(226, 232, 240),
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = true,
        };
        txtUrl.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Navigate(CurrentTab, NormalizeUrl(txtUrl.Text));
                e.Handled = true;
                e.SuppressKeyPress = true;
                canvas.Focus();
            }
        };

        // ordem de dock: Fill primeiro adicionado por último
        var urlWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4), BackColor = Color.FromArgb(30, 41, 59) };
        urlWrap.Controls.Add(txtUrl);
        toolbar.Controls.Add(urlWrap);
        toolbar.Controls.Add(btnBack);
        toolbar.Controls.Add(btnForward);
        toolbar.Controls.Add(btnReload);
        toolbar.Controls.Add(btnHome);

        // barra de status
        statusBar = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(30, 41, 59) };
        statusBar.Paint += (s, e) =>
        {
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
            e.Graphics.DrawString(statusText, font, brush, 8, 3);
        };

        tabStrip = new TabStrip(this);
        canvas = new CanvasPanel(this);

        Controls.Add(canvas);
        Controls.Add(toolbar);
        Controls.Add(tabStrip);
        Controls.Add(statusBar);

        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.FromArgb(226, 232, 240) };
        var miLink = new ToolStripMenuItem("Abrir link em nova aba") { Enabled = false };
        var miCopy = new ToolStripMenuItem("Copiar endereço do link") { Enabled = false };
        miLink.Click += (s, e) => { if (hoverHref != null) NewTab(hoverHref); };
        miCopy.Click += (s, e) => { if (hoverHref != null) { try { Clipboard.SetText(hoverHref); } catch { } } };
        var sep = new ToolStripSeparator();
        var miBack = new ToolStripMenuItem("Voltar");
        var miFwd = new ToolStripMenuItem("Avançar");
        var miReload = new ToolStripMenuItem("Recarregar");
        miBack.Click += (s, e) => GoBack();
        miFwd.Click += (s, e) => GoForward();
        miReload.Click += (s, e) => Reload();
        menu.Items.AddRange(new ToolStripItem[] { miLink, miCopy, sep, miBack, miFwd, miReload });
        canvas.ContextMenuStrip = menu;
        menu.Opening += (s, e) =>
        {
            miLink.Enabled = miCopy.Enabled = hoverHref != null;
            miBack.Enabled = CurrentTab.CanGoBack;
            miFwd.Enabled = CurrentTab.CanGoForward;
        };
    }

    // ---- navegação ---------------------------------------------------------

    internal void NewTab(string url = "about:home")
    {
        var tab = new BrowserTab();
        Tabs.Add(tab);
        ActiveTabIndex = Tabs.Count - 1;
        Navigate(tab, url);
        RefreshChrome();
    }

    internal void CloseTab(int index)
    {
        if (Tabs.Count == 1) { Close(); return; }
        Tabs.RemoveAt(index);
        if (ActiveTabIndex >= Tabs.Count) ActiveTabIndex = Tabs.Count - 1;
        RefreshChrome();
        canvas.Invalidate();
    }

    internal void Navigate(BrowserTab tab, string url, bool pushHistory = true)
    {
        url = url.Trim();
        if (url.Length == 0) return;
        if (pushHistory) tab.NoscriptHops = 0; // navegação do usuário: nova cota de redirects
        int seq = ++tab.LoadSeq;
        tab.Loading = true;
        tab.PendingPostBody = null;
        SetStatus("Carregando " + url + "…");
        if (pushHistory)
        {
            if (tab.HistoryIndex < tab.History.Count - 1)
                tab.History.RemoveRange(tab.HistoryIndex + 1, tab.History.Count - tab.HistoryIndex - 1);
            tab.History.Add(url);
            tab.HistoryIndex = tab.History.Count - 1;
        }
        txtUrl.Text = url;
        RefreshChrome();
        _ = LoadAsync(tab, url, seq);
    }

    /// <summary>Navegação com POST (submissão de formulário method=post).</summary>
    private void NavigatePost(BrowserTab tab, string url, string body)
    {
        url = url.Trim();
        if (url.Length == 0) return;
        int seq = ++tab.LoadSeq;
        tab.Loading = true;
        tab.PendingPostBody = body;
        SetStatus("Carregando (POST) " + url + "…");
        if (tab.HistoryIndex < tab.History.Count - 1)
            tab.History.RemoveRange(tab.HistoryIndex + 1, tab.History.Count - tab.HistoryIndex - 1);
        tab.History.Add(url);
        tab.HistoryIndex = tab.History.Count - 1;
        txtUrl.Text = url;
        RefreshChrome();
        _ = LoadAsync(tab, url, seq);
    }

    private void GoBack()
    {
        var tab = CurrentTab;
        if (!tab.CanGoBack) return;
        tab.HistoryIndex--;
        Navigate(tab, tab.History[tab.HistoryIndex], pushHistory: false);
    }

    private void GoForward()
    {
        var tab = CurrentTab;
        if (!tab.CanGoForward) return;
        tab.HistoryIndex++;
        Navigate(tab, tab.History[tab.HistoryIndex], pushHistory: false);
    }

    private void Reload()
    {
        var tab = CurrentTab;
        if (tab.Url.Length > 0) Navigate(tab, tab.Url, pushHistory: false);
    }

    private async Task LoadAsync(BrowserTab tab, string url, int seq)
    {
        string html;
        Uri? baseUri = null;
        Exception? error = null;

        try
        {
            if (url.Equals("about:home", StringComparison.OrdinalIgnoreCase))
            {
                html = Pages.Home;
            }
            else if (url.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                html = "";
            }
            else if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || System.IO.File.Exists(url))
            {
                var fileUri = url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(url) : new Uri(System.IO.Path.GetFullPath(url));
                baseUri = fileUri;
                html = await File.ReadAllTextAsync(fileUri.LocalPath, Encoding.UTF8);
            }
            else
            {
                var target = new Uri(url);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                HttpResponseMessage? resp = null;
                if (tab.PendingPostBody is string postBody)
                {
                    tab.PendingPostBody = null;
                    var content = new FormUrlEncodedContent(
                        postBody.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(pair =>
                            {
                                int eq = pair.IndexOf('=');
                                return eq >= 0
                                    ? new KeyValuePair<string?, string?>(Uri.UnescapeDataString(pair[..eq].Replace("+", " ")), Uri.UnescapeDataString(pair[(eq + 1)..].Replace("+", " ")))
                                    : new KeyValuePair<string?, string?>(Uri.UnescapeDataString(pair.Replace("+", " ")), "");
                            }).ToList());
                    resp = await http.PostAsync(target, content, cts.Token);
                }
                else
                {
                    resp = await http.GetAsync(target, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                }
                using (resp)
                {
                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
                    if (!resp.IsSuccessStatusCode && bytes.Length == 0)
                        throw new Exception($"HTTP {(int)resp.StatusCode} {resp.StatusCode}");
                    html = DecodeBody(bytes, resp.Content.Headers.ContentType?.CharSet);
                    baseUri = resp.RequestMessage?.RequestUri ?? target;
                }

                // Páginas de substituição do YouTube.
                //
                // DESLIGADO por padrão: o objetivo é a página REAL do YouTube,
                // renderizada e funcional como no Chrome. Antes daqui o HTML
                // real (1,4 MB, com ytInitialData e os bundles Polymer) era
                // jogado fora e trocado por uma página escrita à mão — o que
                // fazia o site "não ficar nada igual".
                //
                // Liga com URANIUM_YT_SUBSTITUTE=1 para o modo antigo.
                bool usarSubstituto = Environment.GetEnvironmentVariable("URANIUM_YT_SUBSTITUTE") == "1";
                if (usarSubstituto && baseUri != null && YouTubePages.IsYouTubeHost(baseUri.Host))
                {
                    VideoManager.Log($"yt: host={baseUri.Host} path={baseUri.AbsolutePath} query={baseUri.Query}");
                    var custom = await YouTubePages.TryBuildAsync(baseUri, html);
                    VideoManager.Log($"yt: custom={(custom == null ? "<null>" : custom.Length.ToString())}");
                    if (custom != null)
                    {
                        html = custom;
                        baseUri = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + "/");
                    }
                    else if (baseUri.AbsolutePath is "/" or "" )
                    {
                        html = YouTubePages.Home();
                        baseUri = new Uri(baseUri.GetLeftPart(UriPartial.Authority) + "/");
                    }
                }
                else if (baseUri != null && YouTubePages.IsYouTubeHost(baseUri.Host))
                {
                    VideoManager.Log($"yt: pagina REAL {html.Length} bytes (substituto desligado)");
                }

                // folhas de estilo externas (limite para não explodir)
                var cssTexts = await FetchExternalCss(html, baseUri, seq);
                if (cssTexts.Length > 0) html += "<style>" + cssTexts + "</style>";
            }
        }
        catch (Exception ex)
        {
            error = ex;
            html = "";
        }

        if (seq != tab.LoadSeq) return; // uma navegação mais nova chegou primeiro

        if (error != null)
        {
            html = Pages.Error(url, error.Message);
            baseUri = null;
        }

        tab.Url = url;
        RenderPage(tab, html, baseUri);
    }

    private async Task<string> FetchExternalCss(string html, Uri baseUri, int seq)
    {
        try
        {
            var doc = new HtmlParser(html).Parse();
            var hrefs = new List<string>();
            void FindLinks(DomNode n)
            {
                if (n.Type == NodeType.Element && n.TagName == "link" &&
                    n.GetAttr("rel").Contains("stylesheet", StringComparison.OrdinalIgnoreCase))
                {
                    var href = n.GetAttr("href");
                    if (href.Length > 0) hrefs.Add(href);
                }
                foreach (var c in n.Children) FindLinks(c);
            }
            FindLinks(doc);
            if (hrefs.Count == 0) return "";
            hrefs = hrefs.Take(8).ToList();

            var tasks = hrefs.Select(async href =>
            {
                try
                {
                    if (!Uri.TryCreate(baseUri, href, out var cssUri) || cssUri.Scheme is not ("http" or "https")) return "";
                    var bytes = await http.GetByteArrayAsync(cssUri);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch { return ""; }
            });
            var results = await Task.WhenAll(tasks);
            int ok = results.Count(t => t.Length > 0);
            VideoManager.Log($"css: {ok}/{hrefs.Count} folhas, {results.Sum(t => (long)t.Length)} chars");
            foreach (var h in hrefs) VideoManager.Log($"css-href: {h.Substring(0, Math.Min(110, h.Length))}");
            return string.Join("\n", results.Where(t => t.Length > 0));
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Coleta <script> em ordem de documento: (external, src-ou-código).</summary>
    private static List<(bool external, string payload)> CollectScripts(DomNode doc)
    {
        var out0 = new List<(bool, string)>();
        void Walk(DomNode n)
        {
            if (n.Type == NodeType.Element && n.TagName == "script")
            {
                string type = n.GetAttr("type").Trim().ToLowerInvariant();
                if (type is "" or "text/javascript" or "application/javascript" or "module")
                {
                    string src = n.GetAttr("src").Trim();
                    if (src.Length > 0) out0.Add((true, src));
                    else
                    {
                        string code = n.CollectText();
                        if (code.Trim().Length > 0) out0.Add((false, code));
                    }
                }
            }
            foreach (var c in n.Children) Walk(c);
        }
        Walk(doc);
        // Não truncar: os scripts de DADOS (ytInitialData, WIZ_global_data, ytcfg)
        // vêm depois dos primeiros no HTML real. Cortar em 25 deixa esses globais
        // indefinidos e o bundle quebra no bootstrap.
        return out0;
    }

    private async Task<string> FetchOneJs(string src, Uri? baseUri)
    {
        try
        {
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = src.IndexOf(',');
                if (comma < 0) return "";
                string head = src.Substring(0, comma);
                string data = src.Substring(comma + 1);
                if (head.Contains(";base64", StringComparison.OrdinalIgnoreCase))
                    return Encoding.UTF8.GetString(Convert.FromBase64String(data));
                return Uri.UnescapeDataString(data);
            }
            if (baseUri == null) return "";
            if (!Uri.TryCreate(baseUri, src, out var jsUri)) return "";
            if (jsUri.IsFile) return await File.ReadAllTextAsync(jsUri.LocalPath, Encoding.UTF8);
            if (jsUri.Scheme is not ("http" or "https")) return "";
            var bytes = await http.GetByteArrayAsync(jsUri);
            // O teto antigo de 300 KB descartava silenciosamente o bundle base-js do
            // YouTube (kevlar_base_module, vários MB) — e é ele que define o Polymer
            // e dispara o evento script-load-dpj. Sem ele nenhum ytd-* é registrado
            // e a SPA nunca monta. Teto alto, com log quando estourar.
            if (bytes.Length > 32_000_000)
            {
                VideoManager.Log($"scripts-GRANDE: {bytes.Length} bytes ignorados ({src.Substring(0, Math.Min(90, src.Length))})");
                return "";
            }
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    /// <summary>Mede a caixa de layout de um nó em coords de viewport (para getBoundingClientRect).</summary>
    private static RectangleF? MeasureBoxFor(BrowserTab tab, DomNode node)
    {
        if (tab.Page == null) return null;
        RectangleF? found = null;
        void Walk(BlockBox b, float dx, float dy, bool fixedUp)
        {
            if (found != null) return;
            bool fx = fixedUp || b.Fixed;
            if (ReferenceEquals(b.Node, node))
            {
                float vy = fx ? b.Y + dy : b.Y + dy - tab.ScrollY;
                found = new RectangleF(b.X + dx, vy, b.W, b.H);
                return;
            }
            foreach (var l in b.Lines) foreach (var p in l.Pieces)
                if (p.Box != null) Walk(p.Box, dx + p.X - p.Box.X, dy + p.Y - p.Box.Y, fx);
            foreach (var c in b.Children) Walk(c, dx, dy, fx);
        }
        try { Walk(tab.Page.Root, 0, 0, false); } catch { }
        return found;
    }

    /// <summary>Executa <script> inserido dinamicamente (loader do Google etc.).</summary>
    private async Task InsertedScriptAsync(BrowserTab tab, DomNode node)
    {
        try
        {
            if (tab.Js == null || tab.Document == null) return;
            string code = "";
            string src = node.GetAttr("src").Trim();
            if (src.Length > 0)
            {
                if (tab.DynScripts >= 10) return;
                tab.DynScripts++;
                code = await FetchOneJs(src, tab.BaseUri);
            }
            else
            {
                code = node.CollectText();
            }
            if (code.Trim().Length == 0 || code.Length > 300_000) return;
            tab.Js.Execute(new List<(string, string)> { (code, "dynamic") });
            if (tab.Js.Dirty) JsDirty(tab);
            else canvas.Invalidate();
        }
        catch { }
    }

    /// <summary>Verifica se a imagem do nó terminou (ok ou falha).</summary>
    private bool IsNodeImageLoaded(BrowserTab tab, DomNode node, out bool failed)
    {
        failed = false;
        try
        {
            if (node.TagName == "svg") return true; // inline: sempre "pronta"
            string src = node.GetAttr("src");
            if (src.Length == 0) return false;
            Uri? abs = null;
            if (tab.BaseUri != null) Uri.TryCreate(tab.BaseUri, src, out abs);
            else Uri.TryCreate(src, UriKind.Absolute, out abs);
            if (abs == null) return false;
            if (images.GetLoaded(abs) != null) return true;
            if (images.IsFailed(abs)) { failed = true; return true; }
            images.Request(abs);
            return false;
        }
        catch { return false; }
    }

    // ---- <video>/<audio>: anexação, clique e host JS ------------------------

    internal void KickVideoTimer()
    {
        try { videoTimer.Start(); } catch { }
    }

    /// <summary>Espaço: alterna o vídeo mais recente da aba atual; false se não houver.</summary>
    private bool TryToggleCurrentVideo()
    {
        var tab = CurrentTab;
        if (tab.Page == null || tab.Page.Videos.Count == 0) return false;
        try
        {
            var (_, node, _) = tab.Page.Videos[^1];
            bool playing = videos.IsPlaying(node);
            if (playing) videos.Pause(node);
            else
            {
                var slot = videos.Find(node);
                if (slot != null && slot.Src.Length > 0) videos.Play(node);
                else AttachVideoNode(tab, node, forceAutoplay: true);
            }
            KickVideoTimer();
            canvas.Invalidate();
            return true;
        }
        catch { return false; }
    }

    /// <summary>Resolve href e anexa o player do vídeo (idempotente por src).</summary>
    internal void AttachVideoNode(BrowserTab tab, DomNode node, bool forceAutoplay = false)
        => AttachVideo(tab, node, forceAutoplay);

    internal void AttachVideoSource(DomNode node, string ignoredSrc, bool autoplay)
    {
        // src mudou via JS: limpa o slot para forçar re-anexo com a nova fonte
        try
        {
            var slot = videos.Find(node);
            if (slot != null && slot.Src.Length > 0)
            {
                var varsrc = node.GetAttr("src");
                string abs = varsrc;
                if (CurrentTab.BaseUri != null && Uri.TryCreate(CurrentTab.BaseUri, varsrc, out var ur)) abs = ur.AbsoluteUri;
                else if (Uri.TryCreate(varsrc, UriKind.Absolute, out var u2)) abs = u2.AbsoluteUri;
                if (slot.Src != abs)
                {
                    videos.StopNode(node);
                    videos.Attach(node, abs, autoplay || node.Attributes.ContainsKey("autoplay"),
                        node.Attributes.ContainsKey("loop"), node.Attributes.ContainsKey("muted"),
                        null, node.GetAttr("slave"));
                }
            }
            else AttachVideo(CurrentTab, node, autoplay);
        }
        catch { }
    }

    private void AttachVideo(BrowserTab tab, DomNode node, bool forceAutoplay)
    {
        try
        {
            if (node.TagName != "video" && node.TagName != "audio") return;
            string src = node.GetAttr("src");
            if (src.Length == 0) return;
            string abs = src;
            if (tab.BaseUri != null && Uri.TryCreate(tab.BaseUri, src, out var u)) abs = u.AbsoluteUri;
            else if (Uri.TryCreate(src, UriKind.Absolute, out var u2)) abs = u2.AbsoluteUri;
            else return;

            var slot = videos.Find(node);
            if (slot != null && slot.Src == abs) return; // já configurado

            bool loop = node.Attributes.ContainsKey("loop");
            bool muted = node.Attributes.ContainsKey("muted");
            bool autoplay = forceAutoplay || node.Attributes.ContainsKey("autoplay");
            string? slave = node.GetAttr("slave");
            if (string.IsNullOrWhiteSpace(slave)) slave = null;
            bool attached = videos.Attach(node, abs, autoplay, loop, muted, null, slave);
            VideoManager.Log($"attach: tag={node.TagName} autoplay={autoplay} ok={attached} {abs[..Math.Min(80, abs.Length)]}");
            if (attached && autoplay)
            {
                KickVideoTimer();
                canvas.Invalidate();
            }
        }
        catch { }
    }

    /// <summary>Percorre o DOM da aba e (re)configura os players de vídeo.</summary>
    private void AttachAllVideos(BrowserTab tab)
    {
        if (tab.Document == null) return;
        void Walk(DomNode n)
        {
            if ((n.TagName == "video" || n.TagName == "audio") && n.GetAttr("src").Length > 0)
                AttachVideoNode(tab, n);
            foreach (var c in n.Children) Walk(c);
        }
        try { Walk(tab.Document); } catch { }
    }

    // ========================================================================
    // Stream real via Innertube client ANDROID_VR (o mesmo caminho do yt-dlp
    // pós-SABR): o servidor devolve formats[] com url JÁ ASSINADA — sem
    // signatureCipher e sem decifrador (o player web 2026 nem tem mais a
    // função). VLC toca a url direta.
    // ========================================================================
    internal static async Task FetchVrStreamAsync(BrowserTab tab, Uri baseUri)
    {
        try
        {
            var vid = "";
            var q = baseUri.Query;
            var i = q.IndexOf("v=", StringComparison.Ordinal);
            if (i >= 0)
            {
                vid = q.Substring(i + 2);
                var amp = vid.IndexOf('&');
                if (amp >= 0) vid = vid.Substring(0, amp);
            }
            if (vid.Length == 0) { VideoManager.Log("vr: sem videoId na url"); return; }
            string corpo = "{\"context\":{\"client\":{\"clientName\":\"ANDROID_VR\",\"clientVersion\":\"1.62.27\",\"deviceMake\":\"Oculus\",\"deviceModel\":\"Quest 3\",\"osName\":\"Android\",\"osVersion\":\"12L\",\"androidSdkVersion\":32,\"hl\":\"pt\",\"gl\":\"BR\",\"utcOffsetMinutes\":-180}},\"videoId\":\"" + vid + "\",\"contentCheckOk\":true,\"racyCheckOk\":true}";
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/player");
            req.Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json");
            req.Headers.UserAgent.ParseAdd("com.google.android.apps.youtube.vr.oculus/1.62.27 (Linux; U; Android 12L; eureka-user Build/SQ3A.220605.009.A1) gzip");
            req.Headers.Add("X-Youtube-Client-Name", "85");
            req.Headers.Add("X-Youtube-Client-Version", "1.62.27");
            using var cli = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, UseCookies = false })
            { Timeout = TimeSpan.FromSeconds(20) };
            using var resp = await cli.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            VideoManager.Log($"vr: HTTP {(int)resp.StatusCode} {json.Length} bytes para vídeo {vid}");
            using var jd = System.Text.Json.JsonDocument.Parse(json);
            var root = jd.RootElement;
            string? st = null;
            if (root.TryGetProperty("playabilityStatus", out var ps) && ps.TryGetProperty("status", out var stv)) st = stv.GetString();
            if (st != "OK") { VideoManager.Log($"vr: playability={st ?? "nulo"} — sem stream"); return; }
            string? melhorMuxed = null, melhorVideo = null, melhorAudio = null;
            long tamMuxed = -1, tamVideo = -1, tamAudio = -1;
            if (root.TryGetProperty("streamingData", out var sd))
            {
                foreach (var grupo in new[] { "formats", "adaptiveFormats" })
                {
                    if (!sd.TryGetProperty(grupo, out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                    foreach (var f in arr.EnumerateArray())
                    {
                        if (!f.TryGetProperty("url", out var uv) || uv.ValueKind != System.Text.Json.JsonValueKind.String) continue;
                        string u = uv.GetString() ?? "";
                        if (u.Length == 0) continue;
                        int itag = 0;
                        if (f.TryGetProperty("itag", out var ig) && ig.ValueKind == System.Text.Json.JsonValueKind.Number) itag = ig.GetInt32();
                        long tam = -1;
                        if (f.TryGetProperty("contentLength", out var cl) && long.TryParse(cl.ToString(), out var tt)) tam = tt;
                        if (itag == 22) melhorMuxed = u;                     // 720p muxed: vence sempre
                        else if (itag == 18 && melhorMuxed == null) melhorMuxed = u;
                        else if (grupo == "adaptiveFormats")
                        {
                            string mime = f.TryGetProperty("mimeType", out var mt) ? (mt.GetString() ?? "") : "";
                            if (mime.StartsWith("video/")) { if (tam > tamVideo) { tamVideo = tam; melhorVideo = u; } }
                            else if (mime.StartsWith("audio/")) { if (tam > tamAudio) { tamAudio = tam; melhorAudio = u; } }
                        }
                    }
                }
            }
            if ((tamVideo > 0 || tamAudio > 0) && tamVideo / 1024 / 1024 >= tamMuxed / 1024 / 1024 && melhorVideo != null && melhorAudio != null)
            {
                // vídeo de alta renda + áudio escravo (VLC sincroniza via slot slave)
                tab.VrStreamUrl = melhorVideo;
                tab.VrAudioSlaveUrl = melhorAudio;
            }
            else if (melhorMuxed != null)
            {
                tab.VrStreamUrl = melhorMuxed;
                tab.VrAudioSlaveUrl = null;
            }
            // semente + re-render mesmo se o fallback já montou sem stream
            if (tab.VrStreamUrl != null && tab.Js != null)
            {
                tab.Js.VrStreamSeedUrl = tab.VrStreamUrl;
                tab.Js.InvalidateWatchFallback();
            }
            VideoManager.Log($"vr: stream ok itag18={(melhorMuxed != null ? "sim" : "não")} video={tamVideo / 1_000_000}MB áudio={tamAudio / 1_000}kbps");
        }
        catch (Exception ex)
        {
            VideoManager.Log("vr-erro: " + ex.Message);
        }
    }

    /// <summary>Espera o &lt;video&gt; do fallback da watch page existir e liga o stream VR.</summary>
    private async Task WatchFallbackVideoAttacherAsync(BrowserTab tab)
    {
        int seq = tab.LoadSeq;
        for (int t = 0; t < 40; t++)
        {
            await Task.Delay(1000);
            try { if (tab.LoadSeq != seq) return; } catch { return; }
            var doc2 = tab.Document;
            if (doc2 == null) return;
            DomNode? video = null;
            try { video = doc2.FindTag("video"); } catch { }
            if (t % 5 == 0) { try { VideoManager.Log($"vr-sonda t+{t}s: video={(video != null ? "sim src=" + video.GetAttr("src").Length : "AUSENTE do DOM")} pecas={(tab.Page?.Videos.Count ?? -1)} links={(tab.Page?.Links?.Count ?? -1)}"); } catch { } }
            if (video == null) continue;
            try { VideoManager.Log("vr-sonda: <video> achado src-len=" + video.GetAttr("src").Length); } catch { }
            try
            {
                BeginInvoke(new Action(() =>
                {
                    try { RelayoutCurrent(); VideoManager.Log("vr-sonda: relayout pós-descoberta"); } catch { }
                }));
            }
            catch { }
            return;
        }
    }

    /// <summary>Toggle play/pause do vídeo sob o clique; true se consumiu o clique.</summary>
    private bool TryVideoClick(BrowserTab tab, Point p)
    {
        if (tab.Page == null) return false;
        float y = p.Y + tab.ScrollY;
        for (int i = tab.Page.Videos.Count - 1; i >= 0; i--)
        {
            var (rect, node, fixedRect) = tab.Page.Videos[i];
            if (rect.Contains(p.X, fixedRect ? p.Y : y))
            {
                try
                {
                    var slot = videos.Find(node);
                    bool playing = videos.IsPlaying(node);
                    if (playing) videos.Pause(node);
                    else if (slot != null && slot.Src.Length > 0) videos.Play(node);
                    else AttachVideoNode(tab, node, forceAutoplay: true);
                }
                catch { }
                KickVideoTimer();
                canvas.Invalidate();
                return true;
            }
        }
        return false;
    }

    // ---- fetch/XHR (JS) → HttpClient -----------------------------------------

    private async Task FetchForJs(BrowserTab tab, JsNetRequest req)
    {
        bool ok = false; int status = 0; string headers = "{}"; string body = "";
        try
        {
            Uri? uri = null;
            if (tab.BaseUri != null) Uri.TryCreate(tab.BaseUri, req.Url, out uri);
            else Uri.TryCreate(req.Url, UriKind.Absolute, out uri);
            if (uri != null && uri.Scheme is "http" or "https")
            {
                using var msg = new HttpRequestMessage(new HttpMethod(req.Method == "" ? "GET" : req.Method), uri);
                try
                {
                    var hh = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(req.HeadersJson);
                    if (hh != null)
                        foreach (var kv in hh)
                            msg.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
                catch { }
                if (!string.IsNullOrEmpty(req.Body) && req.Method is "POST" or "PUT" or "PATCH" or "DELETE")
                    msg.Content = new StringContent(req.Body, Encoding.UTF8, "application/json");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var resp = await http.SendAsync(msg, cts.Token);
                status = (int)resp.StatusCode;
                body = await resp.Content.ReadAsStringAsync(cts.Token);
                if (body.Length > 2_000_000) body = body.Substring(0, 2_000_000);
                var hd = new Dictionary<string, string>();
                foreach (var h in resp.Content.Headers)
                    foreach (var v in h.Value) hd[h.Key.ToLowerInvariant()] = v;
                headers = System.Text.Json.JsonSerializer.Serialize(hd);
                ok = true;
            }
        }
        catch { }
        VideoManager.Log($"fetch: concluído ok={ok} status={status} len={body.Length} url={req.Url}");
        try
        {
            BeginInvoke(() => tab.Js?.EnqueueNetResult(req.Callback, ok, status, headers, body));
        }
        catch { }
    }

    /// <summary>Re-resolve estilos e relayout após mutação JS.</summary>
    private void JsDirty(BrowserTab tab)
    {
        if (tab.Js != null) tab.Js.Dirty = false;
        // NÃO re-resolve o CSS inteiro: o boot real do YouTube altera atributos
        // e o re-walk marca containers como display:none (página vazia). O
        // StyleResolver.Get é lazy — cobre nodes novos sem destruir o cache.
        // try
        // {
        //     if (tab.Styles != null && tab.Document != null)
        //         tab.Styles.Resolve(tab.Document);
        // }
        // catch { }
        RelayoutCurrent();
    }

    /// <summary>Acha o elemento sob o ponto (para listeners em divs etc.).</summary>
    private static DomNode? HitNode(BlockBox box, float x, float yPage, float yView)
    {
        if (box.Style.Hidden) return null;
        float y = box.Fixed ? yView : yPage;
        foreach (var c in box.Children.Where(c => c.IsPositioned).Reverse())
        {
            var r = HitNode(c, x, yPage, yView);
            if (r != null) return r;
        }
        if (x < box.X || x > box.X + box.W || y < box.Y || y > box.Y + box.H) return null;
        foreach (var c in box.Children.Where(c => !c.IsPositioned).Reverse())
        {
            var r = HitNode(c, x, yPage, yView);
            if (r != null) return r;
        }
        return box.Node?.Type == NodeType.Element ? box.Node : null;
    }

    /// <summary>Lê o tema do Windows (para prefers-color-scheme).</summary>
    private static bool DetectDarkMode()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            if (v is int i) return i == 0;
        }
        catch { }
        return false;
    }

    private static string DecodeBody(byte[] bytes, string? headerCharset)
    {
        Encoding enc = Encoding.UTF8;
        string? name = headerCharset;
        if (string.IsNullOrEmpty(name) && bytes.Length > 0)
        {
            // farejamento de charset no <meta charset=...>
            var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096)).ToLowerInvariant();
            int i = head.IndexOf("charset");
            if (i >= 0)
            {
                int eq = head.IndexOf('=', i);
                if (eq >= 0)
                {
                    int start = eq + 1;
                    while (start < head.Length && (head[start] == ' ' || head[start] == '"' || head[start] == '\'')) start++;
                    int end = start;
                    while (end < head.Length && char.IsLetterOrDigit(head[end]) || (end < head.Length && (head[end] == '-' || head[end] == '_'))) end++;
                    if (end > start) name = head.Substring(start, end - start);
                }
            }
        }
        if (!string.IsNullOrEmpty(name))
        {
            try { enc = Encoding.GetEncoding(name); }
            catch { try { enc = Encoding.Latin1; } catch { enc = Encoding.UTF8; } }
        }
        return enc.GetString(bytes);
    }

    /// <summary>
    /// Materializa a preferência de tema em atributos que o CSS da página usa.
    ///
    /// O YouTube marca o modo escuro com `darker-dark-theme` no &lt;html&gt;, mas as
    /// regras que realmente pintam o fundo sÃ£o
    ///     html { background-color:#fff !important }
    ///     html[dark] { background-color:#0f0f0f !important }
    /// e o atributo `dark` sÃ³ Ã© adicionado por JavaScript em tempo de execuÃ§Ã£o.
    /// Como o motor ainda nÃ£o roda o suficiente desse JS, a pÃ¡gina ficava branca.
    /// Aqui honramos o sinal de tema que a PRÃ“PRIA pÃ¡gina jÃ¡ declara no HTML.
    /// </summary>
    private static void ApplyThemeHints(DomNode doc)
    {
        try
        {
            var html = doc.FindTag("html");
            if (html == null) return;
            // YouTube: darker-dark-theme (e o legado dark) significam modo escuro
            bool escuro = html.Attributes.ContainsKey("darker-dark-theme")
                          || html.Attributes.ContainsKey("darker-dark-theme-deprecate")
                          || html.Attributes.ContainsKey("dark")
                          || html.Attributes.ContainsKey("system-icons");
            if (!escuro) return;
            if (!html.Attributes.ContainsKey("dark")) html.Attributes["dark"] = "";
            // o body tambÃ©m recebe a marca em pÃ¡ginas que checam o body
            var body = doc.FindTag("body");
            if (body != null && !body.Attributes.ContainsKey("dark")) body.Attributes["dark"] = "";
            VideoManager.Log("tema: modo escuro detectado no HTML da pagina");
        }
        catch { }
    }

    private async void RenderPage(BrowserTab tab, string html, Uri? baseUri)
    {
        try { videos.StopAll(); } catch { } // players da página anterior fora
        var doc = new HtmlParser(html).Parse();
        ApplyThemeHints(doc);

        // <noscript><meta http-equiv=refresh content="0;url=..."> — o Google usa
        // isso para mandar clientes sem JS à versão com resultados reais
        // (/httpservice/retry/enablejs); um browser sem JS deve seguir o redirect
        if (tab.NoscriptHops < 3)
        {
            // diagnóstico: o parser enxerga o noscript?
            string nosTags = "";
            void VarrNos(DomNode n2)
            {
                if (n2.TagName == "noscript")
                {
                    var dt = "";
                    foreach (var fc in n2.Children) dt += fc.Type + ":" + fc.TagName + ":" + Math.Min(fc.CollectText().Length, 40) + ";";
                    nosTags += "[" + dt + " txt=" + Math.Min(n2.CollectText().Length, 400) + "]";
                }
                foreach (var c2 in n2.Children) VarrNos(c2);
            }
            VarrNos(doc);
            VideoManager.Log("noscript-probe: '" + nosTags + "'");
            var alvo = UrlDeMetaRefresh(doc, baseUri);
            if (alvo != null && alvo != tab.Url)
            {
                tab.NoscriptHops++;
                VideoManager.Log("noscript-refresh: " + alvo);
                Navigate(tab, alvo);
                return;
            }
        }
        tab.Document = doc;
        tab.BaseUri = baseUri;
        tab.Inputs = new Dictionary<DomNode, ControlState>();
        tab.FocusedInput = null;
        tab.OpenSelect = null;
        tab.Js = null;
        tab.DynScripts = 0;
        tab.VrStreamUrl = null;
        tab.VrAudioSlaveUrl = null;
        tab.VrStreamRequested = false;
        if (baseUri != null && baseUri.Host.EndsWith("youtube.com") && baseUri.AbsoluteUri.Contains("/watch"))
        {
            tab.VrStreamRequested = true;
            _ = FetchVrStreamAsync(tab, baseUri);
            _ = WatchFallbackVideoAttacherAsync(tab);
        }

        var titleEl = doc.FindTag("title");
        tab.Title = titleEl?.CollectText().Trim() ?? "";
        if (tab.Title.Length == 0) tab.Title = tab.Url;

        var rules = new List<CssRule>();
        int vw = Math.Max(320, canvas.ClientSize.Width);
        CssParser.SystemDark = DetectDarkMode();
        CssParser.Parse(UserAgentCss.Text, rules, 0, vw);

        // <style> da página
        void CollectStyles(DomNode n, StringBuilder sb)
        {
            if (n.Type == NodeType.Element && n.TagName == "style")
                sb.Append(n.CollectText()).Append('\n');
            foreach (var c in n.Children) CollectStyles(c, sb);
        }
        var sb = new StringBuilder();
        CollectStyles(doc, sb);
        var pageCss = sb.ToString();
        CssParser.Parse(pageCss, rules, 1, vw);

        int vh = Math.Max(200, canvas.ClientSize.Height);
        var resolver = new StyleResolver(rules, vw, vh);
        resolver.Resolve(doc);
        tab.Styles = resolver;

        // @font-face (TTF/OTF): baixa e registra antes do primeiro layout
        await LoadFontFaces(pageCss + html, baseUri);

        // animações CSS: coleta e liga o clock
        try { tab.Anims.Collect(resolver, doc); if (tab.Anims.HasItems) animTimer.Start(); } catch { }

        // JavaScript (Fase F): coleta <script>, baixa externos, executa em ordem
        try
        {
            var scripts = CollectScripts(doc);
            VideoManager.Log($"scripts-coletados: {scripts.Count}");
            if (scripts.Count > 0)
            {
                var js = new JsEngine(doc, resolver);
                js.CurrentUrl = tab.Url;
                js.OnNavigate = u => { try { BeginInvoke(() => { if (!string.IsNullOrEmpty(u)) Navigate(tab, u); else if (tab.Url.Length > 0) Navigate(tab, tab.Url, pushHistory: false); }); } catch { } };
                js.OnLog = m =>
                {
                    try { SetStatus(m.Length > 120 ? m.Substring(0, 120) : m); } catch { }
                    // também grava: sem isso os erros de JS da página real ficam invisíveis
                    VideoManager.Log("jserr: " + (m.Length > 200 ? m.Substring(0, 200) : m));
                };
                js.OnScrollTo = (x, y) => { try { tab.ScrollY = Math.Max(0, y); canvas.Invalidate(); } catch { } };
                js.OnFocusRequest = node => { try { if (ControlKindFor(node) is "text" or "textarea") FocusControl(node); } catch { } };
                js.OnBlurRequest = node => { try { if (tab.FocusedInput == node) { tab.FocusedInput = null; canvas.Invalidate(); } } catch { } };
                js.GetFocused = () => tab.FocusedInput;
                js.GetViewport = () => { try { return (canvas.ClientSize.Width, canvas.ClientSize.Height); } catch { return (1280, 800); } };
                js.GetScroll = () => tab.ScrollY;
                js.MeasureBox = node => MeasureBoxFor(tab, node);
                js.OnScriptInserted = node => { try { _ = InsertedScriptAsync(tab, node); } catch { } };
                js.Videos = new VideoJsHost(this);
                js.OnVideoSrcSet = node => { try { AttachVideoNode(tab, node); KickVideoTimer(); } catch { } };
                js.OnNetRequest = req => { try { _ = FetchForJs(tab, req); } catch { } };
                var code = new List<(string, string)>();
                int fetched = 0, inlineN = 0;
                long bytesTotais = 0;
                // teto generoso: o YouTube real referencia várias dezenas de bundles
                // e cortar cedo deixa a página sem as APIs que o app espera.
                const int MaxExternos = 60;
                foreach (var (external, payload) in scripts)
                {
                    if (!external) { code.Add((payload, "inline")); inlineN++; bytesTotais += payload.Length; continue; }
                    if (fetched >= MaxExternos) continue;
                    fetched++;
                    VideoManager.Log($"scripts-fetch: início #{fetched} {payload.Substring(0, Math.Min(70, payload.Length))}");
                    var relogio = System.Diagnostics.Stopwatch.StartNew();
                    string jsText = await FetchOneJs(payload, baseUri);
                    relogio.Stop();
                    fetched--; // coincide com os contadores originais
                    // visibilidade: qual script mordeu a rede e quanto custou
                    if (jsText.Length > 0 || relogio.ElapsedMilliseconds > 3000)
                        VideoManager.Log($"scripts-fetch: {Math.Max(1, fetched)} {payload.Substring(0, Math.Min(70, payload.Length))} → {jsText.Length} bytes em {relogio.ElapsedMilliseconds}ms");
                    // RETRY: rede instável deixava a cadeia do Polymer incompleta
                    // (o bundle principal tem ~10MB — um FAIL silencia o boot
                    // inteiro: definindo stubs sem as 473 propriedades). Escada
                    // de 4 tentativas com backoff progressivo.
                    int[] esperas = { 300, 800, 2000, 4000 };
                    for (int tent = 0; tent < esperas.Length && jsText.Length == 0; tent++)
                    {
                        await Task.Delay(esperas[tent]);
                        jsText = await FetchOneJs(payload, baseUri);
                    }
                    if (jsText.Length > 0)
                    {
                        code.Add((jsText, payload.Length > 60 ? payload.Substring(0, 60) : payload));
                        fetched++; bytesTotais += jsText.Length;
                    }
                    else
                    {
                        // Sem isto um bundle que falha some sem deixar rastro — foi o
                        // que escondeu o base-js (que define Polymer) por várias rodadas.
                        string curto = payload.Length > 120 ? payload.Substring(payload.Length - 120) : payload;
                        VideoManager.Log($"scripts-FALHOU: {curto}");
                    }
                }
                VideoManager.Log($"scripts: inline={inlineN} externos={fetched} total={code.Count} bytes={bytesTotais}");
                tab.Js = js;
                js.IsImageReady = node => IsNodeImageLoaded(tab, node, out _);
                js.IsImageFailed = node => IsNodeImageLoaded(tab, node, out bool failed) && failed;
                js.OnImgSrcSet = node =>
                {
                    try
                    {
                        if (tab.BaseUri == null) return;
                        string src = node.GetAttr("src");
                        if (src.Length == 0) return;
                        if (Uri.TryCreate(tab.BaseUri, src, out var abs)) images.Request(abs);
                    }
                    catch { }
                };
                js.Execute(code);
                if (js.Dirty) { js.Dirty = false; resolver.Resolve(doc); }
                // stream VR: espera o fetch (~1s) instruir a global que o
                // fallback da watch page consome para criar <video src>
                if (tab.VrStreamRequested && tab.VrStreamUrl == null)
                {
                    for (int w = 0; w < 56 && tab.VrStreamUrl == null; w++) await Task.Delay(250);
                }
                try
                {
                    var urlJson = tab.VrStreamUrl == null ? "''" : System.Text.Json.JsonSerializer.Serialize(tab.VrStreamUrl);
                    if (tab.VrStreamUrl != null) tab.Js.VrStreamSeedUrl = tab.VrStreamUrl;
                    js.Eval("globalThis.__vrStreamUrl = " + urlJson + ";");
                    tab.Js.InvalidateWatchFallback();
                }
                catch { }
                jsTimer.Start();
            }
        }
        catch { tab.Js = null; }

        tab.Loading = false;
        RelayoutCurrent();
        SetStatus("Pronto");
        RefreshChrome();
        try { VideoManager.Log($"render: pecas-video={tab.Page?.Videos.Count ?? -1} controles={tab.Page?.Controls.Count ?? -1} altura={tab.Page?.Height ?? 0} titulo=`{tab.Title}`"); }
        catch { VideoManager.Log("render: page null?"); }
        // Diagnóstico de SPA: quantos elementos o JS realmente criou por tag.
        try
        {
            var porTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int total = 0;
            void Conta(DomNode n)
            {
                if (n.Type == NodeType.Element)
                {
                    total++;
                    porTag.TryGetValue(n.TagName, out int c);
                    porTag[n.TagName] = c + 1;
                }
                foreach (var ch in n.Children) Conta(ch);
                if (n.ShadowRoot != null) Conta(n.ShadowRoot);
            }
            Conta(doc);
            var topo = porTag.Where(kv => kv.Key.StartsWith("yt", StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(kv => kv.Value).Take(12)
                             .Select(kv => kv.Key + "=" + kv.Value);
            VideoManager.Log($"dom: total={total} tags={porTag.Count} yt*=[{string.Join(" ", topo)}]");
            if (tab.Js != null)
            {
                var appUp = tab.Js.Eval("(function(){var a=document.querySelector('ytd-app');return a?(a.hasAttribute('disable-upgrade')?1:0):-1;})()");
                var reg = tab.Js.Eval("(function(){var r=[];try{var a=document.querySelector('ytd-app');}catch(e){} return (typeof customElements!=='undefined' && customElements.get && customElements.get('ytd-app'))?1:0;})()");
                var regMast = tab.Js.Eval("(function(){return (customElements.get && customElements.get('ytd-masthead'))?1:0;})()");
                VideoManager.Log($"ce: ytd-app-inerte={appUp} reg-ytd-app={reg} reg-ytd-masthead={regMast}");
                // O ytd-app existe e está ativo — mas ele tem filhos? tem altura?
                var det = tab.Js.Eval("(function(){try{var a=document.querySelector('ytd-app');if(!a)return 'sem-app';var r=a.getBoundingClientRect?JSON.stringify(a.getBoundingClientRect()):'sem-rect';return 'filhos='+a.children.length+' rect='+r;}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-det: " + det);
                var tagsYt = tab.Js.Eval("(function(){try{var m={};var all=document.querySelectorAll('*');for(var i=0;i<all.length;i++){var t=all[i].tagName.toLowerCase();if(t.indexOf('ytd-')===0||t.indexOf('yt-')===0)m[t]=(m[t]||0)+1;}var r=[];for(var k in m)r.push(k+'='+m[k]);return r.join(' ');}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-ytd: " + tagsYt);
                // ytd-app tem 1188x0: a altura colapsa logo no topo. Mostramos os
                // filhos diretos com display e altura para achar onde quebra.
                var det2 = tab.Js.Eval(@"(function(){try{var a=document.querySelector('ytd-app');if(!a)return 'sem-app';var o=[];for(var i=0;i<a.children.length;i++){var c=a.children[i];var r=c.getBoundingClientRect?c.getBoundingClientRect():null;var cs=window.getComputedStyle?window.getComputedStyle(c):null;o.push(c.tagName.toLowerCase()+'{id='+(c.id||'-')+' disp='+(cs?cs.display:'?')+' h='+(r?Math.round(r.height):'?')+'}');}return o.join(' ');}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-filhos: " + det2);
                // O masthead monta, mas o corpo (player/lista) não. O Polymer monta o
                // conteúdo a partir dos dados globais — conferimos se eles chegaram.
                var dados = tab.Js.Eval(@"(function(){try{
  var r=[];
  r.push('ytInitialData='+(typeof ytInitialData));
  r.push('ytInitialPlayerResponse='+(typeof ytInitialPlayerResponse));
  r.push('ytcfg='+(typeof ytcfg));
  r.push('Polymer='+(typeof Polymer));
  r.push('ytd-watch-flexy-reg='+(customElements.get&&customElements.get('ytd-watch-flexy')?1:0));
  r.push('ytd-page-manager-reg='+(customElements.get&&customElements.get('ytd-page-manager')?1:0));
  r.push('ytd-watch-flexy-no-dom='+document.querySelectorAll('ytd-watch-flexy').length);
  r.push('ytd-page-manager-no-dom='+document.querySelectorAll('ytd-page-manager').length);
  if(typeof ytInitialData==='object'&&ytInitialData){r.push('contents='+(ytInitialData.contents?Object.keys(ytInitialData.contents).join(','):'sem'));}
  if(typeof Polymer==='object'&&Polymer.RenderStatus){r.push('RenderStatus=ok');}
  return r.join(' ');
}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-dados: " + dados);
                // ytd-app monta o masthead mas não cria o ytd-page-manager. Vamos ver
                // até onde o próprio ciclo de vida dele roda.
                var ciclo = tab.Js.Eval(@"(function(){try{
  var C=customElements.get('ytd-app'); if(!C) return 'sem-ctor';
  var p=C.prototype; var o=[];
  o.push('proto-tem-connected='+(typeof p.connectedCallback));
  o.push('proto-tem-ready='+(typeof p.ready));
  o.push('proto-tem-attached='+(typeof p.attached));
  o.push('Polymer.Element='+(typeof (window.Polymer&&Polymer.Element)));
  o.push('Polymer.RenderStatus='+(typeof (window.Polymer&&Polymer.RenderStatus)));
  if(window.Polymer&&Polymer.RenderStatus) o.push('beforeNextRender='+(typeof Polymer.RenderStatus.beforeNextRender));
  o.push('Polymer.settings='+(typeof (window.Polymer&&Polymer.settings)));
  o.push('Polymer.version='+((window.Polymer&&Polymer.version)||'-'));
  var a=document.querySelector('ytd-app');
  o.push('app-upgraded='+(a&&a.constructor?a.constructor.name:'?'));
  o.push('app-tem-pageManager='+(a&&('pageManager' in a)?1:0));
  o.push('app-shadow='+(a&&a.shadowRoot?1:0));
  o.push('app-props='+(a?Object.keys(a).length:0));
  return o.join(' ');
}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-ciclo: " + ciclo);
                // _attachDom (Polymer) faz W=_.et(this); if(W.attachShadow){ W.shadowRoot.appendChild(u) }
                // e morria com "appendChild of undefined". Vemos o que o elemento
                // expõe nesse caminho.
                var att = tab.Js.Eval(@"(function(){try{
  var a=document.querySelector('ytd-app'); if(!a) return 'sem-app';
  var o=[];
  o.push('attachShadow='+(typeof a.attachShadow));
  o.push('shadowRoot='+(a.shadowRoot?('obj filhos='+a.shadowRoot.children.length):'NULL'));
  o.push('_attachDom='+(typeof a._attachDom));
  o.push('root='+(a.root?('obj filhos='+a.root.children.length):'NULL'));
  o.push('shadowRoot-has-append='+(a.shadowRoot&&typeof a.shadowRoot.appendChild));
  o.push('tem-shadyRoot='+(a.shadyRoot?(typeof a.shadyRoot):'NULL'));
  o.push('constructor.name='+(a.constructor&&a.constructor.name));
  o.push('proto-shadowRoot-descriptor='+(a.constructor&&a.constructor.prototype?String(!!Object.getOwnPropertyDescriptor(a.constructor.prototype,'shadowRoot')):'-'));
  return o.join(' ');
}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-att: " + att);
                // Diagnóstico do objeto CRU do ytd-app: que propriedades o
                // ShadyDOM gravou nele? É por aqui que o exportador deve achar
                // a árvore estampada.
                var bruto = tab.Js.Eval(@"(function(){try{
  var a=document.querySelector('ytd-app'); if(!a) return 'sem-el';
  var b=a.__raw; if(!b) return 'sem-raw';
  var nomes=Object.getOwnPropertyNames(b);
  var o=['props='+nomes.length+' chaves='+nomes.slice(0,30).join(',')];
  var ss=typeof b.__shady_shadowRoot; var sr2=typeof b.__shady_root; var sd=typeof b.shadowRoot;
  o.push('ss='+ss+' sroot='+sr2+' sd='+sd);
  try { if (b.__shady_shadowRoot) { var r=b.__shady_shadowRoot; o.push('SRfilhos='+(r.children?r.children.length:(r.childNodes?r.childNodes.length:'?'))); } } catch(e){}
  try { if (b.__shady_root) { var r2=b.__shady_root; o.push('ROOTfilhos='+(r2.children?r2.children.length:(r2.childNodes?r2.childNodes.length:'?'))); } } catch(e){}
  try { if (b.shadowRoot) { var r0=b.shadowRoot; o.push('bruto-sr-filhos='+(r0.children?r0.children.length:(r0.childNodes?r0.childNodes.length:'?'))+' tipo='+typeof r0); } } catch(e0){}
  o.push('expo='+(typeof globalThis.__shadyExport)+' dp='+(typeof globalThis.DOMParser)+' ser='+(typeof globalThis.__serializa));
  // o que os nós DO SR deles têm: tags e shadow roots aninhados?
  var n2 = globalThis.__shadyExport();
  o.push('reexport='+n2);
  var sr = null; try { sr = a.shadowRoot || null; } catch(e5) {}
  if (sr) {
    var fs = []; var fil = sr.children || sr.childNodes || [];
    for (var i2 = 0; i2 < fil.length && i2 < 10; i2++) {
      var nd = fil[i2]; var tg = '?'; try { tg = String(nd.tagName || nd.nodeName).toLowerCase(); } catch(e6) {}
      var nn = 0; try { var s2 = nd.shadowRoot; if (s2) { nn = s2.children ? s2.children.length : (s2.childNodes ? s2.childNodes.length : -1); } } catch(e7) { nn = -2; }
      var kk = 0; try { kk = nd.childNodes ? nd.childNodes.length : (nd.children ? nd.children.length : 0); } catch(e8) {}
      fs.push(tg + '/sr=' + nn + '/k=' + kk);
    }
    o.push('srfilhos=' + fs.join(' '));
  }
  return o.join(' ');
}catch(e){return 'erro:'+e.message;}})()");
                VideoManager.Log("ce-bruto: " + bruto);
            }
        }
        catch { }
    }

    /// <summary>Procura blocos @font-face no CSS e carrega fontes TTF/OTF (woff/woff2 não).</summary>
    private async Task LoadFontFaces(string css, Uri? baseUri)
    {
        int i = 0, loaded = 0;
        while (loaded < 8)
        {
            i = css.IndexOf("@font-face", i, StringComparison.OrdinalIgnoreCase);
            if (i < 0) break;
            int open = css.IndexOf('{', i);
            int close = open >= 0 ? css.IndexOf('}', open) : -1;
            if (open < 0 || close < 0) break;
            string block = css.Substring(open + 1, close - open - 1);
            i = close + 1;

            string family = ExtractDeclaration(block, "font-family").Trim().Trim('"', '\'');
            string src = ExtractDeclaration(block, "src");
            int url0 = src.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
            if (family.Length == 0 || url0 < 0) continue;
            url0 += 4;
            int url1 = src.IndexOf(')', url0);
            if (url1 < 0) continue;
            string url = src.Substring(url0, url1 - url0).Trim().Trim('"', '\'');
            string low = url.ToLowerInvariant();
            if (url.Length == 0 || url.StartsWith("data:") || !(low.EndsWith(".ttf") || low.EndsWith(".otf")))
                continue; // sem base64/woff/woff2

            try
            {
                if (!Uri.TryCreate(baseUri, url, out var fontUri) ||
                    (fontUri.Scheme != "http" && fontUri.Scheme != "https" && !fontUri.IsFile)) continue;
                byte[] data = fontUri.IsFile
                    ? await File.ReadAllBytesAsync(fontUri.LocalPath)
                    : await http.GetByteArrayAsync(fontUri);
                FontLoader.Add(family, data);
                loaded++;
            }
            catch { }
        }
    }

    private static string ExtractDeclaration(string block, string prop)
    {
        int idx = block.IndexOf(prop, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        int colon = block.IndexOf(':', idx);
        if (colon < 0) return "";
        int semi = block.IndexOf(';', colon);
        if (semi < 0) semi = block.Length;
        return block.Substring(colon + 1, semi - colon - 1);
    }

    internal void RelayoutCurrent()
    {
        var tab = CurrentTab;
        if (tab?.Styles == null || tab.Document == null) return;
        try
        {
            float vw = Math.Max(200, canvas.ClientSize.Width - CanvasPanel.ScrollbarWidth);
            var engine = new LayoutEngine(measureG, tab.Styles, images, tab.BaseUri, tab.Inputs);
            tab.Page = engine.Run(tab.Document, vw);
            // players de <video>/<audio> do documento atual
            if (!tab.Loading) AttachAllVideos(tab);
            // âncora (#fragmento) na navegação
            if (tab.ScrollY == 0 && tab.Url.Contains('#'))
            {
                var frag = tab.Url.Substring(tab.Url.IndexOf('#') + 1);
                if (tab.Page.Anchors.TryGetValue(frag, out float ay))
                    tab.ScrollY = ay - 16;
            }
            tab.ScrollY = Math.Clamp(tab.ScrollY, 0, Math.Max(0, tab.Page.Height - canvas.ClientSize.Height));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Erro de layout: " + ex.Message);
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "uranium_layout.log"),
                    DateTime.UtcNow.ToString("HH:mm:ss") + " " + ex + "\n\n");
            }
            catch { }
        }
        canvas.Invalidate();
    }

    private static string NormalizeUrl(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return "about:home";
        if (input.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return input;
        // prefixo yt → busca no YouTube
        if (input.StartsWith("yt ", StringComparison.OrdinalIgnoreCase))
            return "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(input.Substring(3).Trim());
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return input;
        // sem ponto ou com espaço → busca (Google); "yt ..." cobre YouTube
        if (input.Contains(' ') || !input.Contains('.'))
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
        return "https://" + input;
    }

    // ---- interação com o canvas ---------------------------------------------

    internal ControlContext BuildControlContext()
    {
        var tab = CurrentTab;
        return new ControlContext
        {
            Inputs = tab.Inputs,
            Focused = tab.FocusedInput,
            CaretOn = caretOn,
            Pressed = pressedControl,
            OpenSelect = tab.OpenSelect,
            OpenSelectHover = tab.SelectHover,
        };
    }

    private (RectangleF Rect, DomNode Node, string Kind, bool Fixed)? HitTestControl(Point p)
    {
        var tab = CurrentTab;
        if (tab?.Page == null) return null;
        float y = p.Y + tab.ScrollY;
        for (int i = tab.Page.Controls.Count - 1; i >= 0; i--)
        {
            var c = tab.Page.Controls[i];
            if (c.Rect.Contains(p.X, c.Fixed ? p.Y : y)) return c;
        }
        return null;
    }

    internal void HandleCanvasMouseDown(MouseEventArgs e)
    {
        var tab = CurrentTab;
        var thumb = canvas.ScrollBarThumbRect;
        if (!thumb.IsEmpty && e.X >= canvas.Width - CanvasPanel.ScrollbarWidth)
        {
            canvas.DraggingScrollbar = true;
            canvas.DragStartScroll = tab.ScrollY;
            canvas.DragStartY = e.Y;
            canvas.Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left)
        {
            linkClickArmed = true;
            linkClickPos = e.Location;
            var ctrl = HitTestControl(e.Location);
            pressedControl = ctrl?.Node;
            if (pressedControl != null) canvas.Invalidate();
        }
    }

    internal void HandleCanvasMouseMove(MouseEventArgs e)
    {
        var tab = CurrentTab;
        if (canvas.DraggingScrollbar && tab.Page != null)
        {
            var thumb = canvas.ScrollBarThumbRect;
            if (!thumb.IsEmpty)
            {
                float viewH = canvas.ClientSize.Height;
                float docH = tab.Page.Height;
                float dy = e.Y - canvas.DragStartY;
                tab.ScrollY = Math.Clamp(canvas.DragStartScroll + dy * (docH - viewH) / Math.Max(1, viewH - thumb.Height), 0, docH - viewH);
                canvas.Invalidate();
            }
            return;
        }

        // hover dentro do dropdown do select
        if (tab.OpenSelect != null)
        {
            var popup = new RectangleF(tab.SelectPopupRect.X, tab.SelectPopupRect.Bottom, Math.Max(tab.SelectPopupRect.Width, 140), 24 * Math.Min(tab.SelectOptions.Count, 12) + 2);
            int row = (int)((e.Y - popup.Y - 1) / 24);
            int newHover = popup.Contains(e.X, e.Y) && row >= 0 && row < Math.Min(tab.SelectOptions.Count, 12) ? row : -1;
            if (newHover != tab.SelectHover) { tab.SelectHover = newHover; canvas.Invalidate(); }
            return;
        }

        string? href = HitTestLink(e.Location);
        var ctrl = HitTestControl(e.Location);
        if (href != hoverHref || ctrl != null)
        {
            hoverHref = href;
            SetStatus(href ?? "Pronto");
        }
        canvas.Cursor = href != null || (ctrl?.Kind is "button" or "checkbox" or "radio" or "select")
            ? Cursors.Hand
            : ctrl?.Kind is "text" or "textarea" ? Cursors.IBeam : Cursors.Default;
    }

    internal void HandleCanvasMouseUp(MouseEventArgs e)
    {
        var wasPressed = pressedControl;
        pressedControl = null;
        canvas.DraggingScrollbar = false;

        if (e.Button != MouseButtons.Left || !linkClickArmed) { canvas.Invalidate(); return; }
        linkClickArmed = false;
        bool isClick = Math.Abs(e.X - linkClickPos.X) < 5 && Math.Abs(e.Y - linkClickPos.Y) < 5;
        var tab = CurrentTab;

        // dropdown do select aberto?
        if (tab.OpenSelect != null && tab.SelectOptions.Count > 0)
        {
            var popup = new RectangleF(tab.SelectPopupRect.X, tab.SelectPopupRect.Bottom, Math.Max(tab.SelectPopupRect.Width, 140), 24 * Math.Min(tab.SelectOptions.Count, 12) + 2);
            if (isClick && popup.Contains(e.X, e.Y))
            {
                int row = (int)((e.Y - popup.Y - 1) / 24);
                if (row >= 0 && row < tab.SelectOptions.Count)
                {
                    var st = GetInputState(tab.OpenSelect);
                    st.Touched = true;
                    st.SelectedIndex = row;
                }
            }
            tab.OpenSelect = null;
            canvas.Invalidate();
            return;
        }

        var ctrl = isClick ? HitTestControl(e.Location) : null;

        // clique em <video>/<audio> alterna play/pause (antes dos links e controles)
        if (isClick && TryVideoClick(tab, e.Location)) { canvas.Invalidate(); return; }

        // JavaScript primeiro: listeners (com preventDefault) antes da ação padrão
        if (isClick && tab.Js != null && tab.Document != null && tab.Page != null)
        {
            DomNode? target = ctrl?.Node;
            if (target == null)
            {
                string? h0 = HitTestLink(e.Location);
                if (h0 != null) target = FindAnchorElement(tab.Document, h0);
            }
            if (target == null) target = HitNode(tab.Page.Root, e.X, e.Y + tab.ScrollY, e.Y);
            if (target != null)
            {
                bool prevented = false;
                try { prevented = tab.Js.FireClick(target); } catch { }
                // foco nativo em texto sempre: preventDefault bloqueia navegação/submit,
                // mas nunca o foco (senão o JS do site mataria a digitação)
                if (ControlKindFor(target) is "text" or "textarea") FocusControl(target);
                if (tab.Js.Dirty) JsDirty(tab);
                if (prevented) { canvas.Invalidate(); return; }
            }
        }

        if (ctrl != null)
        {
            HandleControlClick(ctrl.Value.Node, ctrl.Value.Kind, ctrl.Value.Rect, wasPressed == ctrl.Value.Node);
            canvas.Invalidate();
            return;
        }

        // clique em qualquer outro lugar limpa o foco
        if (tab.FocusedInput != null || wasPressed != null)
        {
            tab.FocusedInput = null;
            canvas.Invalidate();
        }

        if (isClick)
        {
            var href = HitTestLink(e.Location);
            if (href != null)
            {
                var target = FindLinkTarget(e.Location);
                if (target == "_blank")
                    NewTab(href);
                else if (href.StartsWith('#'))
                {
                    if (tab.Page != null && tab.Page.Anchors.TryGetValue(href.Substring(1), out float ay))
                    {
                        tab.ScrollY = Math.Max(0, ay - 16);
                        canvas.Invalidate();
                    }
                }
                else
                    Navigate(tab, href);
            }
        }
    }

    /// <summary>Classifica um controle como o layout faz (para foco nativo no clique).</summary>
    internal static string ControlKindFor(DomNode node) => node.TagName switch
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

    private void FocusControl(DomNode node)
    {
        var tab = CurrentTab;
        tab.FocusedInput = node;
        caretOn = true;
        try { caretTimer.Start(); canvas.Focus(); canvas.Invalidate(); } catch { }
    }

    private void HandleControlClick(DomNode node, string kind, RectangleF rect, bool wasPressed)
    {
        var tab = CurrentTab;
        switch (kind)
        {
            case "text":
                tab.FocusedInput = node;
                caretOn = true;
                caretTimer.Start();
                canvas.Focus();
                break;
            case "textarea":
                tab.FocusedInput = node;
                caretOn = true;
                caretTimer.Start();
                canvas.Focus();
                break;
            case "checkbox":
                {
                    var st = GetInputState(node);
                    st.Touched = true;
                    st.Checked = !st.Checked;
                    break;
                }
            case "radio":
                {
                    string group = node.GetAttr("name");
                    // desmarca os demais do mesmo grupo e marca este
                    void Walk(DomNode n)
                    {
                        if (n.Type == NodeType.Element && n.TagName == "input" &&
                            n.GetAttr("type").Equals("radio", StringComparison.OrdinalIgnoreCase) &&
                            n.GetAttr("name") == group)
                        {
                            var s = GetInputState(n);
                            s.Touched = true;
                            s.Checked = ReferenceEquals(n, node);
                        }
                        foreach (var c in n.Children) Walk(c);
                    }
                    if (tab.Document != null) Walk(tab.Document);
                    break;
                }
            case "select":
                {
                    tab.OpenSelect = node;
                    tab.SelectOptions = global::native_browser.LayoutEngine.OptionTexts(node);
                    tab.SelectHover = -1;
                    // rect do controle em coordenadas de tela do canvas
                    tab.SelectPopupRect = new RectangleF(rect.X, rect.Y - tab.ScrollY, rect.Width, rect.Height);
                    break;
                }
            case "button":
                if (wasPressed) SubmitFormFor(node);
                break;
        }
    }

    internal void HandleCanvasChar(char c)
    {
        var tab = CurrentTab;
        var node = tab.FocusedInput;
        if (node == null) return;
        var st = GetInputState(node);
        st.Touched = true;

        if (c == '\b')
        {
            if (st.Value.Length > 0) st.Value = st.Value[..^1];
        }
        else if (c == '\r' || c == '\n')
        {
            if (node.TagName == "textarea") st.Value += "\n";
            else SubmitFormFor(node); // Enter envia o formulário
        }
        else if (c >= ' ')
        {
            st.Value += c;
        }
        caretOn = true;
        RelayoutCurrent(); // repinta COM o texto novo (a largura foi remedida)
    }

    private ControlState GetInputState(DomNode node)
    {
        if (!CurrentTab.Inputs.TryGetValue(node, out var st))
        {
            st = new ControlState();
            CurrentTab.Inputs[node] = st;
        }
        return st;
    }

    /// <summary>Envia o formulário GET que contém o nó (monta a query string e navega).</summary>
    private void SubmitFormFor(DomNode node)
    {
        DomNode? form = node;
        while (form != null && form.TagName != "form") form = form.Parent;
        if (form == null) return;

        var action = form.GetAttr("action");
        Uri? target = null;
        if (action.Length > 0)
        {
            Uri.TryCreate(CurrentTab.BaseUri, action, out target);
        }
        target ??= CurrentTab.BaseUri ?? new Uri("about:blank");
        if (target.Scheme is not ("http" or "https" or "file")) return;

        var query = new StringBuilder();
        void Collect(DomNode n)
        {
            if (n.Type == NodeType.Element)
            {
                string tag = n.TagName;
                string type = tag == "input" ? n.GetAttr("type").ToLowerInvariant() : "";
                string name = n.GetAttr("name");
                if (name.Length > 0)
                {
                    CurrentTab.Inputs.TryGetValue(n, out var st);
                    string? value = null;
                    if (tag == "input" && type is "text" or "password" or "hidden" or "email" or "search" or "number" or "tel" or "url")
                        value = st?.Touched == true ? st.Value : n.GetAttr("value");
                    else if (tag == "input" && (type == "checkbox" || type == "radio"))
                    {
                        bool check = st?.Touched == true ? st.Checked : n.Attributes.ContainsKey("checked");
                        if (check) value = n.GetAttr("value") is { Length: > 0 } v ? v : "on";
                    }
                    else if (tag == "select")
                    {
                        var options = global::native_browser.LayoutEngine.OptionTexts(n);
                        int idx = st?.Touched == true ? st.SelectedIndex : 0;
                        if (idx >= 0 && idx < options.Count) value = options[idx];
                    }
                    else if (tag == "textarea")
                        value = st?.Touched == true ? st.Value : n.CollectText().Trim();

                    if (value != null)
                    {
                        if (query.Length > 0) query.Append('&');
                        query.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
                    }
                }
            }
            foreach (var c in n.Children) Collect(c);
        }
        Collect(form);

        string method = form.GetAttr("method").Trim().ToUpperInvariant();
        SetStatus("Enviando formulário" + (method == "POST" ? " (POST)…" : "…"));
        if (method == "POST")
        {
            NavigatePost(CurrentTab, target.AbsoluteUri, query.ToString());
        }
        else
        {
            string url = target.AbsoluteUri;
            if (query.Length > 0)
                url += (url.Contains('?') ? "&" : "?") + query;
            Navigate(CurrentTab, url);
        }
    }
    internal void HandleCanvasMouseLeave()
    {
        hoverHref = null;
        SetStatus("Pronto");
        canvas.Cursor = Cursors.Default;
    }

    internal void ScrollBy(float delta)
    {
        var tab = CurrentTab;
        if (tab?.Page == null) return;
        float max = Math.Max(0, tab.Page.Height - canvas.ClientSize.Height);
        float next = Math.Clamp(tab.ScrollY + delta, 0, max);
        if (Math.Abs(next - tab.ScrollY) > 0.5f)
        {
            tab.ScrollY = next;
            canvas.Invalidate();
        }
    }

    private string? HitTestLink(Point p)
    {
        var tab = CurrentTab;
        if (tab?.Page == null) return null;
        float y = p.Y + tab.ScrollY;
        for (int i = tab.Page.Links.Count - 1; i >= 0; i--)
        {
            var (rect, href, fixed_) = tab.Page.Links[i];
            if (rect.Contains(p.X, fixed_ ? p.Y : y)) return href;
        }
        return null;
    }

    private string? FindLinkTarget(Point p)
    {
        var tab = CurrentTab;
        if (tab?.Page == null || tab.Document == null) return null;
        string? href = HitTestLink(p);
        if (href == null) return null;
        // procura o <a target=...> correspondente (aproximação: primeiro link com esse href)
        foreach (var (rect, h, fixed_) in tab.Page.Links)
        {
            if (h == href && rect.Contains(p.X, fixed_ ? p.Y : p.Y + tab.ScrollY))
            {
                var el = FindAnchorElement(tab.Document, href);
                return el?.GetAttr("target");
            }
        }
        return null;
    }

    private static DomNode? FindAnchorElement(DomNode n, string href)
    {
        if (n.Type == NodeType.Element && n.TagName == "a" && n.GetAttr("href") == href) return n;
        foreach (var c in n.Children)
        {
            var f = FindAnchorElement(c, href);
            if (f != null) return f;
        }
        return null;
    }

    // ---- chrome (título, botões, status) -------------------------------------

    private void SetStatus(string text)
    {
        statusText = text;
        statusBar.Invalidate();
    }

    /// <summary>Procura meta refresh em &lt;noscript&gt; ou no &lt;head&gt; (delay 0) e devolve a URL absoluta.</summary>
    private static string? UrlDeMetaRefresh(DomNode doc, Uri? baseUri)
    {
        string? Acha(DomNode n)
        {
            if (n.Type == NodeType.Element)
            {
                if (n.TagName == "noscript")
                {
                    string? r = MetaRefreshEm(n, baseUri);
                    if (r != null) return r;
                    // parser pode ter guardado o conteúdo como texto: cai para regex
                    var txt = n.CollectText();
                    var m = System.Text.RegularExpressions.Regex.Match(
                        txt, @"url\s*=\s*([^;""'>]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) return Absolutiza(m.Groups[1].Value.Trim(), baseUri);
                }
                else if (n.TagName == "meta" &&
                         string.Equals(n.GetAttr("http-equiv"), "refresh", StringComparison.OrdinalIgnoreCase) &&
                         n.GetAttr("content").StartsWith("0", StringComparison.OrdinalIgnoreCase))
                {
                    var u = ExtraiUrlRefresh(n.GetAttr("content"));
                    if (u != null) return Absolutiza(u, baseUri);
                }
            }
            foreach (var c in n.Children)
            {
                if (c.TagName == "noscript" || c.TagName == "head" || c.TagName == "body" || c.TagName == "#document")
                {
                    var r = Acha(c);
                    if (r != null) return r;
                }
            }
            return null;
        }
        return Acha(doc);
    }

    private static string? MetaRefreshEm(DomNode noscript, Uri? baseUri)
    {
        foreach (var c in noscript.Children)
        {
            if (c.Type == NodeType.Element && c.TagName == "meta" &&
                string.Equals(c.GetAttr("http-equiv"), "refresh", StringComparison.OrdinalIgnoreCase))
            {
                var u = ExtraiUrlRefresh(c.GetAttr("content"));
                if (u != null) return Absolutiza(u, baseUri);
            }
        }
        // conteúdo veio como texto dentro do próprio noscript
        var txt = noscript.CollectText();
        if (txt.IndexOf("http-equiv", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                txt, @"url\s*=\s*([^;""'>]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return Absolutiza(m.Groups[1].Value.Trim(), baseUri);
        }
        return null;
    }

    private static string? ExtraiUrlRefresh(string content)
    {
        if (content.Length == 0) return null;
        int i = content.IndexOf("url=", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var u = content.Substring(i + 4).Trim().Trim('"', '\'');
        return u.Length > 0 ? u : null;
    }

    private static string? Absolutiza(string u, Uri? baseUri)
    {
        var alvo = u.Replace("&amp;", "&");
        try { return baseUri != null ? new Uri(baseUri, alvo).AbsoluteUri : new Uri(alvo).AbsoluteUri; }
        catch { return alvo.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? alvo : null; }
    }

    private void RefreshChrome()
    {
        var tab = CurrentTab;
        Text = (string.IsNullOrEmpty(tab.Title) ? "Uranium Browser" : tab.Title + " — Uranium Browser");
        btnBack.Enabled = tab.CanGoBack;
        btnForward.Enabled = tab.CanGoForward;
        btnBack.ForeColor = tab.CanGoBack ? Color.FromArgb(226, 232, 240) : Color.FromArgb(71, 85, 105);
        btnForward.ForeColor = tab.CanGoForward ? Color.FromArgb(226, 232, 240) : Color.FromArgb(71, 85, 105);
        if (!txtUrl.Focused) txtUrl.Text = tab.Url;
        tabStrip.Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Tabs.Count > 0)
        {
            resizeTimer.Stop();
            resizeTimer.Start();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var tab = CurrentTab;
        switch (keyData)
        {
            case Keys.F5:
                Reload();
                return true;
            case Keys.Control | Keys.L:
                txtUrl.Focus();
                txtUrl.SelectAll();
                return true;
            case Keys.Alt | Keys.Left:
                GoBack();
                return true;
            case Keys.Alt | Keys.Right:
                GoForward();
                return true;
            case Keys.Control | Keys.T:
                NewTab();
                return true;
            case Keys.Control | Keys.W:
                CloseTab(ActiveTabIndex);
                return true;
            case Keys.Control | Keys.Tab:
                ActiveTabIndex = (ActiveTabIndex + 1) % Tabs.Count;
                RefreshChrome();
                canvas.Invalidate();
                return true;
            case Keys.Control | Keys.R:
                Reload();
                return true;
        }

        // rolagem por teclado quando o canvas tem foco
        if (canvas.Focused)
        {
            switch (keyData)
            {
                case Keys.Down: ScrollBy(80); return true;
                case Keys.Up: ScrollBy(-80); return true;
                case Keys.PageDown: ScrollBy(canvas.ClientSize.Height * 0.9f); return true;
                case Keys.PageUp: ScrollBy(-canvas.ClientSize.Height * 0.9f); return true;
                case Keys.Home: ScrollBy(int.MinValue + 1); return true;
                case Keys.End: ScrollBy(int.MaxValue - 1); return true;
                case Keys.Space:
                    if (CurrentTab.FocusedInput == null)
                    {
                        // YouYube-like: espaço pausa/retoma quando há vídeo na página
                        bool usedByVideo = TryToggleCurrentVideo();
                        if (!usedByVideo) ScrollBy(canvas.ClientSize.Height * 0.85f);
                    }
                    return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(wheelFilter);
        try { videos.Dispose(); } catch { }
        base.OnFormClosed(e);
    }
}

// ============================================================================
// Adapter: expõe o VideoManager do form para a JsEngine (IVideoJsHost)
// ============================================================================
internal sealed class VideoJsHost : IVideoJsHost
{
    private readonly VideoManager v;
    private readonly BrowserForm form;

    public VideoJsHost(BrowserForm form)
    {
        this.form = form;
        v = form.VideoHost;
    }

    public void Play(DomNode? n) { v.Play(n); form.KickVideoTimer(); }
    public void Pause(DomNode? n) { v.Pause(n); form.KickVideoTimer(); }
    public bool IsPlaying(DomNode? n) => v.IsPlaying(n);
    public bool IsPaused(DomNode? n) => v.IsPaused(n);
    public bool IsEnded(DomNode? n) => v.IsEnded(n);
    public bool IsMuted(DomNode? n) => v.IsMuted(n);
    public void SetMuted(DomNode? n, bool m) => v.SetMuted(n, m);
    public bool IsLoop(DomNode? n) => v.IsLoop(n);
    public void SetLoop(DomNode? n, bool l) => v.SetLoop(n, l);
    public double GetVolume(DomNode? n) => v.GetVolume(n);
    public void SetVolume(DomNode? n, double x) => v.SetVolume(n, x);
    public double GetTime(DomNode? n) => v.GetTime(n);
    public void SetTime(DomNode? n, double t) => v.SetTime(n, t);
    public double GetDuration(DomNode? n) => v.GetDuration(n);
    public (int w, int h) GetVideoSize(DomNode? n) => v.GetVideoSize(n);
    public void SetSource(DomNode n, string src) => form.AttachVideoSource(n, src, autoplay: false);
}

// ============================================================================
// Filtro global de roda do mouse (encaminha para a janela mesmo sem foco)
// ============================================================================
public class MouseWheelFilter : IMessageFilter
{
    private readonly BrowserForm form;
    public MouseWheelFilter(BrowserForm f) { form = f; }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == 0x20A) // WM_MOUSEWHEEL
        {
            form.ScrollBy(-((short)((long)m.WParam >> 16 & 0xFFFF)) * 1.0f);
            return false;
        }
        return false;
    }
}
