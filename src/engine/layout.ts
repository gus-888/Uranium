import type { Node } from './dom';
import type { Value } from './css';

export type BoxType = 'block' | 'inline' | 'anonymous' | 'text';

let offscreenCtx: CanvasRenderingContext2D | null = null;
function measureTextWidth(text: string, fontSpec: string): number {
  if (typeof window === 'undefined') return text.length * 8;
  if (!offscreenCtx) {
    const canvas = document.createElement('canvas');
    offscreenCtx = canvas.getContext('2d');
  }
  if (offscreenCtx) {
    offscreenCtx.font = fontSpec;
    return offscreenCtx.measureText(text).width;
  }
  return text.length * 8;
}

function getLengthValue(val: Value | undefined): number | null {
  if (val && val.type === 'length') {
    return val.value;
  }
  return null;
}

function getKeywordValue(val: Value | undefined): string | null {
  if (val && val.type === 'keyword') {
    return val.value;
  }
  return null;
}

export class LayoutBox {
  boxType: BoxType;
  styledNode: Node;
  children: LayoutBox[] = [];
  textWord?: string;

  // Flattened Dimensions properties directly on LayoutBox class to save RAM
  x = 0;
  y = 0;
  width = 0;
  height = 0;

  paddingTop = 0;
  paddingRight = 0;
  paddingBottom = 0;
  paddingLeft = 0;

  borderTop = 0;
  borderRight = 0;
  borderBottom = 0;
  borderLeft = 0;

  marginTop = 0;
  marginRight = 0;
  marginBottom = 0;
  marginLeft = 0;

  constructor(boxType: BoxType, styledNode: Node, textWord?: string) {
    this.boxType = boxType;
    this.styledNode = styledNode;
    this.textWord = textWord;
  }

  // Padding box relative boundaries
  paddingBoxWidth(): number { return this.width + this.paddingLeft + this.paddingRight; }
  paddingBoxHeight(): number { return this.height + this.paddingTop + this.paddingBottom; }
  paddingBoxX(): number { return this.x - this.paddingLeft; }
  paddingBoxY(): number { return this.y - this.paddingTop; }

  // Border box relative boundaries
  borderBoxWidth(): number { return this.paddingBoxWidth() + this.borderLeft + this.borderRight; }
  borderBoxHeight(): number { return this.paddingBoxHeight() + this.borderTop + this.borderBottom; }
  borderBoxX(): number { return this.paddingBoxX() - this.borderLeft; }
  borderBoxY(): number { return this.paddingBoxY() - this.borderTop; }

  // Margin box relative boundaries
  marginBoxWidth(): number { return this.borderBoxWidth() + this.marginLeft + this.marginRight; }
  marginBoxHeight(): number { return this.borderBoxHeight() + this.marginTop + this.marginBottom; }
  marginBoxX(): number { return this.borderBoxX() - this.marginLeft; }
  marginBoxY(): number { return this.borderBoxY() - this.marginTop; }

  public layout(containingX: number, containingY: number, containingWidth: number, stackHeight: number) {
    if (this.boxType === 'block' || this.boxType === 'anonymous') {
      this.layoutBlock(containingX, containingY, containingWidth, stackHeight);
    }
  }

  private layoutBlock(containingX: number, containingY: number, containingWidth: number, stackHeight: number) {
    // 1. Calculate width of this block
    this.calculateWidth(containingWidth);

    // 2. Position it relative to the parent block
    this.calculatePosition(containingX, containingY, stackHeight);

    // 3. Layout its children
    this.layoutChildren();

    // 4. Calculate final height
    this.calculateHeight();
  }

  private calculateWidth(containingWidth: number) {
    const style = this.styledNode.specifiedValues || {};

    // Load padding, border and margin values
    this.paddingLeft = getLengthValue(style['padding-left']) ?? getLengthValue(style['padding']) ?? 0;
    this.paddingRight = getLengthValue(style['padding-right']) ?? getLengthValue(style['padding']) ?? 0;
    this.paddingTop = getLengthValue(style['padding-top']) ?? getLengthValue(style['padding']) ?? 0;
    this.paddingBottom = getLengthValue(style['padding-bottom']) ?? getLengthValue(style['padding']) ?? 0;

    this.borderLeft = getLengthValue(style['border-left-width']) ?? getLengthValue(style['border-width']) ?? 0;
    this.borderRight = getLengthValue(style['border-right-width']) ?? getLengthValue(style['border-width']) ?? 0;
    this.borderTop = getLengthValue(style['border-top-width']) ?? getLengthValue(style['border-width']) ?? 0;
    this.borderBottom = getLengthValue(style['border-bottom-width']) ?? getLengthValue(style['border-width']) ?? 0;

    this.marginLeft = getLengthValue(style['margin-left']) ?? getLengthValue(style['margin']) ?? 0;
    this.marginRight = getLengthValue(style['margin-right']) ?? getLengthValue(style['margin']) ?? 0;
    this.marginTop = getLengthValue(style['margin-top']) ?? getLengthValue(style['margin']) ?? 0;
    this.marginBottom = getLengthValue(style['margin-bottom']) ?? getLengthValue(style['margin']) ?? 0;

    // Resolve width property
    const widthVal = style['width'];
    if (widthVal && widthVal.type === 'length') {
      this.width = widthVal.value;
    } else {
      // Auto width: fill remaining width in containing block
      const totalSpacing =
        this.marginLeft + this.marginRight +
        this.borderLeft + this.borderRight +
        this.paddingLeft + this.paddingRight;
      this.width = Math.max(0, containingWidth - totalSpacing);
    }
  }

