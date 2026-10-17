#include <windows.h>
#include <string>
#include <vector>
#include <map>
#include <sstream>
#include <iostream>
#include <algorithm>

// ============================================================================
// 1. Modelagem do DOM (Document Object Model) em C++
// ============================================================================
enum NodeType { ELEMENT_NODE, TEXT_NODE };

struct Node {
    NodeType type;
    std::string tagName;
    std::string textContent;
    std::map<std::string, std::string> attributes;
    std::vector<Node*> children;

    Node(NodeType t) : type(t) {}
    ~Node() {
        for (Node* child : children) {
            delete child;
        }
    }
};

// ============================================================================
// 2. Parser HTML Simplificado em C++
// ============================================================================
class HTMLParser {
private:
    std::string input;
    size_t pos = 0;

    char peek() {
        if (pos >= input.length()) return '\0';
        return input[pos];
    }

    char consumeChar() {
        if (pos >= input.length()) return '\0';
        return input[pos++];
    }

    void consumeWhitespace() {
        while (pos < input.length() && (input[pos] == ' ' || input[pos] == '\t' || input[pos] == '\r' || input[pos] == '\n')) {
            pos++;
        }
    }

    std::string parseTagName() {
        std::string name = "";
        while (pos < input.length() && isalnum(peek())) {
            name += consumeChar();
        }
        return name;
    }

    std::map<std::string, std::string> parseAttributes() {
        std::map<std::string, std::string> attrs;
        while (true) {
            consumeWhitespace();
            if (peek() == '>' || peek() == '/' || peek() == '\0') {
                break;
            }
            std::string name = "";
            while (pos < input.length() && (isalnum(peek()) || peek() == '-')) {
                name += consumeChar();
            }
            consumeWhitespace();
            std::string value = "";
            if (peek() == '=') {
                consumeChar(); // consume '='
                consumeWhitespace();
                if (peek() == '"' || peek() == '\'') {
                    char quote = consumeChar();
                    while (pos < input.length() && peek() != quote) {
                        value += consumeChar();
                    }
                    consumeChar(); // consume end quote
                } else {
                    while (pos < input.length() && !isspace(peek()) && peek() != '>') {
                        value += consumeChar();
                    }
                }
            }
            attrs[name] = value;
        }
        return attrs;
    }

public:
    HTMLParser(const std::string& html) : input(html) {}

    Node* parse() {
        consumeWhitespace();
        if (peek() == '<') {
            return parseElement();
        }
        return parseText();
    }

    Node* parseElement() {
        consumeChar(); // consume '<'
        std::string tagName = parseTagName();
        std::map<std::string, std::string> attrs = parseAttributes();
        
        Node* elem = new Node(ELEMENT_NODE);
        elem->tagName = tagName;
        elem->attributes = attrs;

        if (peek() == '/') {
            consumeChar(); // consume '/'
            consumeChar(); // consume '>'
            return elem; // Self-closing
        }
        consumeChar(); // consume '>'

        // Parse children
        while (true) {
            consumeWhitespace();
            if (pos >= input.length()) break;
            if (peek() == '<' && pos + 1 < input.length() && input[pos + 1] == '/') {
                // Closing tag
                pos += 2; // consume '</'
                parseTagName(); // consume tag name
                consumeChar(); // consume '>'
                break;
            }
            elem->children.push_back(parse());
        }
        return elem;
    }

    Node* parseText() {
        std::string text = "";
        while (pos < input.length() && peek() != '<') {
            text += consumeChar();
        }
        Node* node = new Node(TEXT_NODE);
        node->textContent = text;
        return node;
    }
};

// ============================================================================
// 3. Estilos e Geometria do Layout (Box Model)
// ============================================================================
struct Style {
    COLORREF backgroundColor = CLR_INVALID;
    COLORREF textColor = RGB(0, 0, 0);
    int padding = 0;
    int margin = 0;
    int borderWidth = 0;
    COLORREF borderColor = RGB(0, 0, 0);
    int width = -1; // -1 significa Auto (100% do pai)
    int height = -1;
};

