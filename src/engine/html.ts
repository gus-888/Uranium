import { createElement, createText } from './dom';
import type { Node } from './dom';

export class HTMLParser {
  private pos = 0;
  private input: string;

  constructor(input: string) {
    this.input = input;
  }

  public parse(): Node {
    this.consumeWhitespace();
    const nodes = this.parseNodes();
    if (nodes.length === 1) {
      return nodes[0];
    } else {
      return createElement('html', {}, nodes);
    }
  }

  private nextChar(): string {
    return this.input.charAt(this.pos);
  }

  private startsWith(str: string): boolean {
    const len = str.length;
    if (this.pos + len > this.input.length) return false;
    for (let i = 0; i < len; i++) {
      if (this.input.charCodeAt(this.pos + i) !== str.charCodeAt(i)) {
        return false;
      }
    }
    return true;
  }

  private startsWithIgnoreCase(str: string, offset: number): boolean {
    const len = str.length;
    if (this.pos + offset + len > this.input.length) return false;
    for (let i = 0; i < len; i++) {
      let charCode1 = this.input.charCodeAt(this.pos + offset + i);
      let charCode2 = str.charCodeAt(i);
      
      // Convert to lowercase
      if (charCode1 >= 65 && charCode1 <= 90) charCode1 += 32;
      if (charCode2 >= 65 && charCode2 <= 90) charCode2 += 32;
      
      if (charCode1 !== charCode2) {
        return false;
      }
    }
    return true;
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
    // Avoid full regex test where possible
    while (!this.eof()) {
      const charCode = this.input.charCodeAt(this.pos);
      // check space, tab, newline, carriage return
      if (charCode === 32 || charCode === 9 || charCode === 10 || charCode === 13) {
        this.pos++;
      } else {
        break;
      }
    }
  }

  private parseTagName(): string {
    return this.consumeWhile((c) => /[a-zA-Z0-9-]/.test(c));
  }

  private parseNode(): Node {
    if (this.startsWith('<!--')) {
      this.parseComment();
      return this.parseNode();
    }
    if (this.startsWith('<')) {
      return this.parseElement();
    }
    return this.parseText();
  }

  private parseComment(): void {
    this.pos += 4; // consume '<!--'
    while (!this.eof() && !this.startsWith('-->')) {
      this.pos++;
    }
    if (!this.eof()) {
      this.pos += 3; // consume '-->'
    }
    this.consumeWhitespace();
  }

  private parseElement(): Node {
    this.consumeChar(); // consume '<'
    
    const tagName = this.parseTagName();
    const attrs = this.parseAttributes();
    
    let isSelfClosing = false;
    if (this.startsWith('/>')) {
      this.pos += 2;
      isSelfClosing = true;
    } else if (this.startsWith('>')) {
      this.consumeChar();
    }

    const voidTags = ['img', 'br', 'hr', 'input', 'meta', 'link'];
    if (voidTags.includes(tagName.toLowerCase())) {
      isSelfClosing = true;
    }

    this.consumeWhitespace();

    if (isSelfClosing) {
      return createElement(tagName, attrs, []);
    }

    const children = this.parseNodes(tagName);

    if (this.startsWith('</')) {
      this.pos += 2; // consume '</'
      const closeTagName = this.parseTagName();
      this.consumeWhitespace();
      if (this.startsWith('>')) {
        this.consumeChar();
      }
      if (closeTagName.toLowerCase() !== tagName.toLowerCase()) {
        console.warn(`Tag mismatch: opened <${tagName}> but closed </${closeTagName}>`);
      }
    }

    return createElement(tagName, attrs, children);
  }

  private parseAttributes(): Record<string, string> {
    const attrs: Record<string, string> = {};
    while (true) {
      this.consumeWhitespace();
      if (this.startsWith('>') || this.startsWith('/>') || this.eof()) {
        break;
      }
      const [name, value] = this.parseAttr();
      attrs[name] = value;
    }
    return attrs;
  }

  private parseAttr(): [string, string] {
    const name = this.consumeWhile((c) => /[a-zA-Z0-9-]/.test(c));
    let value = '';
    this.consumeWhitespace();
    if (this.startsWith('=')) {
      this.consumeChar(); // consume '='
      this.consumeWhitespace();
      value = this.parseAttrValue();
    }
    return [name.toLowerCase(), value];
  }

  private parseAttrValue(): string {
    const openQuote = this.nextChar();
    if (openQuote === '"' || openQuote === "'") {
      this.consumeChar(); // consume quote
      const value = this.consumeWhile((c) => c !== openQuote);
      this.consumeChar(); // consume end quote
      return value;
    } else {
      return this.consumeWhile((c) => !/\s|>|\/>/.test(c));
    }
  }

  private parseText(): Node {
    const text = this.consumeWhile((c) => c !== '<');
    const decodedText = text
      .replace(/&nbsp;/g, ' ')
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&amp;/g, '&');
    return createText(decodedText);
  }

  private parseNodes(parentTagName?: string): Node[] {
    const nodes: Node[] = [];
    while (true) {
      this.consumeWhitespace();
      if (this.eof()) {
        break;
      }
      if (parentTagName && this.startsWith(`</`)) {
        if (this.startsWithIgnoreCase(parentTagName, 2)) {
          break;
        }
      }
      const node = this.parseNode();
      if (node.type === 'text') {
        const textNode = node;
        if (textNode.text.trim() === '') {
          continue;
        }
      }
      nodes.push(node);
    }
    return nodes;
  }
}

export function parseHTML(html: string): Node {
  const parser = new HTMLParser(html);
  return parser.parse();
}
