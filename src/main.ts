import './style.css';
import { parseHTML } from './engine/html';
import { parseCSS } from './engine/cssParser';
import { buildStyleTree } from './engine/style';
import { buildLayoutTree, LayoutBox } from './engine/layout';
import { paintLayoutBox } from './engine/paint';
import type { Node } from './engine/dom';

// Presets data
const PRESETS: Record<string, { html: string; css: string }> = {
  welcome: {
    html: `<!-- Página de Boas-Vindas Uranium -->
<div class="welcome-box">
  <h1>Olá de Uranium! ⚛️</h1>
  <p class="description">
    Este é um <strong>motor de renderização</strong> (layout & paint engine) completo, construído do zero em TypeScript e rodando em tempo real.
  </p>
  
  <div class="card">
    <h2>Como Funciona o Pipeline?</h2>
    <p>1. O texto do HTML vira uma árvore DOM.</p>
    <p>2. O CSS é analisado gerando regras CSSOM.</p>
    <p>3. DOM + CSSOM são unidos na Style Tree.</p>
    <p>4. O Layout calcula posições e Box Model.</p>
    <p>5. O Paint desenha as caixas no Canvas.</p>
  </div>
  
  <p class="footer">Altere o HTML ou o CSS à esquerda para ver as atualizações na hora!</p>
</div>`,
    css: `.welcome-box {
  background-color: #ffffff;
  padding: 24px;
  margin: 15px;
  border-width: 3px;
  border-color: #1e293b;
  font-family: sans-serif;
}

h1 {
  color: #1e3a8a;
  font-size: 26px;
  font-weight: bold;
  margin-bottom: 12px;
}

.description {
  color: #475569;
  font-size: 15px;
  line-height: 1.5;
  margin-bottom: 20px;
}

.card {
  background-color: #f1f5f9;
  padding: 16px;
  border-width: 1px;
  border-color: #cbd5e1;
  margin-bottom: 15px;
}

h2 {
  color: #0f172a;
  font-size: 16px;
  font-weight: bold;
  margin-bottom: 10px;
}

p {
  color: #334155;
  font-size: 13px;
  line-height: 1.4;
  margin-bottom: 6px;
}

.footer {
  color: #64748b;
  font-size: 12px;
  font-weight: normal;
  line-height: 1.2;
}`
  },
  boxmodel: {
    html: `<div class="container">
  <div class="box-one">
    Caixa com Margem e Padding
  </div>
  
  <div class="box-two">
    Caixa com Borda Grossa
  </div>

  <div class="nested-outer">
    Pai (Outer)
    <div class="nested-inner">
      Filho (Inner)
    </div>
  </div>
</div>`,
    css: `.container {
  background-color: #f8fafc;
  padding: 20px;
  font-family: sans-serif;
}

.box-one {
  background-color: #3b82f6;
  color: #ffffff;
  margin: 15px;
  padding: 15px;
  font-weight: bold;
}

.box-two {
  background-color: #10b981;
  color: #ffffff;
  border-width: 10px;
  border-color: #047857;
  padding: 10px;
}

.nested-outer {
  background-color: #e2e8f0;
  margin-top: 20px;
  padding: 20px;
  border-width: 2px;
  border-color: #94a3b8;
}

.nested-inner {
  background-color: #f43f5e;
  color: white;
  padding: 10px;
  margin: 10px;
}`
  },
  button3d: {
    html: `<div class="page">
  <h1>Exemplo de Botão 3D</h1>
  <p>Podemos criar layouts bonitos usando padding, bordas e fundos em blocos.</p>
  
  <div class="btn-container">
    <div class="btn-3d">
      Clique Aqui!
    </div>
  </div>
</div>`,
    css: `.page {
  background-color: #111827;
  padding: 30px;
  font-family: sans-serif;
  color: #f3f4f6;
}

h1 {
  font-size: 24px;
  color: #38bdf8;
  margin-bottom: 10px;
}

p {
  font-size: 14px;
  color: #9ca3af;
  margin-bottom: 30px;
}

.btn-container {
  padding: 20px;
  background-color: #1f2937;
  border-width: 1px;
  border-color: #374151;
}

.btn-3d {
  background-color: #e11d48;
  color: #ffffff;
  padding-top: 12px;
  padding-bottom: 12px;
  padding-left: 24px;
  padding-right: 24px;
  width: 180px;
  font-weight: bold;
  text-align: center;
  border-width: 4px;
  border-color: #9f1239;
  margin: 10px;
}`
  },
  columns: {
    html: `<div class="wrapper">
  <h1>Layout com Colunas (Larguras)</h1>
  
  <div class="column-left">
    <h3>Esquerda (40% de largura)</h3>
    <p>Texto da coluna esquerda do nosso motor de layout.</p>
  </div>

  <div class="column-right">
    <h3>Direita (50% de largura)</h3>
    <p>Texto da coluna direita do nosso motor de layout.</p>
  </div>

  <div class="clearfix">
    Elemento Block após as colunas ocupando a largura inteira.
  </div>
</div>`,
    css: `.wrapper {
  background-color: #fafafa;
  padding: 15px;
  font-family: sans-serif;
}

h1 {
  font-size: 20px;
  color: #27272a;
  margin-bottom: 20px;
}

.column-left {
  background-color: #ffe4e6;
  padding: 15px;
  width: 300px;
  margin-bottom: 10px;
  border-width: 1px;
  border-color: #fda4af;
}

.column-right {
  background-color: #ecfeff;
  padding: 15px;
  width: 380px;
  margin-bottom: 10px;
  border-width: 1px;
  border-color: #a5f3fc;
}

.clearfix {
  background-color: #f4f4f5;
  padding: 10px;
  margin-top: 15px;
  border-width: 1px;
  border-color: #e4e4e7;
}`
  },
  textformatting: {
    html: `<div class="wrapper">
  <h1>Formatação de Texto e Parágrafos</h1>
  
  <p class="first-para">
    Os navegadores distribuem o texto horizontalmente, quebrando linhas quando o conteúdo atinge a borda da caixa do pai.
  </p>

  <p class="second-para">
    Nós podemos forçar quebras de linha usando a tag <br /> dentro do HTML.<br />
    Como demonstrado aqui na segunda linha do mesmo parágrafo!
  </p>

  <div class="styled-text">
    Podemos misturar propriedades como font-size e color herdadas.
  </div>
</div>`,
    css: `.wrapper {
  background-color: #ffffff;
  padding: 20px;
  font-family: sans-serif;
}

h1 {
  font-size: 22px;
  color: #4c1d95;
  margin-bottom: 15px;
  font-weight: bold;
}

.first-para {
  color: #1e293b;
  font-size: 14px;
  line-height: 1.5;
  margin-bottom: 16px;
  padding: 10px;
  background-color: #f8fafc;
}

.second-para {
  color: #0f766e;
  font-size: 14px;
  margin-bottom: 16px;
  border-left-width: 4px;
  border-color: #0d9488;
  padding-left: 12px;
}

.styled-text {
  font-size: 18px;
  color: #e11d48;
  font-weight: bold;
  margin-top: 20px;
}`
  }
};