// Parser simples para extrair estilos inline, ex: style="background:#ff0000;padding:20px;"
Style parseInlineStyle(const std::string& styleStr) {
    Style style;
    std::stringstream ss(styleStr);
    std::string declaration;
    
    auto parseColor = [](const std::string& val) -> COLORREF {
        if (val == "red" || val == "#ff0000") return RGB(239, 68, 68);
        if (val == "blue" || val == "#0000ff") return RGB(59, 130, 246);
        if (val == "green" || val == "#00ff00") return RGB(16, 185, 129);
        if (val == "white" || val == "#ffffff") return RGB(255, 255, 255);
        if (val == "dark" || val == "#1e293b") return RGB(30, 41, 59);
        if (val == "grey" || val == "#e2e8f0") return RGB(226, 232, 240);
        if (val == "yellow" || val == "#f59e0b") return RGB(245, 158, 11);
        return CLR_INVALID;
    };

    while (std::getline(ss, declaration, ';')) {
        size_t colon = declaration.find(':');
        if (colon == std::string::npos) continue;
        std::string prop = declaration.substr(0, colon);
        std::string val = declaration.substr(colon + 1);
        
        // Trim whitespace
        prop.erase(remove_if(prop.begin(), prop.end(), isspace), prop.end());
        val.erase(remove_if(val.begin(), val.end(), isspace), val.end());

        if (prop == "background" || prop == "background-color") {
            style.backgroundColor = parseColor(val);
        } else if (prop == "color") {
            style.textColor = parseColor(val);
        } else if (prop == "padding") {
            style.padding = std::stoi(val);
        } else if (prop == "margin") {
            style.margin = std::stoi(val);
        } else if (prop == "border-width") {
            style.borderWidth = std::stoi(val);
        } else if (prop == "border-color") {
            style.borderColor = parseColor(val);
        } else if (prop == "width") {
            style.width = std::stoi(val);
        } else if (prop == "height") {
            style.height = std::stoi(val);
        }
    }
    return style;
}

// Representa uma caixa geométrica calculada pelo motor de Layout
struct LayoutBox {
    Node* node;
    Style style;
    int x = 0, y = 0, width = 0, height = 0;
    std::vector<LayoutBox*> children;

    LayoutBox(Node* n) : node(n) {
        if (node && node->type == ELEMENT_NODE) {
            auto it = node->attributes.find("style");
            if (it != node->attributes.end()) {
                style = parseInlineStyle(it->second);
            }
        }
    }
    
    ~LayoutBox() {
        for (LayoutBox* child : children) {
            delete child;
        }
    }

    // Algoritmo de layout block simplificado (vertical stacking)
    void calculateLayout(int parentX, int parentY, int parentWidth, int& currentStackY) {
        // Margem do Box Model
        int boxMargin = style.margin;
        int boxPadding = style.padding;
        int boxBorder = style.borderWidth;

        // X e Y iniciais
        x = parentX + boxMargin + boxBorder + boxPadding;
        y = parentY + currentStackY + boxMargin + boxBorder + boxPadding;

        // Largura: Se especificada usa ela, senão ocupa a largura disponível do pai
        if (style.width != -1) {
            width = style.width;
        } else {
            width = parentWidth - (boxMargin + boxBorder + boxPadding) * 2;
        }

        // Layout recursivo dos filhos
        int childStackY = 0;
        int innerX = x;
        int innerY = y;
        int innerWidth = width;

        for (LayoutBox* child : children) {
            child->calculateLayout(innerX, innerY, innerWidth, childStackY);
        }

        // Altura: Se especificada usa ela, senão soma a altura dos filhos acumulada
        if (style.height != -1) {
            height = style.height;
        } else {
            // Se for nó de texto, definimos uma altura de linha padrão
            if (node && node->type == TEXT_NODE) {
                height = 20; 
            } else {
                height = childStackY;
            }
        }

        // Acumula altura total ocupada por esta caixa no fluxo vertical do pai
        currentStackY += height + (boxMargin + boxBorder + boxPadding) * 2;
    }
};

// Construtor recursivo da árvore de Layout
LayoutBox* buildLayoutTree(Node* node) {
    LayoutBox* box = new LayoutBox(node);
    for (Node* child : node->children) {
        // Ignora espaços vazios no parser
        if (child->type == TEXT_NODE && child->textContent.find_first_not_of(" \t\r\n") == std::string::npos) {
            continue;
        }
        box->children.push_back(buildLayoutTree(child));
    }
    return box;
}

// ============================================================================
// 4. Pintura Nativa no Windows usando GDI Graphics (Sem WebView/Chromium)
// ============================================================================
void paintLayoutBox(HDC hdc, LayoutBox* box) {
    // 1. Desenhar Fundo se configurado
    if (box->style.backgroundColor != CLR_INVALID) {
        HBRUSH hBrush = CreateSolidBrush(box->style.backgroundColor);
        // Calcula a borda externa da caixa (incluindo padding)
        RECT rect = { 
            box->x - box->style.padding, 
            box->y - box->style.padding, 
            box->x + box->width + box->style.padding, 
            box->y + box->height + box->style.padding 
        };
        FillRect(hdc, &rect, hBrush);
        DeleteObject(hBrush);
    }

    // 2. Desenhar Borda se largura > 0
    if (box->style.borderWidth > 0) {
        HPEN hPen = CreatePen(PS_SOLID, box->style.borderWidth, box->style.borderColor);
        HPEN oldPen = (HPEN)SelectObject(hdc, hPen);
        
        // Posição da borda ao redor do padding box
        int px = box->x - box->style.padding;
        int py = box->y - box->style.padding;
        int pw = box->width + box->style.padding * 2;
        int ph = box->height + box->style.padding * 2;
        
        MoveToEx(hdc, px, py, nullptr);
        LineTo(hdc, px + pw, py);
        LineTo(hdc, px + pw, py + ph);
        LineTo(hdc, px, py + ph);
        LineTo(hdc, px, py);

        SelectObject(hdc, oldPen);
        DeleteObject(hPen);
    }

    // 3. Desenhar Texto se for nó de texto
    if (box->node && box->node->type == TEXT_NODE) {
        SetTextColor(hdc, box->style.textColor);
        SetBkMode(hdc, TRANSPARENT);
        
        // Criar uma fonte limpa sans-serif nativa
        HFONT hFont = CreateFontA(18, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, 
                                  DEFAULT_CHARSET, OUT_OUTLINE_PRECIS, CLIP_DEFAULT_PRECIS, 
                                  CLEARTYPE_QUALITY, VARIABLE_PITCH | FF_SWISS, "Segoe UI");
        HFONT oldFont = (HFONT)SelectObject(hdc, hFont);

        RECT rect = { box->x, box->y, box->x + box->width, box->y + box->height };
        // Converte string para ANSI para desenhar
        DrawTextA(hdc, box->node->textContent.c_str(), -1, &rect, DT_LEFT | DT_WORDBREAK | DT_NOCLIP);

        SelectObject(hdc, oldFont);
        DeleteObject(hFont);
    }

    // 4. Desenha filhos recursivamente
    for (LayoutBox* child : box->children) {
        paintLayoutBox(hdc, child);
    }
}

