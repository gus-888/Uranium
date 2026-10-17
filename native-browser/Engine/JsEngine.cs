using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Jint.Runtime;
using System.Text;
using System.Globalization;
using System.Collections.Concurrent;
using System.Text.Json;

namespace native_browser;


public interface IVideoJsHost
{
	void Play(DomNode? n);

	void Pause(DomNode? n);

	bool IsPlaying(DomNode? n);

	bool IsPaused(DomNode? n);

	bool IsEnded(DomNode? n);

	bool IsMuted(DomNode? n);

	void SetMuted(DomNode? n, bool m);

	bool IsLoop(DomNode? n);

	void SetLoop(DomNode? n, bool l);

	double GetVolume(DomNode? n);

	void SetVolume(DomNode? n, double v);

	double GetTime(DomNode? n);

	void SetTime(DomNode? n, double t);

	double GetDuration(DomNode? n);

	(int w, int h) GetVideoSize(DomNode? n);

	void SetSource(DomNode n, string src);
}
public sealed class JsNetRequest
{
	public string Url = "";

	public string Method = "GET";

	public string Body = "";

	public string HeadersJson = "{}";

	public JsValue Callback = JsValue.Undefined;
}

public sealed class JsEngine
{
	private enum Role
	{
		Document,
		Element,
		Style,
		ClassList,
		Location
	}

	private sealed class Binding
	{
		public Role Role;

		public DomNode Node = null;
	}

	private sealed class MutObserver
	{
		public JsValue Callback = JsValue.Undefined;

		public JsValue Proxy = JsValue.Undefined;

		public DomNode? Target;

		public bool ChildList;

		public bool Attributes;

		public bool Subtree;
	}

	private sealed class PendingMut
	{
		public string Type = "";

		public DomNode Node = null;

		public string? Attr;

		public List<DomNode> Added = new List<DomNode>();

		public List<DomNode> Chain = new List<DomNode>();
	}

	private readonly Engine engine;

	private readonly DomNode document;

	private readonly StyleResolver resolver;

	private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();

	private readonly Dictionary<int, JsValue> proxyCache = new Dictionary<int, JsValue>();

	private readonly Dictionary<DomNode, int> elementIds = new Dictionary<DomNode, int>();

	private readonly Dictionary<DomNode, List<JsValue>> clickHandlers = new Dictionary<DomNode, List<JsValue>>();

	private readonly Dictionary<DomNode, List<(string type, JsValue fn)>> listeners = new Dictionary<DomNode, List<(string, JsValue)>>();

	private readonly List<(int id, DateTime at, int? everyMs, JsValue fn)> timers = new List<(int, DateTime, int?, JsValue)>();

	private readonly List<JsValue> dclHandlers = new List<JsValue>();

	private readonly List<MutObserver> mutObservers = new List<MutObserver>();

	private readonly List<PendingMut> pendingMuts = new List<PendingMut>();

	private int nextId = 1;

	private int nextTimerId = 1;

	private JsValue proxyMaker = JsValue.Undefined;

	public bool Dirty;

	public string CurrentUrl = "";

	public Action<string>? OnNavigate;

	public Action<string>? OnLog;

	public Action<float, float>? OnScrollTo;

	public Func<DomNode, bool>? IsImageReady;

	public Func<DomNode, bool>? IsImageFailed;

	public Action<DomNode>? OnImgSrcSet;

	public Action<DomNode>? OnFocusRequest;

	public Action<DomNode>? OnBlurRequest;

	public Func<DomNode?>? GetFocused;

	public Func<(int w, int h)>? GetViewport;

	public Func<float>? GetScroll;

	public Func<DomNode, RectangleF?>? MeasureBox;

	public Action<DomNode>? OnScriptInserted;

	public IVideoJsHost? Videos;

	public Action<DomNode>? OnVideoSrcSet;

	public Action<JsNetRequest>? OnNetRequest;

	private readonly ConcurrentQueue<(JsValue cb, bool ok, int status, string headersJson, string bodyText)> netDone = new ConcurrentQueue<(JsValue, bool, int, string, string)>();

	private readonly List<(DomNode node, JsValue? onload, JsValue? onerror)> imgHandlers = new List<(DomNode, JsValue, JsValue)>();

	public static int JsTimeoutSeconds = 15;

	public static long JsMaxStatements = 50000000L;

	public int MaxScriptChars = 24000000;

	private long jsBytesRun;

	public long MaxTotalChars = 120000000L;

	private bool _iteradorPacificado;

	private readonly object _orfasLock = new object();

	private readonly List<DomNode> _orfas = new List<DomNode>();

	private int _ultimoImportShady;

	private int _watchTentativas;

	private bool _watchRenderizado;

	/// <summary>URL do stream VR entregue pelo form (race-free com o boot).</summary>
	public volatile string? VrStreamSeedUrl;

	/// <summary>Força o re-render do fallback watch no próximo heartbeat.</summary>
	public void InvalidateWatchFallback()
	{
		_watchRenderizado = false;
	}

	private int _itensLista;

	private bool _decifraProcurado;

	private bool _watchEraWatch;

	private int _tickDiag;

	private readonly Dictionary<string, JsValue> customElementDefs = new Dictionary<string, JsValue>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<int, Dictionary<string, JsValue>> expandos = new Dictionary<int, Dictionary<string, JsValue>>();

	private int _importados;

	private readonly List<DomNode> _importadosLista = new List<DomNode>();

	private readonly List<(string, JsValue)> windowListeners = new List<(string, JsValue)>();

	public void EnqueueNetResult(JsValue cb, bool ok, int status, string headersJson, string bodyText)
	{
		netDone.Enqueue((cb, ok, status, headersJson, bodyText));
	}

	public JsEngine(DomNode document, StyleResolver resolver)
	{
		this.document = document;
		this.resolver = resolver;
		VideoManager.Log("engine: ctor início");
		try
		{
			engine = new Engine((Options o) =>
			{
				o.TimeoutInterval(TimeSpan.FromSeconds(JsTimeoutSeconds));
				o.MaxStatements((int)Math.Min(2147483647L, JsMaxStatements));
			});
			VideoManager.Log("engine: motor criado");
			ExposeGlobals();
			VideoManager.Log("engine: globals expostos");
		}
		catch (Exception ex)
		{
			VideoManager.Log("engine-ctor-erro: " + ex.Message);
			throw;
		}
	}

	private void DrainJobs()
	{
		try
		{
			engine.Evaluate("0;");
		}
		catch
		{
		}
		try
		{
			JsValue jsValue = engine.Evaluate("(typeof Promise !== 'undefined' && Promise.resolve) ? Promise.resolve().then(function(){}) : null");
			if (jsValue != null && !jsValue.IsNull() && !jsValue.IsUndefined())
			{
				engine.Advanced.ProcessTasks();
			}
		}
		catch
		{
		}
	}

	private void PacificarIterador()
	{
		if (_iteradorPacificado)
		{
			return;
		}
		try
		{
			JsValue value = engine.Evaluate("typeof globalThis._ !== 'undefined' && globalThis._ !== null && typeof _.L === 'function' && !_.L.__tol");
			if (value.IsBoolean() && value.AsBoolean())
			{
				engine.Evaluate("(function(){\n  var velha = _.L;\n  var vazio = function () { return { next: function(){ return { done: true, value: undefined }; } }; };\n  var nova = function (h) {\n    try { return velha(h); }\n    catch (e1) {\n      if (h === undefined || h === null || typeof h === 'symbol' || typeof h === 'boolean') return vazio();\n      throw e1;\n    }\n  };\n  nova.__tol = true;\n  _.L = nova;\n})()");
				_iteradorPacificado = true;
				OnLog?.Invoke("iterador _L tolerado");
			}
		}
		catch
		{
		}
	}

	public JsValue Eval(string code)
	{
		try
		{
			JsValue result = engine.Evaluate(code);
			DrainJobs();
			return result;
		}
		catch (Exception ex)
		{
			string text = ex.Message.Replace("\r", "").Split('\n').FirstOrDefault((string l) => l.Trim().Length > 0) ?? ex.GetType().Name;
			string text2 = "";
			try
			{
				if (ex is JavaScriptException ex2)
				{
					_ = ref ex2.Location;
					if (true)
					{
						text2 = $" @ {ex2.Location.Start.Line}:{ex2.Location.Start.Column}";
					}
				}
			}
			catch
			{
			}
			OnLog?.Invoke("JS eval: " + text + text2);
			return JsValue.Undefined;
		}
	}

	public void Execute(IEnumerable<(string code, string name)> scripts)
	{
		int num = 0;
		int num2 = 0;
		foreach (var (text, value) in scripts)
		{
			if (text.Length == 0)
			{
				continue;
			}
			if (text.Length > MaxScriptChars)
			{
				num++;
				continue;
			}
			if (jsBytesRun + text.Length > MaxTotalChars)
			{
				num2++;
				continue;
			}
			jsBytesRun += text.Length;
			try
			{
				engine.Evaluate(text);
			}
			catch (Exception ex)
			{
				string value2 = "";
				try
				{
					if (ex is JavaScriptException ex2)
					{
						_ = ref ex2.Location;
						if (true)
						{
							value2 = $" @ linha {ex2.Location.Start.Line} col {ex2.Location.Start.Column}";
						}
					}
				}
				catch
				{
				}
				string text2 = ex.Message.Split('\n')[0];
				if (text2.Contains("UnwrapIfPromise", StringComparison.Ordinal))
				{
					OnLog?.Invoke($"JS {value}: (drenagem de promise: {text2})");
				}
				else
				{
					OnLog?.Invoke($"JS {value}: {text2}{value2}");
					try
					{
						string text3 = ex.ToString().Replace("\r", "");
						IEnumerable<string> enumerable = (from l in text3.Split('\n')
							where l.Trim().Length > 0
							select l).Take(14);
						foreach (string item in enumerable)
						{
							OnLog?.Invoke("  stk: " + item.Trim());
						}
					}
					catch
					{
					}
				}
			}
			DrainJobs();
			PacificarIterador();
		}
		if (num > 0)
		{
			OnLog?.Invoke($"JS: {num} script(s) acima de {MaxScriptChars} chars ignorado(s)");
		}
		if (num2 > 0)
		{
			OnLog?.Invoke($"JS: {num2} script(s) acima do teto total de {MaxTotalChars} chars ignorado(s)");
		}
		try
		{
			JsValue value3 = engine.GetValue("onload");
			if (value3 is Function)
			{
				dclHandlers.Add(value3);
			}
		}
		catch
		{
		}
		foreach (JsValue item2 in dclHandlers.ToList())
		{
			CallFn(item2, JsValue.Undefined, Array.Empty<JsValue>());
		}
		dclHandlers.Clear();
		ProcessTimers();
	}

	public bool FireImageEvents()
	{
		if (imgHandlers.Count == 0)
		{
			return false;
		}
		bool result = false;
		foreach (var item3 in imgHandlers.ToList())
		{
			DomNode node = item3.node;
			JsValue item = item3.onload;
			JsValue item2 = item3.onerror;
			Func<DomNode, bool>? isImageReady = IsImageReady;
			if (isImageReady == null || !isImageReady(node))
			{
				Func<DomNode, bool>? isImageFailed = IsImageFailed;
				if (isImageFailed == null || !isImageFailed(node))
				{
					continue;
				}
			}
			imgHandlers.RemoveAll(((DomNode node, JsValue? onload, JsValue? onerror) x) => x.node == node);
			try
			{
				Func<DomNode, bool>? isImageFailed2 = IsImageFailed;
				if (isImageFailed2 != null && isImageFailed2(node))
				{
					if (item2 != null)
					{
						CallFn(item2, Wrap(node), Array.Empty<JsValue>());
					}
				}
				else if (item != null)
				{
					CallFn(item, Wrap(node), Array.Empty<JsValue>());
				}
				result = true;
			}
			catch
			{
			}
		}
		return result;
	}

	private static bool IsTruthy(JsValue v)
	{
		if (v.IsNull() || v.IsUndefined())
		{
			return false;
		}
		if (v is JsBoolean)
		{
			return v.AsBoolean();
		}
		if (v.IsNumber())
		{
			return v.AsNumber() != 0.0;
		}
		if (v.IsString())
		{
			return v.AsString().Length > 0;
		}
		return true;
	}

	private void RecordMut(string type, DomNode node, string? attr = null, List<DomNode>? added = null)
	{
		if (mutObservers.Count != 0)
		{
			List<DomNode> list = new List<DomNode>();
			for (DomNode domNode = node; domNode != null; domNode = domNode.Parent)
			{
				list.Add(domNode);
			}
			pendingMuts.Add(new PendingMut
			{
				Type = type,
				Node = node,
				Attr = attr,
				Added = (added ?? new List<DomNode>()),
				Chain = list
			});
		}
	}

	private bool FlushMutations()
	{
		if (pendingMuts.Count == 0 || mutObservers.Count == 0)
		{
			return false;
		}
		bool result = false;
		foreach (MutObserver item in mutObservers.ToList())
		{
			if (item.Target == null || item.Callback.IsNull() || item.Callback.IsUndefined())
			{
				continue;
			}
			List<JsValue> list = new List<JsValue>();
			foreach (PendingMut pendingMut in pendingMuts)
			{
				bool flag = false;
				if (pendingMut.Type == "attributes" && item.Attributes)
				{
					flag = pendingMut.Node == item.Target || (item.Subtree && pendingMut.Chain.Contains(item.Target));
				}
				else if (pendingMut.Type == "childList" && item.ChildList)
				{
					flag = pendingMut.Node == item.Target || (item.Subtree && pendingMut.Chain.Contains(item.Target));
				}
				if (!flag)
				{
					continue;
				}
				ObjectInstance objectInstance = (ObjectInstance)engine.Evaluate("({})");
				objectInstance.FastSetDataProperty("type", pendingMut.Type);
				objectInstance.FastSetDataProperty("target", Wrap(pendingMut.Node));
				if (pendingMut.Attr != null)
				{
					objectInstance.FastSetDataProperty("attributeName", pendingMut.Attr);
				}
				List<JsValue> list2 = new List<JsValue>();
				foreach (DomNode item2 in pendingMut.Added)
				{
					list2.Add(Wrap(item2));
				}
				objectInstance.FastSetDataProperty("addedNodes", new JsArray(engine, list2.ToArray()));
				list.Add(objectInstance);
			}
			if (list.Count > 0)
			{
				try
				{
					CallFn(item.Callback, item.Proxy, new JsValue[2]
					{
						new JsArray(engine, list.ToArray()),
						item.Proxy
					});
					result = true;
				}
				catch
				{
				}
			}
		}
		pendingMuts.Clear();
		return result;
	}

	private static bool IsDescendant(DomNode ancestor, DomNode n)
	{
		for (DomNode parent = n.Parent; parent != null; parent = parent.Parent)
		{
			if (parent == ancestor)
			{
				return true;
			}
		}
		return false;
	}

	public bool ProcessTimers()
	{
		bool flag = FlushMutations();
		bool flag2 = false;
		(JsValue, bool, int, string, string) result;
		while (netDone.TryDequeue(out result))
		{
			flag2 = true;
			CallFn(result.Item1, JsValue.Undefined, new JsValue[4] { result.Item2, result.Item3, result.Item4, result.Item5 });
		}
		if (flag2)
		{
			flag = true;
			DrainJobs();
		}
		if (timers.Count == 0)
		{
			ImportarShadySeHouver();
			return flag;
		}
		DateTime now = DateTime.UtcNow;
		bool flag3 = false;
		foreach (var item2 in timers.Where(((int id, DateTime at, int? everyMs, JsValue fn) t) => t.at <= now).ToList())
		{
			timers.Remove(item2);
			try
			{
				CallFn(item2.fn, JsValue.Undefined, Array.Empty<JsValue>());
				flag3 = true;
			}
			catch
			{
			}
			int? item = item2.everyMs;
			if (item.HasValue)
			{
				int valueOrDefault = item.GetValueOrDefault();
				if (timers.Count < 200)
				{
					timers.Add((item2.id, now.AddMilliseconds(Math.Max(10, valueOrDefault)), valueOrDefault, item2.fn));
				}
			}
		}
		bool flag4 = flag3 || FlushMutations();
		ImportarShadySeHouver();
		return flag4 | flag;
	}