// Global variables holding rendering state
let domRoot: Node | null = null;
let layoutRoot: LayoutBox | null = null;

let activeViewportWidth = 800;
let selectedNode: Node | null = null;
let selectedLayoutBox: LayoutBox | null = null;

// UI Elements
const htmlEditor = document.getElementById('html-editor') as HTMLTextAreaElement;
const cssEditor = document.getElementById('css-editor') as HTMLTextAreaElement;
const htmlLineNumbers = document.getElementById('html-line-numbers') as HTMLDivElement;
const cssLineNumbers = document.getElementById('css-line-numbers') as HTMLDivElement;
const presetSelect = document.getElementById('preset-select') as HTMLSelectElement;
const btnRender = document.getElementById('btn-render') as HTMLButtonElement;

const viewportCanvas = document.getElementById('viewport-canvas') as HTMLCanvasElement;
const overlayCanvas = document.getElementById('overlay-canvas') as HTMLCanvasElement;

const domTreeContainer = document.getElementById('dom-tree-container') as HTMLDivElement;
const layoutTreeContainer = document.getElementById('layout-tree-container') as HTMLDivElement;
const computedStylesTableBody = document.querySelector('#computed-styles-table tbody') as HTMLTableSectionElement;

// Performance timing displays
const perfTimeHtml = document.getElementById('perf-time-html') as HTMLElement;
const perfBarHtml = document.getElementById('perf-bar-html') as HTMLElement;
const perfTimeCss = document.getElementById('perf-time-css') as HTMLElement;
const perfBarCss = document.getElementById('perf-bar-css') as HTMLElement;
const perfTimeStyle = document.getElementById('perf-time-style') as HTMLElement;
const perfBarStyle = document.getElementById('perf-bar-style') as HTMLElement;
const perfTimeLayout = document.getElementById('perf-time-layout') as HTMLElement;
const perfBarLayout = document.getElementById('perf-bar-layout') as HTMLElement;
const perfTimePaint = document.getElementById('perf-time-paint') as HTMLElement;
const perfBarPaint = document.getElementById('perf-bar-paint') as HTMLElement;
const perfTimeTotal = document.getElementById('perf-time-total') as HTMLElement;

