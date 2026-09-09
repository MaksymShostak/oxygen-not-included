// SPDX-License-Identifier: AGPL-3.0-only
/** @typedef {import('mdast').PhrasingContent} PhrasingContent */
/** @typedef {Extract<PhrasingContent, {children: PhrasingContent[]}>} PhrasingParent */
/** @typedef {{node: PhrasingContent, ancestors: PhrasingParent[], start: number, end: number}} PhrasingRun */

/**
 * Presentation marks are idempotent. Canonical runs avoid empty or adjacent
 * delimiter pairs and merge text split only by source tag boundaries. This is
 * a target-tree transformation; native Markdown still owns syntax and escaping.
 * @param {PhrasingContent[]} children
 * @returns {PhrasingContent[]}
 */
export function canonicalGfmPhrasing(children) {
  return assemble(phrasingRuns(children));
}

/**
 * An ordinary blank source line separates paragraphs even inside presentation
 * marks. Split the text projection, then retain each leaf's active ancestors in
 * its paragraph. Non-text leaves occupy a sentinel so a link/image/code value
 * cannot accidentally become a paragraph delimiter. Original trees are untouched.
 * @param {import('mdast').Paragraph} paragraph
 * @returns {import('mdast').Paragraph[]}
 */
export function gfmParagraphs(paragraph) {
  const runs = phrasingRuns(paragraph.children);
  const projection = runs.map(run => run.node.type === 'text' ? run.node.value : '\uFFFC').join('');
  /** @type {{start: number, end: number}[]} */
  const spans = [];
  let start = 0;
  for (const match of projection.matchAll(/\n(?:[ \t]*\n)+/gu)) {
    spans.push({start, end: match.index});
    start = match.index + match[0].length;
  }
  spans.push({start, end: projection.length});
  /** @type {import('mdast').Paragraph[]} */
  const paragraphs = [];
  let firstRun = 0;
  for (const span of spans) {
    /** @type {PhrasingRun[]} */
    const contents = [];
    for (let run = runs[firstRun]; run && run.end <= span.start; run = runs[++firstRun]) { /* Advance over the separator. */ }
    for (let index = firstRun; index < runs.length; index++) {
      const run = runs[index];
      if (!run || run.start >= span.end) break;
      const node = run.node.type === 'text' ? {...run.node,
        value: run.node.value.slice(Math.max(0, span.start - run.start), span.end - run.start)} : run.node;
      contents.push({...run, node});
    }
    const children = assemble(contents);
    if (children.length) paragraphs.push({...paragraph, children});
  }
  return paragraphs;
}

/** @param {PhrasingContent[]} children @returns {PhrasingRun[]} */
function phrasingRuns(children) {
  /** @type {PhrasingRun[]} */
  const runs = [];
  let offset = 0;
  /** @param {PhrasingContent[]} nodes @param {PhrasingParent[]} ancestors */
  function visit(nodes, ancestors) {
    for (const node of nodes) {
      if ('children' in node && node.children.length) {
        const repeated = isPresentation(node) && ancestors.some(parent => parent.type === node.type);
        visit(node.children, repeated ? ancestors : [...ancestors, node]);
      } else if (('children' in node && isPresentation(node)) || (node.type === 'text' && !node.value)) {
        continue;
      } else {
        const length = node.type === 'text' ? node.value.length : 1;
        runs.push({node, ancestors: orderedPresentation(ancestors), start: offset, end: offset + length});
        offset += length;
      }
    }
  }
  visit(children, []);
  return runs;
}

/** @param {PhrasingRun[]} runs @returns {PhrasingContent[]} */
function assemble(runs) {
  /** @type {PhrasingContent[]} */
  const children = [];
  /** @type {PhrasingContent[][]} */
  const containers = [children];
  /** @type {PhrasingParent[]} */
  let preceding = [];
  for (const {node, ancestors} of runs) {
    if (node.type === 'text' && !node.value) continue;
    let common = 0;
    while (common < preceding.length && common < ancestors.length) {
      const left = preceding[common];
      const right = ancestors[common];
      if (left !== right && !(left && right && isPresentation(left) && left.type === right.type)) break;
      common++;
    }
    containers.length = common + 1;
    for (const ancestor of ancestors.slice(common)) {
      /** @type {PhrasingContent[]} */
      const nested = [];
      containers[containers.length - 1]?.push({...ancestor, children: nested});
      containers.push(nested);
    }
    const container = containers[containers.length - 1];
    const previous = container?.at(-1);
    if (previous?.type === 'text' && node.type === 'text') previous.value += node.value;
    else container?.push({...node});
    preceding = ancestors;
  }
  return children;
}

/** @param {PhrasingParent[]} ancestors */
function orderedPresentation(ancestors) {
  /** @type {PhrasingParent[]} */
  const result = [];
  /** @type {PhrasingParent[]} */
  let marks = [];
  function flush() {
    // Bold, italic and deletion commute within a presentation run. Link
    // boundaries retain their identity and order, including empty links.
    result.push(...marks.sort((a, b) => presentationOrder(a) - presentationOrder(b)));
    marks = [];
  }
  for (const ancestor of ancestors) {
    if (isPresentation(ancestor)) marks.push(ancestor);
    else { flush(); result.push(ancestor); }
  }
  flush();
  return result;
}

/** @param {PhrasingContent} node */
function isPresentation(node) {
  return node.type === 'strong' || node.type === 'emphasis' || node.type === 'delete';
}

/** @param {PhrasingParent} node */
function presentationOrder(node) {
  return node.type === 'strong' ? 0 : node.type === 'emphasis' ? 1 : 2;
}
