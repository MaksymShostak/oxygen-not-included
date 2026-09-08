// SPDX-License-Identifier: AGPL-3.0-only
import {createToken, Lexer, CstParser} from 'chevrotain';
import {defaultSteamParseResourceLimits, resolveSteamParseResourceLimits} from '../security/resource-limits.js';
import {findSteamTagDefinition} from './construct-definition.js';

/** @typedef {import('../diagnostics/diagnostic-codes.js').SteamParseDiagnosticCode} SteamParseDiagnosticCode */
/** @typedef {{code: SteamParseDiagnosticCode, message: string, sourceSpan?: import('unist').Position}} SteamSyntaxDiagnostic */
/** @typedef {import('chevrotain').CstNode} NativeCstNode */
/** @typedef {import('chevrotain').IToken} NativeToken */
/** @typedef {Pick<NativeToken, 'startOffset' | 'endOffset' | 'startLine' | 'endLine' | 'startColumn' | 'endColumn'>} NativeSourceCoordinates */
/** @typedef {import('./steam-bbcode-syntax.js').SteamBbcodeSyntaxNode} SteamBbcodeSyntaxNode */

class SteamResourceLimitError extends Error {
  /** @param {SteamParseDiagnosticCode} code @param {string} message */
  constructor(code, message) {
    super(message);
    /** @type {SteamParseDiagnosticCode} */
    this.code = code;
  }
}

