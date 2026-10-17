import type { Node, ElementNode } from './dom';
import { compareSpecificity, getSpecificity } from './css';
import type { Rule, Selector, Value, Specificity } from './css';
import { CSSParser } from './cssParser';

// Check if a selector matches an element
function matchesSelector(elem: ElementNode, selector: Selector): boolean {
  if (selector.tagName && elem.tagName !== selector.tagName) {
    return false;
  }
  if (selector.id && elem.attributes['id'] !== selector.id) {
    return false;
  }
  if (selector.className) {
    const classes = elem.attributes['class'] ? elem.attributes['class'].split(/\s+/) : [];
    if (!classes.includes(selector.className)) {
      return false;
    }
  }
  return true;
}

// Represents a matched rule with its highest specificity selector
interface MatchedRule {
  specificity: Specificity;
  rule: Rule;
}

// Find all rules matching a DOM element
function matchRules(elem: ElementNode, rules: Rule[]): MatchedRule[] {
  const matched: MatchedRule[] = [];
  for (const rule of rules) {
    let bestSpecificity: Specificity | null = null;
    for (const selector of rule.selectors) {
      if (matchesSelector(elem, selector)) {
        const spec = getSpecificity(selector);
        if (!bestSpecificity || compareSpecificity(spec, bestSpecificity) > 0) {
          bestSpecificity = spec;
        }
      }
    }
    if (bestSpecificity) {
      matched.push({ specificity: bestSpecificity, rule });
    }
  }
  return matched;
}

// Get all computed styles for a DOM node, taking specificity and inline styles into account
function specifiedValues(elem: ElementNode, rules: Rule[], parent?: Node): Record<string, Value> {
  const values: Record<string, Value> = {};
  
  // 1. Gather all matching rules and sort by specificity
  const matches = matchRules(elem, rules);
  matches.sort((a, b) => compareSpecificity(a.specificity, b.specificity));
  
  // Apply declarations from lowest to highest specificity
  for (const match of matches) {
    for (const declaration of match.rule.declarations) {
      values[declaration.name] = declaration.value;
    }
  }

  // 2. Parse inline styles (e.g. style="color: red;")
  const inlineStyleAttr = elem.attributes['style'];
  if (inlineStyleAttr) {
    try {
      const inlineParser = new CSSParser(`dummy { ${inlineStyleAttr} }`);
      const parsedRules = inlineParser.parse();
      if (parsedRules.length > 0) {
        for (const declaration of parsedRules[0].declarations) {
          values[declaration.name] = declaration.value;
        }
      }
    } catch (e) {
      console.warn('Error parsing inline style:', inlineStyleAttr, e);
    }
  }

  // 3. Inheritance of properties
  const inheritableProperties = [
    'color',
    'font-size',
    'font-weight',
    'font-family',
    'text-align',
    'line-height'
  ];

  if (parent && parent.specifiedValues) {
    for (const prop of inheritableProperties) {
      if (values[prop] === undefined && parent.specifiedValues[prop] !== undefined) {
        values[prop] = parent.specifiedValues[prop];
      }
    }
  }

  // Set defaults for display
  if (values['display'] === undefined) {
    values['display'] = { type: 'keyword', value: getDisplayDefault(elem.tagName) };
  }

  return values;
}

function getDisplayDefault(tagName: string): string {
  const inlineTags = ['span', 'em', 'strong', 'a', 'br', 'b', 'i', 'img', 'input', 'label'];
  if (inlineTags.includes(tagName.toLowerCase())) {
    return 'inline';
  }
  return 'block';
}

// Mutates the DOM tree in-place, adding specifiedValues to DOM Nodes
export function buildStyleTree(rootNode: Node, rules: Rule[], parent?: Node): Node {
  const values = rootNode.type === 'element' 
    ? specifiedValues(rootNode as ElementNode, rules, parent) 
    : (parent && parent.specifiedValues ? { ...parent.specifiedValues } : {}); // Text nodes inherit parent values

  if (rootNode.type === 'text') {
    values['display'] = { type: 'keyword', value: 'inline' };
  }

  rootNode.specifiedValues = values;

  // Process children recursively
  for (const child of rootNode.children) {
    buildStyleTree(child, rules, rootNode);
  }

  return rootNode;
}