// Stat displays
const statNodes = document.getElementById('stat-nodes') as HTMLElement;
const statRules = document.getElementById('stat-rules') as HTMLElement;
const statBoxes = document.getElementById('stat-boxes') as HTMLElement;
const statCommands = document.getElementById('stat-commands') as HTMLElement;

// Initialize app
function init() {
  setupTabs();
  setupLineNumbers(htmlEditor, htmlLineNumbers);
  setupLineNumbers(cssEditor, cssLineNumbers);
  setupViewportPresets();

  // Load welcome preset by default
  loadPreset('welcome');

  // Listeners
  presetSelect.addEventListener('change', () => {
    loadPreset(presetSelect.value);
  });

  btnRender.addEventListener('click', () => {
    triggerRender();
  });

  // Debounced input render triggers
  let debounceTimeout: number;
  const onEditorInput = () => {
    clearTimeout(debounceTimeout);
    debounceTimeout = window.setTimeout(triggerRender, 400);
  };
  
  htmlEditor.addEventListener('input', () => {
    setupLineNumbers(htmlEditor, htmlLineNumbers);
    onEditorInput();
  });
  cssEditor.addEventListener('input', () => {
    setupLineNumbers(cssEditor, cssLineNumbers);
    onEditorInput();
  });

  // Initial trigger
  triggerRender();
}

function loadPreset(name: string) {
  const preset = PRESETS[name];
  if (preset) {
    htmlEditor.value = preset.html;
    cssEditor.value = preset.css;
    setupLineNumbers(htmlEditor, htmlLineNumbers);
    setupLineNumbers(cssEditor, cssLineNumbers);
    triggerRender();
  }
}

// Line Numbers synchronization
function setupLineNumbers(textarea: HTMLTextAreaElement, lineGutter: HTMLDivElement) {
  const lines = textarea.value.split('\n');
  const count = Math.max(1, lines.length);
  let html = '';
  for (let i = 1; i <= count; i++) {
    html += `<div class="line-number">${i}</div>`;
  }
  lineGutter.innerHTML = html;
}

// Tabs switching handler
function setupTabs() {
  const tabButtons = document.querySelectorAll('.tab-btn');
  tabButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const panel = btn.parentElement;
      if (!panel) return;

      // Deactivate siblings
      panel.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      // Hide active contents in this specific panel section
      const tabTarget = btn.getAttribute('data-tab');
      if (tabTarget) {
        let siblingContentContainer = panel.parentElement;
        if (siblingContentContainer) {
          siblingContentContainer.querySelectorAll('.tab-content').forEach(content => {
            if (content.id === tabTarget) {
              content.classList.remove('hidden');
            } else if (content.parentElement === siblingContentContainer) {
              content.classList.add('hidden');
            }
          });
        }
      }
    });
  });
}

function setupViewportPresets() {
  const buttons = document.querySelectorAll('.viewport-btn');
  buttons.forEach(btn => {
    btn.addEventListener('click', () => {
      buttons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      const width = parseInt(btn.getAttribute('data-width') || '800');
      activeViewportWidth = width;
      
      // Update canvas dimensions
      viewportCanvas.width = width;
      overlayCanvas.width = width;
      
      // Update CSS variables if needed, triggers layout re-execution
      triggerRender();
    });
  });
}

