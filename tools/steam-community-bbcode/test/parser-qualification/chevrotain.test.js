// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {createToken, Lexer, CstParser} from 'chevrotain';

// This checks supported toolkit composition and strict-checker integration.
// It is not a claim that the full Steam grammar already exists.
const Open = createToken({name: 'Open', pattern: /\[b\]/});
const Close = createToken({name: 'Close', pattern: /\[\/b\]/});
const Literal = createToken({name: 'Literal', pattern: /[^[]+/, line_breaks: true});
const tokens = [Open, Close, Literal];
const lexer = new Lexer(tokens);

class PairedTagQualification extends CstParser {
  constructor() {
    super(tokens, {recoveryEnabled: false, nodeLocationTracking: 'full'});
    /** @type {() => import('chevrotain').CstNode} */
    this.paired = this.RULE('paired', () => {
      this.CONSUME(Open);
      this.CONSUME(Literal);
      this.CONSUME(Close);
    });
    this.performSelfAnalysis();
  }
}

test('maintained toolkit supplies CST, source positions and grammar validation directly', () => {
  const source = '[b]°C 😀\r\nnext[/b]';
  const lexed = lexer.tokenize(source);
  assert.deepEqual(lexed.errors, []);
  assert.equal(lexed.tokens.map(token => token.image).join(''), source);
  const parser = new PairedTagQualification();
  parser.input = lexed.tokens;
  const result = parser.paired();
  assert.deepEqual(parser.errors, []);
  assert.equal(result.location?.startOffset, 0);
  assert.equal(result.location?.endOffset, source.length - 1);
});

test('missing closing tokens produce native parse errors without silent recovery', () => {
  const parser = new PairedTagQualification();
  parser.input = lexer.tokenize('[b]missing').tokens;
  parser.paired();
  assert.equal(parser.errors.length, 1);
  assert.match(parser.errors[0]?.message ?? '', /Close/);
});