  private calculatePosition(containingX: number, containingY: number, stackHeight: number) {
    // Horizontal alignment
    this.x = containingX + this.marginLeft + this.borderLeft + this.paddingLeft;

    // Vertical stacking: stack on top of previously accumulated heights in this block
    this.y = containingY + stackHeight + this.marginTop + this.borderTop + this.paddingTop;
  }

  private layoutChildren() {
    const hasBlockChildren = this.children.some(
      (c) => c.boxType === 'block' || c.boxType === 'anonymous'
    );

    if (hasBlockChildren) {
      // Vertical block layout
      this.height = 0;
      let stackHeight = 0;
      for (const child of this.children) {
        child.layout(this.x, this.y, this.width, stackHeight);
        stackHeight += child.marginBoxHeight();
      }
      this.height = stackHeight;
    } else {
      // Inline wrapping layout
      this.layoutInlineChildren();
    }
  }

  private layoutInlineChildren() {
    const inlineItems: LayoutBox[] = [];
    
    const collectInlines = (box: LayoutBox) => {
      if (box.boxType === 'text') {
        const text = box.textWord || '';
        const words = text.split(/(\s+)/g).filter(w => w !== '');
        
        for (const word of words) {
          const wordBox = new LayoutBox('text', box.styledNode, word);
          
          const style = box.styledNode.specifiedValues || {};
          const fontSize = getLengthValue(style['font-size']) ?? 16;
          const fontWeight = getKeywordValue(style['font-weight']) ?? 'normal';
          const fontFamily = getKeywordValue(style['font-family']) ?? 'sans-serif';
          
          wordBox.width = measureTextWidth(word, `${fontWeight} ${fontSize}px ${fontFamily}`);
          wordBox.height = fontSize * 1.2; // Approximate line height
          inlineItems.push(wordBox);
        }
      } else if (box.boxType === 'inline') {
        const tagName = box.styledNode.type === 'element' ? box.styledNode.tagName : '';
        if (tagName === 'br') {
          inlineItems.push(box);
        } else {
          for (const child of box.children) {
            collectInlines(child);
          }
        }
      }
    };

    for (const child of this.children) {
      collectInlines(child);
    }

    const parentWidth = this.width;
    let cursorX = 0;
    let cursorY = 0;
    let currentLineHeight = 0;
    
    const positionedChildren: LayoutBox[] = [];
    let currentLineItems: LayoutBox[] = [];

    const flushLine = () => {
      for (const item of currentLineItems) {
        item.y = this.y + cursorY + (currentLineHeight - item.height);
        positionedChildren.push(item);
      }
      cursorY += currentLineHeight;
      cursorX = 0;
      currentLineHeight = 0;
      currentLineItems = [];
    };

    for (const item of inlineItems) {
      const tagName = item.styledNode.type === 'element' ? item.styledNode.tagName : '';
      
      if (tagName === 'br') {
        flushLine();
        continue;
      }

      if (cursorX + item.width > parentWidth && cursorX > 0) {
        flushLine();
      }

      item.x = this.x + cursorX;
      currentLineItems.push(item);
      cursorX += item.width;
      currentLineHeight = Math.max(currentLineHeight, item.height);
    }

    if (currentLineItems.length > 0) {
      flushLine();
    }

    this.children = positionedChildren;
    this.height = cursorY;
  }

  private calculateHeight() {
    const style = this.styledNode.specifiedValues || {};
    const heightVal = style['height'];
    if (heightVal && heightVal.type === 'length') {
      this.height = heightVal.value;
    }
  }
}

// Convert DOM tree (with computed style properties) directly to layout tree
export function buildLayoutTree(node: Node): LayoutBox | null {
  const display = getKeywordValue(node.specifiedValues?.['display']) || 'block';
  
  if (display === 'none') {
    return null;
  }

  let boxType: BoxType = 'block';
  if (display === 'inline') {
    boxType = node.type === 'text' ? 'text' : 'inline';
  }

  const box = new LayoutBox(
    boxType,
    node,
    node.type === 'text' ? node.text : undefined
  );

  for (const child of node.children) {
    const childBox = buildLayoutTree(child);
    if (childBox) {
      box.children.push(childBox);
    }
  }

  if (box.boxType === 'block' || box.boxType === 'anonymous') {
    box.children = groupInlines(box.children, node);
  }

  return box;
}

function groupInlines(children: LayoutBox[], parentNode: Node): LayoutBox[] {
  const hasBlock = children.some(c => c.boxType === 'block' || c.boxType === 'anonymous');
  if (!hasBlock) {
    return children;
  }

  const grouped: LayoutBox[] = [];
  let inlineGroup: LayoutBox[] = [];

  const flushGroup = () => {
    if (inlineGroup.length > 0) {
      // Create anonymous block with styles assigned directly
      const anonNode: Node = {
        type: 'element',
        tagName: 'anonymous',
        attributes: {},
        children: [],
        specifiedValues: {
          ...(parentNode.specifiedValues || {}),
          'display': { type: 'keyword', value: 'block' }
        }
      };
      
      const anonBox = new LayoutBox('anonymous', anonNode);
      anonBox.children = inlineGroup;
      grouped.push(anonBox);
      inlineGroup = [];
    }
  };

  for (const child of children) {
    if (child.boxType === 'block' || child.boxType === 'anonymous') {
      flushGroup();
      grouped.push(child);
    } else {
      inlineGroup.push(child);
    }
  }
  flushGroup();

  return grouped;
}
