import { LayoutBox } from './layout';
import type { ColorValue, Value } from './css';

function getColor(val: Value | undefined): ColorValue | null {
  if (val && val.type === 'color') {
    return val;
  }
  return null;
}

function colorToString(color: ColorValue): string {
  return `rgba(${color.r}, ${color.g}, ${color.b}, ${color.a})`;
}

function getKeyword(val: Value | undefined): string | null {
  if (val && val.type === 'keyword') {
    return val.value;
  }
  return null;
}

function getLength(val: Value | undefined): number | null {
  if (val && val.type === 'length') {
    return val.value;
  }
  return null;
}

// Zero-allocation direct painting onto canvas context, returns total draw operations count
export function paintLayoutBox(ctx: CanvasRenderingContext2D, box: LayoutBox, stats: { drawCalls: number }) {
  const style = box.styledNode.specifiedValues || {};

  // 1. Paint Background
  const bgColor = getColor(style['background-color']);
  if (bgColor && bgColor.a > 0) {
    ctx.fillStyle = colorToString(bgColor);
    ctx.fillRect(box.borderBoxX(), box.borderBoxY(), box.borderBoxWidth(), box.borderBoxHeight());
    stats.drawCalls++;
  }

  // 2. Paint Border
  const borderLeft = box.borderLeft;
  const borderRight = box.borderRight;
  const borderTop = box.borderTop;
  const borderBottom = box.borderBottom;

  const borderColorVal = getColor(style['border-color']);
  const borderColorStr = borderColorVal ? colorToString(borderColorVal) : 'black';

  if (borderLeft > 0 || borderRight > 0 || borderTop > 0 || borderBottom > 0) {
    const bx = box.borderBoxX();
    const by = box.borderBoxY();
    const bw = box.borderBoxWidth();
    const bh = box.borderBoxHeight();
    
    ctx.fillStyle = borderColorStr;

    if (borderTop > 0) {
      ctx.fillRect(bx, by, bw, borderTop);
      stats.drawCalls++;
    }
    if (borderBottom > 0) {
      ctx.fillRect(bx, by + bh - borderBottom, bw, borderBottom);
      stats.drawCalls++;
    }
    if (borderLeft > 0) {
      ctx.fillRect(bx, by, borderLeft, bh);
      stats.drawCalls++;
    }
    if (borderRight > 0) {
      ctx.fillRect(bx + bw - borderRight, by, borderRight, bh);
      stats.drawCalls++;
    }
  }

  // 3. Paint Text
  if (box.boxType === 'text' && box.textWord) {
    const colorVal = getColor(style['color']) || { type: 'color', r: 0, g: 0, b: 0, a: 1 };
    const fontSize = getLength(style['font-size']) ?? 16;
    const fontWeight = getKeyword(style['font-weight']) ?? 'normal';
    const fontFamily = getKeyword(style['font-family']) ?? 'sans-serif';

    ctx.fillStyle = colorToString(colorVal);
    ctx.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
    ctx.fillText(box.textWord, box.x, box.y + box.height * 0.8);
    stats.drawCalls++;
  }

  // 4. Paint children recursively
  for (const child of box.children) {
    paintLayoutBox(ctx, child, stats);
  }
}
