using LibVLCSharp.Shared;
using System.Collections.Concurrent;

namespace native_browser;

// ============================================================================
// <video>/<audio> com decodificação REAL (LibVLC 3): callbacks de quadro RV32
// alimentam um Bitmap que o Renderer desenha na peça de vídeo.
// - Um slot por nó <video>/<audio> (chaveado por DomNode).
// - Cada slot cria seus delegates (closure captura o slot: sem userData nativo).
// - Display roda na thread do VLC: copia o buffer pinado para o Bitmap com lock.
// ============================================================================
public sealed class VideoManager : IDisposable
{
    public sealed class Slot
    {
        public DomNode Node = null!;
        public string Src = "";
        public string Slave = "";               // faixa de áudio (VLC input-slave)
        public MediaPlayer? Player;
        public byte[] Buffer = Array.Empty<byte>();
        public System.Runtime.InteropServices.GCHandle Pin;
        public Bitmap? Frame;               // quadro publicado (nunca travado pelo VLC)
        public Bitmap? FrameBack;           // destino da escrita do VLC trocado por referência
        public readonly object Gate = new();
        public int W, H;
        public bool Autoplay, Loop, Muted, Controls = true;
        public bool Started;                // player criado e play() chamado
        public bool Ended;
        public long DurationMs;
        public long PositionMs;
        public double Volume = 1;
        public long Frames;                     // quadros recebidos (diag.)
    }

    public static VideoManager? Global;     // Renderer/layout leem por aqui

    private readonly LibVLC vlc;
    private readonly Dictionary<DomNode, Slot> slots = new();

    public Action? OnVideoEvent;            // invalidar canvas (chamado no Poll)