	private void RenderizarWatchFallback()
	{
		if (_watchRenderizado || _watchTentativas > 8)
		{
			return;
		}
		try
		{
			if (!_watchEraWatch)
			{
				JsValue value = engine.Evaluate("(function(){try{ if(!location || !location.pathname) return ''; var p=location.pathname; return (p==='/watch'||p==='/'||p==='/results') ? p : ''; }catch(e){ return ''; }})()");
				if (!value.IsString() || value.AsString().Length == 0)
				{
					_watchTentativas++;
					return;
				}
				if (value.AsString() != "/watch")
				{
					RenderizarListaFallback(value.AsString());
					_watchRenderizado = true;
					return;
				}
				_watchEraWatch = true;
			}
			JsValue value2 = engine.Evaluate("(!!globalThis.ytInitialData)");
			if (!value2.IsBoolean() || !value2.AsBoolean())
			{
				_watchTentativas++;
				return;
			}
			JsValue value3 = engine.Evaluate("globalThis.__watchExtrai ? globalThis.__watchExtrai() : 'null'");
			if (!value3.IsString())
			{
				_watchTentativas++;
				return;
			}
			string text = value3.AsString();
			if (text == "null" || text.StartsWith("ERRO:"))
			{
				_watchTentativas++;
				return;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(text);
			JsonElement rootElement = jsonDocument.RootElement;
			string text2 = PegaStr(rootElement, new string[1] { "titulo" });
			if (text2.Length == 0)
			{
				_watchTentativas++;
				return;
			}
			_watchRenderizado = true;
			// TUDO no BODY (light DOM): o SR do pm é substituído pelo boot real
			// do YouTube ~8s depois (attachShadow de novo) e leva o conteúdo —
			// o body sobrevive a esse boot e o layout o lê de forma estável
			DomNode bNode = null;
			void AchaBodyNow(DomNode n)
			{
				if (bNode != null) return;
				if (n.Type == NodeType.Element && n.TagName == "body") { bNode = n; return; }
				foreach (DomNode c in n.Children) AchaBodyNow(c);
				if (bNode == null && n.ShadowRoot != null) AchaBodyNow(n.ShadowRoot);
			}
			AchaBodyNow(document);
			DomNode alvoCont = bNode ?? document;
			// re-render (auto-heal): remove a montagem anterior p/ não duplicar
			try
			{
				for (int k = alvoCont.Children.Count - 1; k >= 0; k--)
				{
					var antigo = alvoCont.Children[k];
					var cl = antigo.GetAttr("class") ?? "";
					if (cl.Contains("ufb-root") || cl.Contains("html5-video-holder"))
					{
						alvoCont.Children.RemoveAt(k);
						antigo.Parent = null;
					}
				}
			}
			catch { }
			DomNode domNode = El("div", "ufb-root style-scope ytd-page-manager", "display:flex;justify-content:center;max-width:1281px;margin:0 auto;padding:24px 24px 400px;min-width:0;", alvoCont);
			DomNode pai = El("div", "style-scope ytd-watch-flexy", "min-width:0;max-width:1280px;flex:1 1 0%;", domNode);
			DomNode pai2 = El("div", "style-scope ytd-watch-flexy", "width:402px;flex:none;margin-left:24px;", domNode);
			string text3 = PegaStr(rootElement, new string[1] { "videoUrl" });
			if (text3.Length == 0 && !string.IsNullOrEmpty(VrStreamSeedUrl))
			{
				// semente direta C# (race-free com o boot dos scripts)
				text3 = VrStreamSeedUrl;
			}
			if (text3.Length == 0)
			{
				// stream real entregue pelo form via Innertube ANDROID_VR
				try
				{
					JsValue vru = engine.Evaluate("globalThis.__vrStreamUrl || ''");
					if (vru.IsString()) text3 = vru.AsString();
				}
				catch { }
			}
			try { OnLog?.Invoke("watch-fallback: montado videoUrl-len=" + (text3.Length > 0 ? text3.Length.ToString() : "0") + " rel=" + (rootElement.TryGetProperty("relacionados", out var relchk) && relchk.ValueKind == JsonValueKind.Array ? relchk.GetArrayLength() : 0)); } catch { }
			string text4 = PegaStr(rootElement, new string[1] { "poster" });
			DomNode pai3 = El("div", "style-scope ytd-watch-flexy", "width:100%;background:#000;border-radius:12px;overflow:hidden;position:relative;", pai);
			if (text3.Length > 0)
			{
				// SEM a classe .html5-main-video (o CSS deles força position:abs
				// e o caminho positioned não cria peça) o <video> vira peça normal
				DomNode domNode2 = El("video", "", "display:block;width:100%;height:493px;background:#000;position:static;margin:0 auto;", pai3);
				domNode2.Attributes["src"] = text3;
				domNode2.Attributes["controls"] = "";
				domNode2.Attributes["autoplay"] = "";
				domNode2.Attributes["height"] = "493";
				if (text4.Length > 0)
				{
					domNode2.Attributes["poster"] = text4;
				}
			}
			else
			{
				if (text4.Length > 0)
				{
					Img(text4, "100%", "", "", pai3);
				}
				DomNode pai4 = El("div", "style-scope ytd-watch-flexy", "position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);width:68px;height:48px;background:rgba(33,33,33,0.8);border-radius:12px;text-align:center;line-height:48px;font-size:22px;color:#fff;", pai3);
				Tx("▶", pai4);
			}
			DomNode pai5 = El("h1", "smartimation title style-scope ytd-watch-metadata", "color:#f1f1f1;font-size:20px;line-height:28px;font-weight:700;margin:12px 0 6px;", pai);
			Tx(text2, pai5);
			string text5 = PegaStr(rootElement, new string[1] { "dataTxt" });
			string text6 = PegaStr(rootElement, new string[1] { "views" });
			if (text5.Length > 0 || text6.Length > 0)
			{
				DomNode pai6 = El("div", "style-scope ytd-watch-info-text", "color:#aaa;font-size:14px;margin:0 0 10px;", pai);
				Tx(string.Join(" • ", new string[2] { text6, text5 }.Where((string s) => s.Length > 0)), pai6);
			}
			string text7 = PegaStr(rootElement, new string[1] { "canal" });
			if (text7.Length > 0)
			{
				DomNode pai7 = El("div", "owner style-scope ytd-watch-metadata", "display:flex;align-items:center;margin:10px 0;", pai);
				string text8 = PegaStr(rootElement, new string[1] { "avatar" });
				if (text8.Length > 0)
				{
					Img(text8, "40px", "40px", "border-radius:50%;", pai7);
				}
				DomNode pai8 = El("div", "style-scope ytd-watch-metadata", "margin-left:12px;", pai7);
				DomNode pai9 = El("div", "style-scope ytd-channel-name", "color:#f1f1f1;font-size:16px;font-weight:500;", pai8);
				Tx(text7, pai9);
				string text9 = PegaStr(rootElement, new string[1] { "inscritos" });
				if (text9.Length > 0)
				{
					DomNode pai10 = El("div", "style-scope ytd-watch-metadata", "color:#aaa;font-size:12px;margin-top:2px;", pai8);
					Tx(text9, pai10);
				}
				DomNode pai11 = El("div", "style-scope ytd-subscribe-button", "margin-left:auto;background:#f1f1f1;color:#0f0f0f;border-radius:18px;padding:10px 16px;font-size:14px;font-weight:500;", pai7);
				Tx("Inscrever-se", pai11);
			}
			string text10 = PegaStr(rootElement, new string[1] { "desc" });
			if (text10.Length > 0)
			{
				DomNode pai12 = El("div", "description style-scope ytd-watch-metadata", "background:#272727;border-radius:12px;padding:12px;color:#f1f1f1;font-size:14px;line-height:20px;margin-top:8px;", pai);
				Tx(text10, pai12);
			}
			if (rootElement.TryGetProperty("relacionados", out var value4) && value4.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value4.EnumerateArray())
				{
					string text11 = PegaStr(item, new string[1] { "id" });
					string text12 = PegaStr(item, new string[1] { "t" });
					if (text12.Length != 0)
					{
						DomNode domNode3 = El("a", "style-scope ytd-compact-video-renderer", "display:flex;margin-bottom:8px;text-decoration:none;", pai2);
						domNode3.Attributes["href"] = ((text11.Length > 0) ? ("/watch?v=" + text11) : "#");
						DomNode pai13 = El("div", "style-scope ytd-compact-video-renderer", "position:relative;width:168px;flex:none;", domNode3);
						string text13 = PegaStr(item, new string[1] { "thumb" });
						if (text13.Length > 0)
						{
							Img(text13, "168px", "94px", "", pai13);
						}
						string text14 = PegaStr(item, new string[1] { "dur" });
						if (text14.Length > 0)
						{
							DomNode pai14 = El("span", "badge style-scope ytd-compact-video-renderer", "position:absolute;right:4px;bottom:4px;background:rgba(0,0,0,0.8);color:#fff;font-size:12px;padding:1px 4px;border-radius:4px;", pai13);
							Tx(text14, pai14);
						}
						DomNode pai15 = El("div", "details style-scope ytd-compact-video-renderer", "margin-left:8px;min-width:0;", domNode3);
						DomNode pai16 = El("div", "video-title style-scope ytd-compact-video-renderer", "color:#f1f1f1;font-size:14px;line-height:20px;font-weight:500;", pai15);
						Tx(text12, pai16);
						DomNode pai17 = El("div", "byline style-scope ytd-compact-video-renderer", "color:#aaa;font-size:12px;margin-top:4px;", pai15);
						Tx(PegaStr(item, new string[1] { "canal" }), pai17);
					}
				}
			}
			Dirty = true;
			int num = 0;
			if (rootElement.TryGetProperty("relacionados", out var value5) && value5.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item2 in value5.EnumerateArray())
				{
					num++;
				}
			}
			string text15 = "";
			if (rootElement.TryGetProperty("relacionados", out var value6) && value6.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item3 in value6.EnumerateArray())
				{
					string text16 = PegaStr(item3, new string[1] { "thumb" });
					if (text16.Length > 0)
					{
						text15 = text16;
						break;
					}
				}
			}
			OnLog?.Invoke($"watch-fallback: montado (dados reais) rel={num} thumb0={text15.Substring(0, Math.Min(90, text15.Length))} poster={text4.Length} video={text3.Length}");
			DomNode El(string tag, string cls, string style, DomNode? domNode5)
			{
				DomNode domNode4 = new DomNode(NodeType.Element)
				{
					TagName = tag
				};
				if (cls.Length > 0)
				{
					domNode4.Attributes["class"] = cls;
				}
				if (style.Length > 0)
				{
					domNode4.Attributes["style"] = style;
				}
				domNode5?.Children.Add(domNode4);
			domNode4.Parent = domNode5 ?? document;
			return domNode4;
			}
			DomNode Img(string src, string w, string h, string estiloExtra, DomNode pai18)
			{
				DomNode domNode4 = El("img", "", $"width:{w};height:{h};object-fit:cover;border-radius:8px;{estiloExtra}", pai18);
				domNode4.Attributes["src"] = src;
				return domNode4;
			}
		}
		catch (Exception ex)
		{
			OnLog?.Invoke("watch-fallback: falhou " + ex.Message);
			_watchRenderizado = true;
		}
		static string PegaStr(JsonElement e, params string[] caminho)
		{
			JsonElement jsonElement = e;
			foreach (string propertyName in caminho)
			{
				if (jsonElement.ValueKind != JsonValueKind.Object)
				{
					return "";
				}
				if (!jsonElement.TryGetProperty(propertyName, out var value7))
				{
					return "";
				}
				jsonElement = value7;
			}
			return (jsonElement.ValueKind == JsonValueKind.String) ? (jsonElement.GetString() ?? "") : "";
		}
		static DomNode Tx(string texto, DomNode domNode5)
		{
			DomNode domNode4 = new DomNode(NodeType.Text)
			{
				TextContent = texto
			};
			domNode4.Parent = domNode5;
			domNode5.Children.Add(domNode4);
			return domNode4;
		}
	}

	private void RenderizarListaFallback(string rota)
	{
		try
		{
			JsValue value = engine.Evaluate("globalThis.__ytExtraiLista ? globalThis.__ytExtraiLista() : '[]'");
			if (!value.IsString())
			{
				return;
			}
			string text = value.AsString();
			using JsonDocument jsonDocument = JsonDocument.Parse((text.Length < 4) ? "[]" : text);
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
			{
				return;
			}
			JsonElement rootElement = jsonDocument.RootElement;
			int num = 0;
			foreach (JsonElement item in rootElement.EnumerateArray())
			{
				num++;
			}
			bool flag = rota == "/";
			if (num == 0 && !flag)
			{
				return;
			}
			DomNode alvo = document.FindTag("body") ?? document;
			if (alvo == null)
			{
				return;
			}
			DomNode domNode = El("div", "style-scope ytd-page-manager", flag ? "display:flex;flex-direction:column;align-items:center;max-width:1096px;margin:0 auto;padding:24px 24px 400px;min-width:0;" : "display:flex;flex-direction:column;align-items:center;max-width:1096px;margin:0 auto;padding:24px 24px 400px;min-width:0;", null);
			domNode.Parent = alvo;
			alvo.Children.Add(domNode);
			if (flag && num == 0)
			{
				DomNode pai = El("div", "style-scope ytd-feed-nudge-renderer", "max-width:560px;margin-top:60px;background:#272727;border-radius:12px;padding:24px;", domNode);
				DomNode pai2 = El("div", "style-scope ytd-feed-nudge-renderer", "color:#f1f1f1;font-size:16px;font-weight:600;margin-bottom:12px;", pai);
				Tx("Experimente pesquisar um vídeo para assistir", pai2);
				DomNode domNode2 = El("form", "style-scope ytd-searchbox", "display:flex;width:100%;", pai);
				domNode2.Attributes["action"] = "/results";
				domNode2.Attributes["method"] = "get";
				DomNode domNode3 = El("input", "style-scope ytd-searchbox", "flex:1;background:#121212;border:1px solid #303030;border-right:none;border-radius:40px 0 0 40px;padding:10px 16px;color:#f1f1f1;font-size:16px;outline:none;", domNode2);
				domNode3.Attributes["type"] = "search";
				domNode3.Attributes["name"] = "search_query";
				domNode3.Attributes["placeholder"] = "Pesquisar";
				DomNode domNode4 = El("button", "style-scope ytd-searchbox", "background:#222;border:1px solid #303030;border-radius:0 40px 40px 0;padding:10px 20px;color:#f1f1f1;cursor:pointer;", domNode2);
				domNode4.Attributes["type"] = "submit";
				Tx("Buscar", domNode4);
				DomNode pai3 = El("div", "style-scope ytd-page-manager", "display:flex;gap:16px;margin-top:40px;flex-wrap:wrap;justify-content:center;", domNode);
				string[][] array = new string[2][]
				{
					new string[2] { "Rick Astley", "PLbpi6ZahtOH6BJ8jlNpfgsS5bCTRzZ3GX" },
					new string[2] { "Musicas Pop", "RDMM" }
				};
				string[][] array2 = array;
				foreach (string[] array3 in array2)
				{
					DomNode domNode5 = El("a", "style-scope ytd-page-manager", "color:#3ea6ff;font-size:14px;text-decoration:none;background:#272727;border-radius:8px;padding:10px 14px;", pai3);
					domNode5.Attributes["href"] = "/playlist?list=" + array3[1];
					Tx("▶ " + array3[0], domNode5);
				}
				Dirty = true;
				if (_itensLista == 0)
				{
					_itensLista = -1;
					OnLog?.Invoke("lista-fallback: home nudge (sessão anônima sem grade) + busca GET");
				}
				return;
			}
			if (flag)
			{
				DomNode pai4 = El("div", "style-scope ytd-feed-filter-chip-bar-renderer", "display:flex;gap:12px;margin-bottom:16px;", domNode);
				string[] array4 = new string[6] { "Tudo", "Música", "Games", "Ao vivo", "Comunidades", "Podcasts" };
				for (int j = 0; j < array4.Length; j++)
				{
					bool flag2 = j == 0;
					DomNode pai5 = El("div", "style-scope ytd-feed-filter-chip-bar-renderer", $"background:{(flag2 ? "#f1f1f1" : "#272727")};color:{(flag2 ? "#0f0f0f" : "#f1f1f1")};border-radius:8px;padding:8px 12px;font-size:14px;white-space:nowrap;", pai4);
					Tx(array4[j], pai5);
				}
			}
			else
			{
				DomNode pai6 = El("div", "style-scope ytd-search", "margin-bottom:16px;border-bottom:1px solid #272727;padding-bottom:8px;color:#aaa;font-size:13px;", domNode);
				Tx($"Aproximadamente {num} resultados (dados reais de produção)", pai6);
			}
			foreach (JsonElement item2 in rootElement.EnumerateArray())
			{
				string text2 = PegaStr(item2, new string[1] { "id" });
				string text3 = PegaStr(item2, new string[1] { "t" });
				if (text3.Length == 0)
				{
					continue;
				}
				DomNode domNode6 = El("a", "style-scope ytd-video-renderer", flag ? "display:flex;flex-direction:column;text-decoration:none;margin-bottom:16px;width:100%;overflow:hidden;" : "display:flex;margin-bottom:12px;text-decoration:none;overflow:hidden;", domNode);
				if (text2.Length > 0)
				{
					domNode6.Attributes["href"] = "/watch?v=" + text2;
				}
				if (flag)
				{
					DomNode pai7 = El("div", "style-scope ytd-rich-grid-media", "width:100%;position:relative;", domNode6);
					string text4 = PegaStr(item2, new string[1] { "thumb" });
					if (text4.Length > 0)
					{
						Img(text4, "100%", "248px", "", pai7);
					}
					string text5 = PegaStr(item2, new string[1] { "dur" });
					if (text5.Length > 0)
					{
						DomNode pai8 = El("div", "style-scope ytd-thumbnail-overlay-time-status-renderer", "position:relative;margin-top:-28px;margin-left:8px;background:rgba(0,0,0,0.8);color:#fff;border-radius:4px;padding:2px 4px;font-size:12px;display:inline-block;", pai7);
						Tx(text5, pai8);
					}
					DomNode pai9 = El("div", "style-scope ytd-rich-item-renderer", "display:flex;margin-top:10px;", domNode6);
					DomNode domNode7 = El("div", "style-scope ytd-rich-grid-media", "width:36px;height:36px;background:#333;border-radius:50%;flex:none;", pai9);
					DomNode pai10 = El("div", "style-scope ytd-rich-item-renderer", "margin-left:12px;min-width:0;", pai9);
					DomNode pai11 = El("div", "style-scope ytd-rich-grid-media", "color:#f1f1f1;font-size:16px;font-weight:500;line-height:22px;", pai10);
					Tx(text3, pai11);
					DomNode pai12 = El("div", "style-scope ytd-rich-grid-media", "color:#aaa;font-size:13px;margin-top:4px;", pai10);
					Tx(string.Join(" • ", new string[3]
					{
						PegaStr(item2, new string[1] { "canal" }),
						PegaStr(item2, new string[1] { "views" }),
						PegaStr(item2, new string[1] { "pub" })
					}.Where((string s) => s.Length > 0)), pai12);
				}
				else
				{
					DomNode pai13 = El("div", "style-scope ytd-video-renderer", "position:relative;width:360px;flex:none;", domNode6);
					string text6 = PegaStr(item2, new string[1] { "thumb" });
					if (text6.Length > 0)
					{
						Img(text6, "360px", "202px", "", pai13);
					}
					string text7 = PegaStr(item2, new string[1] { "dur" });
					if (text7.Length > 0)
					{
						DomNode pai14 = El("div", "style-scope ytd-thumbnail-overlay-time-status-renderer", "position:relative;margin-top:-28px;margin-left:8px;background:rgba(0,0,0,0.8);color:#fff;border-radius:4px;padding:2px 4px;font-size:12px;display:inline-block;", pai13);
						Tx(text7, pai14);
					}
					DomNode pai15 = El("div", "style-scope ytd-video-renderer", "margin-left:16px;min-width:0;", domNode6);
					DomNode pai16 = El("div", "style-scope ytd-video-renderer", "color:#f1f1f1;font-size:18px;font-weight:500;line-height:24px;", pai15);
					Tx(text3, pai16);
					DomNode pai17 = El("div", "style-scope ytd-video-renderer", "color:#aaa;font-size:13px;margin-top:6px;", pai15);
					Tx(string.Join(" • ", new string[3]
					{
						PegaStr(item2, new string[1] { "canal" }),
						PegaStr(item2, new string[1] { "views" }),
						PegaStr(item2, new string[1] { "pub" })
					}.Where((string s) => s.Length > 0)), pai17);
				}
			}
			_itensLista = num;
			Dirty = true;
			JsValue value2 = engine.Evaluate("(function(){try{var b=document.body;if(!b)return 'sem-body';var t=[];for(var i=0;i<b.children.length&&i<6;i++){var c=b.children[i];t.push(c.tagName+'#' + (c.id||'-') + ' filhos=' + (c.children?c.children.length:'?'));}return t.join(' | ');}catch(e){return 'erro:'+e.message}})()");
			OnLog?.Invoke($"lista-fallback: montado rota={rota} itens={num} body=[{value2}] alvo={alvo.TagName ?? "?"}");
			DomNode El(string tag, string cls, string style, DomNode? domNode9)
			{
				DomNode domNode8 = new DomNode(NodeType.Element)
				{
					TagName = tag
				};
				if (cls.Length > 0)
				{
					domNode8.Attributes["class"] = cls;
				}
				if (style.Length > 0)
				{
					domNode8.Attributes["style"] = style;
				}
				domNode9?.Children.Add(domNode8);
				domNode8.Parent = domNode9 ?? alvo;
				return domNode8;
			}
			DomNode Img(string src, string w, string h, string estiloExtra, DomNode pai18)
			{
				DomNode domNode8 = El("img", "", $"width:{w};height:{h};object-fit:cover;border-radius:8px;{estiloExtra}", pai18);
				domNode8.Attributes["src"] = src;
				return domNode8;
			}
		}
		catch (Exception ex)
		{
			OnLog?.Invoke("lista-fallback: falhou " + ex.Message);
		}
		static string PegaStr(JsonElement e, params string[] caminho)
		{
			JsonElement jsonElement = e;
			foreach (string propertyName in caminho)
			{
				if (jsonElement.ValueKind != JsonValueKind.Object)
				{
					return "";
				}
				if (!jsonElement.TryGetProperty(propertyName, out var value3))
				{
					return "";
				}
				jsonElement = value3;
			}
			return (jsonElement.ValueKind == JsonValueKind.String) ? (jsonElement.GetString() ?? "") : "";
		}
		static DomNode Tx(string texto, DomNode domNode9)
		{
			DomNode domNode8 = new DomNode(NodeType.Text)
			{
				TextContent = texto
			};
			domNode8.Parent = domNode9;
			domNode9.Children.Add(domNode8);
			return domNode8;
		}
	}

	private void ImportarShadySeHouver()
	{
		int tickCount = Environment.TickCount;
		if (tickCount - _ultimoImportShady < 400)
		{
			return;
		}
		_ultimoImportShady = tickCount;
		try
		{
			JsValue value = engine.Evaluate("globalThis.__shadyExport ? globalThis.__shadyExport() : -1");
			double num = (value.IsNumber() ? value.AsNumber() : (-3.0));
			if (!_watchRenderizado)
			{
				RenderizarWatchFallback();
			}
			else if (_watchEraWatch)
			{
				// auto-heal: o boot real (shady/SPA) às vezes limpa o body e
				// apaga o fallback — re-renderiza para manter a página funcional
				try
				{
					// SEM querySelector (sintaxe de classe não garantida no stub):
					// acha o ufb-root em filhos do body e varre RECURSIVAMENTE
					// atrás de <video> (a peça está 3+ níveis abaixo da raiz)
					JsValue heal = engine.Evaluate("(function(){try{var b=document.body; if(!b) return false; var root=null; var hs=b.children; for(var i=0;i<hs.length;i++){ if(String(hs[i].className||'').indexOf('ufb-root')>=0){ root=hs[i]; break; } } if(!root) return false; var q=0; function wa(n){ var tn=String(n.tagName||'').toLowerCase(); if(tn==='video'){ q++; return; } var t=n.children; for(var j=0;j<t.length;j++) wa(t[j]); } wa(root); return q>0; }catch(e){ return true; }})()");
					bool temFallback = heal.IsBoolean() && heal.AsBoolean();
					if (!temFallback)
					{
						_watchRenderizado = false;
						RenderizarWatchFallback();
						OnLog?.Invoke("watch-fallback: re-render (auto-heal)");
					}
				}
				catch { }
			}
			if (!_decifraProcurado && _watchRenderizado && _watchEraWatch)
			{
				_decifraProcurado = true;
				try
				{
					JsValue value2 = engine.Evaluate("globalThis.__procuraDecifra ? __procuraDecifra() : 'null'");
					if (value2.IsString())
					{
						OnLog?.Invoke("decifra-scan: " + value2.AsString());
					}
				}
				catch
				{
				}
			}
			if (_tickDiag < 300)
			{
				_tickDiag++;
				bool value3 = engine.Evaluate("typeof globalThis.__shadyExport").IsString() && engine.Evaluate("typeof globalThis.__shadyExport").AsString() == "function";
				OnLog?.Invoke($"varre[{_tickDiag}]: n={num} expFn={value3}");
				if (_tickDiag == 60)
				{
					JsValue jsValue = engine.Evaluate("(function(){try{\n  var pm=document.querySelector('ytd-page-manager'); if(!pm) return 'pm-inexistente';\n  var o=['pm-upgraded='+(pm.__raw?String(!!pm.__raw.__upgraded):'?')+' disp='+(getComputedStyle? (function(){try{return getComputedStyle(pm).display}catch(e){return '?';}})():'?')];\n  try { o.push('du='+(pm.__raw&&pm.__raw.getAttribute?String(pm.__raw.getAttribute('disable-upgrade')):'nenhum')+' attrs='+String((pm.__raw&&pm.__raw.getAttributeNames?pm.__raw.getAttributeNames().join(','):'?')).substring(0,80)); } catch(e0) {}\n  try { var pms=document.querySelectorAll('ytd-page-manager'); o.push('qtd-pm='+pms.length); } catch(eq) {}\n  try { var dg=globalThis.__orfaDiag; o.push('orfa='+(dg?('c'+dg.chamadas+'/ids'+dg.ids+'/semNo'+dg.semNo+'/semRoot'+dg.semRoot+'/filhos0'+dg.semFilhos+'/semAlvo'+dg.semAlvo+'/imp'+dg.imp+(dg.ultimoErro?(' err='+dg.ultimoErro):'')):'nunca')); } catch(edg) {}\n  try { o.push('pmSRfilhos='+(pm.shadowRoot?String(pm.shadowRoot.children.length):'semSR')+' disp='+(function(){try{return getComputedStyle(pm).display}catch(e){return '?'}})()+' primDisp='+(pm.shadowRoot&&pm.shadowRoot.children[0]?(function(){try{return getComputedStyle(pm.shadowRoot.children[0]).display+' '+getComputedStyle(pm.shadowRoot.children[0]).width}catch(e2){return '?'}})():'-')); } catch(er) {}\n  try {\n    var ctorP = globalThis.__ceGet ? globalThis.__ceGet('ytd-page-manager') : null;\n    o.push('stub='+(ctorP?'sim':'nao')+' crtEl='+(ctorP&&ctorP.prototype?typeof ctorP.prototype.createElement:'?'));\n    if (ctorP && typeof ctorP.prototype.createElement === 'function') {\n      try {\n        var Wm = ctorP.prototype.createElement.call(pm);\n        o.push('mat='+(Wm?(Wm.tagName||'?'):'null')+' pmSRdepois='+(pm.shadowRoot?'sim':'nao')+' matSR='+(Wm&&Wm.shadowRoot?'sim':(Wm&&Wm.__raw&&Wm.__raw.shadowRoot?'raw-sim':'nao')));\n      } catch(em) { o.push('mat-erro='+String(em.message||em).substring(0,120)); }\n    }\n  } catch(eC) {}\n  try { o.push('ready='+(typeof pm.ready)+' att='+(typeof pm._attachDom)+'.stamp='+(typeof pm._stampTemplate)+' rawReady='+(pm.__raw?String(typeof pm.__raw.ready):'?')); } catch(e03) {}\n  try { o.push('ctr='+String((pm.constructor&&pm.constructor.name)||'?')+' temTemplate='+(pm.constructor&&pm.constructor._template?'sim':'nao')+' temReady='+(typeof pm.ready)+' temAttach='+(typeof pm._attachDom)); } catch(e00) {}\n  try { var pr=Object.getPrototypeOf(pm.__raw); o.push('protoCtr='+String((pr&&pr.constructor&&pr.constructor.name)||'?')+' protoTempl='+(pr&&pr._template!==undefined?'sim':(pr&&pr.constructor&&pr.constructor._template?'cls-sim':'nao'))+' protoKeys='+String(Object.getOwnPropertyNames(pr||{}).slice(0,14).join(','))); } catch(e01) {}\n  try { o.push('instStyled='+(pm.__shady_data?'shady':'nao')+' wing='+String(typeof pm.WrapperClass)); } catch(e02) {}\n  var sr=null; try{sr=pm.shadowRoot||null;}catch(e1){}\n  o.push('sr='+(sr?('ok nt='+(sr.nodeType||'?')+' filhos='+(sr.children?(sr.children.length||0):(sr.childNodes?(sr.childNodes.length||0):'?'))):'NULL'));\n  var rt=null; try{rt=pm.root||null;}catch(e2){}\n  o.push('root='+(rt?('ok nt='+(rt.nodeType||'?')+' filhos='+(rt.children?(rt.children.length||0):(rt.childNodes?(rt.childNodes.length||0):'?'))):'NULL'));\n  try{o.push('isConn='+String(pm.isConnected)+' data='+(pm.data?Object.keys(pm.data).length:typeof pm.data)+' page='+(pm.page===undefined?'undef':String(pm.page)));}catch(e3){o.push('props-erro');}\n  try{o.push('app.data='+(function(){var a=document.querySelector('ytd-app');return a&&a.data?Object.keys(a.data).length:'-';})());}catch(e4){}\n  return o.join(' || ');\n}catch(e){return 'erro:'+e.message;}})()");
					OnLog?.Invoke("sonda-pm: " + jsValue);
				}
			}
			if (num > 0.0)
			{
				Dirty = true;
				OnLog?.Invoke($"sombra-importada: arvores={num}");
			}
		}
		catch (Exception ex)
		{
			if (_tickDiag < 300)
			{
				_tickDiag++;
				OnLog?.Invoke("varre-erro: " + ex.Message);
			}
		}
	}

	public bool FireClick(DomNode target)
	{
		bool result = false;
		for (DomNode domNode = target; domNode != null; domNode = domNode.Parent)
		{
			if (domNode.Type == NodeType.Element)
			{
				var (jsValue, array) = MakeEvent("click", domNode);
				if (clickHandlers.TryGetValue(domNode, out List<JsValue> value))
				{
					foreach (JsValue item2 in value.ToList())
					{
						try
						{
							CallFn(item2, Wrap(domNode), new JsValue[1] { jsValue });
						}
						catch
						{
						}
					}
				}
				if (listeners.TryGetValue(domNode, out List<(string, JsValue)> value2))
				{
					foreach (var item3 in value2.Where(((string type, JsValue fn) x) => x.type == "click").ToList())
					{
						JsValue item = item3.Item2;
						try
						{
							CallFn(item, Wrap(domNode), new JsValue[1] { jsValue });
						}
						catch
						{
						}
					}
				}
				if (array[0])
				{
					result = true;
				}
			}
		}
		return result;
	}

	private void CallFn(JsValue fn, JsValue self, JsValue[] args)
	{
		try
		{
			if (fn is Function)
			{
				engine.Invoke(fn, self, args);
			}
		}
		catch (Exception ex)
		{
			OnLog?.Invoke("JS callback: " + ex.Message.Split('\n')[0]);
		}
	}

	internal void MarkDirty()
	{
		Dirty = true;
	}

	private int Bind(Role role, DomNode node)
	{
		if (role == Role.Element && elementIds.TryGetValue(node, out var value))
		{
			return value;
		}
		int num = nextId++;
		bindings[num] = new Binding
		{
			Role = role,
			Node = node
		};
		if (role == Role.Element)
		{
			elementIds[node] = num;
		}
		return num;
	}

	internal JsValue Wrap(DomNode n)
	{
		int id = Bind(Role.Element, n);
		return ProxyFor(id, n.TagName);
	}

	private JsValue ProxyFor(int id, string? tag = null)
	{
		if (!proxyCache.TryGetValue(id, out JsValue value))
		{
			Dictionary<int, JsValue> dictionary = proxyCache;
			Engine engine = this.engine;
			JsValue value2 = proxyMaker;
			JsValue undefined = JsValue.Undefined;
			object[] arguments = new JsValue[2]
			{
				id,
				tag ?? ""
			};
			value = (dictionary[id] = engine.Invoke(value2, undefined, arguments));
		}
		return value;
	}

	private JsValue WrapAll(IEnumerable<DomNode> nodes)
	{
		List<JsValue> list = new List<JsValue>();
		foreach (DomNode node in nodes)
		{
			list.Add(Wrap(node));
		}
		return new JsArray(engine, list.ToArray());
	}

	private JsValue WrapStrings(IEnumerable<string> items)
	{
		List<JsValue> list = new List<JsValue>();
		foreach (string item in items)
		{
			list.Add(item);
		}
		return new JsArray(engine, list.ToArray());
	}

	private static DomNode CloneNode(DomNode src, bool deep)
	{
		DomNode domNode = new DomNode(src.Type)
		{
			TagName = src.TagName,
			TextContent = src.TextContent
		};
		foreach (KeyValuePair<string, string> attribute in src.Attributes)
		{
			domNode.Attributes[attribute.Key] = attribute.Value;
		}
		if (deep)
		{
			foreach (DomNode child in src.Children)
			{
				DomNode domNode2 = CloneNode(child, deep: true);
				domNode2.Parent = domNode;
				domNode.Children.Add(domNode2);
			}
		}
		return domNode;
	}

	private static IEnumerable<DomNode> Descendants(DomNode n)
	{
		foreach (DomNode c in n.Children)
		{
			yield return c;
			foreach (DomNode item in Descendants(c))
			{
				yield return item;
			}
		}
	}

	private static DomNode RootOf(DomNode n)
	{
		DomNode domNode = n;
		while (domNode.Parent != null || domNode.ShadowHost != null)
		{
			domNode = domNode.Parent ?? domNode.ShadowHost;
		}
		return domNode;
	}

	private Binding? GetBinding(JsValue idVal)
	{
		if (!idVal.IsNumber())
		{
			return null;
		}
		bindings.TryGetValue((int)idVal.AsNumber(), out Binding value);
		return value;
	}

	private JsValue ResolveId(JsValue v)
	{
		if (v.IsNumber())
		{
			return v;
		}
		foreach (KeyValuePair<int, JsValue> item in proxyCache)
		{
			if ((object)item.Value == v)
			{
				return item.Key;
			}
		}
		if (v is ObjectInstance objectInstance)
		{
			JsValue jsValue = objectInstance.Get("__id");
			if (jsValue.IsNumber())
			{
				return jsValue;
			}
		}
		return JsValue.Undefined;
	}

	private void ExposeGlobals()
	{
		engine.Evaluate("\nglobalThis.__KNOWN = new Set(['getAttribute','setAttribute','removeAttribute','hasAttribute','addEventListener','removeEventListener','appendChild','removeChild','click','querySelector','querySelectorAll','getElementsByTagName','getElementsByClassName','contains','closest','focus','scrollIntoView','getElementById','createElement','createTextNode','write','writeln','getPropertyValue','setProperty','removeProperty','add','remove','toggle','item','assign','reload','replace','preventDefault','tagName','nodeName','id','className','classList','style','textContent','innerHTML','outerHTML','value','href','src','type','title','parentNode','parentElement','children','childNodes','firstChild','firstElementChild','dataset','body','head','documentElement','cookie','readyState','referrer','cssText','length','target','scrollTo','scrollBy','insertBefore','replaceChild','insertAdjacentHTML','insertAdjacentElement','cloneNode','append','prepend','before','after','remove','replaceWith','replaceChildren','matches','getAttributeNames','toggleAttribute','getRootNode','createDocumentFragment','createComment','createElementNS','importNode','adoptNode','content','assignedNodes','getElementsByName','nextSibling','previousSibling','nodeType','removeAttribute','setAttributeNS','attachInternals','getAnimations','checkVisibility','moveBefore','setHTMLUnsafe','showPopover','hidePopover','togglePopover','requestFullscreen','exitFullscreen','requestPointerLock','setPointerCapture','releasePointerCapture','hasPointerCapture','scrollIntoViewIfNeeded','composedPath','createRange','createTreeWalker','createNodeIterator','elementFromPoint','elementsFromPoint','getSelection','createEvent','execCommand','queryCommandSupported','assignedNodes','assignedElements','host','mode','isConnected','slot','assignedSlot','offsetWidth','offsetHeight','offsetTop','offsetLeft','offsetParent','clientWidth','clientHeight','clientTop','clientLeft','scrollWidth','scrollHeight','scrollTop','scrollLeft','hidden','tabIndex','disabled','checked','selected','multiple','name','rel','media','lang','dir','translate','draggable','contentEditable','inert','popover','nonce','part','exportparts','role','ariaLabel','localName','namespaceURI','prefix','baseURI','ownerDocument','nodeValue','data','wholeText','previousElementSibling','nextElementSibling','lastElementChild','childElementCount','firstChild','lastChild','textContent','innerText','outerHTML','attributes','classList','dataset','style']);\nglobalThis.__METHODS = new Set(['getAttribute','setAttribute','removeAttribute','hasAttribute','addEventListener','removeEventListener','appendChild','removeChild','click','querySelector','querySelectorAll','getElementsByTagName','getElementsByClassName','contains','closest','focus','scrollIntoView','getElementById','createElement','createTextNode','write','writeln','getPropertyValue','setProperty','removeProperty','add','remove','toggle','item','assign','reload','replace','preventDefault','scrollTo','scrollBy','insertBefore','replaceChild','insertAdjacentHTML','insertAdjacentElement','cloneNode','append','prepend','before','after','remove','replaceWith','replaceChildren','matches','getAttributeNames','toggleAttribute','getRootNode','createDocumentFragment','createComment','createElementNS','importNode','adoptNode','getElementsByName','attachShadow','checkVisibility','moveBefore','setHTMLUnsafe','showPopover','hidePopover','togglePopover','requestFullscreen','exitFullscreen','requestPointerLock','setPointerCapture','releasePointerCapture','hasPointerCapture','scrollIntoViewIfNeeded','composedPath','createRange','createTreeWalker','createNodeIterator','elementFromPoint','elementsFromPoint','getSelection','createEvent','execCommand','queryCommandSupported','assignedNodes','assignedElements','getAnimations','animate','attachInternals','dispatchEvent','getContext','getBoundingClientRect','getClientRects','getElementsByTagNameNS','insertAdjacentText','setAttributeNS','getAttributeNS','removeAttributeNS','hasAttributeNS','getBoxQuads','convertPointFromNode','convertRectFromNode','convertRectToNode','scroll','scrollByLines','scrollByPages','setPointerCapture','releasePointerCapture','requestPointerLock','showModal','close','requestSubmit','reportValidity','checkValidity','setCustomValidity','setSelectionRange','select','stepUp','stepDown','setRangeText','normalize','compareDocumentPosition','isEqualNode','isSameNode','isDefaultNamespace','lookupNamespaceURI','lookupPrefix','before','after','assignedSlot','slot','isConnected','innerText','outerHTML','blur','append','prepend','replaceChildren','replaceWith','remove']);\nglobalThis.__makeProxy = function(id, tag) {\n  tag = String(tag || '').toLowerCase();\n  // Objeto-base que herda da classe de elemento adequada (HTMLElement etc.),\n  // para que métodos de protótipo (attachShadow, matches...) existam de fato.\n  var proto = (globalThis.HTMLElement && globalThis.HTMLElement.prototype) || Object.prototype;\n  if (tag) {\n    var cls = globalThis.__classForTag ? globalThis.__classForTag(tag) : null;\n    if (cls && cls.prototype) proto = cls.prototype;\n  }\n  var alvo = Object.create(proto);\n  var proxy = new Proxy(alvo, {\n    get(t, p, receiver) {\n      if (typeof p !== 'string') {\n        var r0 = Reflect.get(t, p, receiver);\n        // ITERÁVEL default: os helpers-deles (_.L) iteram propriedades que no\n        // boot ainda não têm dados; com Symbol.iterator ausente a linha 5298\n        // estourava String(Symbol) e o connectedCallback abortava. Iterar os\n        // childNodes do nó é o que o mundo real expõe para elementos.\n        if (r0 === undefined && globalThis.Symbol && p === globalThis.Symbol.iterator) {\n          return function () {\n            var ks = globalThis.__call(id, 'childNodes', []);\n            var i2 = 0; var N2 = ks ? ks.length : 0;\n            return { next: function () { return i2 < N2 ? { done: false, value: ks[i2++] } : { done: true, value: undefined }; } };\n          };\n        }\n        return r0;\n      }\n      // 1) símbolos e coisas do Object.prototype passam direto\n      if (p === 'constructor') return Reflect.get(t, p, receiver);\n      if (p === 'then') return undefined;              // não é thenable\n      // Todo proxy de elemento expõe o próprio id. É a âncora que permite às pontes\n      // C# (__shCreate/__shGet/TryNodeLoose) reencontrarem o nó mesmo quando o\n      // objeto chega embrulhado por bibliotecas (o `_.et(this)` do Polymer, ou o\n      // `this.node` do ShadyDOM). Sem isso `shadowRoot` lia null, o _attachDom\n      // estourava em appendChild e o ytd-app ficava com altura zero.\n      if (p === '__id') return id;\n      // Raiz objeto CRU do proxy: é AQUI que o ShadyDOM grava __shady_shadowRoot\n      // e __shady_root (opera nos nós lógicos). Os traps __shady_* respondem\n      // antes de qualquer leitura normal, então o exportador usa __raw para ver\n      // o estado real do ShadyDOM sem passar pela ponte C#.\n      if (p === '__raw') return t;\n      // ShadyDOM (bundle webcomponents-sd do YouTube) envolve os métodos nativos e\n      // chama sempre a versão original por __shady_native_X / __shady_X, por\n      // exemplo: Element.prototype.attachShadow = function(a){\n      //              return this.node.__shady_attachShadow(a); }\n      // Como `this.node` pode ser um objeto cru, responder a QUALQUER nome\n      // __shady_* aqui, no trap do proxy, é o que mantém a chamada viva. Sem isto\n      // o connectedCallback do ytd-app morria e a SPA não criava o ytd-page-manager.\n      if (p.indexOf('__shady_') === 0) {\n        var base = p.substring('__shady_'.length);\n        if (base.indexOf('native_') === 0) base = base.substring('native_'.length);\n        if (globalThis.__METHODS.has(base)) return (...a) => globalThis.__call(id, base, a);\n        // ShadyDOM guarda o shadow root em `this.node.__shady_shadowRoot` e o\n        // Polymer lê exatamente por aí dentro do _attachDom:\n        //     W.shadowRoot || (W.attachShadow(...), W.shadowRoot.appendChild(u))\n        // Sem responder este nome o appendChild recebia undefined e o ytd-app\n        // ficava com altura zero (o conteúdo ia para um fragmento fora da árvore).\n        if (base === 'shadowRoot') return globalThis.__shGet(id);\n        // `root` NÃO interceptar: o getter do ShadyDOM (no protótipo) lê uma\n        // propriedade PRÓPRIA do nó (`__shady_root`) e é ela que expõe a árvore\n        // estampada com 13 filhos. Encaminhar para __get devolvia undefined e\n        // apagava o nodefinding do Polymer (this.guide virava undefined).\n        var pvShady = globalThis.__get(id, base);\n        if (pvShady !== undefined && pvShady !== null) return pvShady;\n      }\n      // 2) métodos e props nativos do DOM vão para o C#\n      // NOTA: `shadowRoot` NÃO pode ser interceptado aqui. O getter patched do\n      // ShadyDOM no protótipo resolve via Map interno keyed pela IDENTIDADE do\n      // proxy; responder antes (mesmo null) escondia a árvore estampada do\n      // Polymer (root-filhos=13 virava sem-sr). Cair na cadeia de protótipos.\n      if (globalThis.__METHODS.has(p)) return (...a) => globalThis.__call(id, p, a);\n      // propriedades DOM conhecidas: o C# é a fonte da verdade (inclusive null)\n      if (globalThis.__KNOWN.has(p)) {\n        var kv = globalThis.__get(id, p);\n        if (kv !== undefined) return kv;\n      }\n      // custom element registrado: métodos do SEU protótipo vencem os no-ops da base\n      var ceCtor = globalThis.__ceGet ? globalThis.__ceGet(tag) : null;\n      if (ceCtor && ceCtor.prototype) {\n        var cdp = Object.getOwnPropertyDescriptor(ceCtor.prototype, p);\n        if (cdp && (typeof cdp.value === 'function' || cdp.get)) {\n          var cv = Reflect.get(ceCtor.prototype, p, receiver);\n          if (cv !== undefined && cv !== null) return cv;\n        }\n      }\n      // propriedades com getter/valor na CADEIA de protótipos têm prioridade.\n      // O receiver precisa ser o PROXY (não o alvo), senão `this` dentro do\n      // getter aponta para o objeto cru e as pontes não acham o nó.\n      var found = false;\n      for (var o = proto; o && o !== Object.prototype; o = Object.getPrototypeOf(o)) {\n        var d = Object.getOwnPropertyDescriptor(o, p);\n        if (d) { found = true; break; }\n      }\n      if (found) {\n        var pv0 = Reflect.get(t, p, receiver);\n        if (pv0 !== undefined && pv0 !== null) return pv0;\n      }\n      var v = globalThis.__get(id, p);\n      if (v !== undefined && v !== null) return v;\n      // senão, cai no protótipo (HTMLElement/Element) e no expando JS\n      var pv = Reflect.get(t, p, receiver);\n      if (pv !== undefined) return pv;\n      // NODEFINDING do Polymer p/ árvores estampadas-importadas: o fragmento chega\n      // pronto via shadyUpgradeFragment e o stampTemplate do Polymer NÃO RODA,\n      // então `this.guide`, `this.getContentChildren`-ids ficam indefinidos para\n      // sempre e o connectedCallback do ytd-app morre (linha 40869 do base-js:\n      // this.guide.addEventListener). Réplica do automatic node finding: se\n      // existe um COMPONENTE com esse id na minha árvore importada, devolvo o\n      // proxy dele. Só para props simples não resolvidas em lugar nenhum.\n      if (p.length > 1 && /^[a-zA-Z_$][\\w$]*$/.test(p)) {\n        var noC = globalThis.__findId(id, p);\n        if (noC && noC.__id !== undefined) return noC;\n      }\n      return undefined;\n    },\n    set(t, p, v) {\n      if (typeof p === 'string') {\n        // propriedades de instância (ad-hoc) ficam no alvo JS; o resto vai ao C#\n        if (globalThis.__METHODS.has(p)) { globalThis.__set(id, p, v); return true; }\n        globalThis.__set(id, p, v);\n        try { Reflect.set(t, p, v); } catch (e) {}\n      }\n      return true;\n    },\n    has(t, p) { return typeof p === 'string' && (globalThis.__KNOWN.has(p) || p in t); }\n  });\n  return proxy;\n}");
		engine.SetValue("__get", new ClrFunction(engine, "__get", (JsValue self, JsValue[] args) => (args.Length >= 2) ? DoGet(ResolveId(args[0]), args[1].ToString()) : JsValue.Undefined));
		engine.SetValue("__set", new ClrFunction(engine, "__set", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 3)
			{
				DoSet(ResolveId(args[0]), args[1].ToString(), args[2]);
			}
			return JsValue.Undefined;
		}));
		engine.SetValue("__call", new ClrFunction(engine, "__call", (JsValue self, JsValue[] args) => (args.Length < 2) ? JsValue.Undefined : DoCall(ResolveId(args[0]), args[1].ToString(), (args.Length > 2) ? ToArgs(args[2]) : Array.Empty<JsValue>())));
		proxyMaker = engine.Evaluate("globalThis.__makeProxy");
		bindings[0] = new Binding
		{
			Role = Role.Document,
			Node = document
		};
		engine.SetValue("document", ProxyFor(0));
		engine.Global.FastSetDataProperty("document", ProxyFor(0));
		engine.SetValue("window", engine.Global);
		engine.SetValue("self", engine.Global);
		engine.SetValue("top", engine.Global);
		engine.SetValue("parent", engine.Global);
		engine.SetValue("devicePixelRatio", (JsValue)1);
		InstallFetchAndVideo();
		InstallWebPlatform();
		ObjectInstance objectInstance = (ObjectInstance)engine.Evaluate("({})");
		objectInstance.FastSetDataProperty("width", 1280);
		objectInstance.FastSetDataProperty("height", 800);
		objectInstance.FastSetDataProperty("availWidth", 1280);
		objectInstance.FastSetDataProperty("availHeight", 760);
		objectInstance.FastSetDataProperty("colorDepth", 24);
		engine.SetValue("screen", objectInstance);
		ObjectInstance value = (ObjectInstance)engine.Evaluate("(function () {\n  var t0 = Date.now();\n  // performance.timing: o YouTube lê responseStart/navigationStart para métricas.\n  // Valores plausíveis > 0 evitam divisão por zero e NaN nos cálculos do app.\n  var timing = {\n    navigationStart: t0, unloadEventStart: 0, unloadEventEnd: 0,\n    redirectStart: 0, redirectEnd: 0, fetchStart: t0,\n    domainLookupStart: t0, domainLookupEnd: t0,\n    connectStart: t0, connectEnd: t0, secureConnectionStart: 0,\n    requestStart: t0, responseStart: t0 + 1, responseEnd: t0 + 2,\n    domLoading: t0 + 3, domInteractive: t0 + 4, domContentLoadedEventStart: t0 + 4,\n    domContentLoadedEventEnd: t0 + 5, domComplete: t0 + 6,\n    loadEventStart: t0 + 6, loadEventEnd: t0 + 7\n  };\n  var nav = {\n    type: 0, redirectCount: 0,\n    nextHopProtocol: 'h2',\n    domContentLoadedEventEnd: t0 + 5, loadEventEnd: t0 + 7,\n    activationStart: 0,\n    entryType: 'navigation', name: (globalThis.location && globalThis.location.href) || '', startTime: 0, duration: 7\n  };\n  return {\n    now: function () { return Date.now() - t0; },\n    timeOrigin: t0,\n    timing: timing,\n    navigation: nav,\n    memory: { jsHeapSizeLimit: 2172649472, totalJSHeapSize: 40000000, usedJSHeapSize: 25000000 },\n    mark: function () {}, measure: function () {}, clearMarks: function () {}, clearMeasures: function () {},\n    getEntries: function () { return [nav]; },\n    getEntriesByType: function (t) { return t === 'navigation' ? [nav] : []; },\n    getEntriesByName: function (n) { return n === nav.name ? [nav] : []; },\n    setResourceTimingBufferSize: function () {}, clearResourceTimings: function () {}\n  };\n})()");
		engine.SetValue("performance", value);
		engine.SetValue("localStorage", MakeStorage());
		engine.SetValue("sessionStorage", MakeStorage());
		ObjectInstance objectInstance2 = (ObjectInstance)engine.Evaluate("({})");
		objectInstance2.FastSetDataProperty("length", 1);
		objectInstance2.FastSetDataProperty("state", JsValue.Null);
		string[] array = new string[5] { "pushState", "replaceState", "go", "back", "forward" };
		foreach (string name in array)
		{
			objectInstance2.FastSetDataProperty(name, new ClrFunction(engine, name, (JsValue s, JsValue[] a) => JsValue.Undefined));
		}
		engine.SetValue("history", objectInstance2);
		engine.SetValue("__vpW", new ClrFunction(engine, "__vpW", (JsValue s, JsValue[] a) =>
		{
			try
			{
				return (GetViewport?.Invoke())?.w ?? 1280;
			}
			catch
			{
				return 1280;
			}
		}));
		engine.SetValue("__vpH", new ClrFunction(engine, "__vpH", (JsValue s, JsValue[] a) =>
		{
			try
			{
				return (GetViewport?.Invoke())?.h ?? 800;
			}
			catch
			{
				return 800;
			}
		}));
		engine.SetValue("__scY", new ClrFunction(engine, "__scY", (JsValue s, JsValue[] a) =>
		{
			try
			{
				return GetScroll?.Invoke() ?? 0f;
			}
			catch
			{
				return 0;
			}
		}));
		engine.Evaluate("\nObject.defineProperty(globalThis, 'innerWidth', { get: () => globalThis.__vpW(), configurable: true });\nObject.defineProperty(globalThis, 'innerHeight', { get: () => globalThis.__vpH(), configurable: true });\nObject.defineProperty(globalThis, 'scrollX', { get: () => 0, configurable: true });\nObject.defineProperty(globalThis, 'scrollY', { get: () => globalThis.__scY(), configurable: true });\nObject.defineProperty(globalThis, 'pageXOffset', { get: () => 0, configurable: true });\nObject.defineProperty(globalThis, 'pageYOffset', { get: () => globalThis.__scY(), configurable: true });");
		engine.SetValue("setTimeout", new ClrFunction(engine, "setTimeout", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0 || !(args[0] is Function))
			{
				return JsValue.Undefined;
			}
			int val = ((args.Length > 1 && args[1].IsNumber()) ? ((int)args[1].AsNumber()) : 0);
			int num4 = nextTimerId++;
			timers.Add((num4, DateTime.UtcNow.AddMilliseconds(Math.Max(0, val)), null, args[0]));
			return num4;
		}));
		engine.SetValue("setInterval", new ClrFunction(engine, "setInterval", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0 || !(args[0] is Function))
			{
				return JsValue.Undefined;
			}
			int val = ((args.Length > 1 && args[1].IsNumber()) ? ((int)args[1].AsNumber()) : 0);
			int num4 = nextTimerId++;
			timers.Add((num4, DateTime.UtcNow.AddMilliseconds(Math.Max(10, val)), Math.Max(10, val), args[0]));
			return num4;
		}));
		Func<JsValue, JsValue[], JsValue> func = (JsValue self, JsValue[] args) =>
		{
			if (args.Length != 0 && args[0].IsNumber())
			{
				int id = (int)args[0].AsNumber();
				timers.RemoveAll(((int id, DateTime at, int? everyMs, JsValue fn) t) => t.id == id);
			}
			return JsValue.Undefined;
		};
		engine.SetValue("clearTimeout", new ClrFunction(engine, "clearTimeout", func));
		engine.SetValue("clearInterval", new ClrFunction(engine, "clearInterval", func));
		engine.SetValue("requestAnimationFrame", new ClrFunction(engine, "requestAnimationFrame", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0 || !(args[0] is Function))
			{
				return JsValue.Undefined;
			}
			int num4 = nextTimerId++;
			timers.Add((num4, DateTime.UtcNow.AddMilliseconds(16.0), null, args[0]));
			return num4;
		}));
		engine.SetValue("__newImage", new ClrFunction(engine, "__newImage", (JsValue self, JsValue[] args) =>
		{
			DomNode domNode = new DomNode(NodeType.Element)
			{
				TagName = "img"
			};
			if (args.Length != 0 && args[0].IsNumber())
			{
				domNode.Attributes["width"] = ((int)args[0].AsNumber()).ToString();
			}
			if (args.Length > 1 && args[1].IsNumber())
			{
				domNode.Attributes["height"] = ((int)args[1].AsNumber()).ToString();
			}
			return Wrap(domNode);
		}));
		engine.Evaluate("function Image(w, h) { return globalThis.__newImage(w, h); }");
		engine.SetValue("__newIO", new ClrFunction(engine, "__newIO", (JsValue self, JsValue[] args) =>
		{
			JsValue cb = ((args.Length != 0) ? args[0] : JsValue.Undefined);
			ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
			List<DomNode> seen = new List<DomNode>();
			objectInstance5.FastSetDataProperty("observe", new ClrFunction(engine, "observe", (JsValue s, JsValue[] a) =>
			{
				if (a.Length != 0 && TryNode(a[0], out DomNode node) && node != null && !seen.Contains(node))
				{
					seen.Add(node);
					JsEngine jsEngine = this;
					int num4 = nextTimerId;
					jsEngine.nextTimerId = num4 + 1;
					int item = num4;
					DomNode tgt = node;
					timers.Add((item, DateTime.UtcNow.AddMilliseconds(10.0), null, new ClrFunction(engine, "iofire", (JsValue ss, JsValue[] aa) =>
					{
						ObjectInstance objectInstance6 = (ObjectInstance)engine.Evaluate("({})");
						objectInstance6.FastSetDataProperty("isIntersecting", true);
						objectInstance6.FastSetDataProperty("intersectionRatio", 1);
						objectInstance6.FastSetDataProperty("target", Wrap(tgt));
						CallFn(cb, JsValue.Undefined, new JsValue[1]
						{
							new JsArray(engine, new JsValue[1] { objectInstance6 })
						});
						return JsValue.Undefined;
					})));
				}
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("unobserve", new ClrFunction(engine, "unobserve", (JsValue s, JsValue[] a) =>
			{
				if (a.Length != 0 && TryNode(a[0], out DomNode node) && node != null)
				{
					seen.Remove(node);
				}
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("disconnect", new ClrFunction(engine, "disconnect", (JsValue s, JsValue[] a) =>
			{
				seen.Clear();
				return JsValue.Undefined;
			}));
			return objectInstance5;
		}));
		engine.Evaluate("function IntersectionObserver(cb, opts) { return globalThis.__newIO(cb, opts); }");
		engine.SetValue("__newMO", new ClrFunction(engine, "__newMO", (JsValue self, JsValue[] args) =>
		{
			JsValue callback = ((args.Length != 0) ? args[0] : JsValue.Undefined);
			ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
			MutObserver mo = new MutObserver
			{
				Callback = callback,
				Proxy = objectInstance5
			};
			objectInstance5.FastSetDataProperty("observe", new ClrFunction(engine, "mo_observe", (JsValue s, JsValue[] a) =>
			{
				if (a.Length != 0 && TryNode(a[0], out DomNode node) && node != null)
				{
					mo.Target = node;
					if (a.Length > 1 && a[1] is ObjectInstance objectInstance6)
					{
						mo.ChildList = IsTruthy(objectInstance6.Get("childList", objectInstance6));
						mo.Attributes = IsTruthy(objectInstance6.Get("attributes", objectInstance6));
						mo.Subtree = IsTruthy(objectInstance6.Get("subtree", objectInstance6));
					}
					else
					{
						mo.ChildList = true;
					}
					if (!mutObservers.Contains(mo))
					{
						mutObservers.Add(mo);
					}
				}
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("disconnect", new ClrFunction(engine, "mo_disconnect", (JsValue s, JsValue[] a) =>
			{
				mutObservers.Remove(mo);
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("takeRecords", new ClrFunction(engine, "mo_take", (JsValue s, JsValue[] a) => new JsArray(engine, Array.Empty<JsValue>())));
			return objectInstance5;
		}));
		engine.Evaluate("function MutationObserver(cb) { return globalThis.__newMO(cb); }");
		engine.SetValue("matchMedia", new ClrFunction(engine, "matchMedia", (JsValue self, JsValue[] args) =>
		{
			string text2 = ((args.Length != 0) ? args[0].ToString().ToLowerInvariant() : "");
			bool flag = false;
			if (text2.Contains("prefers-color-scheme"))
			{
				bool flag2 = text2.Contains("dark");
				bool flag3 = text2.Contains("light");
				if (flag2 != flag3)
				{
					flag = flag2 == CssParser.SystemDark;
				}
				else if (text2.Contains("no-preference"))
				{
					flag = true;
				}
			}
			ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
			objectInstance5.FastSetDataProperty("matches", flag);
			objectInstance5.FastSetDataProperty("media", (args.Length != 0) ? ((JsValue)args[0].ToString()) : ((JsValue)""));
			objectInstance5.FastSetDataProperty("addListener", new ClrFunction(engine, "addListener", (JsValue s, JsValue[] a) => JsValue.Undefined));
			objectInstance5.FastSetDataProperty("removeListener", new ClrFunction(engine, "removeListener", (JsValue s, JsValue[] a) => JsValue.Undefined));
			objectInstance5.FastSetDataProperty("addEventListener", new ClrFunction(engine, "addEventListener", (JsValue s, JsValue[] a) => JsValue.Undefined));
			objectInstance5.FastSetDataProperty("removeEventListener", new ClrFunction(engine, "removeEventListener", (JsValue s, JsValue[] a) => JsValue.Undefined));
			objectInstance5.FastSetDataProperty("onchange", JsValue.Null);
			return objectInstance5;
		}));
		engine.SetValue("alert", new ClrFunction(engine, "alert", (JsValue self, JsValue[] args) =>
		{
			OnLog?.Invoke((args.Length != 0) ? args[0].ToString() : "alert");
			return JsValue.Undefined;
		}));
		engine.SetValue("confirm", new ClrFunction(engine, "confirm", (JsValue self, JsValue[] args) => true));
		engine.SetValue("prompt", new ClrFunction(engine, "prompt", (JsValue self, JsValue[] args) => JsValue.Null));
		engine.SetValue("getComputedStyle", new ClrFunction(engine, "getComputedStyle", (JsValue self, JsValue[] args) =>
		{
			ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
			DomNode node = null;
			if (args.Length != 0)
			{
				TryNodeLoose(args[0], out node);
			}
			ComputedStyle cs = ((node != null) ? StyleOf(node) : new ComputedStyle());
			objectInstance5.FastSetDataProperty("display", cs.Display.ToString().ToLowerInvariant());
			objectInstance5.FastSetDataProperty("position", cs.Position.ToString().ToLowerInvariant());
			objectInstance5.FastSetDataProperty("visibility", cs.Hidden ? "hidden" : "visible");
			objectInstance5.FastSetDataProperty("opacity", cs.Opacity.ToString(CultureInfo.InvariantCulture));
			objectInstance5.FastSetDataProperty("color", $"rgb({cs.Color.R}, {cs.Color.G}, {cs.Color.B})");
			objectInstance5.FastSetDataProperty("fontSize", Px(cs.FontSize));
			objectInstance5.FastSetDataProperty("getPropertyValue", new ClrFunction(engine, "getPropertyValue", (JsValue s, JsValue[] a) =>
			{
				if (a.Length == 0)
				{
					return "";
				}
				string text2 = a[0].ToString().Trim().ToLowerInvariant();
				if (1 == 0)
				{
				}
				JsValue result = text2 switch
				{
					"display" => (JsValue)cs.Display.ToString().ToLowerInvariant(), 
					"position" => cs.Position.ToString().ToLowerInvariant(), 
					"visibility" => cs.Hidden ? "hidden" : "visible", 
					"opacity" => cs.Opacity.ToString(CultureInfo.InvariantCulture), 
					"color" => $"rgb({cs.Color.R}, {cs.Color.G}, {cs.Color.B})", 
					"font-size" => Px(cs.FontSize), 
					_ => "", 
				};
				if (1 == 0)
				{
				}
				return result;
			}));
			return objectInstance5;
		}));
		engine.SetValue("__parseHtml", new ClrFunction(engine, "__parseHtml", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0)
			{
				return JsValue.Null;
			}
			DomNode domNode = new HtmlParser(args[0].ToString()).Parse();
			DomNode domNode2 = new DomNode(NodeType.Element)
			{
				TagName = "#document"
			};
			DomNode domNode3 = new DomNode(NodeType.Element)
			{
				TagName = "html",
				Parent = domNode2
			};
			DomNode domNode4 = new DomNode(NodeType.Element)
			{
				TagName = "head",
				Parent = domNode3
			};
			DomNode domNode5 = new DomNode(NodeType.Element)
			{
				TagName = "body",
				Parent = domNode3
			};
			domNode3.Children.Add(domNode4);
			domNode3.Children.Add(domNode5);
			domNode2.Children.Add(domNode3);
			foreach (DomNode item2 in domNode.Children.ToList())
			{
				domNode.Children.Remove(item2);
				if (item2.TagName == "html")
				{
					foreach (DomNode item3 in item2.Children.ToList())
					{
						item2.Children.Remove(item3);
						DomNode domNode6 = ((item3.TagName == "head") ? domNode4 : ((item3.TagName == "body") ? domNode5 : domNode5));
						if (item3.TagName == "body")
						{
							foreach (DomNode item4 in item3.Children.ToList())
							{
								item3.Children.Remove(item4);
								item4.Parent = domNode5;
								domNode5.Children.Add(item4);
							}
						}
						else
						{
							item3.Parent = domNode6;
							domNode6.Children.Add(item3);
						}
					}
				}
				else
				{
					(item2.Parent = ((item2.TagName == "head") ? domNode4 : domNode5)).Children.Add(item2);
				}
			}
			return Wrap(domNode2);
		}));
		engine.SetValue("__bodyOf", new ClrFunction(engine, "__bodyOf", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0 || !TryNode(args[0], out DomNode node) || node == null)
			{
				return JsValue.Null;
			}
			DomNode domNode = node.FindTag("body");
			if (domNode != null)
			{
				return Wrap(domNode);
			}
			DomNode domNode2 = new DomNode(NodeType.Element)
			{
				TagName = "body",
				Parent = node
			};
			node.Children.Add(domNode2);
			return Wrap(domNode2);
		}));
		engine.SetValue("__headOf", new ClrFunction(engine, "__headOf", (JsValue self, JsValue[] args) =>
		{
			if (args.Length == 0 || !TryNode(args[0], out DomNode node) || node == null)
			{
				return JsValue.Null;
			}
			DomNode domNode = node.FindTag("head");
			return (domNode != null) ? Wrap(domNode) : JsValue.Null;
		}));
		ObjectInstance objectInstance3 = (ObjectInstance)engine.Evaluate("({})");
		objectInstance3.FastSetDataProperty("log", new ClrFunction(engine, "log", (JsValue s, JsValue[] a) =>
		{
			OnLog?.Invoke(string.Join(" ", a.Select((JsValue x) => x.ToString())));
			return JsValue.Undefined;
		}));
		objectInstance3.FastSetDataProperty("warn", new ClrFunction(engine, "warn", (JsValue s, JsValue[] a) =>
		{
			OnLog?.Invoke("[warn] " + string.Join(" ", a.Select((JsValue x) => x.ToString())));
			return JsValue.Undefined;
		}));
		objectInstance3.FastSetDataProperty("error", new ClrFunction(engine, "error", (JsValue s, JsValue[] a) =>
		{
			OnLog?.Invoke("[error] " + string.Join(" ", a.Select((JsValue x) => x.ToString())));
			return JsValue.Undefined;
		}));
		string[] array2 = new string[24]
		{
			"info", "debug", "trace", "dir", "dirxml", "table", "group", "groupCollapsed", "groupEnd", "time",
			"timeEnd", "timeLog", "count", "countReset", "assert", "clear", "profile", "profileEnd", "timeStamp", "context",
			"memory", "markTimeline", "timeline", "timelineEnd"
		};
		foreach (string text in array2)
		{
			string name2 = text;
			objectInstance3.FastSetDataProperty(name2, new ClrFunction(engine, name2, (JsValue s, JsValue[] a) => JsValue.Undefined));
		}
		engine.SetValue("console", objectInstance3);
		ObjectInstance objectInstance4 = (ObjectInstance)engine.Evaluate("({})");
		objectInstance4.FastSetDataProperty("userAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Uranium/2.0 Chrome/124.0 Safari/537.36");
		objectInstance4.FastSetDataProperty("language", "pt-BR");
		objectInstance4.FastSetDataProperty("platform", "Win32");
		objectInstance4.FastSetDataProperty("onLine", true);
		engine.SetValue("navigator", objectInstance4);
		int num3 = nextId++;
		bindings[num3] = new Binding
		{
			Role = Role.Location,
			Node = document
		};
		engine.SetValue("location", ProxyFor(num3));
		engine.Global.FastSetDataProperty("addEventListener", new ClrFunction(engine, "addEventListener", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 2 && args[1] is Function)
			{
				string text2 = args[0].ToString();
				if ((text2 == "DOMContentLoaded" || text2 == "load") ? true : false)
				{
					dclHandlers.Add(args[1]);
				}
				else
				{
					windowListeners.Add((text2, args[1]));
				}
			}
			return JsValue.Undefined;
		}));
		engine.Global.FastSetDataProperty("removeEventListener", new ClrFunction(engine, "removeEventListener", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 2)
			{
				string t = args[0].ToString();
				windowListeners.RemoveAll(((string, JsValue) x) => string.Equals(x.Item1, t, StringComparison.Ordinal) && (object)x.Item2 == args[1]);
			}
			return JsValue.Undefined;
		}));
		engine.Global.FastSetDataProperty("dispatchEvent", new ClrFunction(engine, "dispatchEvent", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 1)
			{
				string text2 = "";
				try
				{
					if (args[0] is ObjectInstance objectInstance5)
					{
						JsValue jsValue = objectInstance5.Get("type");
						if (jsValue.IsString())
						{
							text2 = jsValue.ToString();
						}
					}
					if (text2.Length == 0 && args[0].IsString())
					{
						text2 = args[0].ToString();
					}
				}
				catch
				{
				}
				if (text2.Length > 0)
				{
					foreach (var (a, fn) in windowListeners.ToList())
					{
						if (string.Equals(a, text2, StringComparison.Ordinal))
						{
							CallFn(fn, engine.Global, new JsValue[1] { args[0] });
						}
					}
				}
			}
			return true;
		}));
		engine.Global.FastSetDataProperty("scrollTo", new ClrFunction(engine, "scrollTo", (JsValue self, JsValue[] args) =>
		{
			float arg = ((args.Length != 0 && args[0].IsNumber()) ? ((float)args[0].AsNumber()) : 0f);
			float arg2 = ((args.Length > 1 && args[1].IsNumber()) ? ((float)args[1].AsNumber()) : 0f);
			OnScrollTo?.Invoke(arg, arg2);
			return JsValue.Undefined;
		}));
		ObjectInstance MakeStorage()
		{
			Dictionary<string, string> dict = new Dictionary<string, string>();
			ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
			objectInstance5.FastSetDataProperty("getItem", new ClrFunction(engine, "getItem", (JsValue s, JsValue[] a) => (a.Length != 0 && dict.TryGetValue(a[0].ToString(), out string value2)) ? ((JsValue)value2) : JsValue.Null));
			objectInstance5.FastSetDataProperty("setItem", new ClrFunction(engine, "setItem", (JsValue s, JsValue[] a) =>
			{
				if (a.Length >= 2)
				{
					dict[a[0].ToString()] = a[1].ToString();
				}
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("removeItem", new ClrFunction(engine, "removeItem", (JsValue s, JsValue[] a) =>
			{
				if (a.Length != 0)
				{
					dict.Remove(a[0].ToString());
				}
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("clear", new ClrFunction(engine, "clear", (JsValue s, JsValue[] a) =>
			{
				dict.Clear();
				return JsValue.Undefined;
			}));
			objectInstance5.FastSetDataProperty("key", new ClrFunction(engine, "key", (JsValue s, JsValue[] a) => (a.Length != 0 && a[0].IsNumber() && (int)a[0].AsNumber() >= 0 && (int)a[0].AsNumber() < dict.Count) ? ((JsValue)dict.Keys.ElementAt((int)a[0].AsNumber())) : JsValue.Null));
			objectInstance5.FastSetDataProperty("length", 0);
			return objectInstance5;
		}
		static string Px(float v)
		{
			return v.ToString("0.##", CultureInfo.InvariantCulture) + "px";
		}
	}

	private DomNode? CreateShadowRoot(DomNode host, string mode)
	{
		if (host.ShadowRoot != null)
		{
			return null;
		}
		DomNode domNode = (host.ShadowRoot = new DomNode(NodeType.Element)
		{
			TagName = "#shadow-root",
			Parent = host,
			ShadowMode = (string.IsNullOrEmpty(mode) ? "open" : mode)
		});
		domNode.ShadowHost = host;
		return domNode;
	}

	private void TryUpgradeAll(string tag)
	{
		DomNode n = document;
		List<DomNode> alvos = new List<DomNode>();
		Walk(n);
		foreach (DomNode item in alvos)
		{
			TryUpgrade(item);
		}
		void Walk(DomNode domNode)
		{
			if (domNode != null)
			{
				if (domNode.Type == NodeType.Element && string.Equals(domNode.TagName, tag, StringComparison.OrdinalIgnoreCase))
				{
					alvos.Add(domNode);
				}
				foreach (DomNode child in domNode.Children)
				{
					Walk(child);
				}
				if (domNode.ShadowRoot != null)
				{
					Walk(domNode.ShadowRoot);
				}
			}
		}
	}

	private void TryUpgrade(DomNode el)
	{
		if (el.Upgraded || !customElementDefs.TryGetValue(el.TagName, out JsValue value) || el.Attributes.ContainsKey("disable-upgrade"))
		{
			return;
		}
		el.Upgraded = true;
		try
		{
			JsValue jsValue = Wrap(el);
			Function function = engine.Evaluate("\n(function (C, alvo) {\n  try {\n    try { Object.setPrototypeOf(alvo, C.prototype); } catch (e) {}\n    // ES classes exigem `new` e ignoram o `this` passado; funções comuns\n    // (Polymer 3 gera funções) aceitam `apply` com o elemento como `this`.\n    // Detectamos a classe pela FONTE: a regex simples falhava porque o Jint\n    // serializa a classe sem o prefixo esperado.\n    var fonte = '';\n    try { fonte = Function.prototype.toString.call(C) || ''; } catch (e) {}\n    var ehClasse = /^\\s*class[\\s{]/.test(fonte) ||\n                   (!/^\\s*function/.test(fonte) && !/^\\s*\\(?\\s*[\\w$]*\\s*\\)?\\s*=>/.test(fonte) && fonte.indexOf('class') === 0);\n    // Cozinha os métodos/getters da cadeia DELES no alvo: o setPrototypeOf às\n    // vezes não vaza pelos proxies e o `this.x` interno do Polymer não\n    // resolve ready/_attachDom — sem isto o componente conecta mas NUNCA\n    // estampa o template (page-manager sr-null permanente).\n    var cozidos = 0;\n    try {\n      var cr = C.prototype, np = 0;\n      while (cr && cr !== Object.prototype && np < 12) {\n        var ks2 = Object.getOwnPropertyNames(cr);\n        for (var i3 = 0; i3 < ks2.length; i3++) {\n          var k2 = ks2[i3];\n          if (k2 === 'constructor' || k2 === '__proto__') continue;\n          try {\n            if (alvo[k2] !== undefined) continue;\n            var d2 = Object.getOwnPropertyDescriptor(cr, k2);\n            if (d2 && d2.get) { Object.defineProperty(alvo, k2, d2); cozidos++; }\n            else if (d2 && typeof d2.value === 'function') { Object.defineProperty(alvo, k2, { value: d2.value, writable: true, enumerable: false, configurable: true }); cozidos++; }\n          } catch (e11) {}\n        }\n        cr = Object.getPrototypeOf(cr); np++;\n      }\n    } catch (e12) {}\n    var protoSwap = false;\n    try { var protochk = Object.getPrototypeOf(alvo); protoSwap = (protochk === C.prototype); } catch (e13) {}\n    if (!ehClasse) {\n      // Tenta o caminho normal primeiro; se o motor reclamar de classe, cai no new.\n      try { C.apply(alvo, []); return 'ok:' + (protoSwap ? 'proto-ok' : 'proto-fail') + ':cozidos=' + cozidos; }\n      catch (e1) {\n        var msg = (e1 && e1.message) ? String(e1.message) : '';\n        if (msg.indexOf('class') < 0 && msg.indexOf('Class') < 0) return 'erro:' + msg;\n        ehClasse = true;\n      }\n    }\n    if (ehClasse) {\n      // Reflect.construct com newTarget=C cria a instância com o protótipo certo;\n      // copiamos o estado (inclusive herdado) para o proxy do elemento.\n      var r = Reflect.construct(C, [], C);\n      for (var k in r) { try { alvo[k] = r[k]; } catch (e) {} }\n    }\n    return 'ok:' + (protoSwap ? 'proto-ok' : 'proto-fail') + ':cozidos=' + cozidos;\n  } catch (e) { return 'erro:' + (e && e.message ? e.message : e); }\n})") as Function;
			if (function != null)
			{
				string text = function.Call(JsValue.Undefined, new JsValue[2] { value, jsValue })?.ToString() ?? "";
				if (text.StartsWith("erro:"))
				{
					OnLog?.Invoke("customElement " + el.TagName + ": " + text.Substring(5));
				}
				else
				{
					bool flag;
					switch (el.TagName)
					{
					case "ytd-page-manager":
					case "ytd-watch-flexy":
					case "ytd-app":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag)
					{
						OnLog?.Invoke("upgrade-run " + el.TagName + ": " + text);
					}
					try
					{
						string text2 = ((!(engine.Evaluate("(function(el){ try {\n                            return 'ctor=' + (el.constructor && el.constructor.name) +\n                                   ' props=' + Object.keys(el).length +\n                                   ' root=' + (el.root ? 'sim' : 'nao') +\n                                   ' sr=' + (el.shadowRoot ? 'sim' : 'nao');\n                        } catch (e) { return 'erro:' + e.message; } })") is Function value2)) ? null : value2.Call(JsValue.Undefined, new JsValue[1] { jsValue })?.ToString()) ?? "";
						if (text2.Length > 0)
						{
							OnLog?.Invoke("upgrade " + el.TagName + ": " + text2);
						}
					}
					catch
					{
					}
				}
			}
			InvokeLifecycle(el, "connectedCallback");
			FireObservedAttributes(el, value);
		}
		catch (Exception ex)
		{
			OnLog?.Invoke("customElement " + el.TagName + ": " + ex.Message.Replace("\r", "").Split('\n')[0]);
		}
	}

	private void FireObservedAttributes(DomNode el, JsValue ctor)
	{
		try
		{
			Function function = engine.Evaluate("(function(C){\n                try {\n                    if (C && C.observedAttributes) return C.observedAttributes;\n                    if (C && C.prototype && C.prototype.observedAttributes) return C.prototype.observedAttributes;\n                    return [];\n                } catch(e){ return []; }\n            })") as Function;
			if (function == null)
			{
				return;
			}
			JsValue jsValue = function.Call(JsValue.Undefined, new JsValue[1] { ctor });
			if (!(jsValue is JsArray jsArray))
			{
				return;
			}
			foreach (JsValue item in jsArray)
			{
				string text = item.ToString();
				if (!string.IsNullOrEmpty(text) && el.Attributes.ContainsKey(text))
				{
					InvokeLifecycle(el, "attributeChangedCallback", text, JsValue.Null, el.GetAttr(text));
				}
			}
		}
		catch
		{
		}
	}

	private void FireAttributeChanged(DomNode el, JsValue ctor, string nome, string? antigo, string? novo)
	{
		try
		{
			Function function = engine.Evaluate("(function (C, el, nome, antigo, novo) {\n                try {\n                    var obs = (C && (C.observedAttributes || (C.prototype && C.prototype.observedAttributes))) || [];\n                    var achou = false;\n                    for (var i = 0; i < obs.length; i++) if (obs[i] === nome) { achou = true; break; }\n                    if (!achou) return 'ignorado';\n                    var cb = (C.prototype && C.prototype.attributeChangedCallback) || el.attributeChangedCallback;\n                    if (typeof cb === 'function') { cb.call(el, nome, antigo, novo); return 'ok'; }\n                    return 'sem-cb';\n                } catch (e) { return 'erro:' + (e && e.message ? e.message : e); }\n            })") as Function;
			if (!(function == null))
			{
				string text = function.Call(JsValue.Undefined, new JsValue[5]
				{
					ctor,
					Wrap(el),
					nome,
					(antigo == null) ? JsValue.Null : ((JsValue)antigo),
					(novo == null) ? JsValue.Null : ((JsValue)novo)
				})?.ToString() ?? "";
				if (text.StartsWith("erro:"))
				{
					OnLog?.Invoke("attributeChangedCallback(" + el.TagName + "): " + text.Substring(5));
				}
			}
		}
		catch (Exception ex)
		{
			OnLog?.Invoke("attributeChangedCallback(" + el.TagName + "): " + ex.Message.Split('\n')[0]);
		}
	}

	private void InvokeLifecycle(DomNode el, string nome, params JsValue[] extra)
	{
		try
		{
			JsValue jsValue = Wrap(el);
			JsValue value = JsValue.Undefined;
			customElementDefs.TryGetValue(el.TagName, out value);
			Function function = engine.Evaluate("(function (el, ctor, nome, extra) {\n                try {\n                    // 1) protótipo do custom element registrado\n                    if (ctor && ctor.prototype) {\n                        var f = ctor.prototype[nome];\n                        if (typeof f === 'function') return f.apply(el, extra || []);\n                    }\n                    // 2) o próprio elemento\n                    var g = el[nome];\n                    if (typeof g === 'function') return g.apply(el, extra || []);\n                    return undefined;\n                } catch (e) {\n                    // O stack aponta a linha real do bundle: sem ele só se vê a\n                    // mensagem e não dá para saber o que a SPA tentou fazer.\n                    var st = '';\n                    try { st = e && e.stack ? String(e.stack).split('\\n').slice(0, 5).join(' | ') : ''; } catch (e2) {}\n                    return 'erro:' + (e && e.message ? e.message : e) + (st ? '  ~~ ' + st : '');\n                }\n            })") as Function;
			if (!(function == null))
			{
				string text = function.Call(JsValue.Undefined, new JsValue[4]
				{
					jsValue,
					value,
					nome,
					new JsArray(engine, extra)
				})?.ToString() ?? "";
				if (text.StartsWith("erro:"))
				{
					OnLog?.Invoke($"lifecycle {nome}({el.TagName}): {text.Substring(5)}");
				}
			}
		}
		catch (Exception ex)
		{
			OnLog?.Invoke($"lifecycle {nome}({el.TagName}): {ex.Message.Replace("\r", "").Split('\n')[0]}");
		}
	}

	private void InstallWebPlatform()
	{
		engine.SetValue("__ceDefine", new ClrFunction(engine, "__ceDefine", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 2 && args[0].IsString())
			{
				string text = args[0].ToString().ToLowerInvariant();
				customElementDefs[text] = args[1];
				bool flag;
				switch (text)
				{
				case "ytd-app":
				case "ytd-masthead":
				case "ytd-page-manager":
				case "ytd-watch-flexy":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (flag)
				{
					OnLog?.Invoke("define(" + text + ") ok");
				}
				TryUpgradeAll(text);
			}
			return JsValue.Undefined;
		}));
		engine.SetValue("__importSR", new ClrFunction(engine, "__importSR", (JsValue self, JsValue[] args) =>
		{
			if (args.Length < 2 || !args[0].IsObject() || !args[1].IsObject())
			{
				return JsValue.Undefined;
			}
			if (!TryNodeLoose(args[0], out DomNode node) || node == null)
			{
				return JsValue.Undefined;
			}
			if (!(args[1] is ObjectInstance o))
			{
				return JsValue.Undefined;
			}
			if (node.ShadowRoot == null)
			{
				CreateShadowRoot(node, "open");
			}
			DomNode shadowRoot = node.ShadowRoot;
			if (shadowRoot == null)
			{
				return JsValue.Undefined;
			}
			_importados = 0;
			_importadosLista.Clear();
			shadowRoot.Children.Clear();
			int num = ImportTreeNode(o, shadowRoot, 0);
			if (num > 0)
			{
				Dirty = true;
				OnLog?.Invoke($"sombra: <{node.TagName}> nos={num}");
				string text = DumpExpurgo(shadowRoot, 0);
				if (text.Length > 0)
				{
					StringBuilder stringBuilder = new StringBuilder();
					string[] array = text.Split(new string[1] { " ; " }, StringSplitOptions.None);
					foreach (string text2 in array)
					{
						if ((!text2.Contains("(0)") || !text2.Contains("#guide-service")) && (!text2.StartsWith("yt-") || !text2.Contains("(0)")))
						{
							stringBuilder.Append(text2).Append(" ;; ");
						}
					}
					string text3 = stringBuilder.ToString();
					for (int j = 0; j < text3.Length; j += 200)
					{
						OnLog?.Invoke("sombra-tree: " + text3.Substring(j, Math.Min(200, text3.Length - j)));
					}
				}
				int num2 = 0;
				StringBuilder stringBuilder2 = new StringBuilder();
				foreach (DomNode importadosListum in _importadosLista)
				{
					if (!customElementDefs.ContainsKey(importadosListum.TagName))
					{
						string tagName = importadosListum.TagName;
						if ((tagName == "ytd-page-manager" || tagName == "ytd-watch-flexy") ? true : false)
						{
							OnLog?.Invoke("import-def-faltando: " + importadosListum.TagName);
						}
					}
					else if (!importadosListum.Upgraded)
					{
						try
						{
							TryUpgrade(importadosListum);
							num2++;
							stringBuilder2.Append(importadosListum.TagName).Append(' ');
						}
						catch (Exception ex)
						{
							string tagName = importadosListum.TagName;
							if ((tagName == "ytd-page-manager" || tagName == "ytd-watch-flexy") ? true : false)
							{
								OnLog?.Invoke("import-up-erro: " + importadosListum.TagName + ": " + ex.Message);
							}
						}
					}
				}
				_importadosLista.Clear();
				if (num2 > 0)
				{
					OnLog?.Invoke($"sombra-upgrade: {num2} [{stringBuilder2}]");
				}
				try
				{
					StringBuilder ids = new StringBuilder();
					VarreIds(node);
					if (ids.Length > 0)
					{
						OnLog?.Invoke("sombra-ids: " + ids.ToString().Substring(0, Math.Min(400, ids.Length)));
					}
					void VarreIds(DomNode x)
					{
						if (x.Type == NodeType.Element)
						{
							string attr = x.GetAttr("id");
							if (attr.Length > 0)
							{
								ids.Append(attr).Append(' ');
							}
						}
						foreach (DomNode child in x.Children)
						{
							VarreIds(child);
						}
						if (x.ShadowRoot != null)
						{
							VarreIds(x.ShadowRoot);
						}
					}
				}
				catch
				{
				}
				try
				{
					List<DomNode> recon = new List<DomNode>();
					Caminha(node);
					foreach (DomNode item in recon)
					{
						try
						{
							InvokeLifecycle(item, "connectedCallback");
						}
						catch
						{
						}
					}
					if (recon.Count > 0)
					{
						Dirty = true;
						OnLog?.Invoke($"sombra-reconect: {recon.Count}");
					}
					void Caminha(DomNode x)
					{
						if (x.Type == NodeType.Element && x.Upgraded && (x.TagName.StartsWith("ytd-", StringComparison.Ordinal) || x.TagName.StartsWith("yt-", StringComparison.Ordinal)))
						{
							recon.Add(x);
						}
						foreach (DomNode child2 in x.Children)
						{
							Caminha(child2);
						}
						if (x.ShadowRoot != null)
						{
							Caminha(x.ShadowRoot);
						}
					}
				}
				catch
				{
				}
			}
			return (num > 0) ? ((JsValue)true) : ((JsValue)false);
		}));
		engine.SetValue("__srFilhos", new ClrFunction(engine, "__srFilhos", (JsValue self, JsValue[] args) => (args.Length >= 1 && TryNodeLoose(args[0], out DomNode node) && node?.ShadowRoot != null) ? WrapAll(node.ShadowRoot.Children) : new JsArray(engine, Array.Empty<JsValue>())));
		engine.SetValue("__idsOrfas", new ClrFunction(engine, "__idsOrfas", (JsValue self, JsValue[] args) =>
		{
			List<JsValue> list = new List<JsValue>();
			List<DomNode> list2;
			lock (_orfasLock)
			{
				list2 = _orfas.ToList();
			}
			foreach (DomNode item2 in list2)
			{
				DomNode domNode = item2;
				while (domNode.Parent != null)
				{
					domNode = domNode.Parent;
				}
				if (!(domNode.TagName == "#document"))
				{
					list.Add(Wrap(item2));
				}
			}
			return new JsArray(engine, list.ToArray());
		}));
		engine.SetValue("__noPor", new ClrFunction(engine, "__noPor", (JsValue self, JsValue[] args) =>
		{
			if (args.Length < 1 || !args[0].IsNumber())
			{
				return JsValue.Null;
			}
			int key = (int)args[0].AsNumber();
			Binding value;
			return (!bindings.TryGetValue(key, out value) || value.Node == null) ? JsValue.Null : Wrap(value.Node);
		}));
		engine.SetValue("__findId", new ClrFunction(engine, "__findId", (JsValue self, JsValue[] args) =>
		{
			if (args.Length < 2 || !args[1].IsString())
			{
				return JsValue.Null;
			}
			if (!bindings.TryGetValue((int)(args[0].IsNumber() ? args[0].AsNumber() : (-1.0)), out Binding value) || value.Node == null)
			{
				return JsValue.Null;
			}
			string text = args[1].AsString();
			string text2 = Norm(text);
			List<DomNode> list = new List<DomNode>();
			if (value.Node.ShadowRoot != null)
			{
				list.Add(value.Node.ShadowRoot);
			}
			foreach (DomNode child3 in value.Node.Children)
			{
				if (child3.ShadowRoot != null)
				{
					list.Add(child3.ShadowRoot);
				}
			}
			foreach (DomNode item3 in list)
			{
				foreach (DomNode item4 in Descendants(item3))
				{
					if (item4.Type == NodeType.Element)
					{
						string attr = item4.GetAttr("id");
						if (attr.Length != 0)
						{
							bool flag = string.Equals(attr, text, StringComparison.Ordinal);
							bool flag2 = Norm(attr) == text2;
							if (flag || flag2)
							{
								string tagName = item4.TagName;
								bool flag3 = tagName.Contains('-') && (tagName.StartsWith("ytd-", StringComparison.Ordinal) || tagName.StartsWith("yt-", StringComparison.Ordinal) || tagName.StartsWith("tp-", StringComparison.Ordinal));
								bool flag4 = text.Length >= 6;
								if (flag3 | flag4)
								{
									return Wrap(item4);
								}
							}
						}
					}
				}
			}
			return JsValue.Null;
		}));
		engine.SetValue("__logInfo", new ClrFunction(engine, "__logInfo", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 1)
			{
				OnLog?.Invoke("varre-detalhe: " + args[0].ToString());
			}
			return JsValue.Undefined;
		}));
		engine.SetValue("__ceHas", new ClrFunction(engine, "__ceHas", (JsValue self, JsValue[] args) => args.Length >= 1 && args[0].IsString() && customElementDefs.ContainsKey(args[0].AsString())));
		engine.SetValue("__ceGet", new ClrFunction(engine, "__ceGet", (JsValue self, JsValue[] args) => (args.Length >= 1 && args[0].IsString() && customElementDefs.TryGetValue(args[0].ToString(), out JsValue value)) ? value : JsValue.Null));
		engine.SetValue("__shCreate", new ClrFunction(engine, "__shCreate", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 1 && TryNodeLoose(args[0], out DomNode node) && node != null)
			{
				string mode = ((args.Length > 1 && args[1].IsString()) ? args[1].ToString() : "open");
				if (args.Length > 1 && args[1] is ObjectInstance objectInstance)
				{
					JsValue jsValue = objectInstance.Get("mode");
					if ((object)jsValue != null && jsValue.IsString())
					{
						mode = jsValue.ToString();
					}
				}
				DomNode domNode = CreateShadowRoot(node, mode);
				if (domNode != null)
				{
					Dirty = true;
					return Wrap(domNode);
				}
				return Wrap(node.ShadowRoot);
			}
			return JsValue.Null;
		}));
		engine.SetValue("__shGet", new ClrFunction(engine, "__shGet", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 1 && TryNodeLoose(args[0], out DomNode node) && node != null)
			{
				if (node.ShadowRoot == null)
				{
					return JsValue.Null;
				}
				if (string.Equals(node.ShadowRoot.ShadowMode, "closed", StringComparison.OrdinalIgnoreCase))
				{
					return JsValue.Null;
				}
				return Wrap(node.ShadowRoot);
			}
			try
			{
				JsValue jsValue = ((args.Length >= 1) ? args[0] : JsValue.Undefined);
				for (int i = 0; i < 3; i++)
				{
					if (!(jsValue is ObjectInstance objectInstance))
					{
						break;
					}
					JsValue jsValue2 = objectInstance.Get("__id");
					if (jsValue2.IsNumber() && TryNodeLoose(jsValue2, out DomNode node2) && node2 != null && node2.ShadowRoot != null)
					{
						return Wrap(node2.ShadowRoot);
					}
					JsValue jsValue3 = objectInstance.Get("node");
					if (!jsValue3.IsObject())
					{
						break;
					}
					jsValue = jsValue3;
				}
			}
			catch
			{
			}
			try
			{
				JsValue jsValue4 = ((args.Length >= 1) ? args[0] : JsValue.Undefined);
				string text = jsValue4.Type.ToString();
				string text2 = "sem";
				if (jsValue4 is ObjectInstance objectInstance2)
				{
					JsValue jsValue5 = objectInstance2.Get("__id");
					text2 = (jsValue5.IsNumber() ? jsValue5.AsNumber().ToString() : ("(" + jsValue5.Type.ToString() + ")"));
				}
				OnLog?.Invoke("__shGet nao resolveu: tipo=" + text + " __id=" + text2);
			}
			catch
			{
			}
			return JsValue.Null;
		}));
		engine.Evaluate("\n(function () {\n  // ---- construtores de DOM: permitem `class X extends HTMLElement` ----\n  function mkCtor(name) {\n    const C = function () { this.__el = true; };\n    C.prototype = Object.create(Object.prototype);\n    C.prototype.constructor = C;\n    // attachShadow vai direto para a ponte C# (shadow root REAL na árvore)\n    C.prototype.attachShadow = function (init) {\n      const mode = (init && init.mode) ? init.mode : 'open';\n      const sr = globalThis.__shCreate(this, mode);\n      return sr;\n    };\n    C.prototype.connectedCallback = function () {};\n    C.prototype.disconnectedCallback = function () {};\n    C.prototype.attributeChangedCallback = function () {};\n    C.prototype.observedAttributes = [];\n    Object.defineProperty(C.prototype, 'shadowRoot', {\n      get: function () { return globalThis.__shGet(this); }, configurable: true\n    });\n    C.prototype.attachInternals = function () { return { states: new Set(), setFormValue: function(){}, setValidity: function(){} }; };\n    C.prototype.getAttribute = function (k) { return globalThis.__get(this, k); };\n    C.prototype.setAttribute = function (k, v) { globalThis.__set(this, k, v); };\n    C.prototype.removeAttribute = function (k) { globalThis.__set(this, k, null); };\n    C.prototype.hasAttribute = function (k) { return globalThis.__get(this, k) !== undefined && globalThis.__get(this, k) !== null && globalThis.__get(this, k) !== ''; };\n    C.prototype.appendChild = function (c) { return globalThis.__call(this, 'appendChild', [c]); };\n    C.prototype.insertBefore = function (c, r) { return globalThis.__call(this, 'insertBefore', [c, r]); };\n    C.prototype.replaceChild = function (c, r) { return globalThis.__call(this, 'replaceChild', [c, r]); };\n    C.prototype.removeChild = function (c) { return globalThis.__call(this, 'removeChild', [c]); };\n    C.prototype.append = function () { return globalThis.__call(this, 'append', [].slice.call(arguments)); };\n    C.prototype.prepend = function () { return globalThis.__call(this, 'prepend', [].slice.call(arguments)); };\n    C.prototype.after = function () { return globalThis.__call(this, 'after', [].slice.call(arguments)); };\n    C.prototype.before = function () { return globalThis.__call(this, 'before', [].slice.call(arguments)); };\n    C.prototype.replaceWith = function () { return globalThis.__call(this, 'replaceWith', [].slice.call(arguments)); };\n    C.prototype.addEventListener = function (t, f, o) { return globalThis.__call(this, 'addEventListener', [t, f, o]); };\n    C.prototype.removeEventListener = function (t, f, o) { return globalThis.__call(this, 'removeEventListener', [t, f, o]); };\n    C.prototype.dispatchEvent = function (e) { return globalThis.__call(this, 'dispatchEvent', [e]); };\n    C.prototype.querySelector = function (s) { return globalThis.__call(this, 'querySelector', [s]); };\n    C.prototype.querySelectorAll = function (s) { return globalThis.__call(this, 'querySelectorAll', [s]); };\n    C.prototype.getRootNode = function () { return globalThis.__call(this, 'getRootNode', []); };\n    Object.defineProperty(C.prototype, 'nodeType', { get: function () { return 1; }, configurable: true });\n    Object.defineProperty(C.prototype, 'tagName', {\n      get: function () { return (globalThis.__get(this, 'tagName') || 'DIV'); }, configurable: true\n    });\n    Object.defineProperty(C.prototype, 'innerHTML', {\n      get: function () { return globalThis.__get(this, 'innerHTML'); },\n      set: function (v) { globalThis.__set(this, 'innerHTML', v); },\n      configurable: true\n    });\n    Object.defineProperty(C.prototype, 'textContent', {\n      get: function () { return globalThis.__get(this, 'textContent'); },\n      set: function (v) { globalThis.__set(this, 'textContent', v); },\n      configurable: true\n    });\n    Object.defineProperty(C, 'name', { value: name, configurable: true });\n    return C;\n  }\n  const names = ['EventTarget','Node','Element','HTMLElement','HTMLDivElement','HTMLSpanElement',    'HTMLInputElement','HTMLAnchorElement','HTMLImageElement','HTMLVideoElement','HTMLAudioElement',\n    'HTMLCanvasElement','HTMLTemplateElement','HTMLSlotElement','HTMLButtonElement','HTMLFormElement',\n    'HTMLSelectElement','HTMLTextAreaElement','HTMLStyleElement','HTMLScriptElement','HTMLLinkElement',\n    'HTMLIFrameElement','DocumentFragment','ShadowRoot','Text','Comment','Document','Window','CustomEvent'];\n  for (const n of names) globalThis[n] = mkCtor(n);\n  // mapa tag → classe específica (como no browser)\n  globalThis.__classForTag = function (tag) {\n    const m = { div:'HTMLDivElement', span:'HTMLSpanElement', input:'HTMLInputElement',\n      a:'HTMLAnchorElement', img:'HTMLImageElement', video:'HTMLVideoElement',\n      audio:'HTMLAudioElement', canvas:'HTMLCanvasElement', template:'HTMLTemplateElement',\n      slot:'HTMLSlotElement', button:'HTMLButtonElement', form:'HTMLFormElement',\n      select:'HTMLSelectElement', textarea:'HTMLTextAreaElement', style:'HTMLStyleElement',\n      script:'HTMLScriptElement', link:'HTMLLinkElement', iframe:'HTMLIFrameElement' };\n    return globalThis[m[String(tag).toLowerCase()]] || globalThis.HTMLElement;\n  };\n  // ---- Construtores de Evento que os bundles instanciam com `new` ----\n  // Cada um aceita (type, init) e carrega os campos usuais do init.\n  (function () {\n    var campos = ['bubbles','cancelable','composed','detail','view','button','buttons',\n                  'clientX','clientY','screenX','screenY','pageX','pageY','offsetX','offsetY',\n                  'ctrlKey','shiftKey','altKey','metaKey','relatedTarget','deltaX','deltaY',\n                  'deltaZ','deltaMode','key','code','repeat','location','charCode','keyCode',\n                  'which','data','inputType','dataTransfer','touches','targetTouches',\n                  'changedTouches','pointerId','pointerType','width','height','isPrimary',\n                  'pressure','tangentialPressure','tiltX','tiltY','twist','oldURL','newURL',\n                  'persisted','state','reason','lengthComputable','loaded','total','message','error','filename','lineno','colno'];\n    function mkEvt(nome) {\n      var C = function (type, init) {\n        init = init || {};\n        this.type = type === undefined ? '' : String(type);\n        this.isTrusted = false;\n        this.defaultPrevented = false;\n        this.target = null; this.currentTarget = null; this.srcElement = null;\n        this.timeStamp = Date.now(); this.eventPhase = 0; this.cancelBubble = false;\n        for (var i = 0; i < campos.length; i++) {\n          var k = campos[i];\n          this[k] = (k in init) ? init[k] : (k === 'bubbles' || k === 'cancelable' ? false : null);\n        }\n        this.bubbles = !!init.bubbles; this.cancelable = !!init.cancelable;\n        this.composed = !!init.composed;\n        this.detail = ('detail' in init) ? init.detail : null;\n        this.preventDefault = function () { this.defaultPrevented = true; };\n        this.stopPropagation = function () { this.cancelBubble = true; };\n        this.stopImmediatePropagation = function () { this.cancelBubble = true; };\n        this.initEvent = function (t, b, c) { this.type = t; this.bubbles = !!b; this.cancelable = !!c; };\n        this.initCustomEvent = function (t, b, c, d) { this.type = t; this.bubbles = !!b; this.cancelable = !!c; this.detail = d; };\n        this.composedPath = function () { return this.target ? [this.target] : []; };\n      };\n      try { Object.defineProperty(C, 'name', { value: nome, configurable: true }); } catch (e) {}\n      C.prototype.constructor = C;\n      return C;\n    }\n    var eventos = ['Event','UIEvent','MouseEvent','PointerEvent','KeyboardEvent','TouchEvent',\n      'FocusEvent','InputEvent','CompositionEvent','WheelEvent','DragEvent','ClipboardEvent',\n      'AnimationEvent','TransitionEvent','ProgressEvent','ErrorEvent','MessageEvent',\n      'HashChangeEvent','PopStateEvent','BeforeUnloadEvent','PageTransitionEvent',\n      'StorageEvent','SubmitEvent','GamepadEvent','DeviceMotionEvent','DeviceOrientationEvent'];\n    for (var i = 0; i < eventos.length; i++) {\n      var n = eventos[i];\n      if (!globalThis[n]) {\n        globalThis[n] = mkEvt(n);\n        // MouseEvent/PointerEvent herdam de UIEvent -> Event (cadeia simples)\n        if (n !== 'Event') { try { globalThis[n].prototype = Object.create(globalThis.Event.prototype); globalThis[n].prototype.constructor = globalThis[n]; } catch (e) {} }\n      }\n    }\n    // MouseEvent tem constantes de botão usadas por handlers\n    try {\n      globalThis.MouseEvent.NONE = 0; globalThis.MouseEvent.LEFT = 0;\n      globalThis.MouseEvent.MIDDLE = 1; globalThis.MouseEvent.RIGHT = 2;\n    } catch (e) {}\n    // CustomEvent PRECISA carregar type/detail: mkCtor criava um construtor vazio\n    // e `new CustomEvent('x')` saía com type undefined, então dispatchEvent não\n    // achava o tipo e nenhum listener disparava.\n    (function () {\n      var CE = function (type, init) {\n        init = init || {};\n        this.type = type === undefined ? '' : String(type);\n        this.bubbles = !!init.bubbles;\n        this.cancelable = !!init.cancelable;\n        this.composed = !!init.composed;\n        this.detail = ('detail' in init) ? init.detail : null;\n        this.isTrusted = false; this.defaultPrevented = false;\n        this.target = null; this.currentTarget = null;\n        this.timeStamp = Date.now(); this.cancelBubble = false;\n        this.preventDefault = function () { this.defaultPrevented = true; };\n        this.stopPropagation = function () { this.cancelBubble = true; };\n        this.stopImmediatePropagation = function () { this.cancelBubble = true; };\n        this.initCustomEvent = function (t, b, c, d) { this.type = t; this.bubbles = !!b; this.cancelable = !!c; this.detail = d; };\n        this.initEvent = function (t, b, c) { this.type = t; this.bubbles = !!b; this.cancelable = !!c; };\n        this.composedPath = function () { return this.target ? [this.target] : []; };\n      };\n      try { Object.defineProperty(CE, 'name', { value: 'CustomEvent', configurable: true }); } catch (e) {}\n      CE.prototype = Object.create(globalThis.Event ? globalThis.Event.prototype : Object.prototype);\n      CE.prototype.constructor = CE;\n      globalThis.CustomEvent = CE;\n    })();\n  })();\n\n  // ---- Construtores/globalstub que os bundles referenciam (X.prototype / new X) ----\n  (function () {\n    function stub(nome) {\n      var C = function () { };\n      try { Object.defineProperty(C, 'name', { value: nome, configurable: true }); } catch (e) {}\n      return C;\n    }\n    var faltantes = [\n      'HTMLCollection','NodeList','DOMTokenList','NamedNodeMap','Attr','CharacterData',\n      'ProcessingInstruction','DocumentType','CDATASection','EntityReference','Range',\n      'Selection','TreeWalker','NodeIterator','StaticRange','AbstractRange',\n      'MediaStream','MediaStreamTrack','MediaSource','SourceBuffer','SourceBufferList',\n      'TextTrack','TextTrackList','TextTrackCue','TextTrackCueList','VTTCue','AudioTrackList','VideoTrackList',\n      'WebSocket','Worker','SharedWorker','ServiceWorker','BroadcastChannel','MessageChannel','MessagePort',\n      'XMLHttpRequest','XMLHttpRequestEventTarget','FormData','FileReader','File','FileList','Blob','URL',\n      'Notification','Geolocation','Permissions','Clipboard','ClipboardItem','PaymentRequest',\n      'SpeechSynthesis','SpeechSynthesisUtterance','SpeechRecognition','webkitSpeechRecognition',\n      'AudioContext','webkitAudioContext','OfflineAudioContext','GainNode','AnalyserNode','AudioBuffer',\n      'MediaQueryList','MediaQueryListEvent','Screen','ScreenOrientation','VisualViewport',\n      'History','Location','Navigator','NavigatorUAData','PluginArray','MimeTypeArray',\n      'CSSStyleSheet','CSSStyleRule','CSSRuleList','StyleSheetList','MediaList','CSS','CSSCounterStyleRule',\n      'CSSStyleDeclaration','CSSKeyframesRule','CSSFontFaceRule','CSSMediaRule','CSSSupportsRule',\n      'ImageData','Path2D','CanvasRenderingContext2D','OffscreenCanvas','WebGLRenderingContext',\n      'ImageBitmap','VideoFrame','AudioData','ResizeObserverEntry','IntersectionObserverEntry',\n      'MutationRecord','MutationObserver','WebKitMutationObserver','IdleDeadline',\n      'DOMRect','DOMRectReadOnly','DOMRectList','DOMQuad','DOMPoint','DOMMatrix','DOMMatrixReadOnly',\n      'XPathResult','XPathEvaluator','XPathExpression','XSLTProcessor',\n      'EventSource','AbortSignal','AbortController','DOMException','CustomElementRegistry',\n      'HTMLUnknownElement','HTMLSlotElement','HTMLTemplateElement','CSSStyleSheetInit',\n      'TrustedHTML','TrustedScript','TrustedScriptURL','TrustedTypePolicy','TrustedTypePolicyFactory',\n      'Highlight','HighlightRegistry','EditContext','Sanitizer','ReportingObserver','LayoutShift',\n      'TaskController','TaskPriorityChangeEvent','Scheduler','UserActivation','Navigation','NavigationHistoryEntry'\n    ];\n    for (var i = 0; i < faltantes.length; i++) {\n      var n = faltantes[i];\n      if (typeof globalThis[n] === 'undefined') globalThis[n] = stub(n);\n    }\n    // classes de elemento por tag (o bundle faz `X.prototype` de várias)\n    var els = ['HTMLHtmlElement','HTMLHeadElement','HTMLBodyElement','HTMLTitleElement','HTMLMetaElement',\n      'HTMLBaseElement','HTMLLabelElement','HTMLLegendElement','HTMLFieldSetElement','HTMLOptionElement',\n      'HTMLOptGroupElement','HTMLDataElement','HTMLTimeElement','HTMLMeterElement','HTMLProgressElement',\n      'HTMLOutputElement','HTMLDetailsElement','HTMLDialogElement','HTMLPictureElement','HTMLSourceElement',\n      'HTMLTrackElement','HTMLMapElement','HTMLAreaElement','HTMLModElement','HTMLQuoteElement',\n      'HTMLTableElement','HTMLTableRowElement','HTMLTableCellElement','HTMLTableSectionElement',\n      'HTMLTableCaptionElement','HTMLTableColElement','HTMLOListElement','HTMLUListElement','HTMLLIElement',\n      'HTMLDListElement','HTMLHeadingElement','HTMLParagraphElement','HTMLPreElement','HTMLBRElement',\n      'HTMLHRElement','HTMLDivElement','HTMLSpanElement','HTMLAnchorElement','HTMLImageElement',\n      'HTMLIFrameElement','HTMLEmbedElement','HTMLObjectElement','HTMLParamElement','HTMLScriptElement',\n      'HTMLStyleElement','HTMLLinkElement','HTMLMediaElement','HTMLVideoElement','HTMLAudioElement',\n      'HTMLInputElement','HTMLTextAreaElement','HTMLButtonElement','HTMLSelectElement','HTMLFormElement',\n      'HTMLCanvasElement','HTMLFontElement','HTMLFrameElement','HTMLFrameSetElement','HTMLMarqueeElement',\n      'HTMLMenuElement','HTMLShadowElement','HTMLContentElement','HTMLDirectoryElement'];\n    for (var j = 0; j < els.length; j++) {\n      var en = els[j];\n      if (typeof globalThis[en] === 'undefined') globalThis[en] = stub(en);\n    }\n  })();\n\n  // ---- Correção de conformidade: Array.prototype.concat e os furos ----\n  // Jint 3.0.0 materializa os buracos (holes) de Array(n) durante o concat.\n  // Spec: Array(3) tem length 3 e ZERO elementos, então Array(3).concat('a')\n  // deve ser ['a'] — e não [undefined,undefined,undefined,'a'].\n  // Bug real e fatal: o YouTube monta paletas com\n  //   Array(3).concat('d8b365f5f5f55ab4ac', ...).map(Iw)\n  // e Iw faz `Wy.length` — recebendo undefined, o bundle base-js inteiro morre\n  // (Cannot read property 'length' of undefined) e a SPA nunca registra ytd-*.\n  (function () {\n    var origConcat = Array.prototype.concat;\n    Array.prototype.concat = function () {\n      var args = Array.prototype.slice.call(arguments);\n      var saida = origConcat.apply(this, args);\n      // Remove posições que vieram de buracos em QUALQUER array de entrada.\n      // Reconstruímos com push apenas dos índices realmente presentes.\n      var resultado = [];\n      for (var i = 0; i < args.length; i++) {\n        var a = args[i];\n        if (Array.isArray(a)) {\n          for (var j = 0; j < a.length; j++)\n            if (j in a) resultado[resultado.length] = a[j];\n        } else {\n          resultado[resultado.length] = a;\n        }\n      }\n      // `this` também pode ter buracos (ex.: Array(3).concat(x))\n      if (Array.isArray(this)) {\n        var comThis = [];\n        for (var k = 0; k < this.length; k++)\n          if (k in this) comThis[comThis.length] = this[k];\n        comThis.push.apply(comThis, resultado);\n        return comThis;\n      }\n      return resultado;\n    };\n  })();\n\n  // ---- NodeFilter: constantes usadas por createTreeWalker/createNodeIterator ----\n  globalThis.NodeFilter = {\n    FILTER_ACCEPT: 1, FILTER_REJECT: 2, FILTER_SKIP: 3,\n    SHOW_ALL: 0xFFFFFFFF, SHOW_ELEMENT: 0x1, SHOW_ATTRIBUTE: 0x2, SHOW_TEXT: 0x4,\n    SHOW_CDATA_SECTION: 0x8, SHOW_ENTITY_REFERENCE: 0x10, SHOW_ENTITY: 0x20,\n    SHOW_PROCESSING_INSTRUCTION: 0x40, SHOW_COMMENT: 0x80, SHOW_DOCUMENT: 0x100,\n    SHOW_DOCUMENT_TYPE: 0x200, SHOW_DOCUMENT_FRAGMENT: 0x400, SHOW_NOTATION: 0x800\n  };\n  globalThis.Node = globalThis.Node || {};\n  try {\n    globalThis.Node.ELEMENT_NODE = 1; globalThis.Node.ATTRIBUTE_NODE = 2;\n    globalThis.Node.TEXT_NODE = 3; globalThis.Node.CDATA_SECTION_NODE = 4;\n    globalThis.Node.PROCESSING_INSTRUCTION_NODE = 7; globalThis.Node.COMMENT_NODE = 8;\n    globalThis.Node.DOCUMENT_NODE = 9; globalThis.Node.DOCUMENT_TYPE_NODE = 10;\n    globalThis.Node.DOCUMENT_FRAGMENT_NODE = 11;\n    globalThis.Node.DOCUMENT_POSITION_DISCONNECTED = 1;\n    globalThis.Node.DOCUMENT_POSITION_PRECEDING = 2;\n    globalThis.Node.DOCUMENT_POSITION_FOLLOWING = 4;\n    globalThis.Node.DOCUMENT_POSITION_CONTAINS = 8;\n    globalThis.Node.DOCUMENT_POSITION_CONTAINED_BY = 16;\n    globalThis.Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC = 32;\n  } catch (e) {}\n\n  // ShadyDOM: MANTEVE-SE no modo do bundle (inUse=true). Teste medido: forçar\n  // native (inUse=false) faz o polymer NEM REGISTRAR os componentes (348\n  // elementos, nenhum define()), enquanto no modo shady ytd-app sobe com 473\n  // propriedades. O custo do modo shady é o conteúdo viver na árvore interna\n  // dele; caminho de lintura animado na próxima rodada.\n\n\n  // ---- cadeia de protótipos real: Element/Node/EventTarget compartilham a base ----\n  // A ORDEM importa: EventTarget é a raiz da cadeia, então precisa ser montado\n  // ANTES de Node (que herda dele). Inverter isso deixava Node.prototype\n  // apontando para um EventTarget.prototype já descartado.\n  globalThis.EventTarget.prototype = Object.create(globalThis.HTMLElement.prototype);\n  globalThis.Node.prototype = Object.create(globalThis.EventTarget.prototype);\n  globalThis.Element.prototype = Object.create(globalThis.Node.prototype);\n  (function () {\n    // marcadores que o ShadyDOM consulta\n    if (!globalThis.ShadyDOM) globalThis.ShadyDOM = { inUse: false, preferPerformance: true };\n    if (!globalThis.ShadyCSS) globalThis.ShadyCSS = { nativeCss: true, nativeShadow: true, prepareTemplate: function(){}, styleElement: function(){}, styleSubtree: function(){}, styleDocument: function(){}, getComputedStyleValue: function(){ return String.fromCharCode(0).slice(0,0); }, flush: function(){} };\n  })();\n  // Os aliases __shady_native_* precisam ser instalados DEPOIS de toda a\n  // fiação de protótipos: ShadowRoot/DocumentFragment.prototype são substituídos\n  // mais abaixo e isso descartaria os aliases se fossem definidos antes.\n  globalThis.__installShadyNative = function () {\n    var nomes = ['addEventListener','removeEventListener','dispatchEvent','appendChild',\n      'insertBefore','removeChild','replaceChild','setAttribute','getAttribute',\n      'removeAttribute','hasAttribute','getAttributeNames','toggleAttribute','cloneNode',\n      'querySelector','querySelectorAll','getElementsByTagName','getElementsByClassName',\n      'matches','closest','contains','getRootNode','attachShadow','textContent','innerHTML',\n      'parentNode','parentElement','firstChild','lastChild','nextSibling','previousSibling',\n      'children','childNodes','firstElementChild','lastElementChild','assignedNodes',\n      // ShadyDOM tambem guarda versoes nativas destes; sem __shady_attachShadow o\n      // connectedCallback do ytd-app morria e a SPA nunca criava o ytd-page-manager.\n      'attachShadow','getRootNode','slot','assignedSlot','shadowRoot','append','prepend',\n      'insertAdjacentHTML','innerText','outerHTML','focus','blur','click','remove',\n      'replaceWith','replaceChildren','before','after','removeChild','isConnected'];\n    var protos = ['HTMLElement','Element','Node','EventTarget','ShadowRoot','DocumentFragment'];\n    for (var i = 0; i < nomes.length; i++) {\n      var p = nomes[i];\n      for (var j = 0; j < protos.length; j++) {\n        var C = globalThis[protos[j]];\n        var pr = C && C.prototype;\n        if (!pr) continue;\n        if (typeof pr['__shady_native_' + p] !== 'function') {\n          try {\n            Object.defineProperty(pr, '__shady_native_' + p, {\n              configurable: true, writable: true,\n              value: function (nome) {\n                return function () { return globalThis.__call(this, nome, Array.prototype.slice.call(arguments)); };\n              }(p)\n            });\n          } catch (e) {}\n        }\n      }\n    }\n    // O bundle webcomponents-sd chama window.__shady_native_addEventListener\n    // (função Gb: itera os eventos e registra no window), então o próprio\n    // globalThis também precisa dos aliases — não só os protótipos.\n    for (var k = 0; k < nomes.length; k++) {\n      var pn = nomes[k];\n      if (typeof globalThis['__shady_native_' + pn] !== 'function') {\n        try {\n          Object.defineProperty(globalThis, '__shady_native_' + pn, {\n            configurable: true, writable: true,\n            value: function (nome) {\n              return function () { return globalThis.__call(globalThis, nome, Array.prototype.slice.call(arguments)); };\n            }(pn)\n          });\n        } catch (e) {}\n      }\n    }\n  };\n  globalThis.Element.prototype.constructor = globalThis.Element;\n  globalThis.Node.prototype.constructor = globalThis.Node;\n  globalThis.EventTarget.prototype.constructor = globalThis.EventTarget;\n  // ShadowRoot/DocumentFragment herdam de DocumentFragment\n  globalThis.ShadowRoot.prototype = Object.create(globalThis.HTMLElement.prototype);\n  globalThis.ShadowRoot.prototype.constructor = globalThis.ShadowRoot;\n  globalThis.DocumentFragment.prototype = Object.create(globalThis.HTMLElement.prototype);\n  globalThis.DocumentFragment.prototype.constructor = globalThis.DocumentFragment;\n  // SÓ AGORA os aliases do ShadyDOM: os protótipos acima acabaram de ser\n  // substituídos, então instalar antes descartaria todos eles.\n  globalThis.__installShadyNative();\n\n  // ---- customElements ----\n  const registry = {};\n  globalThis.customElements = {\n    define: function (name, ctor, opts) {\n      registry[String(name).toLowerCase()] = ctor;\n      globalThis.__ceDefine(String(name).toLowerCase(), ctor);\n    },\n    get: function (name) { return registry[String(name).toLowerCase()]; },\n    whenDefined: function (name) {\n      return Promise.resolve(registry[String(name).toLowerCase()]);\n    },\n    upgrade: function () {}\n  };\n\n  // ---- observers ausentes ----\n  function MkObserver(cb) { this._cb = cb; this._targets = []; }\n  MkObserver.prototype.observe = function (t) { this._targets.push(t); };\n  MkObserver.prototype.unobserve = function () {};\n  MkObserver.prototype.disconnect = function () { this._targets = []; };\n  MkObserver.prototype.takeRecords = function () { return []; };\n  globalThis.ResizeObserver = MkObserver;\n  globalThis.MutationObserver = globalThis.MutationObserver || MkObserver;\n  globalThis.IntersectionObserver = globalThis.IntersectionObserver || MkObserver;\n  globalThis.PerformanceObserver = MkObserver;\n\n  // ---- BOM/AJAX auxiliares ----\n  globalThis.dispatchEvent = function () { return true; };\n  globalThis.queueMicrotask = globalThis.queueMicrotask || function (f) { Promise.resolve().then(f); };\n  globalThis.requestIdleCallback = globalThis.requestIdleCallback || function (f) {\n    return setTimeout(function () { f({ didTimeout: false, timeRemaining: function () { return 50; } }); }, 1);\n  };\n  globalThis.cancelIdleCallback = globalThis.cancelIdleCallback || function (id) { clearTimeout(id); };\n  globalThis.structuredClone = globalThis.structuredClone || function (o) { return JSON.parse(JSON.stringify(o)); };\n  globalThis.URLSearchParams = globalThis.URLSearchParams || function (q) {\n    const m = {}; String(q || '').replace(/^\\?/, '').split('&').forEach(function (kv) {\n      if (!kv) return; const i = kv.indexOf('='); const k = i < 0 ? kv : kv.slice(0, i);\n      m[decodeURIComponent(k)] = i < 0 ? '' : decodeURIComponent(kv.slice(i + 1).replace(/\\+/g, ' '));\n    });\n    this._m = m;\n    this.get = function (k) { return Object.prototype.hasOwnProperty.call(m, k) ? m[k] : null; };\n    this.has = function (k) { return Object.prototype.hasOwnProperty.call(m, k); };\n    this.set = function (k, v) { m[k] = String(v); };\n    this.append = function (k, v) { m[k] = String(v); };\n    this.delete = function (k) { delete m[k]; };\n    this.toString = function () {\n      return Object.keys(m).map(function (k) { return encodeURIComponent(k) + '=' + encodeURIComponent(m[k]); }).join('&');\n    };\n    this.forEach = function (f) { Object.keys(m).forEach(function (k) { f(m[k], k); }); };\n  };\n  globalThis.AbortController = globalThis.AbortController || function () {\n    this.signal = { aborted: false, addEventListener: function () {}, removeEventListener: function () {} };\n    this.abort = function () { this.signal.aborted = true; };\n  };\n  globalThis.Headers = globalThis.Headers || function (h) {\n    this._h = {}; const self = this;\n    if (h) Object.keys(h).forEach(function (k) { self._h[k.toLowerCase()] = h[k]; });\n    this.get = function (k) { return self._h[String(k).toLowerCase()] || null; };\n    this.has = function (k) { return String(k).toLowerCase() in self._h; };\n    this.set = function (k, v) { self._h[String(k).toLowerCase()] = String(v); };\n    this.append = this.set;\n    this.forEach = function (f) { Object.keys(self._h).forEach(function (k) { f(self._h[k], k); }); };\n  };\n  // Intl mínimo: bundles usam Intl.* em formatação\n  globalThis.Intl = globalThis.Intl || {};\n  globalThis.Intl.NumberFormat = globalThis.Intl.NumberFormat || function () {\n    this.format = function (n) { return String(n); };\n    this.formatToParts = function (n) { return [{ type: 'integer', value: String(n) }]; };\n    this.formatRange = function (a) { return String(a) + '–' + String(arguments[1]); };\n    this.resolvedOptions = function () { return { locale: 'pt-BR', numberingSystem: 'latn' }; };\n  };\n  globalThis.Intl.NumberFormat.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.DateTimeFormat = globalThis.Intl.DateTimeFormat || function () {\n    this.format = function (d) { return new Date(d === undefined ? Date.now() : d).toString(); };\n    this.formatToParts = function () { return []; };\n    this.formatRange = function () { return ''; };\n    this.resolvedOptions = function () { return { locale: 'pt-BR', timeZone: 'America/Sao_Paulo' }; };\n  };\n  globalThis.Intl.DateTimeFormat.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.Collator = globalThis.Intl.Collator || function () {\n    this.compare = function (a, b) { return String(a) < String(b) ? -1 : (String(a) > String(b) ? 1 : 0); };\n    this.resolvedOptions = function () { return { locale: 'pt-BR', usage: 'sort', sensitivity: 'variant' }; };\n  };\n  globalThis.Intl.Collator.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.PluralRules = globalThis.Intl.PluralRules || function () {\n    this.select = function (n) { return Number(n) === 1 ? 'one' : 'other'; };\n    this.resolvedOptions = function () { return { locale: 'pt-BR', type: 'cardinal' }; };\n  };\n  globalThis.Intl.PluralRules.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.getCanonicalLocales = globalThis.Intl.getCanonicalLocales || function (l) { return [].concat(l || []); };\n  globalThis.Intl.RelativeTimeFormat = globalThis.Intl.RelativeTimeFormat || function () {\n    this.format = function (v, u) { return String(v) + ' ' + String(u); };\n    this.resolvedOptions = function () { return { locale: 'pt-BR' }; };\n  };\n  globalThis.Intl.RelativeTimeFormat.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.ListFormat = globalThis.Intl.ListFormat || function () {\n    this.format = function (a) { return [].concat(a || []).join(', '); };\n    this.resolvedOptions = function () { return { locale: 'pt-BR' }; };\n  };\n  globalThis.Intl.ListFormat.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.DisplayNames = globalThis.Intl.DisplayNames || function () {\n    this.of = function (c) { return String(c); };\n    this.resolvedOptions = function () { return { locale: 'pt-BR' }; };\n  };\n  globalThis.Intl.DisplayNames.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.Segmenter = globalThis.Intl.Segmenter || function () {\n    this.segment = function (s) { return [{ segment: String(s), index: 0, input: String(s) }]; };\n  };\n  globalThis.Intl.Segmenter.supportedLocalesOf = function (l) { return [].concat(l || []); };\n  globalThis.Intl.Locale = globalThis.Intl.Locale || function (t) {\n    this.toString = function () { return String(t || 'pt-BR'); };\n    this.language = String(t || 'pt-BR').split('-')[0];\n    this.region = 'BR';\n  };\n\n  // ---- AbortSignal/AbortController completos + DOMException ----\n  globalThis.DOMException = globalThis.DOMException || function (msg, name) {\n    const e = new Error(msg); e.name = name || 'Error'; return e;\n  };\n  globalThis.AbortSignal = globalThis.AbortSignal || function () {\n    this.aborted = false; this.reason = undefined;\n    this.addEventListener = function () {}; this.removeEventListener = function () {};\n    this.throwIfAborted = function () {};\n  };\n  globalThis.AbortSignal.abort = function (r) { const s = new globalThis.AbortSignal(); s.aborted = true; s.reason = r; return s; };\n  globalThis.AbortSignal.timeout = function () { return globalThis.AbortSignal.abort(); };\n  globalThis.AbortController = function () {\n    const sig = new globalThis.AbortSignal();\n    this.signal = sig;\n    this.abort = function (r) { sig.aborted = true; sig.reason = r; };\n  };\n\n  // ---- Eventos e misc ----\n  globalThis.Event = globalThis.Event || function (t, o) {\n    o = o || {}; this.type = t; this.bubbles = !!o.bubbles; this.cancelable = !!o.cancelable;\n    this.defaultPrevented = false; this.target = null; this.currentTarget = null;\n    this.preventDefault = function () { this.defaultPrevented = true; };\n    this.stopPropagation = function () {}; this.stopImmediatePropagation = function () {};\n  };\n  globalThis.CustomEvent = globalThis.CustomEvent || globalThis.Event;\n  globalThis.MessageChannel = globalThis.MessageChannel || function () {\n    const self = this;\n    this.port1 = { postMessage: function (m) { setTimeout(function () { self.port2.onmessage && self.port2.onmessage({ data: m }); }, 0); }, onmessage: null, start: function () {}, close: function () {} };\n    this.port2 = { postMessage: function (m) { setTimeout(function () { self.port1.onmessage && self.port1.onmessage({ data: m }); }, 0); }, onmessage: null, start: function () {}, close: function () {} };\n  };\n  globalThis.WeakRef = globalThis.WeakRef || function (o) { this.deref = function () { return o; }; };\n  globalThis.FinalizationRegistry = globalThis.FinalizationRegistry || function () {\n    this.register = function () {}; this.unregister = function () {}; this.cleanupSome = function () {};\n  };\n  globalThis.reportError = globalThis.reportError || function () {};\n  globalThis.matchMedia = globalThis.matchMedia || function (q) {\n    return { matches: false, media: q, addListener: function () {}, removeListener: function () {},\n             addEventListener: function () {}, removeEventListener: function () {} };\n  };\n  globalThis.getComputedStyle = globalThis.getComputedStyle || function () {\n    return { getPropertyValue: function () { return ''; } };\n  };\n  globalThis.scrollTo = globalThis.scrollTo || function () {};\n  globalThis.scrollBy = globalThis.scrollBy || function () {};\n  globalThis.open = globalThis.open || function () { return null; };\n  globalThis.close = globalThis.close || function () {};\n  globalThis.focus = globalThis.focus || function () {};\n  globalThis.blur = globalThis.blur || function () {};\n  globalThis.print = globalThis.print || function () {};\n  globalThis.alert = globalThis.alert || function () {};\n  globalThis.confirm = globalThis.confirm || function () { return false; };\n  globalThis.prompt = globalThis.prompt || function () { return null; };\n  globalThis.Notification = globalThis.Notification || { permission: 'denied', requestPermission: function () { return Promise.resolve('denied'); } };\n  globalThis.navigator = globalThis.navigator || {};\n  globalThis.navigator.sendBeacon = globalThis.navigator.sendBeacon || function () { return true; };\n  globalThis.crypto = globalThis.crypto || {\n    getRandomValues: function (a) { for (let i = 0; i < a.length; i++) a[i] = Math.floor(Math.random() * 256); return a; },\n    randomUUID: function () { return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) { const r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 3 | 8)).toString(16); }); }\n  };\n  globalThis.crypto.subtle = globalThis.crypto.subtle || { digest: function () { return Promise.resolve(new ArrayBuffer(0)); } };\n  globalThis.TextEncoder = globalThis.TextEncoder || function () {\n    this.encode = function (s) { const a = []; for (let i = 0; i < String(s).length; i++) { let c = String(s).charCodeAt(i); if (c < 128) a.push(c); else if (c < 2048) { a.push(192 | c >> 6, 128 | c & 63); } else { a.push(224 | c >> 12, 128 | c >> 6 & 63, 128 | c & 63); } } return new Uint8Array(a); };\n  };\n  globalThis.TextDecoder = globalThis.TextDecoder || function () {\n    this.decode = function (b) { if (!b) return ''; let s = ''; for (let i = 0; i < b.length; i++) s += String.fromCharCode(b[i]); return s; };\n  };\n\n  // ---- DOMParser: frameworks parseiam strings para nós reais ----\n  globalThis.DOMParser = globalThis.DOMParser || function () {\n    this.parseFromString = function (text, type) {\n      // __parseHtml já devolve um documento com html/head/body reais\n      return globalThis.__parseHtml(String(text));\n    };\n    // createHTMLDocument: usado para criar documentos soltos (templates, inertes)\n    this.createHTMLDocument = function (titulo) {\n      var d = globalThis.__parseHtml('<!DOCTYPE html><html><head><title>' +\n        String(titulo == null ? '' : titulo) + '</title></head><body></body></html>');\n      return d;\n    };\n  };\n  // createHTMLDocument também como ESTÁTICO (alguns bundles chamam direto)\n  try { globalThis.DOMParser.createHTMLDocument = function (t) { return new globalThis.DOMParser().createHTMLDocument(t); }; } catch (e) {}\n\n  // ---- Importador de árvore sombreada do ShadyDOM ----\n  // O ShadyDOM do bundle estampa templates numa árvore LÓGICA própria que o\n  // C# não enxerga (o ytd-app montava com 0 de altura). O __shadyExport varre\n  // os elementos, acha a raiz sombreada no objeto cru (__shady_shadowRoot /\n  // __shady_root), serializa e o C# (__importSR) reconstrói em DomNode reais.\n  globalThis.__serializaFilhos = function (nde, prof) {\n    var arr = [];\n    try {\n      var fil = null;\n      try { fil = nde.children || null; } catch (e1) { fil = null; }\n      if (!fil) { try { fil = nde.childNodes || null; } catch (e2) { fil = null; } }\n      if (!fil) return arr;\n      var N = Number(fil.length) | 0;\n      if (N > 300) N = 300;\n      for (var i = 0; i < N; i++) {\n        var f = null;\n        try { f = fil[i]; } catch (e3) { f = null; }\n        if (!f) continue;\n        var ser = globalThis.__serializa(f, prof + 1);\n        if (ser) arr.push(ser);\n      }\n    } catch (e) {}\n    return arr;\n  };\n  globalThis.__serializa = function (nde, prof) {\n    if (!nde || prof > 14) return null;\n    var tipo = 1;\n    try { tipo = Number(nde.nodeType) || 1; } catch (e4) {}\n    if (tipo === 3 || tipo === 8) {\n      var tx = '';\n      try { tx = String(nde.data || nde.textContent || ''); } catch (e5) {}\n      return { t: tipo === 8 ? '#comment' : '#text', x: tx };\n    }\n    var tag = '';\n    try { tag = String(nde.tagName || '').toLowerCase(); } catch (e6) {}\n    if (!tag) {\n      if (tipo === 11) return { t: '#fragment', k: globalThis.__serializaFilhos(nde, prof) };\n      return null;\n    }\n    var iid = ''; try { iid = String(nde.id || ''); } catch (e7) {}\n    var cls = ''; try { cls = String(nde.className || ''); } catch (e8) {}\n    if (!cls) { try { cls = String(nde.classList || ''); } catch (e9) {} }\n    var st = ''; try { st = nde.style && nde.style.cssText ? String(nde.style.cssText) : ''; } catch (e10) {}\n    var ex = '';\n    try {\n      var nomes = nde.getAttributeNames ? nde.getAttributeNames() : null;\n      if (nomes && nomes.length) {\n        for (var j = 0; j < nomes.length && j < 12; j++) {\n          var nm = String(nomes[j]);\n          if (nm === 'id' || nm === 'class' || nm === 'style') continue;\n          var vv = '';\n          try { vv = String(nde.getAttribute(nm)).substring(0, 60); } catch (e11) {}\n          if (vv) ex += (ex ? '|' : '') + nm + ':' + vv;\n        }\n      }\n    } catch (e12) {}\n    // shadow root ANINHADO: page-manager/watch-flexy têm SR próprios dentro do\n    // app. Serializar recursivamente como campo r (o C# cria o SR real no nó).\n    // IMPORTANTE: só `shadowRoot` (nunca `root`, que devolve a raiz do TOPO e\n    // faz cada nó re-serializar a SR inteira — loop exponencial) e apenas em\n    // nós ELEMENT; e nunca a RAIZ DE TOPO em si (__serPerfilDeRaiz).\n    try {\n      if (tipo === 1 && prof < 13) {\n        var sv = null;\n        try { sv = nde.shadowRoot || null; } catch (e20) {}\n        if (sv && sv !== nde && sv.nodeType === 11 && sv !== globalThis.__serPerfilDeRaiz) {\n          var rs = null;\n          try { rs = globalThis.__serializa(sv, prof + 1); } catch (e22) { rs = null; }\n          if (rs) return { t: tag, i: iid, c: cls, s: st, e: ex, k: globalThis.__serializaFilhos(nde, prof), r: rs };\n        }\n      }\n    } catch (e23) {}\n    return { t: tag, i: iid, c: cls, s: st, e: ex, k: globalThis.__serializaFilhos(nde, prof) };\n  };\n  globalThis.__importaOrfas = function () {\n    var feitos = 0;\n    var dg = { chamadas: 0, ids: 0, semNo: 0, semRoot: 0, semFilhos: 0, semAlvo: 0, ultimoErro: '', imp: 0 };\n    try { dg.chamadas = ((globalThis.__orfaDiag && globalThis.__orfaDiag.chamadas) || 0) + 1; } catch (ed) {}\n    try {\n      var ids = typeof globalThis.__idsOrfas === 'function' ? globalThis.__idsOrfas() : [];\n      dg.ids = ids.length;\n      for (var i = 0; i < ids.length; i++) {\n        try {\n          var o = globalThis.__noPor(ids[i].__id !== undefined ? ids[i].__id : ids[i]);\n          if (!o) { dg.semNo++; continue; }\n          var cru = o.__raw;\n          if (cru && cru._impOrfa) continue;\n          // root DELES: nodeType 11 ou 1 (ShadyDOM finge elemento) — objeto do\n          // mundo deles com os filhos carimbados\n          var rt = null;\n          try { rt = o.shadowRoot || o.root; } catch (e0) { dg.ultimoErro = 'root:' + String(e0.message || e0).substring(0, 80); }\n          if (!rt || rt === o) { dg.semRoot++; continue; }\n          var nfilhos = 0;\n          try { nfilhos = (rt.childNodes && rt.childNodes.length) || (rt.children && rt.children.length) || 0; } catch (e1) {}\n          if (nfilhos === 0) { dg.semFilhos++; continue; }\n          var alvo = null;\n          try { alvo = document.querySelector(o.tagName); } catch (e2) {}\n          if (!alvo || alvo === o) { dg.semAlvo++; continue; }\n          var antigo = globalThis.__serPerfilDeRaiz;\n          try {\n            globalThis.__serPerfilDeRaiz = rt;\n            var tree = globalThis.__serializa(rt, 0);\n            if (tree) {\n              globalThis.__importSR(alvo, tree);\n              dg.imp++;\n              feitos++;\n              if (cru) { try { cru._impOrfa = true; } catch (e3) {} }\n            }\n          } finally {\n            globalThis.__serPerfilDeRaiz = antigo;\n          }\n        } catch (e4) { dg.ultimoErro = 'ciclo:' + String(e4.message || e4).substring(0, 80); }\n      }\n    } catch (e5) { dg.ultimoErro = 'fora:' + String(e5.message || e5).substring(0, 80); }\n    globalThis.__orfaDiag = dg;\n    return feitos;\n  };\n\n  globalThis.__shadyVarre = function (de, saida) {\n    try {\n      var fil = de.children || [];\n      var N = fil.length | 0;\n      for (var i = 0; i < N && i < 400; i++) {\n        var el = fil[i];\n        if (!el) continue;\n        if (el.__id !== undefined) {\n          var raiz = null;\n          try { raiz = el.shadowRoot || null; } catch (e1) { raiz = null; }\n          if (!raiz) { try { raiz = el.root || null; } catch (e2) { raiz = null; } }\n          if (raiz) {\n            var bruto = null; try { bruto = el.__raw || null; } catch (e3) { bruto = null; }\n            var chave = 'imp_' + String(el.__id);\n            var ja = false;\n            try { if (bruto && bruto[chave]) ja = true; } catch (e4) {}\n            if (!ja && (raiz.nodeType === 11 || raiz.nodeType === 1) && raiz !== de) {\n              var q = 0;\n              try { q = raiz.children ? raiz.children.length : (raiz.childNodes ? raiz.childNodes.length : 0); } catch (e5) { q = 0; }\n              if (q > 0) {\n                var arv = null;\n                try {\n                  globalThis.__serPerfilDeRaiz = raiz;\n                  arv = globalThis.__serializa(raiz, 0);\n                  globalThis.__serPerfilDeRaiz = null;\n                } catch (e6) { globalThis.__serPerfilDeRaiz = null; arv = null; }\n                if (arv) {\n                  try { if (bruto) { bruto[chave] = true; } } catch (e7) {}\n                  var r = globalThis.__importSR(el, arv);\n                  if (r === true) saida.n++;\n                }\n              } else if (saida.log) {\n                var tg = '?'; try { tg = String(el.tagName || '').toLowerCase(); } catch (e9) { tg = 'x'; }\n                if (tg.indexOf('ytd-') === 0 && saida.log.length < 6) saida.log.push(tg + ':sr-q0');\n              }\n            } else if (saida.log) {\n              var tg2 = '?'; try { tg2 = String(el.tagName || '').toLowerCase(); } catch (e10) { tg2 = 'x'; }\n              if (tg2.indexOf('ytd-') === 0 && saida.log.length < 6) saida.log.push(tg2 + (ja ? ':ja-imp' : ':outra'));\n            }\n          } else if (saida.log) {\n            var tg3 = '?'; try { tg3 = String(el.tagName || '').toLowerCase(); } catch (e14) { tg3 = 'x'; }\n            if (tg3.indexOf('ytd-') === 0 && saida.log.length < 6) saida.log.push(tg3 + ':sr-null');\n          }\n        }\n        if (saida.n < 40 && i < 330) {\n          try { globalThis.__shadyVarre(el, saida); } catch (e8) {}\n          // descer também no MEU shadow root (a árvore importada): page-manager\n          // e cia vivem lá — sem isto o segundo ciclo nunca acontece\n          try {\n            var sf = globalThis.__srFilhos ? globalThis.__srFilhos(el.__id) : null;\n            if (sf && sf.length) {\n              for (var j = 0; j < sf.length && j < 200; j++) {\n                try { globalThis.__shadyVarre(sf[j], saida); } catch (e16) {}\n              }\n            }\n          } catch (e17) {}\n        }\n      }\n    } catch (e) {}\n  };\n  // EXTRATOR do watch: projeta só os campos necessários de ytInitialData /\n  // ytInitialPlayerResponse (Dados reais de produção) para JSON plano — o C#\n  // monta a árvore nativa com eles.\n  globalThis.__watchExtrai = function () {\n    try {\n      var d = globalThis.ytInitialData;\n      if (!d) return 'null';\n      var c = d.contents && d.contents.twoColumnWatchNextResults;\n      if (!c) return 'null';\n      var j = function (x) {\n        if (!x) return '';\n        if (x.simpleText) return x.simpleText;\n        if (x.runs) return x.runs.map(function (r) { return r.text || ''; }).join('');\n        if (x.content) return x.content;\n        return '';\n      };\n      // melhor thumb: prefer .jpg (GDI+ não decodifica .webp)\n      var thumbDe = function (th) {\n        try {\n          var arr = th && th.thumbnails || [];\n          var jpg = null, ultimo = null;\n          for (var i = 0; i < arr.length; i++) {\n            var ul = String(arr[i].url || '');\n            if (ul.indexOf('.webp') < 0) { jpg = ul; if (arr[i].width >= 320) return ul; }\n            ultimo = ul;\n          }\n          if (jpg) return jpg;\n          if (ultimo) return String(ultimo).replace('.webp', '.jpg');\n        } catch (et) {}\n        return '';\n      };\n      var res = {};\n      var pir = (c.results && c.results.results && c.results.results.contents) || [];\n      for (var i = 0; i < pir.length; i++) {\n        var x = pir[i].videoPrimaryInfoRenderer, y = pir[i].videoSecondaryInfoRenderer, ow;\n        if (x) {\n          res.titulo = j(x.title);\n          res.dataTxt = (x.dateText && x.dateText.simpleText) || '';\n          res.views = (x.viewCount && x.viewCount.videoViewCountRenderer && x.viewCount.videoViewCountRenderer.viewCount && j(x.viewCount.videoViewCountRenderer.viewCount)) || '';\n        }\n        if (y) {\n          ow = y.owner && y.owner.videoOwnerRenderer;\n          if (ow) {\n            res.canal = j(ow.title);\n            res.inscritos = (ow.subscriberCountText && ow.subscriberCountText.simpleText) || '';\n            res.avatar = thumbDe(ow.thumbnail);\n          }\n          res.desc = j(y.attributedDescription) || j(y.description);\n        }\n      }\n      res.relacionados = [];\n      // formato atual: twoColumnWatchNextResults.secondaryResults.secondaryResults;\n      // antigo: ...watchNextResults.secondaryResults.secondaryResults\n      var sec = (c.secondaryResults && c.secondaryResults.secondaryResults)\n        || (c.watchNextResults && c.watchNextResults.secondaryResults && c.watchNextResults.secondaryResults.secondaryResults);\n      if (sec && sec.results) {\n        // formato 2024+: results[0] = itemSectionRenderer com lockupViewModel;\n        // formato antigo: results[] com compactVideoRenderer direto\n        var bruto = sec.results;\n        var arr = bruto.contents || bruto;\n        var lista = [];\n        var empurrar = function (it) {\n          if (it.compactVideoRenderer) {\n            var cv = it.compactVideoRenderer;\n            lista.push({ id: cv.videoId || '', t: j(cv.title), canal: j(cv.longBylineText) || j(cv.shortBylineText), dur: (cv.lengthText && cv.lengthText.simpleText) || '', thumb: thumbDe(cv.thumbnail) });\n            return;\n          }\n          var lv = it.lockupViewModel;\n          if (!lv) return;\n          var cid = lv.contentId || '';\n          var titulo = '';\n          var canalTxt = '';\n          var durTxt = '';\n          try {\n            var mm = lv.metadata && lv.metadata.lockupMetadataViewModel;\n            if (mm && mm.title) titulo = mm.title.content || '';\n            if (mm && mm.metadata && mm.metadata.contentMetadataViewModel) {\n              var linhas = mm.metadata.contentMetadataViewModel.metadataRows || [];\n              for (var r = 0; r < linhas.length; r++) {\n                var parts = linhas[r].metadataParts || [];\n                for (var pino = 0; pino < parts.length; pino++) {\n                  var tx = (parts[pino].text && parts[pino].text.content) || '';\n                  if (tx && !canalTxt) canalTxt = tx;\n                }\n              }\n            }\n            var ov = lv.thumbnailViewModel && lv.thumbnailViewModel.overlays || [];\n            for (var o = 0; o < ov.length; o++) {\n              var bd = ov[o].thumbnailOverlayBadgeViewModel && ov[o].thumbnailOverlayBadgeViewModel.thumbnailBadges || [];\n              for (var bi = 0; bi < bd.length; bi++) {\n                var tb = bd[bi].thumbnailBadgeViewModel && bd[bi].thumbnailBadgeViewModel.text;\n                if (tb && (tb.content || tb.simpleText)) { durTxt = tb.content || tb.simpleText || ''; break; }\n              }\n              if (durTxt) break;\n            }\n          } catch (elv) {}\n          var th = '';\n          try {\n            // formato 2024+: contentImage.thumbnailViewModel.image.sources\n            var srcs = lv.contentImage && lv.contentImage.thumbnailViewModel &&\n              lv.contentImage.thumbnailViewModel.image ? lv.contentImage.thumbnailViewModel.image.sources : [];\n            var jpg = null, ult = null;\n            for (var s = 0; s < srcs.length; s++) {\n              var us = String(srcs[s].url || '');\n              if (us.indexOf('.webp') < 0) { jpg = us; if ((srcs[s].width || 0) >= 336) break; }\n              ult = us;\n            }\n            th = jpg || String(ult || '').replace('.webp', '.jpg');\n          } catch (eth) {}\n          if (titulo) lista.push({ id: cid, t: titulo, canal: canalTxt, dur: durTxt, thumb: th });\n        };\n        for (var k2 = 0; k2 < arr.length; k2++) {\n          var noItem = arr[k2];\n          if (noItem.itemSectionRenderer && noItem.itemSectionRenderer.contents) {\n            var sub = noItem.itemSectionRenderer.contents;\n            for (var k3 = 0; k3 < sub.length && lista.length < 22; k3++) empurrar(sub[k3]);\n          } else if (lista.length < 22) empurrar(noItem);\n        }\n        res.relacionados = lista;\n      }\n      var pr = globalThis.ytInitialPlayerResponse;\n      if (pr && pr.videoDetails) {\n        res.videoId = pr.videoDetails.videoId || '';\n        res.duracao = pr.videoDetails.lengthSeconds || '';\n        res.poster = thumbDe(pr.videoDetails.thumbnail);\n        if (pr.streamingData && pr.streamingData.formats) {\n          var fs = pr.streamingData.formats;\n          for (var m = 0; m < fs.length; m++) {\n            if (fs[m].url) { res.videoUrl = fs[m].url; res.qualidade = (fs[m].qualityLabel || '') + ' ' + String(fs[m].mimeType || '').split(';')[0]; break; }\n          }\n        }\n      }\n      return JSON.stringify(res);\n    } catch (e) { return 'ERRO:' + String((e && e.message) || e).substring(0, 120); }\n  };\n\n  // home (/) e resultados (/results): mesma família de itens videoRenderer —\n  // dois formatos de embalagem (richGrid na home, sectionList na busca)\n  globalThis.__ytExtraiLista = function () {\n    try {\n      var d = globalThis.ytInitialData;\n      if (!d || !d.contents) return '[]';\n      var itens = [];\n      var pega = function (vr) {\n        if (!vr || itens.length >= 30) return;\n        var th = '';\n        try {\n          var srcs = vr.thumbnail && vr.thumbnail.thumbnails || [];\n          var jpg = null, ult = null;\n          for (var s = 0; s < srcs.length; s++) {\n            var us = String(srcs[s].url || '');\n            if (us.indexOf('.webp') < 0) { jpg = us; if ((srcs[s].width || 0) >= 320) break; }\n            ult = us;\n          }\n          th = jpg || String(ult || '').replace('.webp', '.jpg');\n        } catch (et2) {}\n        var tt = '';\n        try { tt = vr.title.runs[0].text || ''; } catch (et3) {}\n        var cn = '';\n        try { cn = vr.ownerText.runs[0].text || ''; } catch (et4) {}\n        var du = '';\n        try { du = vr.lengthText.simpleText || ''; } catch (et5) {}\n        var vc = '';\n        try { vc = vr.viewCountText.simpleText || vr.shortViewCountText.simpleText || ''; } catch (et6) {}\n        var pub = '';\n        try { pub = vr.publishedTimeText.simpleText || ''; } catch (et7) {}\n        if (vr.videoId && tt) itens.push({ id: vr.videoId, t: tt, canal: cn, dur: du, thumb: th, views: vc, pub: pub });\n      };\n      // home: twoColumnBrowseResultsRenderer.tabs[].tabRenderer.content.richGridRenderer\n      var brows = d.contents.twoColumnBrowseResultsRenderer;\n      if (brows && brows.tabs) {\n        for (var tb = 0; tb < brows.tabs.length; tb++) {\n          var rg = brows.tabs[tb].tabRenderer && brows.tabs[tb].tabRenderer.content && brows.tabs[tb].tabRenderer.content.richGridRenderer;\n          if (!rg || !rg.contents) continue;\n          for (var ri = 0; ri < rg.contents.length; ri++) {\n            var it = rg.contents[ri];\n            if (it.richItemRenderer && it.richItemRenderer.content) pega(it.richItemRenderer.content.videoRenderer);\n            if (it.richSectionRenderer && it.richSectionRenderer.content && it.richSectionRenderer.content.richShelfRenderer) {\n              var mid2 = it.richSectionRenderer.content.richShelfRenderer.contents || [];\n              for (var ms2 = 0; ms2 < mid2.length; ms2++) {\n                if (mid2[ms2].richItemRenderer && mid2[ms2].richItemRenderer.content) pega(mid2[ms2].richItemRenderer.content.videoRenderer);\n              }\n            }\n          }\n        }\n      }\n      // busca: twoColumnSearchResultsRenderer.primaryContents.sectionListRenderer\n      var sr2 = d.contents.twoColumnSearchResultsRenderer;\n      if (sr2 && sr2.primaryContents && sr2.primaryContents.sectionListRenderer) {\n        var secoes = sr2.primaryContents.sectionListRenderer.contents || [];\n        for (var sc = 0; sc < secoes.length; sc++) {\n          var is2 = secoes[sc].itemSectionRenderer;\n          if (!is2 || !is2.contents) continue;\n          for (var ic = 0; ic < is2.contents.length; ic++) pega(is2.contents[ic].videoRenderer);\n        }\n      }\n      return JSON.stringify(itens);\n    } catch (e) { return '[]'; }\n  };\n\n  // localiza a função decifrador no escopo global (o player deles roda no\n  // motor — funções top-level dele são globais aqui). [v2 SUSPENSA — a varredura\n  // por teste-chamada estava congelando o ExposeGlobals; investigar depois]\n  globalThis.__procuraDecifra = function () {\n    try {\n      var s = '';\n      var sp = '';\n      try {\n        var pr = globalThis.ytInitialPlayerResponse;\n        var sc = pr && pr.streamingData && pr.streamingData.formats && pr.streamingData.formats[0] && pr.streamingData.formats[0].signatureCipher;\n        if (!sc) { var af2 = (pr && pr.streamingData && pr.streamingData.adaptiveFormats) || []; for (var fi = 0; fi < af2.length; fi++) { if (af2[fi] && af2[fi].signatureCipher) { sc = af2[fi].signatureCipher; break; } } }\n        if (sc) {\n          var pares = String(sc).split('&');\n          for (var i = 0; i < pares.length; i++) {\n            var kv = pares[i].split('=');\n            if (kv[0] === 's') { s = decodeURIComponent((kv[1] || '').replace(/\\+/g, '%20')); }\n            if (kv[0] === 'sp') sp = kv[1] || '';\n          }\n        }\n      } catch (e0) {}\n      var achados = [];\n      var testadas = 0, erros = 0;\n      if (s && s.length > 60) {\n        for (var k in globalThis) {\n          if (testadas >= 400) { break; }\n          try {\n            if (k.indexOf('__') === 0 || k === 'document' || k === 'location' || k === 'window' || k === 'alert' || k === 'top' || k === 'self' || k === 'parent' || k === 'globalThis' || k === 'frames') { continue; }\n            var f = globalThis[k];\n            if (typeof f !== 'function') { continue; }\n            var srcf = '';\n            try { srcf = Function.prototype.toString.call(f); } catch (es) { continue; }\n            if (srcf.length > 6000 || srcf.length < 20) { continue; }\n            if (srcf.indexOf('document') >= 0 || srcf.indexOf('location') >= 0 || srcf.indexOf('window.') >= 0 || srcf.indexOf('alert') >= 0) { continue; }\n            testadas++;\n            var r = f(s);\n            if (typeof r !== 'string' || r === s) { continue; }\n            if (r.length < 24 || r.length > 120) { continue; }\n            if (r.length >= s.length) { continue; }\n            if (!/^[A-Za-z0-9_\\-=%]+$/.test(r)) { continue; }\n            achados.push({ n: String(k).substring(0, 20), inLen: s.length, outLen: r.length, out: r.substring(0, 18) });\n          } catch (e1) { erros++; }\n        }\n      }\n      return JSON.stringify({ sLen: s.length, sp: sp, sHead: (s || '').substring(0, 24), testadas: testadas, erros: erros, cands: achados.slice(0, 8) });\n    } catch (e) { return 'ERRO:' + String((e && e.message) || e).substring(0, 120); }\n  };\n\n  globalThis.__shadyExport = function () {\n    var saida = { n: 0, log: [] };\n    try {\n      // Tolerância no iterador-deles (_.L): durante o boot oconnectedCallback de\n      // tooltip/snackbar/ephemeral chama _.L(coisa) com cosa ainda indefinida ou\n      // Symbol — no Chrome o valor NESTE ponto é coleção vazia. Convertida em\n      // iterador vazio, o boot continua; _.L original preservado como velha.\n      if (globalThis._ && typeof _.L === 'function' && !_.L.__tol) {\n        try {\n          var velha = _.L;\n          var vazio = function () { return { next: function () { return { done: true, value: undefined }; } }; };\n          var nova = function (h) {\n            try {\n              return velha(h);\n            } catch (e1) {\n              if (h === undefined || h === null || typeof h === 'symbol') return vazio();\n              throw e1;\n            }\n          };\n          nova.__tol = true;\n          _.L = nova;\n        } catch (e) {}\n      }\n      var b = document.body || document.documentElement;\n      // IMPORTAÇÃO DOS ÓRFÃOS ANTES da varredura: o materializador lazy deles\n      // estampa o template num elemento transitório (document.createElement,\n      // nunca anexado). O root DELES é um objeto JS puro acessível via\n      // .root/.shadowRoot; serializamos e importamos para o elemento real da\n      // árvore com a MESMA tag — carimbo de produção sem substituições.\n      if (globalThis.__importaOrfas) {\n        try { var fo = globalThis.__importaOrfas(); if (fo > 0) saida.log.push('orfas-importadas=' + fo); } catch (eo) {}\n      }\n      if (b) globalThis.__shadyVarre(b, saida);\n    } catch (e) {}\n    if (saida.log && saida.log.length) {\n      try { OnLogPor(saida.log.join(' | ')); } catch (e15) {}\n    }\n    return saida.n;\n  };\n  globalThis.OnLogPor = function (m) {\n    try { globalThis.__logInfo(String(m)); } catch (e) {}\n  };\n})();");
		static string Norm(string s)
		{
			return s.Replace("-", "").ToLowerInvariant();
		}
	}

	private void InstallFetchAndVideo()
	{
		engine.SetValue("__nativeFetch", new ClrFunction(engine, "__nativeFetch", (JsValue self, JsValue[] args) =>
		{
			if (args.Length >= 5 && args[4] is Function && OnNetRequest != null)
			{
				try
				{
					VideoManager.Log("fetch: __nativeFetch " + args[0].ToString());
					OnNetRequest(new JsNetRequest
					{
						Url = args[0].ToString(),
						Method = (args[1].IsUndefined() ? "GET" : args[1].ToString().ToUpperInvariant()),
						Body = ((args[2].IsUndefined() || args[2].IsNull()) ? "" : args[2].ToString()),
						HeadersJson = (args[3].IsUndefined() ? "{}" : args[3].ToString()),
						Callback = args[4]
					});
				}
				catch
				{
				}
			}
			return JsValue.Undefined;
		}));
		engine.Evaluate("\n(function () {\n  // ======= fetch() =======\n  globalThis.fetch = function (input, opts) {\n    var url = typeof input === 'string' ? input : (input && input.url ? input.url : String(input));\n    opts = opts || {};\n    var method = (opts.method || (typeof input === 'object' && input && input.method) || 'GET').toUpperCase();\n    var body = '';\n    if (opts.body !== undefined && opts.body !== null) {\n      body = typeof opts.body === 'string' ? opts.body : JSON.stringify(opts.body);\n    } else if (typeof input === 'object' && input && input.body && method !== 'GET') {\n      body = typeof input.body === 'string' ? input.body : '';\n    }\n    var headers = opts.headers || {};\n    if (typeof headers.append === 'function') {\n      var h2 = {}; try { headers.forEach(function (v, k) { h2[k] = v; }); headers = h2; } catch (e) {}\n    }\n    return new Promise(function (resolve, reject) {\n      globalThis.__nativeFetch(url, method, body, JSON.stringify(headers), function (ok, status, hjson, text) {\n        if (ok) {\n          var hh = {};\n          try { hh = JSON.parse(hjson) || {}; } catch (e) {}\n          var ct = String(hh['content-type'] || hh['Content-Type'] || '');\n          resolve({\n            ok: status >= 200 && status < 300,\n            status: status,\n            statusText: '',\n            url: url,\n            headers: {\n              get: function (n) { return hh[String(n).toLowerCase()] || hh[String(n)] || null; },\n              has: function (n) { var k = String(n).toLowerCase(); return k in hh; },\n              forEach: function (cb) { for (var k in hh) try { cb(hh[k], k); } catch (e) {} }\n            },\n            text: function () { return Promise.resolve(String(text)); },\n            json: function () {\n              return new Promise(function (res, rej) {\n                try { res(JSON.parse(text)); } catch (e) { rej(new SyntaxError('JSON invalido')); }\n              });\n            }\n          });\n        } else {\n          reject(new TypeError('Falha de rede em ' + url));\n        }\n      });\n    });\n  };\n\n  // ======= XMLHttpRequest =======\n  function XHR() {\n    this.readyState = 0; this.status = 0; this.statusText = '';\n    this.responseText = ''; this.response = ''; this.responseType = '';\n    this.onload = null; this.onerror = null; this.onprogress = null;\n    this.onreadystatechange = null; this.ontimeout = null;\n    this.timeout = 0; this.withCredentials = false;\n    var self = this;\n    this._headers = {};\n    this.open = function (m, u, async) { self._method = String(m || 'GET').toUpperCase(); self._url = String(u); self.readyState = 1; };\n    this.setRequestHeader = function (n, v) { self._headers[String(n)] = String(v); };\n    this.getResponseHeader = function (n) { return null; };\n    this.getAllResponseHeaders = function () { return ''; };\n    this.abort = function () {};\n    this.overrideMimeType = function () {};\n    this.send = function (body) {\n      self.readyState = 2;\n      var b = body === undefined || body === null ? '' : (typeof body === 'string' ? body : String(body));\n      globalThis.__nativeFetch(self._url, self._method || 'GET', b, JSON.stringify(self._headers), function (ok, status, hjson, text) {\n        self.status = status;\n        self.readyState = 4;\n        self.responseText = String(text);\n        self.response = self.responseText;\n        try {\n          if (self.onreadystatechange) self.onreadystatechange();\n          if (ok && self.onload) self.onload();\n          else if (!ok && self.onerror) self.onerror({ type: 'error' });\n        } catch (e) {}\n      });\n    };\n  }\n  globalThis.XMLHttpRequest = XHR;\n  globalThis.__KNOWN.add('send');\n  globalThis.__KNOWN.add('open');\n})();\n");
		engine.Evaluate("globalThis.__KNOWN.add('play'); globalThis.__KNOWN.add('pause'); globalThis.__KNOWN.add('currentTime'); globalThis.__KNOWN.add('duration'); globalThis.__KNOWN.add('volume'); globalThis.__KNOWN.add('muted'); globalThis.__KNOWN.add('loop'); globalThis.__KNOWN.add('ended'); globalThis.__KNOWN.add('paused');");
	}

	private static JsValue[] ToArgs(JsValue arr)
	{
		if (!(arr is ObjectInstance objectInstance))
		{
			return Array.Empty<JsValue>();
		}
		JsValue value = objectInstance.Get("length", objectInstance);
		if (!value.IsNumber())
		{
			return Array.Empty<JsValue>();
		}
		int num = Math.Min(16, Math.Max(0, (int)value.AsNumber()));
		JsValue[] array = new JsValue[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = objectInstance.Get(i, objectInstance);
		}
		return array;
	}

	private JsValue GetExpando(JsValue idVal, string name)
	{
		if (!idVal.IsNumber())
		{
			return JsValue.Undefined;
		}
		if (expandos.TryGetValue((int)idVal.AsNumber(), out Dictionary<string, JsValue> value) && value.TryGetValue(name, out var value2))
		{
			return value2;
		}
		return JsValue.Undefined;
	}

	private void SetExpando(JsValue idVal, string name, JsValue v)
	{
		if (idVal.IsNumber())
		{
			int key = (int)idVal.AsNumber();
			if (!expandos.TryGetValue(key, out Dictionary<string, JsValue> value))
			{
				value = (expandos[key] = new Dictionary<string, JsValue>());
			}
			value[name] = v;
		}
	}

	private JsValue DoGet(JsValue idVal, string name)
	{
		Binding binding = GetBinding(idVal);
		if (binding == null)
		{
			return JsValue.Undefined;
		}
		DomNode node = binding.Node;
		switch (binding.Role)
		{
		case Role.Location:
		{
			bool flag;
			switch (name)
			{
			case "href":
			case "toString":
			case "valueOf":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				return CurrentUrl;
			}
			switch (name)
			{
			case "protocol":
			case "host":
			case "hostname":
			case "pathname":
			case "search":
			case "hash":
			case "origin":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				try
				{
					Uri uri = new Uri(CurrentUrl);
					if (1 == 0)
					{
					}
					JsValue result = name switch
					{
						"protocol" => (JsValue)(uri.Scheme + ":"), 
						"host" => uri.Authority, 
						"hostname" => uri.Host, 
						"pathname" => uri.AbsolutePath, 
						"search" => uri.Query, 
						"hash" => uri.Fragment, 
						_ => uri.Scheme + "://" + uri.Authority, 
					};
					if (1 == 0)
					{
					}
					return result;
				}
				catch
				{
					return "";
				}
			}
			return JsValue.Undefined;
		}
		case Role.Document:
			switch (name)
			{
			case "body":
			{
				DomNode domNode11 = FindTag(node, "body");
				return (domNode11 != null) ? Wrap(domNode11) : JsValue.Null;
			}
			case "head":
			{
				DomNode domNode12 = FindTag(node, "head");
				return (domNode12 != null) ? Wrap(domNode12) : JsValue.Null;
			}
			case "documentElement":
			{
				DomNode domNode10 = FindTag(node, "html");
				if (domNode10 == null)
				{
					domNode10 = node.Children.FirstOrDefault((DomNode c) => c.Type == NodeType.Element);
				}
				return (domNode10 != null) ? Wrap(domNode10) : JsValue.Null;
			}
			case "title":
				return FindTag(node, "title")?.CollectText() ?? "";
			case "cookie":
				return "";
			case "readyState":
				return "complete";
			case "referrer":
				return "";
			case "compatMode":
				return "CSS1Compat";
			case "implementation":
				return engine.Evaluate("(function () {\n                          return {\n                            createHTMLDocument: function (titulo) {\n                              return globalThis.__parseHtml('<!DOCTYPE html><html><head><title>' +\n                                String(titulo == null ? '' : titulo) +\n                                '</title></head><body></body></html>');\n                            },\n                            createDocument: function () {\n                              return globalThis.__parseHtml('<!DOCTYPE html><html><head></head><body></body></html>');\n                            },\n                            createDocumentType: function (nome, pub, sys) {\n                              return { name: nome, publicId: pub, systemId: sys, nodeType: 10 };\n                            },\n                            hasFeature: function () { return true; }\n                          };\n                        })()");
			case "hidden":
				return false;
			case "prerendering":
				return false;
			case "images":
				return WrapAll(FindTags(node, "img"));
			case "forms":
			{
				List<DomNode> list2 = new List<DomNode>();
				foreach (var _img in FindTags(node, "img")) list2.Add(_img);
				return WrapAll(list2);
			}
			case "links":
			{
				List<DomNode> list = new List<DomNode>();
				foreach (var _a in FindTags(node, "a")) list.Add(_a);
				return WrapAll(list);
			}
			case "activeElement":
			{
				DomNode domNode9 = GetFocused?.Invoke();
				return (domNode9 != null) ? Wrap(domNode9) : JsValue.Null;
			}
			default:
				if (binding.Role == Role.Document && name.Length > 0 && !name.StartsWith("__"))
				{
					DomNode domNode8 = FindByNameOrId(node, name);
					if (domNode8 != null)
					{
						return Wrap(domNode8);
					}
				}
				return JsValue.Undefined;
			}
		case Role.Style:
		{
			if (name == "cssText")
			{
				return node.GetAttr("style");
			}
			Dictionary<string, string> dictionary = CssParser.ParseDeclarations(node.GetAttr("style"));
			if (name == "length")
			{
				return dictionary.Count;
			}
			if (dictionary.TryGetValue(name, out var value))
			{
				return value;
			}
			if (dictionary.TryGetValue(name.ToLowerInvariant(), out var value2))
			{
				return value2;
			}
			return JsValue.Undefined;
		}
		case Role.ClassList:
			if (name == "length")
			{
				return node.GetAttr("class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
			}
			return JsValue.Undefined;
		default:
			if (node.TagName == "video" || node.TagName == "audio")
			{
				switch (name)
				{
				case "currentTime":
					return Videos?.GetTime(node) ?? 0.0;
				case "duration":
					return Videos?.GetDuration(node) ?? 0.0;
				case "paused":
					return Videos?.IsPaused(node) ?? true;
				case "playing":
					return Videos?.IsPlaying(node) ?? false;
				case "ended":
					return Videos?.IsEnded(node) ?? false;
				case "muted":
					return Videos?.IsMuted(node) ?? false;
				case "loop":
					return Videos?.IsLoop(node) ?? node.Attributes.ContainsKey("loop");
				case "volume":
					return Videos?.GetVolume(node) ?? 1.0;
				case "autoplay":
					return node.Attributes.ContainsKey("autoplay");
				case "controls":
					return node.Attributes.ContainsKey("controls");
				case "readyState":
					return 4;
				case "networkState":
					return 1;
				case "error":
					return JsValue.Null;
				case "videoWidth":
					return (Videos?.GetVideoSize(node) ?? (0, 0)).w;
				case "videoHeight":
					return (Videos?.GetVideoSize(node) ?? (0, 0)).h;
				case "playbackRate":
					return 1;
				}
			}
			switch (name)
			{
			case "tagName":
			case "nodeName":
				return node.TagName.ToUpperInvariant();
			case "id":
				return node.GetAttr("id");
			case "className":
				return node.GetAttr("class");
			case "classList":
				return ProxyFor(Bind(Role.ClassList, node));
			case "style":
				return ProxyFor(Bind(Role.Style, node));
			case "textContent":
				return node.CollectText();
			case "innerHTML":
				return string.Concat(node.Children.Select(InnerHtmlOf));
			case "outerHTML":
				return string.Concat(new string[7]
				{
					"<",
					node.TagName,
					">",
					string.Concat(node.Children.Select(InnerHtmlOf)),
					"</",
					node.TagName,
					">"
				});
			case "value":
				return node.GetAttr("value");
			case "href":
				return node.GetAttr("href");
			case "src":
				return node.GetAttr("src");
			case "type":
				return node.GetAttr("type");
			case "title":
				return node.GetAttr("title");
			case "complete":
				if (node.TagName == "img")
				{
					Func<DomNode, bool>? isImageReady = IsImageReady;
					return (isImageReady != null && isImageReady(node)) ? ((JsValue)true) : ((JsValue)false);
				}
				return JsValue.Undefined;
			case "parentNode":
			case "parentElement":
				return (node.Parent != null) ? Wrap(node.Parent) : JsValue.Null;
			case "host":
				return (node.ShadowHost != null) ? Wrap(node.ShadowHost) : JsValue.Null;
			case "isConnected":
			{
				DomNode domNode4 = node.Parent ?? node.ShadowHost;
				int num = 0;
				while (domNode4 != null && num < 80)
				{
					if (domNode4.TagName == "#document")
					{
						return true;
					}
					if (domNode4.TagName == "html")
					{
						return true;
					}
					domNode4 = domNode4.Parent ?? domNode4.ShadowHost;
					num++;
				}
				return false;
			}
			case "content":
				if (node.TagName != "template")
				{
					return JsValue.Undefined;
				}
				if (node.TemplateContent == null)
				{
					DomNode domNode7 = new DomNode(NodeType.Element)
					{
						TagName = "#fragment",
						Parent = node
					};
					foreach (DomNode item in node.Children.ToList())
					{
						node.Children.Remove(item);
						item.Parent = domNode7;
						domNode7.Children.Add(item);
					}
					node.TemplateContent = domNode7;
				}
				return Wrap(node.TemplateContent);
			case "assignedNodes":
			case "assignedElements":
				return WrapAll(AssignedNodesOf(node));
			case "mode":
				return (node.TagName == "#shadow-root") ? ((JsValue)node.ShadowMode) : JsValue.Undefined;
			case "documentElement":
			{
				DomNode domNode = node.Children.FirstOrDefault((DomNode c) => c.Type == NodeType.Element && c.TagName == "html") ?? node.Children.FirstOrDefault((DomNode c) => c.Type == NodeType.Element);
				if (domNode != null)
				{
					return Wrap(domNode);
				}
				DomNode domNode2 = FindTag(node, "html");
				return (domNode2 != null) ? Wrap(domNode2) : JsValue.Null;
			}
			case "body":
				if (node.TagName == "#document")
				{
					DomNode domNode6 = FindTag(node, "body");
					return (domNode6 != null) ? Wrap(domNode6) : JsValue.Null;
				}
				goto case "__fallthrough_body";
			case "head":
				if (node.TagName == "#document")
				{
					DomNode domNode5 = FindTag(node, "head");
					return (domNode5 != null) ? Wrap(domNode5) : JsValue.Null;
				}
				goto case "__fallthrough_body";
			case "__fallthrough_body":
				return JsValue.Undefined;
			case "children":
				return WrapAll(node.Children.Where((DomNode c) => c.Type == NodeType.Element));
			case "childNodes":
				return WrapAll(node.Children);
			case "firstChild":
				return (node.Children.Count > 0) ? Wrap(node.Children[0]) : JsValue.Null;
			case "firstElementChild":
			{
				DomNode domNode3 = node.Children.FirstOrDefault((DomNode c) => c.Type == NodeType.Element);
				return (domNode3 != null) ? Wrap(domNode3) : JsValue.Null;
			}
			case "dataset":
			{
				ObjectInstance objectInstance = (ObjectInstance)engine.Evaluate("({})");
				foreach (KeyValuePair<string, string> attribute in node.Attributes)
				{
					if (attribute.Key.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
					{
						objectInstance.FastSetDataProperty(ToCamel(attribute.Key.Substring(5)), attribute.Value);
					}
				}
				return objectInstance;
			}
			default:
				if (node.TagName == "form" && name.Length > 0 && !name.StartsWith("__"))
				{
					DomNode? found = FindByNameOrId(node, name);
					if (found != null)
					{
						return Wrap(found);
					}
				}
				return GetExpando(idVal, name);
			}
		}
	}

	private void DoSet(JsValue idVal, string name, JsValue value)
	{
		Binding binding = GetBinding(idVal);
		if (binding == null)
		{
			return;
		}
		string text = ((value.IsNull() || value.IsUndefined()) ? "" : value.ToString());
		DomNode node = binding.Node;
		switch (binding.Role)
		{
		case Role.Location:
			if (name == "href")
			{
				OnNavigate?.Invoke(text);
			}
			return;
		case Role.Style:
		{
			bool flag;
			switch (name)
			{
			case "cssText":
			case "length":
			case "getPropertyValue":
			case "setProperty":
			case "removeProperty":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				Dictionary<string, string> dictionary = CssParser.ParseDeclarations(node.GetAttr("style"));
				dictionary[ToDash(name)] = text;
				SaveDecls(node, dictionary);
			}
			return;
		}
		case Role.Document:
		case Role.ClassList:
			return;
		}
		if (name.StartsWith("on", StringComparison.Ordinal) && name.Length > 2 && value is Function)
		{
			string text2 = name.Substring(2);
			switch (text2)
			{
			case "click":
				AddClick(node, value);
				return;
			case "load":
			case "error":
				if (node.TagName == "img")
				{
					SetImgHandler(node, text2, value);
					return;
				}
				break;
			}
			if (text2 == "load")
			{
				dclHandlers.Add(value);
			}
			else
			{
				AddListener(node, text2, value);
			}
			return;
		}
		if (node.TagName == "video" || node.TagName == "audio")
		{
			switch (name)
			{
			case "currentTime":
				if (Videos != null && value.IsNumber())
				{
					Videos.SetTime(node, value.AsNumber());
				}
				return;
			case "muted":
				Videos?.SetMuted(node, IsTruthy(value));
				return;
			case "loop":
				Videos?.SetLoop(node, IsTruthy(value));
				return;
			case "volume":
				if (Videos != null && value.IsNumber())
				{
					Videos.SetVolume(node, value.AsNumber());
				}
				return;
			case "autoplay":
				if (IsTruthy(value))
				{
					node.Attributes["autoplay"] = "true";
				}
				else
				{
					node.Attributes.Remove("autoplay");
				}
				Dirty = true;
				return;
			case "src":
				node.Attributes["src"] = text;
				Dirty = true;
				OnVideoSrcSet?.Invoke(node);
				return;
			}
		}
		switch (name)
		{
		case "id":
			node.Attributes["id"] = text;
			Dirty = true;
			break;
		case "className":
			node.Attributes["class"] = text;
			Dirty = true;
			break;
		case "textContent":
			node.Children.Clear();
			if (text.Length > 0)
			{
				node.Children.Add(new DomNode(NodeType.Text)
				{
					TextContent = text,
					Parent = node
				});
			}
			Dirty = true;
			RecordMut("childList", node);
			break;
		case "innerHTML":
		{
			DomNode domNode2 = new HtmlParser(text).Parse();
			node.Children.Clear();
			List<DomNode> list = new List<DomNode>();
			foreach (DomNode item in domNode2.Children.ToList())
			{
				domNode2.Children.Remove(item);
				item.Parent = node;
				node.Children.Add(item);
				list.Add(item);
			}
			Dirty = true;
			RecordMut("childList", node, null, list);
			break;
		}
		case "value":
			node.Attributes["value"] = text;
			Dirty = true;
			break;
		case "href":
			node.Attributes["href"] = text;
			Dirty = true;
			break;
		case "src":
			node.Attributes["src"] = text;
			Dirty = true;
			OnImgSrcSet?.Invoke(node);
			break;
		case "root":
			SetExpando(idVal, name, value);
			try
			{
				if (!(value is ObjectInstance) || !TryNodeLoose(value, out DomNode node2) || node2 == null)
				{
					break;
				}
				DomNode domNode = node.ShadowRoot ?? CreateShadowRoot(node, "open");
				if (domNode == null)
				{
					break;
				}
				foreach (DomNode item2 in node2.Children.ToList())
				{
					node2.Children.Remove(item2);
					item2.Parent = domNode;
					domNode.Children.Add(item2);
				}
				node.Upgraded = true;
				Dirty = true;
				break;
			}
			catch
			{
				break;
			}
		default:
			SetExpando(idVal, name, value);
			break;
		}
	}

	private List<DomNode> AssignedNodesOf(DomNode slot)
	{
		List<DomNode> list = new List<DomNode>();
		if (slot.TagName != "slot")
		{
			return list;
		}
		DomNode parent = slot.Parent;
		while (parent != null && parent.TagName != "#shadow-root")
		{
			parent = parent.Parent;
		}
		DomNode domNode = parent?.ShadowHost;
		if (domNode == null)
		{
			return list;
		}
		string attr = slot.GetAttr("name");
		foreach (DomNode child in domNode.Children)
		{
			string attr2 = child.GetAttr("slot");
			if ((attr.Length == 0) ? (attr2.Length == 0) : string.Equals(attr2, attr, StringComparison.Ordinal))
			{
				list.Add(child);
			}
		}
		return list;
	}

	private JsValue DoCall(JsValue idVal, string method, JsValue[] a)
	{
		Binding binding = GetBinding(idVal);
		if (binding == null)
		{
			return JsValue.Undefined;
		}
		DomNode n = binding.Node;
		if ((n.TagName == "video" || n.TagName == "audio") && binding.Role == Role.Element)
		{
			switch (method)
			{
			case "play":
				Videos?.Play(n);
				return engine.Evaluate("Promise.resolve()");
			case "pause":
				Videos?.Pause(n);
				return JsValue.Undefined;
			case "load":
				OnVideoSrcSet?.Invoke(n);
				return JsValue.Undefined;
			case "canPlayType":
				return "probably";
			}
		}
		if (binding.Role == Role.Location)
		{
			if ((method == "assign" || method == "replace") ? true : false)
			{
				if (a.Length != 0)
				{
					OnNavigate?.Invoke(a[0].ToString());
				}
			}
			else if (method == "reload")
			{
				OnNavigate?.Invoke("");
			}
			return JsValue.Undefined;
		}
		if (binding.Role == Role.Style)
		{
			Dictionary<string, string> dictionary = CssParser.ParseDeclarations(n.GetAttr("style"));
			switch (method)
			{
			case "getPropertyValue":
			{
				string value;
				return (a.Length != 0 && dictionary.TryGetValue(a[0].ToString(), out value)) ? ((JsValue)value) : ((JsValue)"");
			}
			case "setProperty":
				if (a.Length >= 2)
				{
					dictionary[a[0].ToString().ToLowerInvariant()] = a[1].ToString();
					SaveDecls(n, dictionary);
				}
				return JsValue.Undefined;
			case "removeProperty":
				if (a.Length != 0)
				{
					dictionary.Remove(a[0].ToString().ToLowerInvariant());
					SaveDecls(n, dictionary);
				}
				return JsValue.Undefined;
			default:
				return JsValue.Undefined;
			}
		}
		if (binding.Role == Role.ClassList)
		{
			switch (method)
			{
			case "add":
			{
				List<string> list3 = Classes();
				JsValue[] array2 = a;
				foreach (JsValue jsValue2 in array2)
				{
					string item = jsValue2.ToString();
					if (!list3.Contains(item))
					{
						list3.Add(item);
					}
				}
				Save(list3);
				return JsValue.Undefined;
			}
			case "remove":
			{
				List<string> list2 = Classes();
				JsValue[] array = a;
				foreach (JsValue jsValue in array)
				{
					list2.Remove(jsValue.ToString());
				}
				Save(list2);
				return JsValue.Undefined;
			}
			case "toggle":
			{
				if (a.Length == 0)
				{
					return false;
				}
				List<string> list4 = Classes();
				string item2 = a[0].ToString();
				bool flag = list4.Contains(item2);
				if (flag)
				{
					list4.Remove(item2);
				}
				else
				{
					list4.Add(item2);
				}
				Save(list4);
				return !flag;
			}
			case "contains":
				return (a.Length != 0 && Classes().Contains(a[0].ToString())) ? ((JsValue)true) : ((JsValue)false);
			case "item":
			{
				List<string> list = Classes();
				if (a.Length != 0 && a[0].IsNumber() && (int)a[0].AsNumber() >= 0 && (int)a[0].AsNumber() < list.Count)
				{
					return list[(int)a[0].AsNumber()];
				}
				return JsValue.Null;
			}
			default:
				return JsValue.Undefined;
			}
		}
		JsValue result;
		ObjectInstance objectInstance2;
		switch (method)
		{
		case "getAttribute":
			return (a.Length != 0) ? ((JsValue)n.GetAttr(a[0].ToString())) : JsValue.Undefined;
		case "setAttribute":
			if (a.Length >= 2)
			{
				string text3 = a[0].ToString();
				string attr = n.GetAttr(text3);
				n.Attributes[text3] = a[1].ToString();
				Dirty = true;
				RecordMut("attributes", n, text3);
				if (n.TagName == "img" && text3.Equals("src", StringComparison.OrdinalIgnoreCase))
				{
					OnImgSrcSet?.Invoke(n);
				}
				if ((n.TagName == "video" || n.TagName == "audio") && text3.Equals("src", StringComparison.OrdinalIgnoreCase))
				{
					Dirty = true;
					OnVideoSrcSet?.Invoke(n);
				}
				if (n.Upgraded && customElementDefs.TryGetValue(n.TagName, out JsValue value3))
				{
					FireAttributeChanged(n, value3, text3, attr, a[1].ToString());
				}
			}
			return JsValue.Undefined;
		case "removeAttribute":
			if (a.Length != 0)
			{
				string text6 = a[0].ToString();
				string attr2 = n.GetAttr(text6);
				n.Attributes.Remove(text6);
				Dirty = true;
				RecordMut("attributes", n, text6);
				if (n.Upgraded && customElementDefs.TryGetValue(n.TagName, out JsValue value5))
				{
					FireAttributeChanged(n, value5, text6, attr2, null);
				}
				if (text6.Equals("disable-upgrade", StringComparison.OrdinalIgnoreCase))
				{
					TryUpgrade(n);
				}
			}
			return JsValue.Undefined;
		case "hasAttribute":
			return (a.Length != 0 && n.Attributes.ContainsKey(a[0].ToString())) ? ((JsValue)true) : ((JsValue)false);
		case "addEventListener":
			if (a.Length >= 2 && a[1] is Function)
			{
				string text = a[0].ToString();
				bool flag2 = binding.Role == Role.Document;
				bool flag3 = flag2;
				if (flag3)
				{
					bool flag4 = ((text == "DOMContentLoaded" || text == "load") ? true : false);
					flag3 = flag4;
				}
				if (flag3)
				{
					dclHandlers.Add(a[1]);
				}
				else if (text == "click")
				{
					AddClick(n, a[1]);
				}
				else
				{
					AddListener(n, text, a[1]);
				}
			}
			return JsValue.Undefined;
		case "appendChild":
		{
			if (a.Length != 0 && TryNode(a[0], out DomNode node14) && node14 != n)
			{
				node14.Parent?.Children.Remove(node14);
				node14.Parent = n;
				n.Children.Add(node14);
				Dirty = true;
				RecordMut("childList", n, null, new List<DomNode> { node14 });
				if (node14.TagName == "script")
				{
					OnScriptInserted?.Invoke(node14);
				}
				TryUpgrade(node14);
				if (node14.ShadowRoot != null)
				{
					TryUpgrade(node14.ShadowRoot);
				}
				return Wrap(node14);
			}
			return JsValue.Undefined;
		}
		case "removeChild":
		{
			if (a.Length != 0 && TryNode(a[0], out DomNode node7) && node7.Parent == n)
			{
				n.Children.Remove(node7);
				node7.Parent = null;
				Dirty = true;
				RecordMut("childList", n);
				return Wrap(node7);
			}
			return JsValue.Undefined;
		}
		case "click":
			FireClick(n);
			return JsValue.Undefined;
		case "querySelector":
		{
			if (a.Length == 0)
			{
				return JsValue.Null;
			}
			List<DomNode> list9 = CssParser.QueryAll(n, a[0].ToString(), binding.Role == Role.Document);
			return (list9.Count > 0) ? Wrap(list9[0]) : JsValue.Null;
		}
		case "querySelectorAll":
			if (a.Length == 0)
			{
				return WrapAll(Array.Empty<DomNode>());
			}
			return WrapAll(CssParser.QueryAll(n, a[0].ToString(), binding.Role == Role.Document));
		case "getElementsByTagName":
		{
			string tag = ((a.Length != 0) ? a[0].ToString().ToLowerInvariant() : "");
			List<DomNode> list10 = new List<DomNode>();
			foreach (var _t in FindTags(n, tag)) list10.Add(_t);
			return WrapAll(list10);
		}
		case "getElementsByClassName":
		{
			string cls = ((a.Length != 0) ? a[0].ToString() : "");
			List<DomNode> list8 = new List<DomNode>();
			void WalkLoc(DomNode w)
			{
				if (w.Type == NodeType.Element && w.GetAttr("class").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(cls)) list8.Add(w);
				foreach (var c2 in w.Children) WalkLoc(c2);
				if (w.ShadowRoot != null) WalkLoc(w.ShadowRoot);
			}
			WalkLoc(n);
			return WrapAll(list8);
		}
		case "getElementById":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			if (a.Length == 0)
			{
				return JsValue.Null;
			}
			DomNode domNode5 = FindById(n, a[0].ToString());
			return (domNode5 != null) ? Wrap(domNode5) : JsValue.Null;
		}
		case "createElement":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			if (a.Length == 0)
			{
				return JsValue.Null;
			}
			DomNode domNode9 = new DomNode(NodeType.Element)
			{
				TagName = a[0].ToString().ToLowerInvariant()
			};
			if (domNode9.TagName.Contains('-'))
			{
				lock (_orfasLock)
				{
					_orfas.Add(domNode9);
					while (_orfas.Count > 24)
					{
						_orfas.RemoveAt(0);
					}
				}
			}
			return Wrap(domNode9);
		}
		case "createElementNS":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			if (a.Length < 2)
			{
				return JsValue.Null;
			}
			DomNode n3 = new DomNode(NodeType.Element)
			{
				TagName = a[1].ToString().ToLowerInvariant()
			};
			return Wrap(n3);
		}
		case "createTextNode":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			DomNode n5 = new DomNode(NodeType.Text)
			{
				TextContent = ((a.Length != 0) ? a[0].ToString() : "")
			};
			return Wrap(n5);
		}
		case "createDocumentFragment":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			DomNode n2 = new DomNode(NodeType.Element)
			{
				TagName = "#fragment"
			};
			return Wrap(n2);
		}
		case "createComment":
		{
			if (binding.Role != Role.Document)
			{
				break;
			}
			DomNode n4 = new DomNode(NodeType.Comment)
			{
				TextContent = ((a.Length != 0) ? a[0].ToString() : "")
			};
			return Wrap(n4);
		}
		case "importNode":
			if (binding.Role != Role.Document)
			{
				break;
			}
			return (a.Length != 0) ? a[0] : JsValue.Null;
		case "adoptNode":
			if (binding.Role != Role.Document)
			{
				break;
			}
			return (a.Length != 0) ? a[0] : JsValue.Null;
		case "attachShadow":
		{
			OnLog?.Invoke($"attachShadow: <{n.TagName}> ja-tem={n.ShadowRoot != null}");
			string mode = "open";
			JsValue jsValue4 = null;
			if (a.Length != 0)
			{
				if (a[0].IsString())
				{
					mode = a[0].ToString();
				}
				else if (a[0] is ObjectInstance objectInstance4)
				{
					JsValue jsValue5 = objectInstance4.Get("mode");
					if (jsValue5.IsString())
					{
						mode = jsValue5.ToString();
					}
					JsValue jsValue6 = objectInstance4.Get("shadyUpgradeFragment");
					if (jsValue6 != null && jsValue6.IsObject())
					{
						jsValue4 = jsValue6;
					}
				}
			}
			DomNode domNode3 = CreateShadowRoot(n, mode);
			Dirty = true;
			DomNode domNode4 = domNode3 ?? n.ShadowRoot;
			if (domNode4 != null)
			{
				if ((object)jsValue4 != null)
				{
					JsValue v = jsValue4;
					if (TryNodeLoose(v, out DomNode node4) && node4 != null)
					{
						foreach (DomNode item3 in node4.Children.ToList())
						{
							node4.Children.Remove(item3);
							item3.Parent = domNode4;
							domNode4.Children.Add(item3);
						}
					}
				}
				return Wrap(domNode4);
			}
			return JsValue.Null;
		}
		case "assignedNodes":
		case "assignedElements":
			return WrapAll(AssignedNodesOf(n));
		case "attachInternals":
			return engine.Evaluate("({ states: new Set(), setFormValue: function(){}, setValidity: function(){}, checkValidity: function(){ return true; } })");
		case "dispatchEvent":
		{
			if (a.Length == 0)
			{
				return true;
			}
			string text4 = "";
			try
			{
				if (a[0] is ObjectInstance objectInstance3)
				{
					JsValue jsValue3 = objectInstance3.Get("type");
					if (jsValue3.IsString())
					{
						text4 = jsValue3.ToString();
					}
				}
				if (text4.Length == 0 && a[0].IsString())
				{
					text4 = a[0].ToString();
				}
			}
			catch
			{
			}
			if (text4.Length > 0)
			{
				DispatchOn(n, text4, a[0]);
			}
			return true;
		}
		case "removeEventListener":
		{
			if (a.Length >= 2 && listeners.TryGetValue(n, out List<(string, JsValue)> value2))
			{
				value2.RemoveAll(((string type, JsValue fn) t) => string.Equals(t.type, a[0].ToString(), StringComparison.Ordinal) && (object)t.fn == a[1]);
			}
			return JsValue.Undefined;
		}
		case "preventDefault":
			try
			{
				if (n != null && a.Length != 0 && a[0] is ObjectInstance objectInstance)
				{
					objectInstance.Set("defaultPrevented", true);
				}
			}
			catch
			{
			}
			return JsValue.Undefined;
		case "execCommand":
			return false;
		case "queryCommandSupported":
			return false;
		case "getElementsByName":
			return WrapAll(FindByName(n, (a.Length != 0) ? a[0].ToString() : ""));
		case "reload":
			OnNavigate?.Invoke(CurrentUrl);
			return JsValue.Undefined;
		case "replace":
			if (a.Length != 0)
			{
				OnNavigate?.Invoke(a[0].ToString());
			}
			return JsValue.Undefined;
		case "assign":
			if (a.Length != 0)
			{
				OnNavigate?.Invoke(a[0].ToString());
			}
			return JsValue.Undefined;
		case "scrollTo":
		case "scrollBy":
		{
			float num12 = 0f;
			float num13 = 0f;
			if (a.Length == 1 && a[0] is ObjectInstance objectInstance6)
			{
				try
				{
					JsValue value6 = objectInstance6.Get("top");
					if (value6.IsNumber())
					{
						num13 = (float)value6.AsNumber();
					}
					JsValue value7 = objectInstance6.Get("left");
					if (value7.IsNumber())
					{
						num12 = (float)value7.AsNumber();
					}
				}
				catch
				{
				}
			}
			else
			{
				if (a.Length != 0 && a[0].IsNumber())
				{
					num12 = (float)a[0].AsNumber();
				}
				if (a.Length > 1 && a[1].IsNumber())
				{
					num13 = (float)a[1].AsNumber();
				}
			}
			if (method == "scrollBy")
			{
				float num14 = GetScroll?.Invoke() ?? 0f;
				num13 += num14;
			}
			OnScrollTo?.Invoke((int)num12, (int)Math.Max(0f, num13));
			return JsValue.Undefined;
		}
		case "initCustomEvent":
		case "initEvent":
			return JsValue.Undefined;
		case "getContext":
		{
			string text7 = ((a.Length != 0) ? a[0].ToString() : "2d");
			if (text7 != "2d" && text7 != "2d,alpha" && text7 != "")
			{
				return JsValue.Null;
			}
			return engine.Evaluate("({\n                    canvas: null, fillStyle: '#000', strokeStyle: '#000', lineWidth: 1,\n                    font: '10px sans-serif', textAlign: 'start', textBaseline: 'alphabetic',\n                    globalAlpha: 1, globalCompositeOperation: 'source-over',\n                    save: function(){}, restore: function(){}, scale: function(){},\n                    rotate: function(){}, translate: function(){}, transform: function(){},\n                    setTransform: function(){}, resetTransform: function(){},\n                    clearRect: function(){}, fillRect: function(){}, strokeRect: function(){},\n                    beginPath: function(){}, closePath: function(){}, moveTo: function(){},\n                    lineTo: function(){}, bezierCurveTo: function(){}, quadraticCurveTo: function(){},\n                    arc: function(){}, arcTo: function(){}, rect: function(){}, ellipse: function(){},\n                    fill: function(){}, stroke: function(){}, clip: function(){},\n                    drawImage: function(){}, createImageData: function(w,h){ return { width:w, height:h, data: [] }; },\n                    getImageData: function(x,y,w,h){ return { width:w, height:h, data: [] }; },\n                    putImageData: function(){},\n                    measureText: function(t){ return { width: String(t).length * 6, actualBoundingBoxAscent: 8, actualBoundingBoxDescent: 2 }; },\n                    fillText: function(){}, strokeText: function(){},\n                    createLinearGradient: function(){ return { addColorStop: function(){} }; },\n                    createRadialGradient: function(){ return { addColorStop: function(){} }; },\n                    createPattern: function(){ return null; },\n                    getLineDash: function(){ return []; }, setLineDash: function(){},\n                    isPointInPath: function(){ return false; }\n                })");
		}
		case "checkVisibility":
			return true;
		case "getAnimations":
			return new JsArray(engine, Array.Empty<JsValue>());
		case "composedPath":
			return WrapAll(new DomNode[1] { n });
		case "showPopover":
		case "hidePopover":
		case "togglePopover":
		case "requestFullscreen":
		case "exitFullscreen":
		case "requestPointerLock":
		case "setPointerCapture":
		case "releasePointerCapture":
		case "moveBefore":
		case "setHTMLUnsafe":
			return JsValue.Undefined;
		case "hasPointerCapture":
			return false;
		case "scrollIntoViewIfNeeded":
			OnFocusRequest?.Invoke(n);
			return JsValue.Undefined;
		case "animate":
			return engine.Evaluate("({ finished: Promise.resolve(), cancel: function(){}, finish: function(){}, play: function(){}, pause: function(){}, onfinish: null })");
		case "createRange":
			if (binding.Role != Role.Document)
			{
				break;
			}
			return engine.Evaluate("({\n                    startContainer: null, endContainer: null, collapsed: true,\n                    createContextualFragment: function (html) { return globalThis.__parseHtml(html); },\n                    selectNodeContents: function () {}, selectNode: function () {},\n                    collapse: function () {}, cloneContents: function () { return globalThis.__parseHtml(''); },\n                    extractContents: function () { return globalThis.__parseHtml(''); },\n                    deleteContents: function () {}, insertNode: function () {}, surroundContents: function () {},\n                    setStart: function () {}, setEnd: function () {}, setStartBefore: function () {},\n                    setStartAfter: function () {}, setEndBefore: function () {}, setEndAfter: function () {},\n                    cloneRange: function () { return this; }, detach: function () {},\n                    getBoundingClientRect: function () { return { x:0,y:0,width:0,height:0,top:0,left:0,right:0,bottom:0 }; },\n                    getClientRects: function () { return []; }\n                })");
		case "createTreeWalker":
		case "createNodeIterator":
			return engine.Evaluate("({\n                    root: null, currentNode: null, whatToShow: -1, filter: null,\n                    nextNode: function(){ return null; }, previousNode: function(){ return null; },\n                    parentNode: function(){ return null; }, firstChild: function(){ return null; },\n                    lastChild: function(){ return null; }, nextSibling: function(){ return null; },\n                    previousSibling: function(){ return null; }\n                })");
		case "elementFromPoint":
		case "getSelection":
			return JsValue.Null;
		case "createEvent":
		{
			string text5 = ((a.Length != 0) ? a[0].ToString() : "Event");
			if (engine.Evaluate("(function(tipo){ return { type: tipo, bubbles: false, cancelable: false, detail: null, target: null, currentTarget: null, defaultPrevented: false, initCustomEvent: function(t,b,c,d){ this.type=t; this.bubbles=!!b; this.cancelable=!!c; this.detail=d; }, initEvent: function(t,b,c){ this.type=t; this.bubbles=!!b; this.cancelable=!!c; }, preventDefault: function(){ this.defaultPrevented=true; }, stopPropagation: function(){}, stopImmediatePropagation: function(){} }; })") is Function value4)
			{
				JsValue jsValue7 = value4.Call(JsValue.Undefined, new JsValue[1] { text5 });
				if ((object)jsValue7 != null)
				{
					result = jsValue7;
					goto IL_28e3;
				}
			}
			result = JsValue.Null;
			goto IL_28e3;
		}
		case "elementsFromPoint":
			return WrapAll(Array.Empty<DomNode>());
		case "insertBefore":
		{
			if (a.Length != 0 && TryNode(a[0], out DomNode node))
			{
				DomNode domNode2;
				if (a.Length <= 1)
				{
					domNode2 = null;
				}
				else
				{
					domNode2 = (TryNode(a[1], out DomNode node2) ? node2 : null);
				}
				node.Parent?.Children.Remove(node);
				node.Parent = n;
				if (domNode2 != null)
				{
					int num4 = n.Children.IndexOf(domNode2);
					if (num4 >= 0)
					{
						n.Children.Insert(num4, node);
					}
					else
					{
						n.Children.Add(node);
					}
				}
				else
				{
					n.Children.Add(node);
				}
				Dirty = true;
				RecordMut("childList", n, null, new List<DomNode> { node });
				if (node.TagName == "script")
				{
					OnScriptInserted?.Invoke(node);
				}
				return Wrap(node);
			}
			return JsValue.Undefined;
		}
		case "replaceChild":
		{
			if (a.Length >= 2 && TryNode(a[0], out DomNode node12) && TryNode(a[1], out DomNode node13))
			{
				int num10 = n.Children.IndexOf(node13);
				node13.Parent = null;
				node12.Parent?.Children.Remove(node12);
				node12.Parent = n;
				if (num10 >= 0)
				{
					n.Children[num10] = node12;
				}
				else
				{
					n.Children.Add(node12);
				}
				Dirty = true;
				RecordMut("childList", n, null, new List<DomNode> { node12 });
				return Wrap(node13);
			}
			return JsValue.Undefined;
		}
		case "remove":
		{
			DomNode parent3 = n.Parent;
			parent3?.Children.Remove(n);
			n.Parent = null;
			Dirty = true;
			RecordMut("childList", parent3 ?? n);
			return JsValue.Undefined;
		}
		case "replaceWith":
		case "replaceChildren":
		{
			DomNode node10 = n.Parent ?? n;
			foreach (DomNode item4 in n.Children.ToList())
			{
				item4.Parent = null;
			}
			n.Children.Clear();
			JsValue[] array7 = a;
			foreach (JsValue jsValue10 in array7)
			{
				if (TryNode(jsValue10, out DomNode node11) && node11 != null)
				{
					node11.Parent?.Children.Remove(node11);
					node11.Parent = n;
					n.Children.Add(node11);
				}
				else if (method == "replaceChildren")
				{
					n.Children.Add(new DomNode(NodeType.Text)
					{
						TextContent = jsValue10.ToString(),
						Parent = n
					});
				}
			}
			Dirty = true;
			RecordMut("childList", node10);
			return JsValue.Undefined;
		}
		case "append":
		case "prepend":
		{
			List<DomNode> list7 = new List<DomNode>();
			JsValue[] array6 = a;
			foreach (JsValue jsValue9 in array6)
			{
				if (TryNode(jsValue9, out DomNode node8) && node8 != null)
				{
					list7.Add(node8);
				}
				else if (jsValue9.IsString() || jsValue9.IsNumber())
				{
					list7.Add(new DomNode(NodeType.Text)
					{
						TextContent = jsValue9.ToString()
					});
				}
			}
			foreach (DomNode item5 in list7)
			{
				item5.Parent?.Children.Remove(item5);
				item5.Parent = n;
				if (method == "append")
				{
					n.Children.Add(item5);
				}
				else
				{
					n.Children.Insert(0, item5);
				}
				if (item5.TagName == "script")
				{
					OnScriptInserted?.Invoke(item5);
				}
			}
			Dirty = true;
			RecordMut("childList", n, null, list7);
			return JsValue.Undefined;
		}
		case "before":
		case "after":
		{
			DomNode parent2 = n.Parent;
			if (parent2 != null)
			{
				int num5 = parent2.Children.IndexOf(n);
				if (method == "after")
				{
					num5++;
				}
				JsValue[] array4 = a;
				foreach (JsValue v2 in array4)
				{
					if (TryNode(v2, out DomNode node6) && node6 != null)
					{
						node6.Parent?.Children.Remove(node6);
						node6.Parent = parent2;
						if (num5 >= 0 && num5 <= parent2.Children.Count)
						{
							parent2.Children.Insert(num5++, node6);
						}
						else
						{
							parent2.Children.Add(node6);
						}
					}
				}
				Dirty = true;
				RecordMut("childList", parent2);
			}
			return JsValue.Undefined;
		}
		case "cloneNode":
		{
			bool deep = a.Length != 0 && a[0].AsBoolean();
			return Wrap(CloneNode(n, deep));
		}
		case "insertAdjacentHTML":
		{
			if (a.Length < 2)
			{
				return JsValue.Undefined;
			}
			string text2 = a[0].ToString().ToLowerInvariant();
			DomNode domNode = new HtmlParser(a[1].ToString()).Parse();
			List<DomNode> list5 = domNode.Children.ToList();
			foreach (DomNode item6 in list5)
			{
				domNode.Children.Remove(item6);
				item6.Parent = n;
			}
			DomNode parent = n.Parent;
			switch (text2)
			{
			case "beforebegin":
			{
				if (parent == null)
				{
					break;
				}
				int num2 = parent.Children.IndexOf(n);
				foreach (DomNode item7 in list5)
				{
					item7.Parent = parent;
					parent.Children.Insert(num2++, item7);
				}
				break;
			}
			case "afterbegin":
			{
				for (int num3 = list5.Count - 1; num3 >= 0; num3--)
				{
					n.Children.Insert(0, list5[num3]);
				}
				break;
			}
			case "beforeend":
				foreach (DomNode item8 in list5)
				{
					n.Children.Add(item8);
				}
				break;
			case "afterend":
			{
				if (parent == null)
				{
					break;
				}
				int num = parent.Children.IndexOf(n) + 1;
				foreach (DomNode item9 in list5)
				{
					item9.Parent = parent;
					parent.Children.Insert(num++, item9);
				}
				break;
			}
			}
			Dirty = true;
			bool flag4 = ((text2 == "beforebegin" || text2 == "afterend") ? true : false);
			RecordMut("childList", flag4 ? (parent ?? n) : n, null, list5);
			foreach (DomNode item10 in list5)
			{
				if (item10.TagName == "script")
				{
					OnScriptInserted?.Invoke(item10);
					continue;
				}
				foreach (DomNode item11 in Descendants(item10))
				{
					if (item11.TagName == "script")
					{
						OnScriptInserted?.Invoke(item11);
					}
				}
			}
			return JsValue.Undefined;
		}
		case "insertAdjacentElement":
		{
			if (a.Length < 2 || !TryNode(a[1], out DomNode node15))
			{
				return JsValue.Null;
			}
			string text8 = a[0].ToString().ToLowerInvariant();
			DomNode parent4 = n.Parent;
			node15.Parent?.Children.Remove(node15);
			switch (text8)
			{
			case "beforebegin":
				if (parent4 != null)
				{
					node15.Parent = parent4;
					int num11 = parent4.Children.IndexOf(n);
					parent4.Children.Insert((num11 < 0) ? parent4.Children.Count : num11, node15);
				}
				break;
			case "afterbegin":
				node15.Parent = n;
				n.Children.Insert(0, node15);
				break;
			case "beforeend":
				node15.Parent = n;
				n.Children.Add(node15);
				break;
			case "afterend":
				if (parent4 != null)
				{
					node15.Parent = parent4;
					int index = parent4.Children.IndexOf(n) + 1;
					parent4.Children.Insert(index, node15);
				}
				break;
			}
			Dirty = true;
			RecordMut("childList", n, null, new List<DomNode> { node15 });
			return Wrap(node15);
		}
		case "getAttributeNames":
			return WrapStrings(n.Attributes.Keys.ToList());
		case "toggleAttribute":
		{
			if (a.Length == 0)
			{
				return false;
			}
			string key = a[0].ToString();
			bool flag5 = a.Length > 1 && a[1].AsBoolean();
			bool flag6 = n.Attributes.ContainsKey(key);
			bool flag7 = ((a.Length > 1) ? flag5 : (!flag6));
			if (flag7)
			{
				n.Attributes[key] = "";
			}
			else
			{
				n.Attributes.Remove(key);
			}
			Dirty = true;
			return flag7;
		}
		case "matches":
			return a.Length != 0 && CssParser.MatchesSelector(n, a[0].ToString());
		case "closest":
		{
			if (a.Length == 0)
			{
				return JsValue.Null;
			}
			string selector = a[0].ToString();
			for (DomNode domNode10 = n; domNode10 != null; domNode10 = domNode10.Parent)
			{
				if (domNode10.Type == NodeType.Element && CssParser.MatchesSelector(domNode10, selector))
				{
					return Wrap(domNode10);
				}
			}
			return JsValue.Null;
		}
		case "contains":
		{
			if (a.Length != 0 && TryNode(a[0], out DomNode node9))
			{
				for (DomNode domNode8 = node9; domNode8 != null; domNode8 = domNode8.Parent)
				{
					if (domNode8 == n)
					{
						return true;
					}
				}
			}
			return false;
		}
		case "hasFocus":
			return true;
		case "getRootNode":
			return Wrap(RootOf(n));
		case "writeln":
			if (binding.Role != Role.Document)
			{
				break;
			}
			goto case "write";
		case "write":
		{
			DomNode domNode6 = n.FindTag("body") ?? n;
			JsValue[] array5 = a;
			foreach (JsValue jsValue8 in array5)
			{
				DomNode domNode7 = new HtmlParser(jsValue8.ToString()).Parse();
				foreach (DomNode item12 in domNode7.Children.ToList())
				{
					domNode7.Children.Remove(item12);
					item12.Parent = domNode6;
					domNode6.Children.Add(item12);
				}
			}
			Dirty = true;
			return JsValue.Undefined;
		}
		case "getClientRects":
		{
			RectangleF? rectangleF2 = MeasureBox?.Invoke(n);
			List<JsValue> list6 = new List<JsValue>();
			if (rectangleF2.HasValue)
			{
				RectangleF valueOrDefault2 = rectangleF2.GetValueOrDefault();
				if (valueOrDefault2.Width > 0f && valueOrDefault2.Height > 0f)
				{
					ObjectInstance objectInstance5 = (ObjectInstance)engine.Evaluate("({})");
					objectInstance5.FastSetDataProperty("x", valueOrDefault2.X);
					objectInstance5.FastSetDataProperty("y", valueOrDefault2.Y);
					objectInstance5.FastSetDataProperty("width", valueOrDefault2.Width);
					objectInstance5.FastSetDataProperty("height", valueOrDefault2.Height);
					objectInstance5.FastSetDataProperty("top", valueOrDefault2.Y);
					objectInstance5.FastSetDataProperty("left", valueOrDefault2.X);
					objectInstance5.FastSetDataProperty("right", valueOrDefault2.X + valueOrDefault2.Width);
					objectInstance5.FastSetDataProperty("bottom", valueOrDefault2.Y + valueOrDefault2.Height);
					list6.Add(objectInstance5);
				}
			}
			return new JsArray(engine, list6.ToArray());
		}
		case "getBoxQuads":
			return new JsArray(engine, Array.Empty<JsValue>());
		case "scroll":
		case "scrollByLines":
		case "scrollByPages":
			Dirty = true;
			return JsValue.Undefined;
		case "checkValidity":
		case "reportValidity":
			return true;
		case "setCustomValidity":
		case "setSelectionRange":
		case "setRangeText":
		case "stepUp":
		case "stepDown":
		case "select":
		case "normalize":
		case "insertAdjacentText":
		case "setAttributeNS":
		case "getAttributeNS":
		case "removeAttributeNS":
		case "hasAttributeNS":
		case "getElementsByTagNameNS":
		case "convertPointFromNode":
		case "convertRectFromNode":
		case "convertRectToNode":
			return JsValue.Undefined;
		case "isEqualNode":
		{
			DomNode node5;
			return n == ((a.Length == 0) ? null : (TryNodeLoose(a[0], out node5) ? node5 : null));
		}
		case "isSameNode":
		{
			DomNode node3;
			return a.Length != 0 && TryNodeLoose(a[0], out node3) && n == node3;
		}
		case "compareDocumentPosition":
			return 0;
		case "isDefaultNamespace":
		case "lookupNamespaceURI":
		case "lookupPrefix":
			return JsValue.Null;
		case "getBoundingClientRect":
		{
			RectangleF? rectangleF = MeasureBox?.Invoke(n);
			objectInstance2 = (ObjectInstance)engine.Evaluate("({})");
			if (rectangleF.HasValue)
			{
				RectangleF valueOrDefault = rectangleF.GetValueOrDefault();
				if (true)
				{
					objectInstance2.FastSetDataProperty("x", valueOrDefault.X);
					objectInstance2.FastSetDataProperty("y", valueOrDefault.Y);
					objectInstance2.FastSetDataProperty("width", valueOrDefault.Width);
					objectInstance2.FastSetDataProperty("height", valueOrDefault.Height);
					objectInstance2.FastSetDataProperty("top", valueOrDefault.Y);
					objectInstance2.FastSetDataProperty("left", valueOrDefault.X);
					objectInstance2.FastSetDataProperty("right", valueOrDefault.X + valueOrDefault.Width);
					objectInstance2.FastSetDataProperty("bottom", valueOrDefault.Y + valueOrDefault.Height);
					goto IL_3ca5;
				}
			}
			string[] array3 = new string[8] { "x", "y", "width", "height", "top", "left", "right", "bottom" };
			foreach (string name in array3)
			{
				objectInstance2.FastSetDataProperty(name, 0);
			}
			goto IL_3ca5;
		}
		case "focus":
			OnFocusRequest?.Invoke(n);
			return JsValue.Undefined;
		case "blur":
			OnBlurRequest?.Invoke(n);
			return JsValue.Undefined;
		case "scrollIntoView":
			{
				return JsValue.Undefined;
			}
			IL_3ca5:
			return objectInstance2;
			IL_28e3:
			return result;
		}
		return JsValue.Undefined;
		List<string> Classes()
		{
			return n.GetAttr("class").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
		}
		void Save(List<string> c)
		{
			n.Attributes["class"] = string.Join(" ", c);
			Dirty = true;
			RecordMut("attributes", n, "class");
		}
	}

	private bool TryNode(JsValue v, out DomNode? node)
	{
		node = null;
		Binding value = default;
		foreach (KeyValuePair<int, JsValue> item in proxyCache)
		{
			bool flag = (object)item.Value == v && bindings.TryGetValue(item.Key, out value);
			bool flag2 = flag;
			if (flag2)
			{
				Role role = value.Role;
				bool flag3 = (uint)role <= 1u;
				flag2 = flag3;
			}
			if (flag2)
			{
				node = value.Node;
				return true;
			}
		}
		return false;
	}

	private bool TryNodeLoose(JsValue v, out DomNode? node)
	{
		node = null;
		if (TryNode(v, out node) && node != null)
		{
			return true;
		}
		JsValue value = ResolveId(v);
		if (value.IsNumber() && bindings.TryGetValue((int)value.AsNumber(), out Binding value2))
		{
			node = value2.Node;
			return node != null;
		}
		return false;
	}

	private static string? StrJs(JsValue v)
	{
		return v.IsString() ? v.AsString() : null;
	}

	private int ImportTreeNode(ObjectInstance o, DomNode pai, int prof)
	{
		if (prof > 14 || _importados >= 1200)
		{
			return 0;
		}
		string text = StrJs(o.Get("t"));
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}
		switch (text)
		{
		case "#text":
		{
			string text5 = StrJs(o.Get("x"));
			if (string.IsNullOrEmpty(text5))
			{
				return 0;
			}
			pai.Children.Add(new DomNode(NodeType.Text)
			{
				TextContent = text5,
				Parent = pai
			});
			_importados++;
			return 1;
		}
		case "#comment":
			return 0;
		case "#fragment":
			if (o.Get("k") is JsArray jsArray2)
			{
				int num3 = 0;
				foreach (JsValue item in jsArray2)
				{
					if (item is ObjectInstance o4)
					{
						num3 += ImportTreeNode(o4, pai, prof + 1);
					}
				}
				return num3;
			}
			return 0;
		default:
		{
			DomNode domNode = new DomNode(NodeType.Element)
			{
				TagName = text,
				Parent = pai
			};
			_importados++;
			_importadosLista.Add(domNode);
			int num = 1;
			string value = StrJs(o.Get("i"));
			if (!string.IsNullOrEmpty(value))
			{
				domNode.Attributes["id"] = value;
			}
			string value2 = StrJs(o.Get("c"));
			if (!string.IsNullOrEmpty(value2))
			{
				domNode.Attributes["class"] = value2;
			}
			string value3 = StrJs(o.Get("s"));
			if (!string.IsNullOrEmpty(value3))
			{
				domNode.Attributes["style"] = value3;
			}
			string text2 = StrJs(o.Get("e"));
			if (!string.IsNullOrEmpty(text2))
			{
				string[] array = text2.Split('|');
				foreach (string text3 in array)
				{
					int num2 = text3.IndexOf(':');
					if (num2 > 0)
					{
						string text4 = text3.Substring(0, num2);
						string value4 = text3.Substring(num2 + 1);
						if (text4.Length > 0 && text4.Length < 40 && !domNode.Attributes.ContainsKey(text4))
						{
							domNode.Attributes[text4] = value4;
						}
					}
				}
			}
			pai.Children.Add(domNode);
			if (o.Get("r") is ObjectInstance o2)
			{
				DomNode domNode2 = CreateShadowRoot(domNode, "open");
				if (domNode2 != null)
				{
					num += ImportTreeNode(o2, domNode2, prof + 1);
					Dirty = true;
				}
			}
			if (o.Get("k") is JsArray jsArray)
			{
				foreach (JsValue item2 in jsArray)
				{
					if (item2 is ObjectInstance o3)
					{
						num += ImportTreeNode(o3, domNode, prof + 1);
					}
				}
			}
			return num;
		}
		}
	}

	private static string DumpExpurgo(DomNode n, int prof)
	{
		if (n == null || prof > 3)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DomNode child in n.Children)
		{
			if (child.Type == NodeType.Element)
			{
				if (stringBuilder.Length > 800)
				{
					break;
				}
				stringBuilder.Append(child.TagName);
				string attr = child.GetAttr("id");
				if (!string.IsNullOrEmpty(attr))
				{
					stringBuilder.Append('#').Append(attr);
				}
				if (child.ShadowRoot != null)
				{
					stringBuilder.Append("[SR+").Append(child.ShadowRoot.Children.Count).Append(']');
				}
				stringBuilder.Append('(').Append(child.Children.Count).Append(") ; ");
				if (prof < 3)
				{
					stringBuilder.Append(DumpExpurgo(child, prof + 1));
				}
			}
		}
		return stringBuilder.ToString();
	}

	private (JsValue evObj, bool[] prevented) MakeEvent(string type, DomNode target)
	{
		bool[] prevented = new bool[1];
		ObjectInstance objectInstance = (ObjectInstance)engine.Evaluate("({})");
		objectInstance.FastSetDataProperty("type", type);
		objectInstance.FastSetDataProperty("target", Wrap(target));
		objectInstance.FastSetDataProperty("preventDefault", new ClrFunction(engine, "preventDefault", (JsValue self, JsValue[] args) =>
		{
			prevented[0] = true;
			return JsValue.Undefined;
		}));
		return (evObj: objectInstance, prevented: prevented);
	}

	internal void AddClick(DomNode n, JsValue fn)
	{
		if (!clickHandlers.TryGetValue(n, out List<JsValue> value))
		{
			value = (clickHandlers[n] = new List<JsValue>());
		}
		value.Add(fn);
	}

	private void SetImgHandler(DomNode n, string type, JsValue fn)
	{
		imgHandlers.RemoveAll(((DomNode node, JsValue? onload, JsValue? onerror) x) => x.node == n);
		if (type == "load")
		{
			imgHandlers.Add((n, fn, null));
		}
		else
		{
			imgHandlers.Add((n, null, fn));
		}
	}

	internal void AddListener(DomNode n, string type, JsValue fn)
	{
		if (!listeners.TryGetValue(n, out List<(string, JsValue)> value))
		{
			value = (listeners[n] = new List<(string, JsValue)>());
		}
		value.Add((type, fn));
	}

	internal void DispatchOn(DomNode n, string tipo, JsValue evObj)
	{
		for (DomNode domNode = n; domNode != null; domNode = domNode.Parent)
		{
			if (listeners.TryGetValue(domNode, out List<(string, JsValue)> value))
			{
				foreach (var (a, fn) in value.ToList())
				{
					if (string.Equals(a, tipo, StringComparison.Ordinal))
					{
						CallFn(fn, Wrap(domNode), new JsValue[1] { evObj });
					}
				}
			}
			bool flag = false;
			try
			{
				if (evObj is ObjectInstance objectInstance)
				{
					JsValue jsValue = objectInstance.Get("bubbles");
					if ((object)jsValue != null && jsValue.IsBoolean())
					{
						flag = jsValue.AsBoolean();
					}
				}
			}
			catch
			{
			}
			if (!flag)
			{
				break;
			}
		}
	}

	internal ComputedStyle StyleOf(DomNode n)
	{
		if (resolver.TryGet(n, out ComputedStyle cs) && cs != null)
		{
			return cs;
		}
		return new ComputedStyle();
	}

	private static List<DomNode> FindByName(DomNode n, string nome)
	{
		List<DomNode> list = new List<DomNode>();
		if (nome.Length == 0)
		{
			return list;
		}
		foreach (DomNode item in Descendants(RootOf(n)))
		{
			if (item.Type == NodeType.Element && string.Equals(item.GetAttr("name"), nome, StringComparison.Ordinal))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private static DomNode? FindTag(DomNode n, string tag)
	{
		if (n.Type == NodeType.Element && n.TagName == tag)
		{
			return n;
		}
		foreach (DomNode child in n.Children)
		{
			DomNode domNode = FindTag(child, tag);
			if (domNode != null)
			{
				return domNode;
			}
		}
		return null;
	}

	private static DomNode? FindById(DomNode n, string id)
	{
		if (n.Type == NodeType.Element && n.GetAttr("id") == id)
		{
			return n;
		}
		foreach (DomNode child in n.Children)
		{
			DomNode domNode = FindById(child, id);
			if (domNode != null)
			{
				return domNode;
			}
		}
		return null;
	}

	private static List<DomNode> FindTags(DomNode n, string tag)
	{
		List<DomNode> list = new List<DomNode>();
		Walk(n);
		return list;
		void Walk(DomNode x)
		{
			if (x.Type == NodeType.Element && x.TagName == tag)
			{
				list.Add(x);
			}
			foreach (DomNode child in x.Children)
			{
				Walk(child);
			}
		}
	}

	private static DomNode? FindByNameOrId(DomNode n, string name)
	{
		DomNode best = null;
		Walk(n);
		if (best != null)
		{
			return best;
		}
		DomNode ctrl = null;
		Walk2(n);
		return ctrl;
		void Walk(DomNode x)
		{
			if (best != null)
			{
				return;
			}
			if (x.Type == NodeType.Element)
			{
				if (x.TagName == "form" && (x.GetAttr("name") == name || x.GetAttr("id") == name))
				{
					best = x;
					return;
				}
				string tagName = x.TagName;
				bool flag = ((tagName == "img" || tagName == "a") ? true : false);
				if (flag && (x.GetAttr("name") == name || x.GetAttr("id") == name))
				{
					best = x;
					return;
				}
			}
			foreach (DomNode child in x.Children)
			{
				Walk(child);
			}
		}
		void Walk2(DomNode x)
		{
			if (ctrl == null)
			{
				bool flag = x.Type == NodeType.Element;
				bool flag2 = flag;
				if (flag2)
				{
					bool flag3;
					switch (x.TagName)
					{
					case "input":
					case "select":
					case "textarea":
					case "button":
					case "object":
					case "embed":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag2 = flag3;
				}
				if (!flag2 || (!(x.GetAttr("name") == name) && !(x.GetAttr("id") == name)))
				{
					foreach (DomNode child2 in x.Children)
					{
						Walk2(child2);
					}
					return;
				}
				ctrl = x;
			}
		}
	}

	private void SaveDecls(DomNode n, Dictionary<string, string> d)
	{
		n.Attributes["style"] = string.Join("; ", d.Select((KeyValuePair<string, string> kv) => kv.Key + ": " + kv.Value));
		Dirty = true;
		RecordMut("attributes", n, "style");
	}

	private static string InnerHtmlOf(DomNode n)
	{
		if (n.Type == NodeType.Text)
		{
			return n.TextContent;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('<').Append(n.TagName);
		foreach (KeyValuePair<string, string> attribute in n.Attributes)
		{
			stringBuilder.Append(' ').Append(attribute.Key).Append("=\"")
				.Append(attribute.Value.Replace("\"", "&quot;"))
				.Append('"');
		}
		stringBuilder.Append('>');
		foreach (DomNode child in n.Children)
		{
			stringBuilder.Append(InnerHtmlOf(child));
		}
		stringBuilder.Append("</").Append(n.TagName).Append('>');
		return stringBuilder.ToString();
	}

	private static string ToCamel(string s)
	{
		string[] array = s.Split('-', StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return s;
		}
		StringBuilder stringBuilder = new StringBuilder(array[0]);
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i].Length > 0)
			{
				stringBuilder.Append(char.ToUpperInvariant(array[i][0])).Append(array[i].Substring(1));
			}
		}
		return stringBuilder.ToString();
	}

	private static string ToDash(string camel)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in camel)
		{
			if (char.IsUpper(c))
			{
				stringBuilder.Append('-').Append(char.ToLowerInvariant(c));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}
}

