import type { Rule, Selector, Declaration, Value, ColorValue } from './css';

export class CSSParser {
  private pos = 0;
  private input: string;

  constructor(input: string) {
    this.input = input;
  }

  public parse(): Rule[] {
    const rules: Rule[] = [];
    this.consumeWhitespace();
    while (!this.eof()) {
      if (this.startsWith('/*')) {
        this.consumeComment();
        continue;
      }
      rules.push(this.parseRule());
      this.consumeWhitespace();
    }
    return rules;
  }

  private nextChar(): string {
    return this.input.charAt(this.pos);
  }

  private startsWith(str: string): boolean {
    return this.input.slice(this.pos, this.pos + str.length) === str;
  }

  private eof(): boolean {
    return this.pos >= this.input.length;
  }

  private consumeChar(): string {
    const char = this.nextChar();
    this.pos++;
    return char;
  }

  private consumeWhile(test: (char: string) => boolean): string {
    let result = '';
    while (!this.eof() && test(this.nextChar())) {
      result += this.consumeChar();
    }
    return result;
  }

  private consumeWhitespace(): void {
    this.consumeWhile((c) => /\s/.test(c));
  }

  private consumeComment(): void {
    this.pos += 2; // consume '/*'
    while (!this.eof() && !this.startsWith('*/')) {
      this.pos++;
    }
    if (!this.eof()) {
      this.pos += 2; // consume '*/'
    }
    this.consumeWhitespace();
  }

  private parseRule(): Rule {
    const selectors = this.parseSelectors();
    this.consumeWhitespace();
    const declarations = this.parseDeclarations();
    return { selectors, declarations };
  }

  private parseSelectors(): Selector[] {
    const selectors: Selector[] = [];
    while (true) {
      selectors.push(this.parseSelector());
      this.consumeWhitespace();
      const char = this.nextChar();
      if (char === ',') {
        this.consumeChar(); // consume ','
        this.consumeWhitespace();
      } else if (char === '{') {
        break;
      } else if (this.eof()) {
        break;
      } else {
        // Fallback for malformed CSS
        this.consumeChar();
      }
    }
    return selectors;
  }

  private parseSelector(): Selector {
    const selector: Selector = {};
    const char = this.nextChar();
    if (char === '#') {
      this.consumeChar(); // consume '#'
      selector.id = this.parseIdentifier();
    } else if (char === '.') {
      this.consumeChar(); // consume '.'
      selector.className = this.parseIdentifier();
    } else if (/[a-zA-Z0-9-*]/.test(char)) {
      const name = this.parseIdentifier();
      if (name !== '*') {
        selector.tagName = name;
      }
    }
    return selector;
  }

  private parseIdentifier(): string {
    return this.consumeWhile((c) => /[a-zA-Z0-9-_*]/.test(c));
  }

  private parseDeclarations(): Declaration[] {
    // Expect '{'
    if (this.nextChar() === '{') {
      this.consumeChar();
    }
    this.consumeWhitespace();

    const declarations: Declaration[] = [];
    while (true) {
      if (this.nextChar() === '}' || this.eof()) {
        break;
      }
      if (this.startsWith('/*')) {
        this.consumeComment();
        continue;
      }
      declarations.push(this.parseDeclaration());
      this.consumeWhitespace();
    }

    // Expect '}'
    if (this.nextChar() === '}') {
      this.consumeChar();
    }
    return declarations;
  }

  private parseDeclaration(): Declaration {
    const name = this.consumeWhile((c) => /[a-zA-Z-]/.test(c)).toLowerCase();
    this.consumeWhitespace();
    // Expect ':'
    if (this.nextChar() === ':') {
      this.consumeChar();
    }
    this.consumeWhitespace();
    const value = this.parseValue();
    this.consumeWhitespace();
    // Expect ';'
    if (this.nextChar() === ';') {
      this.consumeChar();
    }
    return { name, value };
  }