    public static void Log(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "uranium_video.log"),
                DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + msg + "\n");
        }
        catch { }
    }

    public VideoManager()
    {
        try
        {
            Core.Initialize();
            vlc = new LibVLC(
                "--avcodec-hw=none",        // decode por software: quadros caem em RAM via callbacks
                "--no-video-title-show",
                "--no-osd",
                "--quiet");
        }
        catch
        {
            vlc = null!;
        }
    }

    public bool Available => vlc != null;

    // ---- ciclo de vida ------------------------------------------------------

    public Slot? Find(DomNode? n) => n != null && slots.TryGetValue(n, out var s) ? s : null;

    public Slot EnsureSlot(DomNode node)
    {
        if (!slots.TryGetValue(node, out var s))
        {
            s = new Slot { Node = node };
            s.Pin = System.Runtime.InteropServices.GCHandle.Alloc(s.Buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            slots[node] = s;
        }
        return s;
    }

    /// <summary>Cria o MediaPlayer do slot com closures próprias. src deve ser absoluto.</summary>
    private bool StartPlayer(Slot s, string src, bool autoplay, string? slaveUrl = null)
    {
        if (vlc == null || string.IsNullOrWhiteSpace(src)) return false;
        try
        {
            if (!Uri.TryCreate(src, UriKind.Absolute, out var abs) ||
                (abs.Scheme != "http" && abs.Scheme != "https" && !abs.IsFile))
            {
                Log($"start: url inválida {src}");
                return false;
            }

            StopPlayer(s);

            var mp = new MediaPlayer(vlc);
            mp.EnableHardwareDecoding = false;
            mp.EnableMouseInput = false;
            mp.EnableKeyInput = false;

            // closures por slot: identificam o slot sem depender do opaque nativo
            mp.SetVideoFormatCallbacks(
                (ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines) =>
                    FormatFor(this, s, chroma, ref width, ref height, ref pitches, ref lines),
                (ref IntPtr opaque) => { });
            mp.SetVideoCallbacks(
                (IntPtr opaque, IntPtr planes) =>
                {
                    try { System.Runtime.InteropServices.Marshal.WriteIntPtr(planes, s.Pin.AddrOfPinnedObject()); } catch { }
                    return IntPtr.Zero;
                },
                (IntPtr opaque, IntPtr planes, IntPtr picture) => { },
                (IntPtr opaque, IntPtr picture) => DisplayFor(s));

            var media = new Media(vlc, abs.AbsoluteUri, FromType.FromLocation);
            media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            media.AddOption(":avcodec-hw=none");

            s.Src = src;
            s.Slave = slaveUrl ?? "";
            s.Player = mp;
            if (autoplay)
            {
                s.Started = true;
                if (mp.Play(media) && !string.IsNullOrEmpty(s.Slave))
                {
                    try { mp.AddSlave(MediaSlaveType.Audio, s.Slave, true); Log("start: slave de áudio anexado"); }
                    catch (Exception ex) { Log("start: slave EX " + ex.Message); }
                }
                Log($"start: play={autoplay} {abs.Scheme}://{abs.Host}{abs.AbsolutePath[..Math.Min(60, abs.AbsolutePath.Length)]}");
            }
            else
            {
                s.Started = false; // esperando o usuário clicar play
                mp.Dispose();
                s.Player = null;   // cria de novo no Play() — evita começar a tocar por conta própria
            }
            return true;
        }
        catch (Exception ex)
        {
            Log("start: EX " + ex.Message);
            return false;
        }
    }

    /// <summary>(Re)define a fonte e prepara o player. Se autoplay, começa a tocar.</summary>
    public bool Attach(DomNode node, string src, bool autoplay, bool loop, bool muted, double? volume = null, string? slaveUrl = null)
    {
        var s = EnsureSlot(node);
        s.Autoplay = autoplay;
        s.Loop = loop;
        s.Muted = muted;
        if (volume.HasValue) s.Volume = Math.Clamp(volume.Value, 0, 1);

        if (s.Player == null || s.Src != src)
            return StartPlayer(s, src, autoplay, slaveUrl);

        try { s.Player.Mute = muted; s.Player.Volume = (int)Math.Round(s.Volume * 100); } catch { }
        return true;
    }

    public void Play(DomNode? node)
    {
        if (node == null || !slots.TryGetValue(node, out var s)) return;
        try
        {
            if (s.Player == null)
            {
                if (s.Src.Length > 0) { if (StartPlayer(s, s.Src, autoplay: true, s.Slave)) KickSlave(s); }
                return;
            }
            var st = s.Player.State;
            if (st is VLCState.Ended or VLCState.Stopped or VLCState.NothingSpecial)
            {
                s.Ended = false;
                if (!s.Started) { s.Started = true; s.Player.Play(); }
                else s.Player.Play();
            }
            else
            {
                s.Started = true;
                s.Player.SetPause(false);
            }
        }
        catch { }
    }

    private void KickSlave(Slot s)
    {
        if (s.Player == null || string.IsNullOrEmpty(s.Slave)) return;
        try { s.Player.AddSlave(MediaSlaveType.Audio, s.Slave, true); Log("play: slave de áudio anexado"); }
        catch (Exception ex) { Log("play: slave EX " + ex.Message); }
    }

    public void Pause(DomNode? node)
    {
        if (node == null || !slots.TryGetValue(node, out var s) || s.Player == null) return;
        try { s.Player.SetPause(true); } catch { }
    }

    public bool IsPlaying(DomNode? node)
    {
        if (node == null || !slots.TryGetValue(node, out var s) || s.Player == null) return false;
        try { return s.Player.State is VLCState.Playing or VLCState.Buffering; } catch { return false; }
    }

    public bool IsPaused(DomNode? node)
    {
        if (node == null || !slots.TryGetValue(node, out var s) || s.Player == null) return true;
        try { return s.Player.State == VLCState.Paused; }
        catch { return true; }
    }

    public void SetTime(DomNode? node, double seconds)
    {
        if (node == null || !slots.TryGetValue(node, out var s) || s.Player == null) return;
        try { s.Player.Time = Math.Max(0, (long)(seconds * 1000)); s.Ended = false; } catch { }
    }

    public double GetTime(DomNode? node) => node != null && slots.TryGetValue(node, out var s) ? s.PositionMs / 1000.0 : 0;

    public double GetDuration(DomNode? node) => node != null && slots.TryGetValue(node, out var s) ? s.DurationMs / 1000.0 : 0;

    public bool IsEnded(DomNode? node) => node != null && slots.TryGetValue(node, out var s) && s.Ended;

    public bool IsMuted(DomNode? node) => node != null && slots.TryGetValue(node, out var s) && s.Muted;
    public bool IsLoop(DomNode? node) => node != null && slots.TryGetValue(node, out var s) && s.Loop;
    public double GetVolume(DomNode? node) => node != null && slots.TryGetValue(node, out var s) ? s.Volume : 1;
    public (int w, int h) GetVideoSize(DomNode? node) => node != null && slots.TryGetValue(node, out var s) ? (s.W, s.H) : (0, 0);

    public void SetLoop(DomNode? node, bool loop) { if (node != null && slots.TryGetValue(node, out var s)) s.Loop = loop; }
    public void SetMuted(DomNode? node, bool m) { if (node != null && slots.TryGetValue(node, out var s)) { s.Muted = m; if (s.Player != null) try { s.Player.Mute = m; } catch { } } }
    public void SetVolume(DomNode? node, double v) { if (node != null && slots.TryGetValue(node, out var s)) { s.Volume = Math.Clamp(v, 0, 1); if (s.Player != null) try { s.Player.Volume = (int)Math.Round(s.Volume * 100); } catch { } } }

    public Bitmap? GetFrame(DomNode? node) => node != null && slots.TryGetValue(node, out var s) ? s.Frame : null;

    /// <summary>Para o player de um nó específico (JS trocou src, por exemplo).</summary>
    public void StopNode(DomNode? node)
    {
        if (node == null || !slots.TryGetValue(node, out var s)) return;
        StopPlayer(s);
        s.Src = "";
        s.Slave = "";
    }

    /// <summary>Existe player em aberto na árvore (mantém o timer de pintura vivo).</summary>
    public bool HasActivePlayer(DomNode? document)
    {
        if (document == null || slots.Count == 0) return false;
        foreach (var kv in slots)
        {
            bool belongs = false;
            for (DomNode? e = kv.Key; e != null; e = e.Parent)
                if (ReferenceEquals(e, document)) { belongs = true; break; }
            if (!belongs) continue;
            var s = kv.Value;
            if (s.Player != null)
            {
                try
                {
                    var st = s.Player.State;
                    if (st is VLCState.Playing or VLCState.Buffering or VLCState.Opening or VLCState.Paused)
                        return true;
                }
                catch { }
            }
        }
        return false;
    }

    /// <summary>Para e descarta TUDO (nova página carregando).</summary>
    public void StopAll()
    {
        foreach (var kv in slots.ToList())
        {
            StopPlayer(kv.Value);
            if (kv.Value.Pin.IsAllocated) kv.Value.Pin.Free();
            try { kv.Value.Frame?.Dispose(); } catch { }
        }
        slots.Clear();
    }

    /// <summary>Remove slots cujos nós não pertencem mais ao documento atual.</summary>
    public void Prune(DomNode? document)
    {
        if (document == null) return;
        foreach (var kv in slots.ToList())
        {
            bool belongs = false;
            for (DomNode? e = kv.Key; e != null; e = e.Parent)
                if (ReferenceEquals(e, document)) { belongs = true; break; }
            if (!belongs)
            {
                StopPlayer(kv.Value);
                if (kv.Value.Pin.IsAllocated) kv.Value.Pin.Free();
                try { kv.Value.Frame?.Dispose(); } catch { }
                slots.Remove(kv.Key);
            }
        }
    }

    private void StopPlayer(Slot s)
    {
        try
        {
            if (s.Player != null)
            {
                s.Player.Stop();
                s.Player.Dispose();
                s.Player = null;
            }
        }
        catch { }
        s.Started = false;
        s.Ended = false;
    }

    /// <summary>Atualiza posição/duração/l TVs dos slots; chamado pelo timer da UI. true se algo está tocando.</summary>
    public bool Poll()
    {
        bool anyPlaying = false;
        Slot? endedSlot = null;
        foreach (var kv in slots)
        {
            var s = kv.Value;
            if (s.Player == null) continue;
            try
            {
                var st = s.Player.State;
                if (st == VLCState.Playing || st == VLCState.Buffering) anyPlaying = true;
                long len = s.Player.Length;
                long t = s.Player.Time;
                if (len > 0) s.DurationMs = len;
                if (t >= 0) s.PositionMs = t;
                bool ended = st == VLCState.Ended;
                if (ended && !s.Ended)
                {
                    s.Ended = true;
                    s.PositionMs = s.DurationMs;
                    endedSlot = s;
                }
            }
            catch { }
        }
        return anyPlaying;
    }

    public void Dispose()
    {
        foreach (var kv in slots) StopPlayer(kv.Value);
        slots.Clear();
        try { if (vlc != null) vlc.Dispose(); } catch { }
    }

    // ---- callbacks (thread do VLC) -------------------------------------------

    private static uint FormatFor(VideoManager self, Slot s, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(new byte[] { 82, 86, 51, 50 }, 0, chroma, 4); // "RV32"
            if (width == 0) width = 640;
            if (height == 0) height = 360;
            pitches = width * 4u;
            lines = height;
            lock (s.Gate)
            {
                s.W = (int)width; s.H = (int)height;
                s.Buffer = new byte[width * height * 4];
                if (s.Pin.IsAllocated) s.Pin.Free();
                s.Pin = System.Runtime.InteropServices.GCHandle.Alloc(s.Buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
                s.Frame?.Dispose();
                s.FrameBack?.Dispose();
                s.Frame = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                s.FrameBack = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            }
            Log($"format cb: {width}x{height}");
            return 1u;
        }
        catch (Exception ex)
        {
            Log("format cb: EX " + ex.Message);
            return 0u;
        }
    }

    private static void DisplayFor(Slot s)
    {
        s.Frames++;
        if (s.Frames == 1 || s.Frames % 90 == 0)
            Log($"display: quadro {s.Frames} ({s.W}x{s.H})");
        try
        {
            lock (s.Gate)
            {
                var back = s.FrameBack;
                if (back == null || back.Width != s.W || back.Height != s.H) return;
                var d = back.LockBits(new Rectangle(0, 0, back.Width, back.Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(s.Buffer, 0, d.Scan0, Math.Min(s.Buffer.Length, d.Stride * d.Height));
                }
                finally
                {
                    back.UnlockBits(d);
                }
                // publica: quem desenha usa a referência publicada (sem LockBits estourando)
                var pub = s.Frame;
                s.Frame = back;
                s.FrameBack = pub;
            }
        }
        catch { }
    }
}

