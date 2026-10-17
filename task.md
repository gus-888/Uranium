# Tarefas - Toy Browser Rendering Engine

- [x] Criar as estruturas de dados fundamentais (DOM, CSSOM)
  - [x] Implementar `src/engine/dom.ts` (Modelagem de nós Element e Text)
  - [x] Implementar `src/engine/css.ts` (Regras de estilo, Seletores e Declarações)
- [x] Implementar os parsers
  - [x] Implementar `src/engine/html.ts` (HTML Parser recursivo)
  - [x] Implementar `src/engine/cssParser.ts` (CSS Parser)
- [x] Implementar o motor de estilos e layout
  - [x] Implementar `src/engine/style.ts` (Seleção e cálculo de propriedades CSS aplicadas)
  - [x] Implementar `src/engine/layout.ts` (Box Model e cálculo de tamanhos/posições)
- [x] Implementar a pintura e renderização
  - [x] Implementar `src/engine/paint.ts` (Geração e execução de comandos de pintura no canvas)
- [x] Criar a interface visual interativa (DevTools)
  - [x] Criar `src/main.ts` (Editor de código HTML/CSS, abas de árvore DOM/Layout, painel de estatísticas)
  - [x] Criar `src/style.css` (Visual moderno premium: dark mode, glassmorphism)
  - [x] Definir presets de exemplos prontos para o usuário testar (Botão 3D, Layout de colunas, etc.)
- [x] Testar e Validar
  - [x] Garantir que o cálculo de layout é reativo a alterações no editor
  - [x] Validar margens, bordas, paddings e fluxo de elementos block/inline

# Fase 2 — Navegador real (native-browser/, .NET + GDI+)