  private parseValue(): Value {
    const char = this.nextChar();
    
    // Parse Color hex
    if (char === '#') {
      this.consumeChar(); // consume '#'
      const hex = this.consumeWhile((c) => /[a-fA-F0-9]/.test(c));
      return this.parseHexColor(hex);
    }

    // Parse rgb/rgba
    if (this.startsWith('rgb')) {
      return this.parseRgbColor();
    }

    // Parse Length or Keyword
    const raw = this.consumeWhile((c) => /[a-zA-Z0-9-.,%]/.test(c));
    
    // Check if Length (e.g. 20px)
    if (raw.endsWith('px')) {
      const numStr = raw.slice(0, -2);
      const val = parseFloat(numStr);
      if (!isNaN(val)) {
        return { type: 'length', value: val, unit: 'px' };
      }
    }
    
    // Otherwise it is a keyword (like block, inline, red, etc.)
    // Check if it's a named color
    const namedColor = this.parseNamedColor(raw);
    if (namedColor) {
      return namedColor;
    }

    return { type: 'keyword', value: raw };
  }

  private parseHexColor(hex: string): ColorValue {
    let r = 0, g = 0, b = 0, a = 1;
    if (hex.length === 3) {
      r = parseInt(hex[0] + hex[0], 16);
      g = parseInt(hex[1] + hex[1], 16);
      b = parseInt(hex[2] + hex[2], 16);
    } else if (hex.length === 6) {
      r = parseInt(hex.slice(0, 2), 16);
      g = parseInt(hex.slice(2, 4), 16);
      b = parseInt(hex.slice(4, 6), 16);
    } else if (hex.length === 8) {
      r = parseInt(hex.slice(0, 2), 16);
      g = parseInt(hex.slice(2, 4), 16);
      b = parseInt(hex.slice(4, 6), 16);
      a = parseInt(hex.slice(6, 8), 16) / 255;
    }
    return { type: 'color', r, g, b, a };
  }

  private parseRgbColor(): ColorValue {
    const isRgba = this.startsWith('rgba');
    this.consumeWhile((c) => c !== '(');
    this.consumeChar(); // consume '('
    this.consumeWhitespace();

    const r = parseInt(this.consumeWhile((c) => /[0-9]/.test(c)), 10);
    this.consumeChar(); // consume ',' or whitespace
    this.consumeWhitespace();

    const g = parseInt(this.consumeWhile((c) => /[0-9]/.test(c)), 10);
    this.consumeChar(); // consume ',' or whitespace
    this.consumeWhitespace();

    const b = parseInt(this.consumeWhile((c) => /[0-9]/.test(c)), 10);
    this.consumeWhitespace();

    let a = 1;
    if (isRgba && this.nextChar() === ',') {
      this.consumeChar(); // consume ','
      this.consumeWhitespace();
      a = parseFloat(this.consumeWhile((c) => /[0-9.]/.test(c)));
    }
    
    // consume until ')'
    this.consumeWhile((c) => c !== ')');
    this.consumeChar(); // consume ')'

    return { type: 'color', r, g, b, a };
  }

  private parseNamedColor(name: string): ColorValue | null {
    const colors: Record<string, [number, number, number]> = {
      black: [0, 0, 0],
      white: [255, 255, 255],
      red: [255, 0, 0],
      green: [0, 128, 0],
      blue: [0, 0, 255],
      yellow: [255, 255, 0],
      orange: [255, 165, 0],
      purple: [128, 0, 128],
      pink: [255, 192, 203],
      gray: [128, 128, 128],
      grey: [128, 128, 128],
      brown: [165, 42, 42],
      cyan: [0, 255, 255],
      magenta: [255, 0, 255],
      silver: [192, 192, 192],
      gold: [255, 215, 0],
      navy: [0, 0, 128],
      teal: [0, 128, 128],
      olive: [128, 128, 0],
      maroon: [128, 0, 0],
      coral: [255, 127, 80],
      transparent: [0, 0, 0] // Handle separately for alpha
    };

    const color = colors[name.toLowerCase()];
    if (color) {
      return {
        type: 'color',
        r: color[0],
        g: color[1],
        b: color[2],
        a: name.toLowerCase() === 'transparent' ? 0 : 1
      };
    }
    return null;
  }
}

export function parseCSS(css: string): Rule[] {
  const parser = new CSSParser(css);
  return parser.parse();
}