// RegExp patterns describe tokens for the maintained lexer. The recursive
// language grammar below belongs to Chevrotain's DSL, not a substitution pass.
const CodeStart = createToken({name: 'CodeStart', pattern: /\[code(?=[\s=\]])/i, push_mode: 'codeAttributes'});
const NoParseStart = createToken({name: 'NoParseStart', pattern: /\[noparse(?=[\s=\]])/i, push_mode: 'noParseAttributes'});
const HorizontalRuleStart = createToken({name: 'HorizontalRuleStart', pattern: /\[hr(?=[\s=\]])/i, push_mode: 'attributes'});
const TagStart = createToken({name: 'TagStart', pattern: /\[[a-z][a-z0-9]*/i, push_mode: 'attributes'});
const ClosingTag = createToken({name: 'ClosingTag', pattern: /\[\/[a-z][a-z0-9]*\s*\]/i, line_breaks: true});
const ItemBoundary = createToken({name: 'ItemBoundary', pattern: /\[\*\]/});
const LiteralText = createToken({name: 'LiteralText', pattern: /[^\[]+/, line_breaks: true});
const LiteralBracket = createToken({name: 'LiteralBracket', pattern: /\[/});
const HeaderEnd = createToken({name: 'HeaderEnd', pattern: /\]/, pop_mode: true});
const CodeHeaderEnd = createToken({name: 'CodeHeaderEnd', pattern: /\]/, categories: [HeaderEnd], pop_mode: true, push_mode: 'codeBody'});
const NoParseHeaderEnd = createToken({name: 'NoParseHeaderEnd', pattern: /\]/, categories: [HeaderEnd], pop_mode: true, push_mode: 'noParseBody'});
const AttributeSpace = createToken({name: 'AttributeSpace', pattern: /\s+/, line_breaks: true});
const QuotedAttribute = createToken({name: 'QuotedAttribute', pattern: /"[^"]*"|'[^']*'/, line_breaks: true});
const AttributeText = createToken({name: 'AttributeText', pattern: /[^\s\]="']+/});
const AttributeEquals = createToken({name: 'AttributeEquals', pattern: /=/});
const UnclosedAttributeQuote = createToken({name: 'UnclosedAttributeQuote', pattern: /["']/});
const CodeEnd = createToken({name: 'CodeEnd', pattern: /\[\/code\s*\]/i, line_breaks: true, pop_mode: true});
const CodeText = createToken({name: 'CodeText', pattern: /(?:[^\[]|\[(?!\/code\s*\]))+/i, line_breaks: true});
const NoParseEnd = createToken({name: 'NoParseEnd', pattern: /\[\/noparse\s*\]/i, line_breaks: true, pop_mode: true});
const NoParseText = createToken({name: 'NoParseText', pattern: /(?:[^\[]|\[(?!\/noparse\s*\]))+/i, line_breaks: true});
const attributeTokens = [AttributeSpace, QuotedAttribute, AttributeText, AttributeEquals, UnclosedAttributeQuote];
const modes = {
  text: [CodeStart, NoParseStart, HorizontalRuleStart, TagStart, ClosingTag, ItemBoundary, LiteralText, LiteralBracket],
  attributes: [...attributeTokens, HeaderEnd],
  codeAttributes: [...attributeTokens, CodeHeaderEnd],
  noParseAttributes: [...attributeTokens, NoParseHeaderEnd],
  codeBody: [CodeEnd, CodeText],
  noParseBody: [NoParseEnd, NoParseText],
};
const lexer = new Lexer({modes, defaultMode: 'text'}, {positionTracking: 'full'});
const vocabulary = [...new Set(Object.values(modes).flat())];

class SteamBbcodeGrammar extends CstParser {
  constructor() {
    super(vocabulary, {recoveryEnabled: false, nodeLocationTracking: 'full'});
    /** @type {Readonly<import('../security/resource-limits.js').SteamParseResourceLimits>} */
    this.limits = defaultSteamParseResourceLimits;
    /** @type {number} */
    this.depth = 0;
    /** @type {number} */
    this.nodeCount = 0;
    /** @type {Map<string, number>} Last closing-token offset for each name in this input. */
    this.lastClosingOffset = new Map();
    /** @type {() => import('chevrotain').CstNode} */
    this.document = this.RULE('document', () => {
      this.MANY(() => this.OR([
        {ALT: () => this.SUBRULE(this.element, {LABEL: 'entry'})},
        {ALT: () => {
          this.ACTION(() => this.countNode());
          this.CONSUME(ClosingTag, {LABEL: 'entry'});
        }},
      ]));
      // Chevrotain checks trailing input at the top-level rule. Consuming its
      // synthetic EOF would include the sentinel's -1 coordinates in this CST.
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.element = this.RULE('element', () => {
      this.ACTION(() => this.countNode());
      this.OR([
        {ALT: () => this.SUBRULE(this.tag)},
        {ALT: () => this.SUBRULE(this.code)},
        {ALT: () => this.SUBRULE(this.noParse)},
        {ALT: () => this.SUBRULE(this.horizontalRule)},
        {ALT: () => this.CONSUME(ItemBoundary)},
        {ALT: () => this.CONSUME(LiteralText)},
        {ALT: () => this.CONSUME(LiteralBracket)},
      ]);
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.tagHeaderTail = this.RULE('tagHeaderTail', () => {
      this.MANY(() => this.OR([
        {ALT: () => this.CONSUME(AttributeSpace)},
        {ALT: () => this.CONSUME(QuotedAttribute)},
        {ALT: () => this.CONSUME(AttributeText)},
        {ALT: () => this.CONSUME(AttributeEquals)},
        {ALT: () => this.CONSUME(UnclosedAttributeQuote)},
      ]));
      this.OPTION(() => this.CONSUME(HeaderEnd));
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.tag = this.RULE('tag', () => {
      this.ACTION(() => this.enterTag());
      const opening = this.CONSUME(TagStart);
      this.SUBRULE(this.tagHeaderTail);
      const ownsBody = this.ACTION(() => {
        const name = opening.image.slice(1).toLowerCase();
        return findSteamTagDefinition(name) !== undefined || (this.lastClosingOffset.get(name) ?? -1) > opening.startOffset;
      });
      // An unknown bracket label without a matching closing token is literal
      // source, not an owner of subsequent document content. The native grammar
      // still owns nesting for known tags and explicitly paired future tags.
      this.OPTION({GATE: () => ownsBody, DEF: () => {
        this.MANY(() => this.SUBRULE(this.element));
        // Optional closers preserve unfinished editor input. Structural
        // interpretation diagnoses absence/mismatched names before conversion.
        this.OPTION2(() => this.CONSUME(ClosingTag));
      }});
      this.ACTION(() => { this.depth -= 1; });
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.code = this.RULE('code', () => {
      this.ACTION(() => this.enterTag());
      this.CONSUME(CodeStart);
      this.SUBRULE(this.tagHeaderTail);
      this.OPTION(() => this.CONSUME(CodeText));
      this.OPTION2(() => this.CONSUME(CodeEnd));
      this.ACTION(() => { this.depth -= 1; });
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.noParse = this.RULE('noParse', () => {
      this.ACTION(() => this.enterTag());
      this.CONSUME(NoParseStart);
      this.SUBRULE(this.tagHeaderTail);
      this.OPTION(() => this.CONSUME(NoParseText));
      this.OPTION2(() => this.CONSUME(NoParseEnd));
      this.ACTION(() => { this.depth -= 1; });
    });
    /** @type {() => import('chevrotain').CstNode} */
    this.horizontalRule = this.RULE('horizontalRule', () => {
      this.ACTION(() => this.enterTag());
      this.CONSUME(HorizontalRuleStart);
      this.SUBRULE(this.tagHeaderTail);
      this.OPTION({GATE: () => this.LA(1).image.toLowerCase().trim() === '[/hr]', DEF: () => this.CONSUME(ClosingTag)});
      this.ACTION(() => { this.depth -= 1; });
    });
    this.performSelfAnalysis();
  }

  countNode() {
    this.nodeCount += 1;
    if (this.nodeCount > this.limits.maxNodeCount) throw new SteamResourceLimitError('STEAM_MAX_NODE_COUNT_EXCEEDED', 'Source element count exceeds maxNodeCount.');
  }

  enterTag() {
    this.depth += 1;
    if (this.depth > this.limits.maxNestingDepth) throw new SteamResourceLimitError('STEAM_MAX_NESTING_DEPTH_EXCEEDED', 'Construct nesting exceeds maxNestingDepth.');
  }
}

// Grammar analysis is performed once. The native input setter resets parser
// state; per-call counters are reset below. Parsing is synchronous and invokes
// no user callbacks. Returned CSTs are not reused or rewritten by later calls.
const parser = new SteamBbcodeGrammar();
const encoder = new TextEncoder();

class SteamNamedAttributeGrammar extends CstParser {
  constructor() {
    super(vocabulary, {recoveryEnabled: false, nodeLocationTracking: 'full'});
    /** @type {() => NativeCstNode} */
    this.namedAttributes = this.RULE('namedAttributes', () => {
      this.MANY_SEP({SEP: AttributeSpace, DEF: () => this.SUBRULE(this.assignment)});
    });
    /** @type {() => NativeCstNode} */
    this.assignment = this.RULE('assignment', () => {
      this.CONSUME(AttributeText, {LABEL: 'name'});
      this.CONSUME(AttributeEquals);
      this.OR([
        {ALT: () => this.CONSUME(QuotedAttribute, {LABEL: 'value'})},
        {ALT: () => this.AT_LEAST_ONE(() => this.OR2([
          {ALT: () => this.CONSUME2(AttributeText, {LABEL: 'value'})},
          {ALT: () => this.CONSUME2(AttributeEquals, {LABEL: 'value'})},
        ]))},
      ]);
    });
    this.performSelfAnalysis();
  }
}
const namedAttributeParser = new SteamNamedAttributeGrammar();

/**
 * Parse named attributes with the same maintained lexer and native grammar DSL.
 * Only called for headers already bounded by the document parser. Positional
 * attributes belong to their tag's scalar semantic contract. No vendor objects
 * leave this boundary, and invalid assignments do not become a partial map.
 *
 * @param {string} rawAttributes
 * @returns {readonly Readonly<{name: string, value: string}>[] | undefined}
 */
export function parseSteamNamedAttributes(rawAttributes) {
  const lexed = lexer.tokenize(rawAttributes.trim(), 'attributes');
  if (lexed.errors.length) return undefined;
  namedAttributeParser.input = lexed.tokens;
  const cst = namedAttributeParser.namedAttributes();
  if (namedAttributeParser.errors.length) return undefined;
  return Object.freeze((cst.children['assignment'] ?? []).map(entry => {
    if (!('children' in entry)) throw new Error('A native attribute assignment must be a CST node.');
    const name = firstToken(entry, 'name');
    const values = entry.children['value'] ?? [];
    if (!name) throw new Error('A native attribute assignment has no name.');
    const tokens = values.map(value => {
      if (!('image' in value)) throw new Error('A native attribute value must be a token.');
      return value;
    });
    const quoted = tokens[0]?.tokenType === QuotedAttribute;
    const value = tokens.map(token => token.image).join('');
    return Object.freeze({name: name.image, value: quoted ? value.slice(1, -1) : value});
  }));
}

/**
 * Native parser qualification boundary. Vendor CSTs stay inside this layer;
 * this function is not an exported package API.
 *
 * @param {string} source
 * @param {Partial<import('../security/resource-limits.js').SteamParseResourceLimits>} [resourceLimits]
 * @returns {{source: string, cst: NativeCstNode | undefined, tokens: readonly NativeToken[], children: readonly SteamBbcodeSyntaxNode[], diagnostics: readonly SteamSyntaxDiagnostic[]}}
 */
export function parseSteamBbcodeSyntax(source, resourceLimits = {}) {
  if (typeof source !== 'string') throw new TypeError('Steam BBCode source must be a string.');
  const limits = resolveSteamParseResourceLimits(resourceLimits);
  if (source.length > limits.maxInputBytes || encoder.encode(source).byteLength > limits.maxInputBytes) {
    return {source, cst: undefined, tokens: [], children: [], diagnostics: [{code: 'STEAM_MAX_INPUT_BYTES_EXCEEDED', message: 'UTF-8 input size exceeds maxInputBytes.'}]};
  }
  const lexed = lexer.tokenize(source);
  const attributeError = checkAttributeLimit(source, lexed.tokens, limits.maxAttributeBytes);
  if (attributeError) return {source, cst: undefined, tokens: lexed.tokens, children: [], diagnostics: [attributeError]};
  parser.input = lexed.tokens;
  parser.limits = limits;
  parser.depth = 0;
  parser.nodeCount = 0;
  parser.lastClosingOffset.clear();
  for (const token of lexed.tokens) {
    if (token.tokenType === ClosingTag) parser.lastClosingOffset.set(token.image.slice(2, -1).trim().toLowerCase(), token.startOffset);
  }
  /** @type {NativeCstNode | undefined} */
  let cst;
  try {
    cst = parser.document();
  } catch (error) {
    if (!(error instanceof SteamResourceLimitError)) throw error;
    return {source, cst: undefined, tokens: lexed.tokens, children: [], diagnostics: [{code: error.code, message: error.message}]};
  }
  return {source, cst, tokens: lexed.tokens, children: Object.freeze((cst?.children['entry'] ?? []).map(entry => projectSyntax(entry, source))), diagnostics: [
    ...lexed.errors.map(error => (/** @satisfies {SteamSyntaxDiagnostic} */ ({code: 'STEAM_LEXICAL_ERROR', message: error.message}))),
    ...parser.errors.map(error => (/** @satisfies {SteamSyntaxDiagnostic} */ ({code: 'STEAM_SYNTAX_ERROR', message: error.message}))),
    ...inspectStructure(cst, source),
  ]};
}

/** @param {string} source @param {readonly NativeToken[]} tokens @param {number} maximum @returns {SteamSyntaxDiagnostic | undefined} */
function checkAttributeLimit(source, tokens, maximum) {
  /** @type {number | undefined} */
  let start;
  for (const token of tokens) {
    if ([TagStart, CodeStart, NoParseStart, HorizontalRuleStart].includes(token.tokenType)) start = (token.endOffset ?? token.startOffset) + 1;
    if (start !== undefined && [HeaderEnd, CodeHeaderEnd, NoParseHeaderEnd].includes(token.tokenType)) {
      if (encoder.encode(source.slice(start, token.startOffset)).byteLength > maximum) return {code: 'STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED', message: 'UTF-8 attribute size exceeds maxAttributeBytes.'};
      start = undefined;
    }
  }
  if (start !== undefined && encoder.encode(source.slice(start)).byteLength > maximum) return {code: 'STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED', message: 'UTF-8 attribute size exceeds maxAttributeBytes.'};
  return undefined;
}

/** @param {NativeCstNode | undefined} cst @param {string} source @returns {SteamSyntaxDiagnostic[]} */
function inspectStructure(cst, source) {
  if (!cst) return [];
  /** @type {SteamSyntaxDiagnostic[]} */
  const diagnostics = [];
  /** @param {SteamParseDiagnosticCode} code @param {string} message @param {NativeSourceCoordinates | undefined} coordinates */
  function add(code, message, coordinates) {
    const sourceSpan = unistSourceSpan(source, coordinates);
    diagnostics.push({code, message, ...(sourceSpan ? {sourceSpan} : {})});
  }
  const pending = [cst];
  while (pending.length > 0) {
    const node = pending.pop();
    if (!node) break;
    if (node.name === 'tagHeaderTail' && !node.children['HeaderEnd']) add('STEAM_UNCLOSED_TAG_HEADER', 'An opening tag has no closing bracket.', node.location);
    if (node.children['UnclosedAttributeQuote']) add('STEAM_UNCLOSED_ATTRIBUTE_QUOTE', 'A tag attribute contains an unclosed quote.', node.location);
    const opening = firstToken(node, 'TagStart') ?? firstToken(node, 'CodeStart') ?? firstToken(node, 'NoParseStart');
    if (opening) {
      const closing = firstToken(node, 'ClosingTag') ?? firstToken(node, 'CodeEnd') ?? firstToken(node, 'NoParseEnd');
      if (!closing) add('STEAM_UNCLOSED_TAG', `Opening ${opening.image} has no closing tag.`, node.location);
      else if (opening.image.slice(1).toLowerCase() !== closing.image.slice(2, -1).trim().toLowerCase()) add('STEAM_MISMATCHED_CLOSING_TAG', `${opening.image} is paired with ${closing.image}.`, node.location);
    }
    for (const children of Object.values(node.children)) {
      for (const child of children) {
        if ('children' in child) pending.push(child);
        else if (node.name === 'document') add('STEAM_UNMATCHED_CLOSING_TAG', `No opening tag precedes ${child.image}.`, child);
      }
    }
  }
  return diagnostics;
}

/**
 * Translate native token-boundary coordinates into the ongoing unist contract.
 * Chevrotain locates the final character inclusively; unist locates the point
 * after it. A terminal line ending therefore advances to column 1 of the next
 * line. Offsets/columns retain JavaScript UTF-16 units, including surrogate pairs.
 * Empty native rules have no character range and do not invent a source span.
 *
 * @param {string} source
 * @param {NativeSourceCoordinates | undefined} coordinates
 * @returns {import('unist').Position | undefined}
 */
function unistSourceSpan(source, coordinates) {
  if (!coordinates) return undefined;
  const {startOffset, endOffset, startLine, endLine, startColumn, endColumn} = coordinates;
  if (endOffset === undefined || startLine === undefined || endLine === undefined || startColumn === undefined || endColumn === undefined) return undefined;
  if (![startOffset, endOffset, startLine, endLine, startColumn, endColumn].every(Number.isFinite) || startOffset < 0 || endOffset < startOffset || endOffset >= source.length) return undefined;
  const ending = source[endOffset];
  const endsLine = ending === '\r' || ending === '\n';
  return {
    start: {line: startLine, column: startColumn, offset: startOffset},
    end: {line: endLine + (endsLine ? 1 : 0), column: endsLine ? 1 : endColumn + 1, offset: endOffset + 1},
  };
}

/** @param {NativeCstNode} node @param {string} label @returns {NativeToken | undefined} */
function firstToken(node, label) {
  const child = node.children[label]?.[0];
  return child && 'image' in child ? child : undefined;
}

/**
 * Project the native CST once at the parser boundary. Steam syntax retains raw
 * spelling and source ranges; no parser-vendor object escapes in these records.
 * Opaque bodies remain values, and source list boundaries remain boundaries.
 *
 * @param {NativeCstNode | NativeToken} entry
 * @param {string} source
 * @returns {SteamBbcodeSyntaxNode}
 */
function projectSyntax(entry, source) {
  if ('image' in entry) {
    const fields = syntaxSource(source, entry);
    if (entry.tokenType === ItemBoundary) return Object.freeze({type: 'steamListItemBoundary', ...fields});
    if (entry.tokenType === ClosingTag) return Object.freeze({type: 'steamUnmatchedClosingTag', tagName: entry.image.slice(2, -1).trim().toLowerCase(), ...fields});
    return Object.freeze({type: 'steamText', value: entry.image, ...fields});
  }
  if (entry.name === 'element') {
    const child = Object.values(entry.children)[0]?.[0];
    if (!child) throw new Error('Native element has no alternative.');
    return projectSyntax(child, source);
  }
  const opening = firstToken(entry, 'TagStart') ?? firstToken(entry, 'CodeStart') ?? firstToken(entry, 'NoParseStart') ?? firstToken(entry, 'HorizontalRuleStart');
  const headerTail = entry.children['tagHeaderTail']?.[0];
  if (!opening || !headerTail || !('children' in headerTail)) throw new Error('Native tag has no header.');
  const headerEnd = firstToken(headerTail, 'HeaderEnd');
  const closing = firstToken(entry, 'ClosingTag') ?? firstToken(entry, 'CodeEnd') ?? firstToken(entry, 'NoParseEnd');
  const fields = syntaxSource(source, entry.location);
  const header = {
    ...fields,
    rawAttributes: source.slice((opening.endOffset ?? opening.startOffset) + 1, headerEnd?.startOffset ?? fields.sourceSpan.end.offset),
    headerClosed: headerEnd !== undefined,
    ...(closing ? {closingTagName: closing.image.slice(2, -1).trim().toLowerCase()} : {}),
  };
  if (entry.name === 'code' || entry.name === 'noParse') {
    return Object.freeze({type: 'steamOpaqueTag', tagName: entry.name === 'code' ? 'code' : 'noparse',
      value: firstToken(entry, entry.name === 'code' ? 'CodeText' : 'NoParseText')?.image ?? '', ...header});
  }
  return Object.freeze({type: 'steamTag', tagName: opening.image.slice(1).toLowerCase(),
    children: Object.freeze((entry.children['element'] ?? []).map(child => projectSyntax(child, source))), ...header});
}

/** @param {string} source @param {NativeSourceCoordinates | undefined} coordinates @returns {import('./steam-bbcode-syntax.js').SteamSyntaxSource} */
function syntaxSource(source, coordinates) {
  const span = unistSourceSpan(source, coordinates);
  if (!span) throw new Error('A nonempty native syntax element has no source range.');
  return {rawSource: source.slice(span.start.offset, span.end.offset), sourceSpan: Object.freeze({
    start: Object.freeze(span.start), end: Object.freeze(span.end),
  })};
}