// core render engine runner
function triggerRender() {
  const t0 = performance.now();

  // 1. Parsing HTML
  const tHtml0 = performance.now();
  const htmlStr = htmlEditor.value;
  domRoot = parseHTML(htmlStr);
  const tHtml1 = performance.now();

  // 2. Parsing CSS
  const tCss0 = performance.now();
  const cssStr = cssEditor.value;
  const rules = parseCSS(cssStr);
  const tCss1 = performance.now();

  // 3. Style Matching (DOM + CSSOM rules)
  const tStyle0 = performance.now();
  buildStyleTree(domRoot, rules);
  const tStyle1 = performance.now();

  // 4. Layout calculations
  const tLayout0 = performance.now();
  layoutRoot = buildLayoutTree(domRoot);
  
  if (layoutRoot) {
    layoutRoot.layout(0, 0, activeViewportWidth, 0);

    // Adjust canvas height to match computed layout tree height
    const totalHeight = Math.max(450, layoutRoot.marginBoxHeight() + 50);
    viewportCanvas.height = totalHeight;
    overlayCanvas.height = totalHeight;
  }
  const tLayout1 = performance.now();

  // 5. Paint generation & Drawing (Direct Draw)
  const tPaint0 = performance.now();
  let drawCallsCount = 0;
  if (layoutRoot) {
    const ctx = viewportCanvas.getContext('2d');
    if (ctx) {
      ctx.clearRect(0, 0, viewportCanvas.width, viewportCanvas.height);
      const stats = { drawCalls: 0 };
      paintLayoutBox(ctx, layoutRoot, stats);
      drawCallsCount = stats.drawCalls;
    }
  } else {
    // Clear canvas
    const ctx = viewportCanvas.getContext('2d');
    if (ctx) ctx.clearRect(0, 0, viewportCanvas.width, viewportCanvas.height);
  }
  const tPaint1 = performance.now();

  const t1 = performance.now();

  // Timing differences
  const dHtml = tHtml1 - tHtml0;
  const dCss = tCss1 - tCss0;
  const dStyle = tStyle1 - tStyle0;
  const dLayout = tLayout1 - tLayout0;
  const dPaint = tPaint1 - tPaint0;
  const dTotal = t1 - t0;

  // Render analytics UI
  updatePerformanceDashboard(dHtml, dCss, dStyle, dLayout, dPaint, dTotal);
  updatePageStats(rules.length, drawCallsCount);

  // Render DOM tree list
  renderDOMTree();
  renderLayoutTree();

  // Highlight/Inspector state reset/preservation
  clearHighlight();
  if (selectedNode) {
    highlightSelectedNode();
  }
}

function updatePerformanceDashboard(
  dHtml: number,
  dCss: number,
  dStyle: number,
  dLayout: number,
  dPaint: number,
  dTotal: number
) {
  perfTimeHtml.innerText = `${dHtml.toFixed(2)} ms`;
  perfTimeCss.innerText = `${dCss.toFixed(2)} ms`;
  perfTimeStyle.innerText = `${dStyle.toFixed(2)} ms`;
  perfTimeLayout.innerText = `${dLayout.toFixed(2)} ms`;
  perfTimePaint.innerText = `${dPaint.toFixed(2)} ms`;
  perfTimeTotal.innerText = `${dTotal.toFixed(2)} ms`;

  // Scale widths
  const maxTime = Math.max(0.1, dHtml, dCss, dStyle, dLayout, dPaint);
  perfBarHtml.style.width = `${(dHtml / maxTime) * 100}%`;
  perfBarCss.style.width = `${(dCss / maxTime) * 100}%`;
  perfBarStyle.style.width = `${(dStyle / maxTime) * 100}%`;
  perfBarLayout.style.width = `${(dLayout / maxTime) * 100}%`;
  perfBarPaint.style.width = `${(dPaint / maxTime) * 100}%`;
}

function countNodes(node: Node): number {
  let count = 1;
  for (const child of node.children) {
    count += countNodes(child);
  }
  return count;
}

function countBoxes(box: LayoutBox): number {
  let count = 1;
  for (const child of box.children) {
    count += countBoxes(child);
  }
  return count;
}

function updatePageStats(rulesCount: number, commandsCount: number) {
  const nodesCount = domRoot ? countNodes(domRoot) : 0;
  const boxesCount = layoutRoot ? countBoxes(layoutRoot) : 0;

  statNodes.innerText = nodesCount.toString();
  statRules.innerText = rulesCount.toString();
  statBoxes.innerText = boxesCount.toString();
  statCommands.innerText = commandsCount.toString();
}

