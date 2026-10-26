import assert from 'node:assert/strict';
import test from 'node:test';
import {compareLatexPair} from './semantic-evaluator.mjs';
import {initializeMathJax} from './mathjax-runtime.mjs';

const mathJax = await initializeMathJax();
const compare = (reference, prediction, seed = 'contract-test') => compareLatexPair(reference, prediction, {
  toMathMl: mathJax.toMathMl,
  adaptor: mathJax.adaptor,
  sampleCount: 24,
  seedMaterial: seed
});

test('numeric substitution recognizes common algebraic rewrites', async () => {
  const result = await compare(String.raw`x^2+2x+1`, String.raw`(x+1)^2`);
  assert.equal(result.status, 'sampled-equivalent');
  assert.equal(result.validSubstitutionCount, 24);
});

test('MathJax function-application markers are handled for supported functions', async () => {
  const result = await compare(String.raw`\sin(x)`, String.raw`\sin x`);
  assert.equal(result.status, 'sampled-equivalent');
  assert.equal(result.validSubstitutionCount, 24);
  const operatorName = await compare(String.raw`\operatorname { s i n } x`, String.raw`\sin x`);
  assert.equal(operatorName.status, 'sampled-equivalent');
});

test('presentation punctuation and square-bracket grouping do not create operands', async () => {
  const result = await compare('[x+1]^2.', '(x+1)^2');
  assert.equal(result.status, 'sampled-equivalent');
  assert.equal(result.validSubstitutionCount, 24);
});

test('comma-delimited tuples and min/max arguments are evaluated componentwise', async () => {
  const tuple = await compare('(x, x^2)', '(x, x*x)');
  const differentTuple = await compare('(x, x^2)', '(x^2, x)');
  const min = await compare(String.raw`\min(x,1)`, String.raw`\min(1,x)`);
  const relations = await compare('x<y, y>x', 'x<y, y>x');
  assert.equal(tuple.status, 'sampled-equivalent');
  assert.equal(tuple.validSubstitutionCount, 24);
  assert.equal(differentTuple.status, 'sampled-different');
  assert.equal(min.status, 'sampled-equivalent');
  assert.equal(relations.status, 'sampled-equivalent');
});

test('unknown function applications remain not assessable', async () => {
  const result = await compare('f(x)', 'f(x)');
  assert.equal(result.status, 'not-assessable');
  assert.equal(result.reason, 'insufficient-valid-substitutions');
});

test('numeric substitution detects equations with different real solution sets', async () => {
  const result = await compare(String.raw`x^2=4`, String.raw`x=2`);
  assert.equal(result.status, 'sampled-different');
  assert.ok(result.mismatchCount > 0);
});

test('relation direction reversal is accepted as a sampled relation match', async () => {
  const result = await compare(String.raw`x+1=3`, String.raw`3=x+1`);
  assert.equal(result.status, 'sampled-equivalent');
  const inequality = await compare('x<y', 'y>x');
  assert.equal(inequality.status, 'sampled-equivalent');
});

test('matching false truth tables do not establish relation equivalence', async () => {
  const equation = await compare('y=x+a', 'y=x+b');
  assert.equal(equation.status, 'not-assessable');
  assert.equal(equation.reason, 'relation-boundary-unproven');
  const inequality = await compare('x<100', 'x<200');
  assert.equal(inequality.status, 'not-assessable');
  assert.equal(inequality.reason, 'relation-boundary-unproven');
  const tuple = await compare('x<100, y=200', 'x<200, y=200');
  assert.equal(tuple.status, 'not-assessable');
  assert.equal(tuple.reason, 'relation-boundary-unproven');
});

test('domain mismatches are not silently discarded', async () => {
  const result = await compare(String.raw`x/x`, '1');
  assert.equal(result.status, 'sampled-different');
  assert.ok(result.mismatchCount > 0);
});

test('unsupported binders fail closed as not assessable', async () => {
  const result = await compare(String.raw`\sum_{i=1}^{n} i`, String.raw`n(n+1)/2`);
  assert.equal(result.status, 'not-assessable');
  assert.match(result.reason, /unsupported-mathml/);
});

test('seeded substitutions are reproducible', async () => {
  const first = await compare(String.raw`x^2+1`, String.raw`x^2+1`, 'same-seed');
  const second = await compare(String.raw`x^2+1`, String.raw`x^2+1`, 'same-seed');
  assert.deepEqual(first, second);
});

test.after(() => mathJax.done());