// ============================================================================
// 5. Configuração da Janela Principal do Win32
// ============================================================================
LayoutBox* g_LayoutRoot = nullptr;

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_PAINT: {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hwnd, &ps);

            // Pintar fundo cinza escuro do navegador (Área de exibição)
            HBRUSH bgBrush = CreateSolidBrush(RGB(15, 23, 42));
            FillRect(hdc, &ps.rcPaint, bgBrush);
            DeleteObject(bgBrush);

            // Executa a pintura recursiva nativa baseada na nossa árvore de layout
            if (g_LayoutRoot) {
                paintLayoutBox(hdc, g_LayoutRoot);
            }

            EndPaint(hwnd, &ps);
            break;
        }
        case WM_SIZE: {
            // Ao redimensionar a janela, recalcula o Layout em tempo real
            int width = LOWORD(lParam);
            if (g_LayoutRoot) {
                int stackY = 0;
                // Executa o motor geométrico passando a nova largura
                g_LayoutRoot->calculateLayout(20, 20, width - 40, stackY);
                InvalidateRect(hwnd, nullptr, TRUE);
            }
            break;
        }
        case WM_DESTROY:
            PostQuitMessage(0);
            break;
        default:
            return DefWindowProc(hwnd, msg, wParam, lParam);
    }
    return 0;
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow) {
    // 1. Exemplo de código HTML/CSS inline a ser parseado pelo nosso motor
    std::string htmlCode = 
        "<div style=\"background: dark; padding: 25; margin: 10; border-width: 2; border-color: blue;\">"
        "  <div style=\"color: white; margin: 5;\">Ola de Uranium Native Browser! ⚛</div>"
        "  <div style=\"color: grey; margin: 5;\">Este navegador e 100 por cento escrito em C++ nativo e GDI.</div>"
        "  <div style=\"background: red; padding: 15; margin: 15; color: white;\">"
        "    Esta caixa vermelha tem padding 15px e margem 15px."
        "  </div>"
        "  <div style=\"background: yellow; padding: 15; margin: 15; color: dark;\">"
        "    Nosso parser HTML leu este arquivo e o motor de Layout Win32 calculou a geometria!"
        "  </div>"
        "</div>";

    std::cout << "Iniciando Uranium C++ Native Rendering Engine..." << std::endl;

    // 2. Executar o HTML Parser nativo em C++
    HTMLParser parser(htmlCode);
    Node* domTree = parser.parse();

    // 3. Gerar árvore de Layout nativa
    g_LayoutRoot = buildLayoutTree(domTree);

    // 4. Executar cálculo de Layout inicial (resolvendo tamanhos e coordenadas)
    int initialStackY = 0;
    g_LayoutRoot->calculateLayout(20, 20, 760, initialStackY);

    // 5. Registrar classe de janela do Windows
    WNDCLASSEX wc = {0};
    wc.cbSize = sizeof(WNDCLASSEX);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = "UraniumBrowserClass";
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);

    if (!RegisterClassEx(&wc)) {
        MessageBox(nullptr, "Erro ao registrar classe de janela!", "Erro", MB_OK | MB_ICONERROR);
        return 1;
    }

    // 6. Criar janela nativa
    HWND hwnd = CreateWindowEx(
        0, "UraniumBrowserClass", "Uranium Native C++ Browser Engine (GDI)",
        WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 800, 600,
        nullptr, nullptr, hInstance, nullptr
    );

    if (!hwnd) {
        MessageBox(nullptr, "Erro ao criar janela!", "Erro", MB_OK | MB_ICONERROR);
        return 1;
    }

    ShowWindow(hwnd, nCmdShow);
    UpdateWindow(hwnd);

    // Loop de mensagens do Windows
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    // Cleanup
    delete g_LayoutRoot;
    delete domTree;

    return (int)msg.wParam;
}