// Tree view traversals
function renderDOMTree() {
  domTreeContainer.innerHTML = '';
  if (!domRoot) return;

  const buildTreeUI = (node: Node, depth: number): HTMLElement => {
    const item = document.createElement('div');
    item.className = 'tree-node';
    if (depth === 0) item.classList.add('root');

    const content = document.createElement('span');
    content.className = 'tree-node-content';
    if (node === selectedNode) content.classList.add('selected');

    if (node.type === 'element') {
      let attrsStr = '';
      for (const [key, val] of Object.entries(node.attributes)) {
        attrsStr += ` <span class="attr-name">${key}</span>=<span class="attr-val">"${val}"</span>`;
      }
      content.innerHTML = `&lt;<span class="tag-name">${node.tagName}</span>${attrsStr}&gt;`;
    } else {
      const displayVal = node.text.trim();
      content.innerHTML = `<span class="text-content">"${displayVal.length > 25 ? displayVal.slice(0, 25) + '...' : displayVal}"</span>`;
    }

    item.appendChild(content);

    // Event handlers for DevTools highlights
    content.addEventListener('mouseenter', (e) => {
      e.stopPropagation();
      highlightNode(node);
    });

    content.addEventListener('mouseleave', () => {
      clearHighlight();
    });

    content.addEventListener('click', (e) => {
      e.stopPropagation();
      selectNode(node);
    });

    if (node.children && node.children.length > 0) {
      const childrenWrapper = document.createElement('div');
      childrenWrapper.className = 'tree-children';
      for (const child of node.children) {
        childrenWrapper.appendChild(buildTreeUI(child, depth + 1));
      }
      item.appendChild(childrenWrapper);
      
      // Append closing tag
      if (node.type === 'element') {
        const closeTag = document.createElement('div');
        closeTag.className = 'tree-node closing-tag';
        closeTag.innerHTML = `&lt;/<span class="tag-name">${node.tagName}</span>&gt;`;
        item.appendChild(closeTag);
      }
    }

    return item;
  };

  domTreeContainer.appendChild(buildTreeUI(domRoot, 0));
}

function renderLayoutTree() {
  layoutTreeContainer.innerHTML = '';
  if (!layoutRoot) return;

  const buildLayoutUI = (box: LayoutBox, depth: number): HTMLElement => {
    const item = document.createElement('div');
    item.className = 'tree-node';
    if (depth === 0) item.classList.add('root');

    const content = document.createElement('span');
    content.className = 'tree-node-content';
    if (box === selectedLayoutBox) content.classList.add('selected');

    const name = box.boxType === 'anonymous' ? 'Anonymous' : 
                 (box.styledNode.type === 'element' ? box.styledNode.tagName : 'Text');
                 
    content.innerHTML = `<span class="tag-name">${name}</span> <span class="box-info">${box.boxType} (${Math.round(box.width)}x${Math.round(box.height)})</span>`;

    item.appendChild(content);

    content.addEventListener('mouseenter', (e) => {
      e.stopPropagation();
      highlightLayoutBox(box);
    });

    content.addEventListener('mouseleave', () => {
      clearHighlight();
    });

    content.addEventListener('click', (e) => {
      e.stopPropagation();
      selectLayoutBox(box);
    });

    if (box.children && box.children.length > 0) {
      const childrenWrapper = document.createElement('div');
      for (const child of box.children) {
        childrenWrapper.appendChild(buildLayoutUI(child, depth + 1));
      }
      item.appendChild(childrenWrapper);
    }

    return item;
  };

  layoutTreeContainer.appendChild(buildLayoutUI(layoutRoot, 0));
}

// Find LayoutBox corresponding to DOM node
function findBoxForNode(box: LayoutBox, node: Node): LayoutBox | null {
  if (box.styledNode === node) {
    return box;
  }
  for (const child of box.children) {
    const res = findBoxForNode(child, node);
    if (res) return res;
  }
  return null;
}

// Element highlighters (Draw onto overlayCanvas)
function highlightNode(node: Node) {
  if (!layoutRoot) return;
  const box = findBoxForNode(layoutRoot, node);
  if (box) {
    highlightLayoutBox(box);
  }
}