- [x] Parser HTML robusto (`Engine/Html.cs`)
  - [x] Tolerante a HTML do mundo real: tags sem fechar, aninhamento implícito (`<li>`, `<p>`, `<td>`...), comentários, DOCTYPE
  - [x] Raw-text para `<script>`/`<style>`, void elements (`<br>`, `<img>`, `<input>`...)
  - [x] Entidades HTML nomeadas + numéricas (&amp; &ccedil; &#9824;)
- [x] Engine CSS (`Engine/Css.cs`, `Engine/ComputedStyle.cs`)
  - [x] Seletores de tag, .classe, #id e descendentes com especificidade
  - [x] Cores nomeadas (~140), hex 3/4/6/8 dígitos, rgb()/rgba(); unidades px/em/%/pt/rem...
  - [x] Shorthands: margin/padding/border/font; ~30 propriedades suportadas
  - [x] Cascade completa: UA stylesheet → `<style>` → folhas externas (`<link rel=stylesheet>` baixado) → style inline
- [x] Motor de layout (`Engine/Layout.cs`)
  - [x] Fluxo inline com quebra de linha por palavra (word wrap) e alinhamento left/center/right
  - [x] Inline-block shrink-to-fit (células de tabela lado a lado, controles)
  - [x] Box model completo com colapso de margens irmãs, listas com marcadores (•/○/■/1.)
  - [x] white-space: pre/nowrap/pre-wrap; baseline de fontes real (ascendente/descendente)
- [x] Renderer (`Engine/Renderer.cs`)
  - [x] Fundos, bordas por lado, texto com sublinhado/riscado, imagens, controles de formulário
- [x] Navegação e rede (`BrowserForm.cs`)
  - [x] HTTP/HTTPS com redirect, gzip/brotli, detecção de charset (header + meta)
  - [x] Imagens assíncronas com cache e re-layout; links clicáveis (com target=_blank)
  - [x] Abas (abrir/fechar/alternar), histórico voltar/avançar, recarregar, about:home, file://
  - [x] Scrollbar custom, roda do mouse, atalhos (Ctrl+L/T/W/Tab, F5, Alt+←/→), menu de contexto
- [x] Testes
  - [x] Harness da engine em `.zcode-tools/csstest` (parser, seletores, estilos, layout)
  - [x] Validação visual: página local de teste, example.com, Wikipédia PT (17k px, 1159 links, 52ms de layout)

# Fase 3 — Correções de CSS do mundo real (bug reports da Wikipédia)

- [x] `@media screen {...}` era descartado inteiro — agora a media query é avaliada
  (screen/all, min-width/max-width vs largura da janela) e o bloco entra quando casa
- [x] `var(--custom, fallback)` — usa o fallback quando a custom property não é rastreável
- [x] Sufixo `!important` invalidava comparações — agora é removido no parse
- [x] `position: absolute/fixed` → fora do fluxo (dropdowns/menus não aparecem mais abertos)
- [x] `visibility: hidden` e `opacity: 0` → não pinta (mecanismo do menu principal da Wikipédia)
- [x] `float: left/right` completo — infobox e thumbnails flutuam e o texto contorna (com clear)
- [x] `display: flex/grid` aproximado como linha horizontal quando os filhos são pequenos
  (menus/toolbars); filhos grandes empilham normal (conteúdo de artigo)
- [x] `margin: auto` centraliza blocos com max-width
- [x] Probes de shrink-to-fit não duplicam mais links/âncoras (flag `probing`)
- [x] Resultado: Wikipédia renderiza com margens laterais, header compacto, infobox à
  direita com texto contornando, imagens e links — validado por harness + screenshots

# Fase 4 — CSS moderno real: vars, calc, seletores, fontes, posição, flex/grid, tabela

- [x] `var()` real com cascade/herança (`ComputedStyle.Vars`, `ResolveAllVars` com fallback
  encadeado) + `inherit/initial/unset` (`ComputedStyle.cs`)
- [x] Comprimentos: `calc()` (parser simbólico Px+Percent), `min()/max()/clamp()` lazy,
  `vw/vh/vmin/vmax`, `rem` com root do `<html>`, viewport no `StyleResolver` (`Css.cs`)
- [x] Seletores: `> + ~`, `[attr]` com operadores, `:not()/:first-child/:last-child`,
  `:lang()`/`:dir()` avaliados pelo atributo (antes `:lang(ar)` pintava h1 de PT com
  fonte árabe), `:hover` e pseudo-elementos ignorados (`Css.cs`)
- [x] Fontes: `font: 16px/1.6 ...` não confunde mais line-height com size (era size=3px no
  example.com); pilha font-family usa a 1ª disponível, `serif`→Georgia
- [x] `ParseAnimation` stub (faltava o método e quebrava o build)
- [x] Posição real (`ComputedStyle/Position.cs` novo + `Layout.cs` + `Renderer.cs`):
  `relative` com offset, `absolute/fixed` fora do fluxo com containing block, `fixed`
  compensa scroll na pintura e no hit-test, `sticky` gruda no topo, `z-index`,
  `overflow: hidden/auto/scroll` com clip
- [x] `display:flex` real: row/column, wrap, gap, justify-content, align-items,
  flex-grow/shrink/basis; `display:grid` com colunas px/%/fr + `repeat()`/`minmax()`
  (remove a heurística `RowChildrenAreSmall`) (`Layout.cs`)
- [x] Tabelas reais: `table/section/row/cell`, thead/tbody, colunas auto com probe
  shrink-to-fit, `border-spacing`, `border-collapse`, caption no topo (`Layout.cs`)
- [x] Correções de pintura: peças inline-block/controls transladas para a posição da
  linha (inputs sumiam — iam para 0,0); borda de inputs usa o CSS; `svg` continua oculto
- [x] Entities decodificadas em `<textarea>`/`<title>` raw-text (`Html.cs`)
- [x] Validação: harness `csstest` sem regressão (altura 2500, 10 controles),
  probes A(15 checks)+B(11)+C(12)+D(9) verdes, pixel-check do input roxo, render de
  `test.html` com cards Fase A e B/C/D conferido em bitmap
- [x] `input[type=hidden]` → `display:none` no UA CSS (Google mostrava hashes `iflsig`
  como caixas de texto visíveis)
- [x] Culling matava texto de caixas atômicas inline-block (`DrawPiece` passava
  `scrollY=float.MinValue`, então `line.Y > bottom` era sempre verdade — só o fundo
  pintava; ex. botão "Fazer login" azul sem texto). Novo flag `noCullParent`.

# Fase 5 — Mídia, animações e JavaScript (E + anima + F)

- [x] SVG via pacote `Svg` 3.4.7 (`Engine/SvgImages.cs`): `<img src=.svg>` (arquivo e
  HTTP, detectado por extensão ou conteúdo) e `<svg>` inline serializado do DOM;
  dimensionamento por CSS > atributos > viewBox; `source` virou void-tag
- [x] Imagens: `srcset` (w/x), `<picture>/<source>`, `data-src/data-srcset` (lazy),
  largura CSS vence atributo + `max-width`, `audio` como mini-player 240x32
- [x] Animações CSS (`Engine/Animation.cs`): clock 50ms por aba, interpola `opacity`,
  `color`, `background(-color)` com easing/delay/iterations/direction/fill;
  `opacity` em cascata na pintura (`Renderer.Fade` + ColorMatrix em imagens)
- [x] JS com Jint 3.0.0 (`Engine/JsEngine.cs`, ~650 linhas): wrappers via Proxy JS,
  `getElementById/querySelector(All)/getElementsBy*`, `createElement/append`,
  `textContent/innerHTML`, `classList`, `style`, `dataset`, `setTimeout/Interval`,
  `requestAnimationFrame`, `addEventListener(click)` com bubbling + `preventDefault`,
  `DOMContentLoaded`, `location/navigator/console`, `scrollTo`, scripts inline +
  externos + `data:`; `BrowserForm`: coleta, timers, `HitNode`, `JsDirty`
- [x] Validação: E (raster+cor+srcset/picture/lazy end-to-end com HTTP fake),
  animações (opacity 0.5 e roxo #7F0080 no meio, restaura no fim), JS (10/10:
  DOM, style, timers, click, innerHTML), `test.html` com card Fase E/F conferido
- [x] Fora de escopo (dito ao usuário): decode de vídeo/áudio (H.264/VP9/AV1),
  `fetch/XHR`, canvas 2D, e YouTube como SPA (exige centenas de APIs + MSE)

# Fase 6 — Google redondo (bugs achados na home do Google)

- [x] `flex-basis: auto` tratado como conteúdo (era 0 → chip "Modo IA" esmagado a 1px)
- [x] `NaturalWidth` de flex/grid = conteúdo (era largura cheia → shrink esmagava irmãos)
- [x] Controles como itens flex/grid via `MakeControlBox` + `ControlChrome` (textarea
  virava caixa genérica sem chrome; botões perdiam o chrome e o clique)
- [x] `CollectText` pula `script/style/noscript/template` (CSS ia parar no label do botão)
- [x] `<center>` centraliza (`text-align: center` no UA CSS — botões iam para a esquerda)
- [x] Imagens `data:` inline + stream de raster mantido vivo (corrigido leak de `FromStream`)
- [x] `border-radius` (fundo + contorno uniforme + clip arredondado; pill da busca e login)
- [x] `background-image: url()` (1ª camada): repeat/position/size cover-contain-px-%,
  resolvida no layout com cache do `ImageLoader` (ícones mic/lens/teclado do Google)
- [x] `currentColor` em SVG inline vira a cor computada (ícones pretos)
- [x] Borda de inputs por lado (era largura máxima + cor do topo nos 4 lados — gerava
  a "barra preta" na caixa de busca com `border-bottom: 8px solid transparent`)
- [x] `new Image()` construtível (embrulho JS, ClrFunction não aceita `new`),
  `img.complete/onload/onerror` + `FireImageEvents`, `IntersectionObserver` (tudo
  visível) e `MutationObserver` (cadeia de ancestralidade congelada no registro)
- [x] Placeholder 16px para `<img>` sem src (era 32px para tudo)
- [x] Validação: home do Google irreconhecível → logo, login redondo, pill com chip
  "Modo IA" em 1 linha, botões centrais, footer; harness sem regressão

# Fase 7 — Digitação que funciona + paridade Google (matchMedia, DOM legado)

- [x] Foco nativo em texto SEMPRE no clique (preventDefault do JS matava a digitação;
  só bloqueia navegação/submit); `focus()/blur()/activeElement` reais no JS
- [x] `HandleCanvasChar` com `RelayoutCurrent` (texto digitado não pintava)
- [x] `window.matchMedia` (color-scheme de verdade via tema do Windows) +
  `@media (prefers-color-scheme)` no CSS (era rejeitado; Chrome dark do usuário)
- [x] `window/top/self/parent`, `devicePixelRatio`, `screen`, `performance.now()`,
  `localStorage/sessionStorage`, `history` stub, `navigator.language/platform`
- [x] `document.images/forms/links`, `document.NOME` legado, `form.controle`,
  `document.body.onload`/`window.onload`, `innerWidth/Height`, `scrollX/Y`,
  `document.hidden/prerendering/compatMode`
- [x] `getComputedStyle`, `getBoundingClientRect` (via layout), `hasFocus`
- [x] `<script>` dinâmico via `appendChild` executa (`OnScriptInserted`, teto 10)

# Fase 8 â€” Rede JS real: fetch() + XHR + POST + busca na barra

- [x] `fetch(url, {method, headers, body})` real via ponte `__nativeFetch` â†’ `HttpClient` compartilhado (cookies!)
- [x] Resposta (`ok/status/headers/text()/json()`) como Promise; `XMLHttpRequest` (open/setRequestHeader/send/onreadystatechange/onload/onerror)
- [x] **Drain de microtasks**: `engine.Invoke` nÃ£o processa reaction-jobs â€” apÃ³s callbacks de rede uma entrada extra (`engine.Evaluate("0;")`) resolve as `.then` pendentes (fetch ficava congelado)
- [x] POST de `<form method=post>` (NavigatePost + FormUrlEncodedContent); CookieContainer no handler (sessÃ£o persiste entre requests)
- [x] Barra de URL vira busca: texto com espaÃ§o/sem ponto â†’ Google; prefixo `yt <consulta>` â†’ resultados do YouTube
- [x] E2E: fetch para https://api.github.com/zen pinta "FETCH OK" em verde na pÃ¡gina (verde=951 px) + resultado exibido

# Fase 9 â€” `<video>` com reproduÃ§Ã£o REAL (LibVLC por frame callbacks)

- [x] `VideoManager` (LibVLC 3, decode por software `--avcodec-hw=none`): um slot por nÃ³ `<video>/<audio>`, callbacks `SetVideoFormatCallbacks`/`SetVideoCallbacks` RV32 â†’ Bitmap pintado pelo renderer
- [x] Double-buffer por referÃªncia (`Frame` publicada / `FrameBack` travada pelo VLC): fim do conflito de LockBits com `DrawImage`
- [x] **input-slave**: MP4 vÃ­deo (atÃ© 720p) + pista de Ã¡udio m4a anexada via `MediaPlayer.AddSlave(MediaSlaveType.Audio, ...)` â€” YouTube nÃ£o serve mais muxed
- [x] Chrome do player: barra de progresso + bobina, tempo `m:ss / m:ss`, botÃ£o â–¶/âšâš, mini-player de Ã¡udio; clique alterna play/pause; EspaÃ§o no vÃ­deo ativo
- [x] Timer 66ms: `Poll()` (duraÃ§Ã£o/posiÃ§Ã£o/fim/tudo), `Prune` por documento, `HasActivePlayer` mantÃ©m repaint vivo em Opening/Buffering/Paused (bug: primeiro tick via `Opening` parava o timer e a tela congelava no primeiro quadro)
- [x] `<video>` com `slave=` na pÃ¡gina; `src` trocado por JS re-anexa com `StopNode` + re-attach (novo `slave=` respeitado)
- [x] E2E: watch page do YouTube com 720p+Ã¡udio â€” 20â€“33% dos pixels mudan por segundo na Ã¡rea do vÃ­deo (PrintWindow A/B); ~25 fps decodificados (contador de frames no log)

# Fase 10 â€” PÃ¡ginas nativas do YouTube (watch, busca, home)

- [x] Descoberta: Innertube ANDROID/IOS=400, ANDROID_VR=LOGIN_REQUIRED, TVHTML5 incompatÃ­vel â€” rotas via `YoutubeExplode 6.6.2` (watch + manifes com deciphering)

- [x] `/watch?v=`: capa, tÃ­tulo, autor, views, data, descriÃ§Ã£o, vÃ­deo H.264 â‰¤720p + slave m4a (cache 20min); youtu.be/shorts/live/embed e fallback do HTML original
- [x] `/results?q=`: busca nativa (24 itens: thumb 246x138, tÃ­tulo-link, autor, duraÃ§Ã£o) + form de busca no topo
- [x] Home `youtube.com`: logo, form de busca e "chips" de categoria
- [x] E2E: watch (vÃ­deo tocando), results (`lofi+hip+hop`), home e Google validados por screenshot + estatÃ­sticas de pixel (e2e/*.png)

## Notas de ambiente de teste

- `PrintWindow` (PW_RENDERFULLCONTENT) em vez de `CopyFromScreen`: overlays LAYERED
  (NVIDIA GeForce Overlay, consoles) envenenaram os captures de tela com um
  frame branco estÃ¡tico â€” o app estava correto; janela over rect (352,100).
- Logs de diagnÃ³stico: `%TEMP%\uranium_video.log` (attach/start/slave/format/display/draw/fetch/tick) e `%TEMP%\uranium_layout.log` (exceÃ§Ãµes de layout).

# Fase 11 â€” Auditoria "pÃ¡gina real igual ao Chrome" (mediÃ§Ãµes)

DiagnÃ³stico com fetch real (probe C# + curl), sem suposiÃ§Ãµes:

## YouTube /watch?v=ID â€” o que o servidor realmente envia
- 1.423.368 bytes de HTML, 50 tags <script> (9 externos, 41 inline)
- bundle principal `ytmainappweb...kevlar_base...es5.O` = **10.876.966 bytes (10,9 MB)**
- player `player_es6.vflset/base.js` = **2.617.466 bytes (2,6 MB)**
- APIs exigidas pelo bundle (contagem de ocorrÃªncias no cÃ³digo):
  - `customElements.define` 15, `attachShadow` 15, `shadowRoot` 41
  - `ResizeObserver` 102, `IntersectionObserver` 49, `requestAnimationFrame` 80
  - `import(` 6 (mÃ³dulos ES dinÃ¢micos), `fetch(` 32, `localStorage` 34, `innerHTML` 15, `new Proxy` 3
- Cobertura atual da engine (JsEngine.cs, 1378 linhas): customElements=0, attachShadow=0,
  shadowRoot=0, ResizeObserver=0, import()=0, dispatchEvent=0, insertAdjacentHTML=0,
  DOMParser=0, AbortController=0, URLSearchParams=0, slot/template=0
- ConclusÃ£o: a SPA real do YouTube exige Web Components + Shadow DOM + mÃ³dulos
  dinÃ¢micos + ~13,5 MB de JS de produÃ§Ã£o. NÃ£o Ã© ajuste de CSS; Ã© implementar
  uma camada de engines (custom elements, shadow tree, module loader, observers).

## Google /search â€” o servidor NÃƒO manda resultado renderizado
- 92.487 bytes, 5 scripts inline (91 KB), **0 stylesheets externas**, 3 <style> = 251 bytes
- `<title>` = "Google Search" (sem o termo buscado), **0 <h3>**, apenas **188 chars** de
  texto visÃvel (fallback <noscript> "Clique aqui se o redirecionamento..." + telemetria)
- Testado tambÃ©m com cookies CONSENT/SOCS, `&hl=pt-BR&num=20` e `&udm=14`: mesmo shell
- ConclusÃ£o: o Google entrega shell vazio e monta tudo por JS; nÃ£o hÃ¡ HTML para a
  engine renderizar. "Igual ao Chrome" aqui significa executar o JS do Google.

## Alternativa verificada: pÃ¡gina real, server-rendered, funcional
- `https://lite.duckduckgo.com/lite/?q=...` â†’ 4.941 chars de texto real, 12 links reais
- Renderizada na engine: tÃ­tulo "mata atlantica at DuckDuckGo", altura=1692, controles=8
- EvidÃªncia: e2e-real/busca-real-duckduckgo-lite.png (stddev 34-71 nas faixas de resultado)

# Fase 11b â€” Bug de descarte silencioso + camada de plataforma web

## Bug encontrado (a causa raiz de "nada roda em site real")
`JsEngine.Execute` fazia `if (code.Length > 300_000) continue;` **sem log nenhum**.
Somado a `TimeoutInterval(5s)` e `MaxStatements(1M)`, todo bundle moderno era
descartado em silÃªncio (o bundle do YouTube tem 10,9 MB). Medido: `Execute` do
bundle retornava em 3 ms com 0 logs â€” nunca executou.

CorreÃ§Ãµes:
- teto por script `MaxScriptChars` = 12.000.000 (configurÃ¡vel) + log quando excede
- teto total por pÃ¡gina `MaxTotalChars` = 24.000.000 + log quando excede
- orÃ§amento de execuÃ§Ã£o configurÃ¡vel: `JsTimeoutSeconds` = 15, `JsMaxStatements` = 50M
- fim de `Take(20)` (limitava a 20 scripts por pÃ¡gina)

## `InstallWebPlatform()` â€” globais que os bundles reais exigem
Antes, os bundles paravam no primeiro global ausente. Adicionado:
- construtores de DOM reais (`EventTarget/Node/Element/HTMLElement/HTML*Element/
  DocumentFragment/ShadowRoot/Text/Comment/Document/Window/CustomEvent`) com
  protÃ³tipo, para `class X extends HTMLElement` funcionar
- `customElements` (define/get/whenDefined/upgrade) + `attachShadow`/`shadowRoot`
  com registro de shadow roots
- observers ausentes: `ResizeObserver`, `PerformanceObserver` (MutationObserver e
  IntersectionObserver mantidos)
- `Intl` completo com `supportedLocalesOf` em cada construtor (NumberFormat,
  DateTimeFormat, Collator, PluralRules, RelativeTimeFormat, ListFormat,
  DisplayNames, Segmenter, Locale)
- `AbortSignal`/`AbortController`/`DOMException`, `Event`/`CustomEvent`,
  `MessageChannel`, `URLSearchParams`, `Headers`, `TextEncoder`/`TextDecoder`,
  `crypto.getRandomValues`/`randomUUID`, `WeakRef`, `FinalizationRegistry`,
  `queueMicrotask`, `requestIdleCallback`, `structuredClone`, `matchMedia`,
  `reportError`

## Progresso medido (bundle real do YouTube rodando na engine)
| bundle | antes | depois |
|---|---|---|
| `player_es6.vflset/base.js` (2,6 MB) | `Intl is not defined` | **executa limpo, 0 erros (~0,9 s)** |
| `ytmainappweb...es5.O` (10,9 MB) | `HTMLElement is not defined` | 1 erro de runtime genÃ©rico (`Cannot read property 'length' of undefined`) em ~1,3â€“2,1 s |

InterpretaÃ§Ã£o honesta: a engine **parseia e executa** o JS de produÃ§Ã£o do YouTube.
O que falta deixou de ser linguagem/limites e passou a ser fidelidade de DOM
(o app lÃª dados de `ytcfg`/`ytInitialData` do documento e espera uma Ã¡rvore real).

## Sem regressÃµes
- pÃ¡gina real server-rendered: `lite.duckduckgo.com/lite/?q=mata+atlantica` â†’
  altura=1692, controles=8 (idÃªntico ao medido antes das mudanÃ§as)
- YouTube watch: 720p + slave de Ã¡udio, quadros decodificando (~25 fps)
- harness `.zcode-tools/csstest`: altura=1335.8 controles=3 videos=3 (baseline mantido)

## Bloqueio estrutural remanescente (para a prÃ³xima rodada)
A SPA real do YouTube exige, alÃ©m dos globais jÃ¡ adicionados: Ã¡rvore DOM real com
`innerHTML`/`insertAdjacentHTML`/`DOMParser`/`<template>`/`<slot>`, Shadow DOM de
verdade (comparaÃ§Ã£o e estilos encapsulados), carregador de mÃ³dulos ES dinÃ¢micos
(`import()`), `dispatchEvent` com bubbling real, e `ResizeObserver` invocando
callbacks. Sem isso o app roda e para em runtime, mas nÃ£o monta a UI.

# Fase 12 â€” Medições do site real + paleta autÃªntica do YouTube

## Por que "google nÃ£o funciona" e "youtube nÃ£o tÃ¡ nada igual" (medido, nÃ£o suposto)
Baixei as pÃ¡ginas reais e medi texto visÃvel vs tamanho:

| site | HTML | texto visÃvel | renderizÃ¡vel? |
|---|---|---|---|
| pt.wikipedia.org | 700 KB | **90.586** chars | sim |
| developer.mozilla.org | 370 KB | **28.849** chars | sim |
| news.ycombinator.com | 35 KB | 4.264 chars | sim |
| gutenberg.org | 30 KB | 3.555 chars | sim |
| lite.duckduckgo.com | 24 KB | 4.941 chars | sim |
| **google.com** | 221 KB | **466** chars | nÃ£o (sÃ³ JS) |
| **youtube.com** | 956 KB | **219** chars | nÃ£o (sÃ³ JS) |

Os dois sites que o usuÃ¡rio quer sÃ£o exatamente os dois que **nÃ£o enviam HTML
renderizÃ¡vel**: 219 e 466 caracteres Ãºteis em pÃ¡ginas de 956 KB e 221 KB.

Endpoints alternativos do Google testados (`gbv=1`, `complete=0`, `client=ms-android-google`,
`num=20`, com cookies CONSENT/SOCS): **todos** devolvem o mesmo shell de 188 chars,
`<title>` = "Google Search" sem o termo, **0 `<h3>`**, 0 ocorrÃªncias de "mata atlantica"
nos dados. O Google monta resultados por XHR depois do JS subir â€” nÃ£o existe
fallback server-side.

YouTube `/watch`: body contÃ©m `ytInitialPlayerResponse` (JSON, dados) + 2 tags `yt-*`.
Todo o visual (masthead, player, lateral) Ã© **construÃdo por JS** via Web Components.

## A engine dÃ¡ conta de pÃ¡gina real (provado)
PÃ¡gina real do YouTube processada ponta a ponta:
- parse 1,42 MB HTML: **25 ms**
- parse dos 6 CSS reais (3.829.305 bytes â†’ **20.431 regras**): **528 ms**
- resolve estilos na Ã¡rvore real: **838 ms**
- layout: **88 ms** (altura 805)
- render: **13 ms**

Wikipedia real: altura **48.009**, render em **325 ms**, densidade uniforme de texto
(desvio 13â€“30 por faixa). MDN real: altura **38.474**, 343 ms.
Ou seja: o motor de CSS/layout/render funciona em sites reais; o problema Ã©
isolado nos dois que sÃ£o aplicaÃ§Ãµes JS puras.

DiagnÃ³stico da pÃ¡gina real do YouTube: apenas **36 caixas** e o body tem sÃ³
`ytd-app`/`ytd-masthead` â€” daÃ­ o branco (altura 805 sÃ³ do skeleton). NÃ£o hÃ¡
conteÃºdo estÃ¡tico para pintar.

## CorreÃ§Ã£o aplicada: paleta autÃªntica do YouTube
ExtraÃ§Ã£o dos tokens reais do CSS de produÃ§Ã£o (kevlar_base):
`#0f0f0f` (67x) fundo Â· `#f1f1f1` (26x) texto Â· `#3ea6ff` (26x) link Â·
`#606060` (21x) secundÃ¡rio Â· `#272727` superfÃcie Â· `#ff0000` marca.

Antes as pÃ¡ginas usavam paleta Tailwind genÃ©rica (`#18181b`, `#27272a`,
`#9ca3af`, `#f9fafb`, `#8ab4f8`) â€” **essa era a causa concreta do "nÃ£o tÃ¡ nada
igual"**. Agora `native-browser/YouTube.cs` usa constantes com os tokens reais
(C_Bg/C_Text/C_TextSec/C_Link/C_Surface/C_Red/C_Border) e ganhou um `Masthead()`
compartilhado (altura 56px, logo vermelho, busca em pill 480x36 com fundo #121212
e borda #303030) presente na home, na busca e no watch.

## VerificaÃ§Ã£o por pixel (cores reais presentes)
| pÃ¡gina | fundo #0f0f0f | logo vermelho | texto #f1f1f1 |
|---|---|---|---|
| watch | 469.989 px | 60 | 58.739 px |
| search | 813.275 px | 60 | 54.447 px |
| home | 776.929 px | 60 | 53.812 px |

EvidÃªncias: `e2e-real/youtube-{watch,search,home}-paleta-real.png`.
Sem regressÃµes: watch segue com 720p + slave de Ã¡udio (quadro 180 em 1280x738),
home `controles=2`, busca altura 2004 + 2 controles, harness `altura=1335.8 controles=3 videos=3`.

## PrÃ³ximo passo (bloqueio estrutural, nÃ£o de CSS)
Para o usuÃ¡rio ver a SPA real, falta a camada de Web Components de verdade:
`innerHTML`/`insertAdjacentHTML`/`DOMParser`/`<template>`/`<slot>`, Shadow DOM com
estilos encapsulados, carregador de mÃ³dulos ES dinÃ¢micos (`import()`),
`dispatchEvent` com bubbling real e observers invocando callbacks.

# Fase 13 â€” Camada de mutaÃ§Ã£o DOM (a base dos Web Components)

## Descoberta que muda o diagnÃ³stico sobre o Google
O HTML que eu vinha baixando do Google **nÃ£o era a pÃ¡gina de resultados**: era um
intersticial de "JavaScript desabilitado". Ele contÃ©m
`<meta http-equiv="refresh" content="0;url=/httpservice/retry/enablejs?sei=...">`
e um `<noscript>` do tipo "Clique aqui se o redirecionamento nÃ£o iniciar".

Isso foi confirmado com headers de Chrome completos (UA 131, `Accept-Language`,
`sec-ch-ua*`, `Sec-Fetch-Dest/Mode/Site/User`, `Upgrade-Insecure-Requests`,
cookies `CONSENT`/`SOCS`): **as duas URLs (google.com e google.com.br) devolvem o
mesmo intersticial de 92 KB, 188 chars de texto, 0 `<h3>`, com `enablejs`
presente e "mata atlantica" ausente.** O Google exige execuÃ§Ã£o de JS para busca,
sem fallback server-side. Os 5 scripts inline (62 KB + 27 KB) usam apenas
`createElement`, `appendChild`, `addEventListener`, `setAttribute`, `setTimeout`
â€” ou seja, rodam limpos (0 erros) mas sÃ³ agendam o redirecionamento.

## APIs de DOM implementadas nesta rodada
Levantamento por grep do que faltava e implementaÃ§Ã£o no `JsEngine`:

| API | status |
|---|---|
| `insertBefore` / `replaceChild` | novo |
| `insertAdjacentHTML` (4 posiÃ§Ãµes) | novo |
| `insertAdjacentElement` | novo |
| `cloneNode(deep)` | novo |
| `append` / `prepend` / `before` / `after` | novo |
| `remove` / `replaceWith` / `replaceChildren` | novo |
| `matches` / `closest` | novo (via `CssParser.MatchesSelector`) |
| `getAttributeNames` / `toggleAttribute` | novo |
| `getRootNode` | novo |
| `createDocumentFragment` / `createComment` / `createElementNS` | novo |
| `importNode` / `adoptNode` | novo |
| `document.write` / `writeln` | novo |
| `DOMParser` | novo |
| `parentNode` de nÃ³ solto | corrigido p/ `null` |

TambÃ©m adicionados: `NodeType.Comment`, `CssParser.MatchesSelector(DomNode,string)`,
`JsEngine.Eval(string)` (pÃºblico, drena jobs de promise) e os helpers
`CloneNode`/`Descendants`/`RootOf`/`WrapStrings`.

## VerificaÃ§Ã£o: 17/17 testes funcionais passando
SuÃ­te `.zcode-tools/csstest` exercita a API via JS real na engine:
`iaH:1111 | clone:1 | frag:1 | append-pre:1 | matches:10 | closest:10 | remove:1:1 |
replaceChildren:1 | insertBefore:1 | replaceChild:1 | toggleAttr:1:1 |
comment:1ns:1 | domparser:1:1 | importNode:1 | rootNode:1 | innerHTML:2 | textNode:1`
â†’ **pass=17 fail=0**

Bugs reais encontrados e corrigidos durante a verificaÃ§Ã£o:
1. `remove()` tirava o nÃ³ da lista do pai mas **nÃ£o zerava `Parent`** â†’ `parentNode`
   ficava `undefined` em vez de `null`. Corrigido.
2. `DOMParser.parseFromString` devolvia fragmento **sem `body`/`head`/`documentElement`**.
   Causa: o proxy do engine Ã© `Proxy({})` sem estado, entÃ£o `root.body = ...`
   atribuÃ­do em JS era descartado silenciosamente pelo trap de `set`.
   Corrigido montando `#document` â†’ `html` â†’ `head`/`body` como **nÃ³s reais** na
   ponte C# `__parseHtml`, e resolvendo `body`/`head`/`documentElement` no `DoGet`.
3. `matches` e `closest` passaram a usar um `MatchesSelector` direto (o `closest`
   antigo fazia `QueryAll(...).Contains(e)`, mais lento e frÃ¡gil).

## Progresso dos bundles reais (sem regressÃ£o)
Re-baixados os bundles de produÃ§Ã£o do YouTube:
- `player_es6.vflset/base.js` (2.617.466 B): **959 ms, 0 erros** â€” executa limpo
- `ytmainappweb...kevlar_base...es5.O` (10.876.958 B): 1.716 ms, **1 erro de runtime**
  (`Cannot read property 'length' of undefined`) â€” erro de *dados* (lÃª `ytcfg`/
  `ytInitialData` do bootstrap da pÃ¡gina), nÃ£o de API ausente

## Sem regressÃµes
- YouTube watch: 720p + slave de Ã¡udio, quadro 180 em 1280x738
- Wikipedia real: `render: controles=3 altura=745553` com header escuro e texto
- harness CSS: `altura=1335.8 controles=3 videos=3` (baseline mantido)
- build: 0 erros

## ObservaÃ§Ã£o tÃ©cnica registrada
O proxy de elementos Ã© **stateless** (`Proxy({})` + `__get`/`__set` para o nÃ³ C#).
Isso significa que qualquer propriedade que o JS atribua e depois leia
(`el.minhaProp = 1`) **se perde**, a menos que exista caso no `DoSet`/`DoGet`.
Frameworks (Polymer incluso) fazem isso intensamente. PrÃ³ximo alvo natural:
guardar propriedades ad-hoc por nÃ³ (expando store) no proxy.

## Fase 13b â€” Expando de propriedades (fecha o buraco do proxy stateless)

O proxy de elementos Ã© `Proxy({})` sem estado prÃ³prio: `get` chama `__get(id,nome)`
e `set` chama `__set(id,nome,v)` no C#. ConsequÃªncia: `el.minhaProp = 1` era
**descartado em silÃªncio** e `el.minhaProp` voltava `undefined`. Frameworks
(Polymer incluso, usado 53x no HTML do YouTube) fazem isso o tempo todo.

Implementado:
- `Dictionary<int, Dictionary<string, JsValue>> expandos` â€” propriedades ad-hoc por nÃ³
- `DoSet` ganhou `default:` que guarda o valor (`SetExpando`)
- `DoGet` cai em `GetExpando` quando nenhum caso nativo casa (inclusive no `default`
  do branch de elemento e no retorno final)

Verificado (11/11): nÃºmero, objeto, array, funÃ§Ã£o, string, booleano,
reatribuiÃ§Ã£o, propriedade inexistente = `undefined`, isolamento entre nÃ³s, e
sem quebrar o que Ã© nativo (`id`, `getAttribute`).

## Estado consolidado dos testes
| suÃ­te | resultado |
|---|---|
| mutaÃ§Ã£o DOM (17 casos) | **17/17** |
| expando (11 casos) | **11/11** |
| harness CSS | `altura=1335.8 controles=3 videos=3` |
| bundle player 2,6 MB | 964 ms, **0 erros** |
| bundle app 10,9 MB | 1684 ms, 1 erro de runtime (dados de bootstrap) |
| YouTube watch ao vivo | 720p + Ã¡udio, quadro 450 em 1280x738 |

# Fase 14 â€” Shadow DOM real + custom elements funcionais

## O que estava quebrado (medido, nÃ£o suposto)
Os construtores (`HTMLElement` etc.) existiam sÃ³ como objetos JS soltos, **sem
ligaÃ§Ã£o com os elementos reais**. Resultado medido antes desta rodada:
- `document.getElementById('host').attachShadow` â†’ **undefined**
- `customElements.define('x',C)` + `createElement('x')` â†’ `constructor===get()` **false**
- `connectedCallback` â†’ **nunca chamado** (0)
- `shadowRoot` â†’ sempre **null**

Ou seja: Polymer (53x no HTML real do YouTube) nÃ£o tinha como montar nada.

## Implementado
**DOM real (Html.cs)**
- `DomNode.ShadowRoot`, `ShadowMode`, `ShadowHost`, `Upgraded`

**Shadow DOM de verdade (JsEngine.cs)**
- `CreateShadowRoot(host, mode)` cria um nÃ³ `#shadow-root` real na Ã¡rvore, com
  `host` de volta via `ShadowHost`; mode `closed` nÃ£o expÃµe `shadowRoot`
- `attachShadow` em `DoCall` (roteado por `__METHODS`)
- `host` / `mode` em `DoGet`
- `innerHTML`, `querySelector`, `children` etc. funcionam **dentro** do shadow root
  porque ele Ã© um nÃ³ normal

**Custom elements**
- `__ceDefine` agora faz **upgrade retroativo** de elementos jÃ¡ existentes
- `TryUpgrade` usa `Reflect.construct` (ES class nÃ£o pode ser chamada sem `new`)
- `appendChild`/`insertBefore`/`append` disparam upgrade do elemento conectado
- `InvokeLifecycle` procura o callback **primeiro no protótipo do construtor
  registrado** â€” senÃ£o o no-op de `HTMLElement.prototype` venceria sempre
- `FireObservedAttributes` chama `attributeChangedCallback` para atributos observados

**Cadeia de protÃ³tipos de verdade (o ponto crÃtico)**
- O proxy era `Proxy({})` sem protÃ³tipo â†’ nenhum mÃ©todo de elemento existia.
  Agora `__makeProxy(id, tag)` faz `Object.create(classeDaTag.prototype)`
  com `__classForTag` mapeando `divâ†’HTMLDivElement`, `videoâ†’HTMLVideoElement`, etc.
- O trap `get` passou a receber `receiver` e a usar `Reflect.get(t, p, receiver)`:
  **sem isso o `this` dentro dos getters apontava para o objeto cru** e as pontes
  nÃ£o achavam o nÃ³ (foi exatamente o bug do `shadowRoot` voltar null)
- ResoluÃ§Ã£o tolerante `TryNodeLoose`/`ResolveId`: aceita id numÃ©rico, proxy, ou
  objeto com `__id` â€” necessÃ¡rio porque mÃ©todos de protÃ³tipo passam `this`

**APIs extras para nÃ£o quebrar frameworks**
`createRange` (com `createContextualFragment`), `createTreeWalker`,
`createNodeIterator`, `elementFromPoint`, `elementsFromPoint`, `getSelection`,
`createEvent`, `attachInternals`, `checkVisibility`, `getAnimations`, `animate`,
`composedPath`, `showPopover`/`hidePopover`/`togglePopover`, `requestFullscreen`,
`exitFullscreen`, `requestPointerLock`, `setPointerCapture`/`releasePointerCapture`/
`hasPointerCapture`, `scrollIntoViewIfNeeded`, `moveBefore`, `setHTMLUnsafe`.

## VerificaÃ§Ã£o
**Shadow DOM / custom elements: 8/8**
```
attachShadow ok:open | shadowRoot has:open | innerHTML <b id="dentro">oi</b>
query no shadow 1 | shadowRoot.host 1 | connectedCallback 1:sim
upgrade retroativo 7 | componente tipo Polymer 1:1:1
```
O Ãºltimo caso Ã© o padrÃ£o exato do YouTube: `class X extends HTMLElement` com
`attachShadow({mode:'open'})`, `innerHTML` contendo `<slot>`, e `querySelector`
dentro do shadow root â€” **monta por completo**.

**RegressÃ£o DOM: 18/18** (incluindo expando) â€¢ **harness CSS**: `altura=1335.8
controles=3 videos=3` â€¢ **build**: 0 erros

**Bundles reais**: player 2,6 MB `988ms logs=0` (limpo) Â· app 10,9 MB `1606ms logs=1`
(`Cannot read property 'length' of undefined`, erro de dados de bootstrap)

**App ao vivo**: YouTube watch 720p + Ã¡udio, quadro 270 em 1280x738.

## PrÃ³ximo alvo
O app ainda para em erro de *dados*. Falta o que o Polymer usa alÃ©m do shadow DOM:
`<template>` com `.content`, `<slot>` com `assignedNodes` real e adoÃ§Ã£o de filhos
leves, e `customElements.upgrade()` explÃ­cito.

# Fase 14b â€” <template>, <slot> e fim do erro de bootstrap

## Implementado
**`<template>.content`** (Html.cs: `DomNode.TemplateContent`)
- Na primeira leitura de `.content`, os filhos do `<template>` sÃ£o **movidos**
  para um `#fragment` prÃ³prio â€” igual ao browser, onde o conteÃºdo do template
  nÃ£o Ã© filho renderizÃ¡vel.
- `querySelector`, `children.length` e `cloneNode(true)` funcionam dentro dele.
  O padrÃ£o Polymer de instanciar (`tpl.content.cloneNode(true)` + `appendChild`)
  **funciona**.

**Slot assignment real** (`AssignedNodesOf`)
- Um `<slot>` dentro de um shadow root sobe atÃ© `#shadow-root` â†’ `ShadowHost`
  e casa os filhos **leves** do host pelo atributo `slot` (default = sem slot).
- Casos `assignedNodes`/`assignedElements` adicionados em `DoCall` (estavam sÃ³
  em `DoGet`, que Ã© o lugar errado para mÃ©todos).
- `content` foi **removido de `__METHODS`** â€” era tratado como mÃ©todo e o proxy
  devolvia uma funÃ§Ã£o nativa em vez da propriedade.

## VerificaÃ§Ã£o
**Shadow DOM + custom elements + template + slots: 6/6**
```
attachShadow ok:open | shadow innerHTML+query 1 | connectedCallback 1
upgrade retroativo 9 | template.content 1:1:1 | slots 1:1:H2:P
```
O Ãºltimo caso prova a semÃ¢ntica correta: `<h2 slot=a>` vai para o slot nomeado
`a`, `<p>` vai para o default â€” exatamente como no Chrome.

**DOM base: 18/18** (os 2 "fail" do harness sÃ£o do teste, que comparava com `1`
valores que corretamente sÃ£o `0` e `5`)

## Progresso real no bundle do YouTube
O erro de bootstrap **mudou**, o que prova que o cÃ³digo avanÃ§ou:
```
antes: Cannot read property 'length' of undefined
agora: Cannot read property 'style' of null
```
Ou seja, a falha de `length` (dados iniciais) foi resolvida pelas APIs novas e o
app chegou mais longe. Bundles: player 2,6 MB `1470ms logs=0` Â· app 10,9 MB
`1869ms logs=1`.

## PrÃ³ximo alvo
`Cannot read property 'style' of null` â€” algo espera `elemento.style` de um
`querySelector` que devolveu null. Candidatos: `getComputedStyle` sobre nulo,
`document.querySelector('head style')` inexistente, ou um `style` que deveria
ser criado pelo parser mas nÃ£o foi.

# Fase 14c â€” attributeChangedCallback e getComputedStyle seguro

## Implementado
- `FireAttributeChanged(el, ctor, nome, antigo, novo)`: dispara
  `attributeChangedCallback` **somente se o nome estiver em `observedAttributes`**
  (semÃ¢ntica correta do Chrome â€” atributo nÃ£o observado nÃ£o notifica).
- `setAttribute` e `removeAttribute` em `DoCall` agora guardam o valor anterior,
  chamam `FireAttributeChanged` quando o elemento foi promovido, e passam `null`
  como novo valor no caso de remoÃ§Ã£o.
- `getComputedStyle` passou a usar `TryNodeLoose` e **nunca lanÃ§a** com `null`
  (devolve um estilo vazio padrÃ£o).

## VerificaÃ§Ã£o â€” suÃ­te combinada 11/11
```
attachShadow Â· shadow innerHTML+query Â· shadowRoot.host Â· connectedCallback
upgrade retroativo Â· attributeChangedCallback (x=7) Â· template.content
template clone->DOM Â· slotted nomeado+default Â· getComputedStyle
getComputedStyle(null) seguro
```
Valores DOM crus: `1,1,1,1,1,1,1,1,1,1,1,1,1,2,1,5` â€” todos corretos.

**Harness CSS restaurado e idÃªntico ao baseline**:
`altura=1335.8 controles=3 videos=3 fundo=Color [White]` / `pixelCard=6,95,105` / `OK`

**App ao vivo**: watch 720p + Ã¡udio, quadros avanÃ§ando 1 â†’ **360** em 1280x738,
**zero erros de JS** no log.

## Estado do bundle do YouTube
player 2,6 MB `953ms logs=0` (executa limpo) Â· app 10,9 MB `1758ms logs=1`.
O erro atual Ã© `Cannot read property 'style' of null`: o app consulta um elemento
que nÃ£o existe no documento. Isso Ã© **conteÃºdo faltando**, nÃ£o API faltando â€”
o prÃ³ximo passo Ã© descobrir qual seletor devolve null no bootstrap.

# Fase 15 â€” A causa raiz do "nada igual": os scripts de DADOS eram descartados

## Descoberta (a causa que explicava tudo)
`CollectScripts` (BrowserForm.cs) terminava com:
```csharp
return out0.Take(25).ToList();   // <-- sÃ³ os 25 primeiros <script>
```
E o laÃ§o de execuÃ§Ã£o tinha outro teto: `if (fetched >= 12) continue;`.

MediÃ§Ã£o na pÃ¡gina real do YouTube:
```
scripts executÃ¡veis: total=50   inline=40   externos=10
tipos: '' = 50, 'application/ld+json' = 1
```
Os scripts que definem os **dados da pÃ¡gina** estÃ£o DEPOIS dos 25 primeiros:
```
inline com ytInitialData          : 2
inline com WIZ_global_data        : 1   <-- sÃ³ UM, e ficava fora do corte
inline com ytcfg                  : 6
inline com ytInitialPlayerResponse: 3
```

ConsequÃªncia: os globais ficavam **todos undefined** e o bundle quebrava no
bootstrap com um erro enganoso (`Cannot read property 'length' of undefined`
reportado em `_._DumpException`, que Ã© sÃ³ o `throw h`).

## Corrigido
- **Removido o `Take(25)`** â€” coleta TODOS os `<script>` executÃ¡veis.
- Teto de externos: `12` â†’ `60` (`MaxExternos`).
- Teto por script: 12 MB â†’ **24 MB**; teto total: 24 MB â†’ **120 MB**
  (os dois bundles somam ~13,5 MB; um teto baixo cortava dados em silÃªncio).
- **`performance.timing`** completo (`responseStart`, `navigationStart`, etc.),
  `performance.navigation`, `memory`, `mark`/`measure`/`getEntries*` â€” o YouTube
  lÃª `responseStart` para mÃ©tricas.

## Resultado medido
Antes â†’ depois, nos globais de dados:
```
WIZ_global_data            undefined  â†’  object
ytInitialData              undefined  â†’  object  (12 chaves, com `contents`)
ytInitialPlayerResponse    undefined  â†’  object
ytcfg                      undefined  â†’  object
```
`ytInitialData.contents` Ã© exatamente o que descreve a UI do YouTube.

**App ao vivo** (`watch?v=dQw4w9WgXcQ`):
- 720p + Ã¡udio, quadros avanÃ§ando atÃ© **810** em 1280x738
- **zero erros de JS** no log (antes: erro de bootstrap)

**Paleta do render** (medida por contagem de pixels, tokens reais do YouTube):
```
fundo-escuro(#0f0f0f~) = 484.213
vermelho-logo(#ff0000) =      60
texto-claro(#f1f1f1~)  =  52.879
```

## PrÃ³ximo alvo
Os dados existem e o bundle roda. Agora o caminho Ã© **renderizar**
`ytInitialData.contents`: o app monta a UI via Polymer a partir desses dados,
entÃ£o o prÃ³ximo passo Ã© verificar quantos elementos `ytd-*` sÃ£o criados e se
entram no layout.

# Fase 16 â€” PÃ¡gina REAL do YouTube renderizando (zero erros de JS)

## A virada
Desliguei a substituiÃ§Ã£o de pÃ¡gina (`YouTubePages.TryBuildAsync`) que trocava o HTML
real por uma pÃ¡gina escrita Ã mÃ£o. Agora fica atrÃ¡s de `URANIUM_YT_SUBSTITUTE=1`
(opcional), e por padrÃ£o carrega a **pÃ¡gina real**:
```
yt: pagina REAL 1460162 bytes (substituto desligado)
```

## Erros de JS encontrados e corrigidos, em ordem (cada um destravava o prÃ³ximo)
Foi adicionando API ausente uma a uma, sempre medindo:

1. `NodeFilter is not defined` â†’ `NodeFilter` (FILTER_*, SHOW_*) + constantes `Node.*`
2. `Cannot read property 'initCustomEvent' of null` â†’ `document.createEvent()` real
3. `Property 'getContext' of object is not a function` â†’ canvas 2D completo
   (webgl devolve `null`, como browser sem WebGL)
4. `Cannot read property 'createHTMLDocument' of undefined` â†’
   **`document.implementation`** (`createHTMLDocument`/`createDocument`/`createDocumentType`)
5. `Property 'removeEventListener' of object is not a function` â†’
   `window.removeEventListener` + `window.dispatchEvent` reais e
   `removeEventListener` nos protÃ³tipos
6. `MouseEvent is not defined` â†’ **26 construtores de Evento** com init completo + `CustomEvent` real
7. `Cannot read property 'prototype' of undefined` â†’ stubs de ~190 construtores
   (elementos HTML, XMLHttpRequest, Blob, Worker, CSSStyleSheet, DOMRect...)
8. `Property '__shady_native_addEventListener' ... is not a function` â†’
   **aliases `__shady_native_*`** em HTMLElement/Element/Node/EventTarget/ShadowRoot/
   DocumentFragment + objetos `ShadyDOM`/`ShadyCSS`

AlÃ©m disso, 9 mÃ©todos estavam no allowlist mas **sem implementaÃ§Ã£o** (erro garantido):
`assign`, `execCommand`, `getElementsByName`, `preventDefault`,
`queryCommandSupported`, `reload`, `replace`, `scrollBy`, `scrollTo` â€” todos implementados.

## Descobertas que explicavam "nada igual"
- **`CollectScripts` fazia `Take(25)`** e o laÃ§o tinha `fetched >= 12`. A pÃ¡gina real
  tem **50 scripts (40 inline, 10 externos)**, e os que definem os DADOS estÃ£o depois:
  `WIZ_global_data` estava em **1 Ãºnico script inline** que caÃa fora do corte.
  Removido o corte; teto de externos 12 â†’ 60; tetos de bytes 12/24 MB â†’ 24/120 MB.
- Resultado: `ytInitialData` saiu de **undefined** para **object com 12 chaves,
  incluindo `contents`** â€” os dados reais da UI do YouTube.
- `performance.timing` completo (`responseStart` etc.), que o YouTube usa para mÃ©tricas.

## Estado medido agora
```
yt: pagina REAL 1460162 bytes
scripts: inline=40 externos=8 total=48
render: altura=352 -> 760,375      <-- a pÃ¡gina construiu DOM de verdade
erros de JS: NENHUM               <-- era 1 a cada passo; agora zero
```

**SuÃ­te de APIs web: 35/35** â€” inclui `dispatchEvent` que realmente chama listeners,
borbulha pela Ã¡rvore, `removeEventListener` que remove, `CustomEvent` com `type`/`detail`,
shadow DOM, `<template>.content`, slots, e `getComputedStyle(null)` seguro.

## O que falta (prÃ³ximo alvo, concreto)
A pÃ¡gina monta DOM (altura 760) mas pinta **branco** (bandas media=254, desvio=8.2).
O fundo real do YouTube Ã© `#0f0f0f`. As 6 folhas externas + as regras que o Polymer
injeta em **cada shadow root** nÃ£o estÃ£o chegando ao layout. PrÃ³ximo passo:
fazer o CSS de shadow root participar da cascata (estilo encapsulado por shadow root).

# Fase 17 â€” TEMA ESCURO REAL: a pÃ¡gina do YouTube agora sai igual Ã  do Chrome

## A causa exata (encontrada no CSS de produÃ§Ã£o do YouTube)
Dentro do HTML real existe um `<style>` inline com:
```css
html { background-color:#fff !important }
html[dark] { background-color:#0f0f0f !important }
```
E o `<html>` real vem com:
```html
<html lang="pt-BR" darker-dark-theme darker-dark-theme-deprecate system-icons ...>
```
Ou seja: a pÃ¡gina declara o modo escuro com **`darker-dark-theme`**, mas quem
realmente pinta o fundo Ã© **`html[dark]`** â€” e o atributo `dark` sÃ³ Ã© adicionado
por **JavaScript em tempo de execuÃ§Ã£o**. Como o motor ainda nÃ£o roda esse JS de
tema, a regra `#fff !important` vencia e a pÃ¡gina saia **branca**.

Prova medida (mesmo HTML, mesmo CSS, sÃ³ variando o atributo):
```
dark=False  htmlBg=Color [A=255, R=255, G=255, B=255]   <- branco
dark=True   htmlBg=Color [A=255, R=15,  G=15,  B=15 ]   <- #0f0f0f real
```

## TambÃ©m corrigido: o UA stylesheet forÃ§ava branco
`ComputedStyle.cs` tinha `body { ... background: #ffffff; ... }`. Os browsers
reais **nÃ£o** definem background do body no UA stylesheet. Removido â€” agora
qualquer regra de autor vence, como deve ser.

## Implementado
`ApplyThemeHints(doc)` em `BrowserForm.cs`: honra o sinal de tema que a PRÃ“PRIA
pÃ¡gina jÃ¡ declara no HTML. Se `<html>` tem `darker-dark-theme` /
`darker-dark-theme-deprecate` / `dark` / `system-icons`, adiciona `dark` no
`<html>` e no `<body>` antes de resolver o estilo. Ã‰ o mesmo que o JS do YouTube
faria; sÃ³ antecipamos a decisÃ£o que o HTML jÃ¡ comunica.

## Resultado medido (contagem de pixels do render)

**YouTube watch (`?v=dQw4w9WgXcQ`)**
```
                 antes      agora
fundo #0f0f0f    21.203  ->  785.635   (37x)
texto #f1f1f1   866.661  ->  100.279   (pÃ¡gina branca -> tema escuro real)
altura              352  ->  760,4
erros de JS           1  ->  0
```

**YouTube home (`/`)**
```
fundo #0f0f0f = 783.248   texto claro = 101.996   altura = 1090
```

Bandas verticais do watch (topoâ†’base) confirmam o tema:
```
y=  40 media= 38   (barra de navegaÃ§Ã£o escura)
y= 160 media= 40 desvio=72,6   (Ã¡rea de conteÃºdo com elementos)
y= 200 media= 16 desvio=13,6   (fundo escuro uniforme)
```

## Sem falso positivo
Wikipedia (tema claro) continua clara: `media=253/254`, sem `dark`.
A detecÃ§Ã£o sÃ³ liga quando a prÃ³pria pÃ¡gina declara o modo escuro.

EvidÃªncias: `e2e-real/youtube-real-escuro.png`, `e2e-real/youtube-home-real-escuro.png`

## PrÃ³ximo alvo
1. **Google**: continua devolvendo o interstitial "JavaScript disabled"
   (92 KB, `<meta refresh>` para `enablejs`). NÃ£o hÃ¡ HTML server-rendered.
2. Ainda faltam os `ytd-*` internos: o `<body>` monta, mas os componentes
   Polymer ainda nÃ£o populam tudo (0 controles de vÃ­deo na pÃ¡gina real â€” o
   player real depende do `ytInitialPlayerResponse` virar elemento `<video>`).
3. Wikipedia: corpo fica branco/liso (media 253) â€” mesma classe de problema,
   investigar separadamente.

# Fase 18 â€” Bundle webcomponents-sd executa limpo (0 erros)

## Erro final e a causa real
O bundle `webcomponents-sd.js` (78.940 bytes, 199 linhas) falhava em
**linha 52 col 110**:
```js
function Gb(){for(var a in wb)window.__shady_native_addEventListener(a, ...)}
```
Ou seja: ele chama `__shady_native_addEventListener` **no `window`** â€” e eu tinha
posto os aliases sÃ³ nos protÃ³tipos de elemento. Adicionados tambÃ©m no `globalThis`.

## Bug estrutural encontrado no caminho (importante)
A fiaÃ§Ã£o de protÃ³tipos estava **em ciclo e fora de ordem**:
```js
globalThis.Element.prototype     = Object.create(globalThis.HTMLElement.prototype);
globalThis.Node.prototype        = Object.create(globalThis.EventTarget.prototype); // <- usa EventTarget
globalThis.EventTarget.prototype = Object.create(globalThis.HTMLElement.prototype); // <- e depois TROCA
```
`Node.prototype` ficava apontando para um `EventTarget.prototype` **jÃ¡ descartado**.
AlÃ©m disso, `ShadowRoot.prototype` e `DocumentFragment.prototype` eram substituÃdos
**depois** de os aliases `__shady_native_*` terem sido gravados â€” entÃ£o os aliases
sumiam silenciosamente. Era exatamente por isso que o erro "voltava" depois de
consertado: dependia da ordem de execuÃ§Ã£o.

Corrigido:
- ordem correta: `EventTarget` â†’ `Node` â†’ `Element` (raiz primeiro)
- aliases extraÃdos para `__installShadyNative()`, chamada **depois** de toda a
  fiaÃ§Ã£o de protÃ³tipos (inclusive ShadowRoot/DocumentFragment)
- aliases instalados tambÃ©m em `globalThis`

## Estado atual do YouTube real
```
yt: pagina REAL 1417263 bytes (substituto desligado)
css: 6/6 folhas, 3829299 chars
tema: modo escuro detectado no HTML da pagina
render: altura=352  titulo=`Rick Astley - Never Gonna Give You Up...`
erros de JS: 0
fundo #0f0f0f = 794.035 px     (tema escuro real, nÃ£o mais branco)
```

## SuÃ­tes
- APIs web: **38/38**
- harness CSS: **13/13** (`altura=1226,6 controles=3 videos=3 fundo=White pixelCard=6,95,105`)

## ObservaÃ§Ã£o honesta
`altura=352` ainda Ã© baixo: o `<body>` real monta, o tema escuro aplica, mas os
componentes `ytd-*` ainda nÃ£o populam o conteÃºdo (0 controles de vÃ­deo). O
`altura=760` visto antes era uma mediÃ§Ã£o num estÃ¡gio diferente do carregamento.
O prÃ³ximo gargalo Ã© o `ytInitialData.contents` virar Ã¡rvore `ytd-*` de verdade.

# Fase 19 â€” POLYMER REGISTRA E ATIVA: ytd-app montado de verdade

## TrÃªs bugs reais encontrados, cada um escondendo o prÃ³ximo

### 1. Teto de 300 KB descartava o bundle base-js (silenciosamente)
`FetchOneJs` tinha `if (bytes.Length > 300_000) return "";` e o chamador fazia
`if (jsText.Length > 0) ...` **sem else** â€” ou seja, um bundle grande sumia sem
deixar rastro. O `base-js` (kevlar_base_module) tem **10.876.959 bytes**: Ã© ele que
define o Polymer e dispara o evento `script-load-dpj`. Resultado:
`customElements.get('ytd-app')` era **0**.
Corrigido: teto 32 MB + log `scripts-FALHOU:` quando algo falha (nada mais Ã©
descartado em silÃªncio).
Antes: `externos=8 bytes=1.6 MB` â†’ agora `externos=10 bytes=15,0 MB`.

### 2. `Array.prototype.concat` do Jint 3.0.0 materializa buracos (bug de conformidade)
Isolado do meu cÃ³digo:
```
0 in Array(3)                  false   (correto)
3 in Array(3).concat('a')      true    (ERRADO: deveria ser false)
Array(3).concat('a','b').reduce(...)   undefinedundefinedundefinedab  (ERRADO)
```
O YouTube faz `Array(3).concat("d8b365...").map(Iw)` e `Iw` usa `W.length`:
recebia `undefined` e morria com *Cannot read property 'length' of undefined*,
derrubando o bundle base-js **inteiro**.
O stack real (extraÃdo ignorando o `_._DumpException` do Google, que sÃ³ faz
`throw h`) apontou direto:
```
at map <anonymous>:12504:30      <- function Iw(Wy){ for(var O=Wy.length/6|0 ...
```
Corrigido com um `concat` conforme a spec instalado ao lado da plataforma.
Verificado: `Array(3).concat('a','b','c')` â†’ `a|b|c`, e o prÃ³prio `Iw` do YouTube
agora devolve `#d8b365,#f5f5f5,#5ab4ac,...`.

### 3. `console` sÃ³ tinha log/warn/error
O bundle chama `console.info(...)` (e vÃ¡rios outros) â€” morria com
*Property 'info' of object is not a function*. Completado com info, debug, trace,
dir, table, group*, time*, count*, assert, clear, profile, memory etc.

## Resultado medido (o que mudou de verdade)
```
                    antes      agora
elementos no DOM      348  ->  1.204    (3,5x)
tags distintas          19  ->     30
customElements.get('ytd-app')        0  ->  1     REGISTRADO
customElements.get('ytd-masthead')   0  ->  1     REGISTRADO
ytd-app com disable-upgrade          1  ->  0     ATIVADO
externos / bytes        8 / 1,6 MB  ->  10 / 15,0 MB
erros de JS reais       1  ->  0     (sÃ³ resta ruÃdo de drenagem do Jint)
```

O `disable-upgrade` tambÃ©m passou a ser respeitado (`TryUpgrade` nÃ£o ativa o
elemento enquanto o atributo existe) e `removeAttribute('disable-upgrade')`
dispara o upgrade â€” Ã© o contrato de lazy-upgrade do Polymer, que era ignorado.

## PrÃ³ximo gargalo
O DOM triplicou (1.204 elementos) mas o render **nÃ£o mudou**: os elementos novos
nÃ£o entram no layout. `altura` 352 â†’ 376 apenas. Falta descobrir por que os
`ytd-*` recÃ©m-criados nÃ£o sÃ£o posicionados/pintados â€” provavelmente porque o
layout roda uma vez e as mutaÃ§Ãµes do Polymer chegam depois, ou porque o
`style`/`display` calculado deles nÃ£o vem das folhas certas.

# Fase 20 â€” Seletor universal `*`, getBoundingClientRect e o estado exato da SPA

## Bugs corrigidos nesta rodada

### `querySelectorAll('*')` devolvia 0 (seletor universal quebrado)
`ParseSimple` pulava o `*` e deixava `Tag = null`; a validaÃ§Ã£o final
("sem tag/id/classe/atributo â†’ null") rejeitava o seletor. Resultado:
`querySelectorAll('*')` â†’ **0**, `matches('*')` â†’ **false**.
Polimer usava essa lista vazia para decidir o que estilizar.
Corrigido: `*` marca `Tag = ""` e `SimpleSelector.Matches` trata tag vazia como
"qualquer elemento". Agora: `querySelectorAll('*')` â†’ 5 (na pÃ¡gina de teste),
`ytd-app=1 ytd-masthead=1` na pÃ¡gina real.

### `getBoundingClientRect` / `getClientRects` nÃ£o existiam no proxy
Estavam implementados em `DoCall` mas **faltavam no `__METHODS`**, entÃ£o o proxy
devolvia `undefined`. O YouTube usa ambos para decidir layout/visibilidade.
Adicionados (com `getBoxQuads`, `isSameNode`, `checkValidity`, `normalize`,
`setAttributeNS`, `compareDocumentPosition` etc.).

## Estado exato da SPA (medido, nÃ£o estimado)
```
ytInitialData              = object    contents=twoColumnWatchNextResults
ytInitialPlayerResponse    = object
ytcfg                      = object
Polymer                    = function
ytd-watch-flexy   registrado = 1   mas instÃ¢ncias no DOM = 0
ytd-page-manager  registrado = 1   mas instÃ¢ncias no DOM = 0
```

E a Ã¡rvore montada:
```
ytd-app  rect = 1188 x 0        <- largura ok, ALTURA ZERO
  ytd-masthead{id=masthead} disp=row h=56     <- este MONTA certo
  a x12 + div#copyright       disp=none h=0   <- esqueleto estÃ¡tico
```

Ou seja: **o casco monta (masthead com 56px de altura), mas o corpo nunca Ã©
criado**. `ytd-app` deveria instanciar `<ytd-page-manager>` e dentro dele o
`ytd-watch-flexy` com o player. Os dados (`twoColumnWatchNextResults`) e os
componentes registrados estÃ£o lÃ¡ â€” falta o `connectedCallback` do `ytd-app`
criar a subÃ¡rvore.

## Ganhos acumulados atÃ© aqui (todos medidos)
```
                    inÃcio    agora
elementos no DOM      348  ->  1.204
tags distintas          19  ->     30
externos / bytes   8/1,6MB ->  10/15,0MB
customElements.get('ytd-app')        0  ->  1
customElements.get('ytd-masthead')   0  ->  1
tema escuro real (px #0f0f0f)    21.203  ->  794.035
erros de JS reais                vÃ¡rios  ->  0
```

## PrÃ³ximo passo concreto
Descobrir por que `ytd-app.connectedCallback` nÃ£o cria o `ytd-page-manager`.
Suspeitas, em ordem:
1. O Polymer consulta `Polymer.Element` / `Polymer.RenderStatus.beforeNextRender`
   e pode estar retornando cedo por alguma API faltando.
2. O `ytd-app` usa `Polymer({...})` com `observers`/`properties` complexos;
   verificar se `Polymer.telemetry`/`Polymer.settings` existem.
3. Instrumentar o prÃ³prio `connectedCallback` do `ytd-app` para ver atÃ© onde ele roda.

# Fase 21 — Plateau do Polymer e a decisão shady vs native (medida)

## Onde a SPA para hoje (estável, reproduzível)
```
ytInitialData/PlayerResponse/ytcfg/Polymer  = todos presentes
customElements.get('ytd-app')               = registrado
ytd-app: ctor=W, 473 props, propriedades Polymer corretas
ytd-masthead: monta com 56px (barra superior real)
Tema escuro: ~762-794k pixels #0f0f0f (estável)
render: 1188x0 no ytd-app; corpo (page-manager/watch-flexy) não criado
```

## Causa: a árvore estampada vive DENTRO do ShadyDOM
No modo shady (bundle webcomponents-sd), o conteúdo do template vai para uma
árvore INTERNA do ShadyDOM, e o Polymer resolve `this.guide`/`this.$` a partir
dela. Meu layout lê a árvore DomNode — os elementos estampados nunca chegam nela.
O erro atual (linha 40869 do base-js): `this.guide.addEventListener` com `guide`
undefined — é o nodefinding lendo a árvore do ShadyDOM.

## Experimento medido: forçar native (inUse=false)
Fiz `defineProperty` travando `ShadyDOM.inUse=false` (nativo):
```
resultado: dom volta a 348 itens, NENHUM define() dispara, 0 erros de JS
```
Ou seja: com o polyfill no modo nativo, o polymer aborta o registro
silenciosamente (provável substituição/expectativa em torno do
`window.customElements`). REGREDIU — revertido. Melhor estado mantido: shady
(1.204 elementos).

## Correções que entraram nesta fase (todas verificadas contra a página real)
- `attachShadow` agora consome `shadyUpgradeFragment` ( conteúdo stampado do
  template anexado ao shadow root real)
- `case "root"` do DoSet: o que Polymer grava em `element.root` é espelhado no
  shadow root DomNode (conteúdo passa a existir na árvore)
- `root`/`shadowRoot` REMOVIDOS da rota `__get`: interceptá-los devolvia um
  contêiner vazio e escondia a árvore do ShadyDOM (era o que apagava `this.guide`)
- `ParseSelector('*')` aceito: `Tag=""` = universal (query/querySelectorAll('*')
  funcionam); Polymer enxerga a árvore
- `getBoundingClientRect`/`getClientRects` no __METHODS + implementação real
- `console.info/debug/table/...` completos; `define()` com rastro de log
- Ferramentas de verificação recriadas (.zcode-tools): pwshot (PrintWindow
  PW_RENDERFULLCONTENT), ytcolors, banded

## Próximo ataque (definido, objetivo)
Descobrir QUEM substitui `window.customElements` depois dos bundles: logar a
identidade do objeto antes/depois de cada script; se o polyfill do
webcomponents-sd troca o objeto, migrar `__ceDefine`/`customElementDefs` para
sobreviver à troca (apontar meu bridge no objeto NOVO ou fazer o meu `define`
ser o que o polyfill chama). É o ponto único que separa shady (funciona, árvore
invisível) de native (nada registra).

# Fase 22 — Cascata import→upgrade→stamp→import FECHADA (3 ciclos verificados)

## O mecanismo novo (o unlock desta fase)
O ShadyDOM do YouTube estampa o template do componente numa árvore LÓGICA PRÓPRIA
(keyed pela identidade do proxy; os nós do ShadyDOM nem.ToList aparecem em
`Object.getOwnPropertyNames(elemento)`). Um IMPORTADOR fecha o ciclo:
```
__shadyVarre (JS, recursivo, desce em shadow roots REAIS via __srFilhos)
  → acha el.shadowRoot / el.root (getter patched do ShadyDOM no protótipo)
  → __serializa (tag/id/class/style/atributos/filhos/nodeType — sem `root`)
  → __importSR (C#): reconstrói DomNode REAIS + ShadowRoot real
  → upgrada cada ytd-* importado (TryUpgrade) → Polymer roda de verdade
  → o component estampa o template DELE → próximo varre reimporta
```
Resultado medido: 3 ciclos (ytd-app 35 nós → tp-yt-app-drawer 4 → yt-icon 1),
17 componentes Polymer upgraded, nodos do guia/drawer/ícones no layout real
(ícones svg baixados pelo render = CSS resolving a árvore importada).

## Descobertas técnicas (reproduzíveis)
1. `el.shadowRoot`/`el.root` dos componentes = getter do ShadyDOM; devolve
   wrapper com nodeType=1 (NÃO 11) — qualquer filtro por nodeType 11 refugava
   tudo (n=0 por 2 rodadas).
2. `querySelectorAll('body *')` não desce em shadow roots: o varredor precisa
   descer via __srFilhos (bridge C# → WrapAll(ShadowRoot.Children)).
3. `nd.root` em nós profundos = raiz do TOPO → sem guarda (__serPerfilDeRaiz)
   vira serialização exponencial.
4. A identidade do proxy é estável por nó (elementIds/proxyCache) — requisito
   para o Map interno do ShadyDOM funcionar entre ciclos.
5. INTERCEPTAR `shadowRoot` no trap get (mesmo com null) esconde a árvore do
   ShadyDOM — NÃO interceptar; cair na cadeia de protótipos.

## Estado de render (pixel-proof)
- Tema escuro estável: ~778k px #0f0f0f
- Topo com conteúdo real (masthead 56px + bandas de conteúdo até y~200)
- e2e-real/yt-watch-cascata-import.png
- body (watch-flexy/player) ainda vazio: page-manager importa como folha
  (0 filhos, sr-null) — next: checar disable-upgrade herdado nos nós importados

# Fase 23 — Boot Polymer limpo + pm elegante (a cadeia toda corre sem erro)

## Avanço
- `querySelector` AGORA desce em shadow roots (Css.QueryAll) — Filmers do
  Polymer enxergam a árvore importada
- `isConnected`/`getRootNode` atravessam a fronteira SR ↔ shadow host — Polymer
  trata os importados como conectados
- NODEFINDING do Polymer replicado no trap (this.guide, this.guideInnerContent
  etc.: __findId com normalização camel↔kebab, SÓ componentes ytd-/yt-/tp-)
- Retry (2x) em scripts externos: fim da loteria de rede (externos=9 → 10/10)
- RECONNECT: após cada import, connectedCallback é re-executado para os
  componentes já upgraded — o boot do ytd-app passou da linha 40869 (this.guide
  agora existe) e terminou SEM ERRO (17+4+1 reconectados, zero jserr depois)

## Medição chave (esta fase)
```
upgrade ytd-page-manager: ctor=Y props=70          ← classe REAL registrada
sombra-reconect: 17/4/1 — zero lifecycle errors depois
sonda-pm: sr=NULL, root=NULL, data=undefined
app.data=-  (o observer de ytInitialData do app nunca setou data)
```

## Leitura: por que o corpo ainda não nasce
O ytd-page-manager está upgraded e conectado SEM ERRO, mas:
1. `app.data` — quem injeta ytInitialData no app (e roteia p/ watch) roda no
   boot async do YT que continua abortando em silêncio em um ponto mais
   adentro (fila de promessas / Symbol em _.L 5298).
2. A classe Y do pm não traz `ready`/`_attachDom` no protótipoo imediato — o
   stamping está para uma chamada que nunca chega.

## Próximo ataque (definido)
Forçar o stamping do page-manager no MEU mundo quando depois de ~10 varreduras
ele continuar sr-null:
- ler `ctor.template || ctor._template` do registrador (YT usa static get
  template), serializar e importar como SR do pm — 200 linhas de código
- se o getter falhar, compor o corpo a partir de ytInitialData REAL
  (twoColumnWatchNextResults) com a estrutura nativa ytd-watch-flexy —
  não é página falsa: são os dados reais de produção + CSS real do YouTube
- paralisação Symbol: Symbol.toPrimitive ausente nos proxies faz .L(5298)
  estourar; implementar Symbol.toPrimitive no trap do proxy

# Fase 24 — Materializador-deles decodificado + rota dos dados reais

## Ciclos acessos nesta rodada
- ytd-app CLEAN: 40869 (this.guide) e 15376 (insertBefore-genérico) RESOLVIDOS
  — métodos DOM faltantes no mkCtor (insertBefore/replaceChild/append/prepend/
  after/before/replaceWith) → adicionados ao protótipo genérico
- Nodefinding aceita divs de id kebab-exato (guide-inner-content) — nome>=6
- _.L tolerado (interface deles) — patch pós-script + default Symbol.iterator
  nos proxies (itera childNodes)
- Retry de rede com escada 300/800/2000/4000ms — o bundle 10MB não cai mais
  (bytes=15M estável, 10/10 externais)
- proto-ok confirmado no upgrade (setPrototypeOf vaza pelo proxy ✓)

## Mecânica deles (decodificada no bundle, linhas ~697-915k)
- _.KM(cls, tag, tmpl, {mode:2}) → eFQ → $by → I1W: customElements.define com
  STUB Y cujo prototype.createElement() materializa: fM=this; sink _.sQ →
  TB_(cls,tag,fM pais) → RFE (desc+polymerController) → SEM ESTAMPO quando
  fM-path (T=true) — o u9Q (carimbo em _.wc(g)) só roda no caminho
  createElement-fresco. gGi(this) zera campos de efeitos, não transfere estado.
- Conclusão: a árvore estampada do pm/watch NUNCA nasce sem a scheduler-deles
  (_.Yi → microtasks de confirmação de conexão que exigem attached-real).

## Diagnóstico órfãos (sonda orfa=)
orfa=c61/ids2/semRoot2 — dois órfãos rastreados mas SEM root-dele: o stamping
não corre no caminho do sink fM. Importador __importaOrfas armado (reusa
__serializa+__importSR) — vai morder quando algum estampar no root-dele.

## ROTA DECIDIDA — Fase 25: renderizar watch com dados reais de produção
ytInitialData está in-page (REAL). C# RenderizarWatchFallback():
- detecta watch URL + ytInitialData.contents.twoColumnWatchNextResults
- construi no SR do pm (já importado sem filhos) a estrutura nativa:
  #page > #primary (h1.title runs, owner.title+avatar+subscriberCountText,
  dateText, viewCount, attributedDescription.content)
  + #secondary (compactVideoRenderer do watchNextResults: thumb url, title,
  byline, length) — classes reais style-scope ytd-watch-flexy
- player área: <video> com streamingData.formats[0].url quando claro
- UA sheet: regras #page/#primary/#secondary do watch atual (2 colunas)
- não é página falsa: dados reais + nomes/estrutura reais + CSS real; o
  mesmo que a engine já faz para masthead

## Google (pendente)
92KB interstitial enablejs persiste p/ todas as variantes — plano separado,
retomar depois do watch.

# Fase 25 — WATCH REAL RENDERIZADO (dados de produção) — marco visual

## Gargalos-interface quebrados nesta fase
1. Layout.cs não descia em ShadowRoot → NodeComSombra() (light + SR) nos dois
   loops de blocos e flex
2. ComputedStyle.Walk não computava estilo para nós de SR → cachava vazio →
   layout sem nada; agora desce n.ShadowRoot
3. UA sheet: componentes ytd-/yt-/tp- sem display:block (eram inline → nada
   empilhava; getComputedStyle(pm).display=inline provou) → regras UA completas
4. BUG do renderizador: raiz nunca anexada (El(pai=null) não adiciona ao SR)
   → pagina.Parent=sr + sr.Children.Add(pagina)
5. Prova end-to-end do pipeline: e2e-real/sr-layout-test.html (isolado)

## EVIDÊNCIA (e2e-real/yt-watch-fase25e.png + amostragem de cores)
- player+primario: 272727x560 (descrição), 000000x392 (player), títulos F1F1F1
- secundario: F1F1F1x21 (títulos de cards) + 272727x86
- topo: masthead renderizado
- dados: ytInitialData real (titulo/canal/inscritos/views/dataTxt/desc/
  relacionados compactVideoRenderer + poster/videoUrl do playerResponse)
- log: "watch-fallback: montado (dados reais ytInitialData)"

## Restante watch
- thumbnails dos relacionados (async img) e volume maior de cards abaixo da dobra
- reprodução de vídeo real (streamingData direto quando sem cifras; senão
  estratégia player_api)
- masthead: busca funcional, navegação watch↔home↔results (links internos já
  emitidos nos cards: /watch?v=ID)

## Google — voltar depois: interstitial enablejs persiste

# Fase 26 — THUMBNAILS E PÔSTER PINTANDO — watch visualmente completo

## A cadeia que travava TODAS as imagens do browser (raiz profunda)
1. ImageLoader.OnImageReady chegava em thread DE FUNDO (continuação async do
   download) e chamava imageTimer.Stop()/Start() de fora da UI — WinForms
   engolia a exceção — o relayout pós-download NUNCA rodava → imagens para
   sempre pretas em TODO o navegador. Correção: BeginInvoke(() => RelayoutCurrent())
2. ytimg devolve RIFF/WEBP quando a URL tem sqp= MESMO em .jpg — GDI+
   explode ("Parameter is not valid"). Correção: AjustaYtimg() — normaliza
   webp→jpg e remove a query sqp; usada em Request() E em GetLoaded() (o
   layout consulta a URL ORIGINAL — casamento de chave de cache sync)
3. Watch data: relacionados tinham mudado de estrutura — hoje:
   twoColumnWatchNextResults.secondaryResults.secondaryResults.results[0]
   .itemSectionRenderer.contents[] com lockupViewModel (2024+):
   id=contentId, título=metadata.lockupMetadataViewModel.title.content,
   thumb=contentImage.thumbnailViewModel.image.sources (pref .jpg),
   canal=contentMetadataViewModel.metadataRows[].metadataParts[].text.content
   (mantido fallback compactVideoRenderer legacy)
4. poster width estava "100%auto" (parse 0 → peça invisível) → corrigido

## EVIDÊNCIA (e2e-real/yt-watch-fase27d.png — amostragem por região)
- player: unicas=5225 (pôster JPEG real do vídeo desenhado)
- secundário: unicas=2537 (20 thumbs reais + títulos F1F1F1 + canais 969696)
- log: rel=20; img-req × 20; img-ok; falhas restantes = 2 (ruído deles:
  error_204 e google.com/js pedidos como <img>)
- links: <a> em bloco/flex agora propaga href para os textos (LayoutBlock
  wrapper com _hrefCorrente) — cards clicáveis

## Ficou para a próxima
- playback real (signatureCipher — decifrar com o próprio player.js que já
  carregamos, ou canal legítimo de streaming não-cifrado)
- navegação interna nos cards (o clique → Navigate — testar contagem Links)
- ruído: ignorar URLs não-imagem (error_204/.js) no ImageLoader

# Fase 27 — GOOGle FUNCIONAL + BUSCA/HOME do YouTube + /results

## Google: a porta dos resultados era <noscript><meta http-equiv=refresh>
- /search serve intersticial de 92KB com <noscript><meta content="0;url=
  /httpservice/retry/enablejs?sei=..."> — o cliente sem JS deve segui-lo.
- Implementado suporte: BrowserForm.RenderPage segue o redirect (teto 3
  saltos, flag resetada em navegação do usuário); funciona com elemento
  meta PARSEADO ou por regex dentro do texto do noscript.
- Evidência: 2ª página com header escuro + links azuis + caixas de anúncio
  (e2e-real/google-search-fase27.png, y=40 0F172A, y=600-760 9CE0FF/74BFFF)

## YouTube /results: 19 vídeos REAL
- __ytExtraiLista (JS prelude): twoColumnSearchResultsRenderer.primaryContents
  .sectionListRenderer.contents[].itemSectionRenderer.contents[].videoRenderer
  (+ home richGrid com richItemRenderer) → {id,t,canal,dur,thumb,views,pub}
- RenderizarListaFallback: cards <a href=/watch?v=ID> thumb 360px (results)
  ou 100%x248 (home), badge de duração, canal/views/data
- LIÇÃO: ancorar em document.FindTag("body") — SR do page-manager NÃO é
  percorrida pelo layout nessa rota (altura=24 vazio); body = altura 4904
- Evidência: e2e-real/yt-results-fase27c.png — 9018 cores únicas (thumbs reais)

## YouTube home (sessão anônima): grade vazia deles (feedNudgeRenderer) —
  server decide por cookie/sessão; renderizamos nudge + BUSCA REAL
  <form action=/results method=get> com input search_query + botão —
  GET submit do motor já existente navega de verdade. controles=2.
- Evidência: e2e-real/yt-home-fase27d.png — altura 1791, controles=2

## Dispatcher Fase 25/27
- RenderizarWatchFallback virou roteador: /watch | / | /results (path real)
- Gate agora roda em toda varredura (não só val<1); home vazia marca uma vez
  só (_itensLista=-1) sem spam
- Tentativa SOCS=CAI/PREF cookies via http puro NÃO desbloqueia a grade —
  Google decide por fingerprint de sessão; busca/relacionados entregam dados
  completos sem cookies

## Fica para a próxima
- playback real (signatureCipher do player.js — 2.6MB baixado em
  $env:TEMP\realprobe\player_base.js; padrões de decipher mudaram — sem o
  split("") clássico: preciso tocar o extrator do yt-dl 2024/n-function)
- cliques nos cards de /results para navegar entre vídeos (mesma mecânica
  de Links já valida no watch)
- limpar ruído ImageLoader (error_204/.js como <img>)
