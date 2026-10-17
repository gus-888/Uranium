import type { Value } from './css';

export type NodeType = 'text' | 'element';

export interface BaseNode {
  type: NodeType;
  children: Node[];
  specifiedValues?: Record<string, Value>; // Styles attached directly to DOM nodes to save RAM
}

export interface ElementNode extends BaseNode {
  type: 'element';
  tagName: string;
  attributes: Record<string, string>;
}

export interface TextNode extends BaseNode {
  type: 'text';
  text: string;
  children: never[]; // Text nodes do not have children
}

export type Node = ElementNode | TextNode;

export function createElement(tagName: string, attributes: Record<string, string> = {}, children: Node[] = []): ElementNode {
  return {
    type: 'element',
    tagName: tagName.toLowerCase(),
    attributes,
    children
  };
}

export function createText(text: string): TextNode {
  return {
    type: 'text',
    text,
    children: []
  };
}