function highlightLayoutBox(box: LayoutBox) {
  const ctx = overlayCanvas.getContext('2d');
  if (!ctx) return;

  // Clear previous overlay
  ctx.clearRect(0, 0, overlayCanvas.width, overlayCanvas.height);

  // Draw Margin Area (Orange)
  ctx.fillStyle = 'rgba(245, 158, 11, 0.4)';
  ctx.fillRect(box.marginBoxX(), box.marginBoxY(), box.marginBoxWidth(), box.marginBoxHeight());

  // Draw Border Area (Red)
  ctx.fillStyle = 'rgba(239, 68, 68, 0.4)';
  const bx = box.borderBoxX();
  const by = box.borderBoxY();
  const bw = box.borderBoxWidth();
  const bh = box.borderBoxHeight();
  ctx.clearRect(bx, by, bw, bh); // clip inside
  ctx.fillRect(bx, by, bw, bh);

  // Draw Padding Area (Green)
  ctx.fillStyle = 'rgba(16, 185, 129, 0.4)';
  const px = box.paddingBoxX();
  const py = box.paddingBoxY();
  const pw = box.paddingBoxWidth();
  const ph = box.paddingBoxHeight();
  ctx.clearRect(px, py, pw, ph); // clip inside
  ctx.fillRect(px, py, pw, ph);

  // Draw Content Area (Blue)
  ctx.fillStyle = 'rgba(59, 130, 246, 0.4)';
  ctx.fillRect(box.x, box.y, box.width, box.height);

  // Draw text label on box
  ctx.fillStyle = '#1e293b';
  ctx.font = 'bold 11px sans-serif';
  const name = box.styledNode.type === 'element' ? box.styledNode.tagName : 'text';
  const label = `${name} | ${Math.round(box.width)} × ${Math.round(box.height)}`;
  const textWidth = ctx.measureText(label).width;

  ctx.fillRect(box.x, box.y - 18, textWidth + 8, 16);
  ctx.fillStyle = '#f8fafc';
  ctx.fillText(label, box.x + 4, box.y - 6);
}

function clearHighlight() {
  const ctx = overlayCanvas.getContext('2d');
  if (ctx) {
    ctx.clearRect(0, 0, overlayCanvas.width, overlayCanvas.height);
  }
}

function highlightSelectedNode() {
  if (selectedNode) {
    highlightNode(selectedNode);
  }
}

// Selector Handlers
function selectNode(node: Node) {
  selectedNode = node;
  if (layoutRoot) {
    selectedLayoutBox = findBoxForNode(layoutRoot, node);
  }
  updateInspectorDetails();
  renderDOMTree();
  renderLayoutTree();
}

function selectLayoutBox(box: LayoutBox) {
  selectedLayoutBox = box;
  selectedNode = box.styledNode;
  updateInspectorDetails();
  renderDOMTree();
  renderLayoutTree();
}

function updateInspectorDetails() {
  if (!selectedLayoutBox) return;

  const box = selectedLayoutBox;

  // Update Box Model values
  document.getElementById('bm-margin-top')!.innerText = box.marginTop.toString();
  document.getElementById('bm-margin-bottom')!.innerText = box.marginBottom.toString();
  document.getElementById('bm-margin-left')!.innerText = box.marginLeft.toString();
  document.getElementById('bm-margin-right')!.innerText = box.marginRight.toString();

  document.getElementById('bm-border-top')!.innerText = box.borderTop.toString();
  document.getElementById('bm-border-bottom')!.innerText = box.borderBottom.toString();
  document.getElementById('bm-border-left')!.innerText = box.borderLeft.toString();
  document.getElementById('bm-border-right')!.innerText = box.borderRight.toString();

  document.getElementById('bm-padding-top')!.innerText = box.paddingTop.toString();
  document.getElementById('bm-padding-bottom')!.innerText = box.paddingBottom.toString();
  document.getElementById('bm-padding-left')!.innerText = box.paddingLeft.toString();
  document.getElementById('bm-padding-right')!.innerText = box.paddingRight.toString();

  document.getElementById('bm-content-dim')!.innerText = `${Math.round(box.width)} x ${Math.round(box.height)}`;

  // Update Computed Styles Table
  computedStylesTableBody.innerHTML = '';
  const styles = box.styledNode.specifiedValues || {};
  
  for (const [prop, val] of Object.entries(styles)) {
    let displayVal = '';
    if (val.type === 'keyword') displayVal = val.value;
    else if (val.type === 'length') displayVal = `${val.value}${val.unit}`;
    else if (val.type === 'color') displayVal = `rgba(${val.r}, ${val.g}, ${val.b}, ${val.a})`;

    const row = document.createElement('tr');
    row.innerHTML = `<td>${prop}</td><td>${displayVal}</td>`;
    computedStylesTableBody.appendChild(row);
  }
}

// Start everything when scripts load
init();
