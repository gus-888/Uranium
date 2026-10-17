export interface Rule {
  selectors: Selector[];
  declarations: Declaration[];
}

export type Selector = SimpleSelector;

export interface SimpleSelector {
  tagName?: string;
  id?: string;
  className?: string;
}

export interface Declaration {
  name: string;
  value: Value;
}

export type Value = KeywordValue | LengthValue | ColorValue;

export interface KeywordValue {
  type: 'keyword';
  value: string;
}

export interface LengthValue {
  type: 'length';
  value: number;
  unit: 'px';
}

export interface ColorValue {
  type: 'color';
  r: number;
  g: number;
  b: number;
  a: number; // 0 to 1
}

export type Specificity = [number, number, number];

export function getSpecificity(selector: Selector): Specificity {
  let a = selector.id ? 1 : 0;
  let b = selector.className ? 1 : 0;
  let c = selector.tagName ? 1 : 0;
  return [a, b, c];
}

// Helper to compare specificities: returns > 0 if s1 > s2, < 0 if s1 < s2, 0 if equal
export function compareSpecificity(s1: Specificity, s2: Specificity): number {
  if (s1[0] !== s2[0]) return s1[0] - s2[0];
  if (s1[1] !== s2[1]) return s1[1] - s2[1];
  return s1[2] - s2[2];
}
