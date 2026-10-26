import {createHash} from 'node:crypto';

const RELATIONS = new Set(['=', '≠', '<', '>', '≤', '≥', '≈', '≃']);
const MULTIPLICATION = new Set(['*', '×', '·', '⋅', '∗', '⁢', '\u2062']);
const APPLY = new Set(['⁡', '\u2061']);
const SUPPORTED_FUNCTIONS = new Set([
  'sin', 'cos', 'tan', 'arcsin', 'arccos', 'arctan', 'sinh', 'cosh', 'tanh',
  'exp', 'ln', 'log', 'abs', 'min', 'max'
]);
const IGNORED_NODES = new Set(['mspace']);
const WRAPPERS = new Set(['math', 'semantics', 'mrow', 'mstyle', 'mpadded']);
const DEFAULT_SAMPLE_COUNT = 24;
const MIN_VALID_SAMPLES = 12;
const RELATIVE_TOLERANCE = 1e-9;

export function parseMathMl(mathml, adaptor) {
  const document = adaptor.parser.parseFromString(mathml, 'text/html', adaptor);
  const math = document.body.children.find((child) => child.kind === 'math');
  if (!math) throw new UnsupportedMathError('MathML root missing');
  return parseNode(math);
}

export function compareAstPair(reference, prediction, {
  sampleCount = DEFAULT_SAMPLE_COUNT,
  seedMaterial = ''
} = {}) {
  const referenceKind = reference.type === 'relation' ? 'relation' : 'expression';
  const predictionKind = prediction.type === 'relation' ? 'relation' : 'expression';
  if (referenceKind !== predictionKind) {
    return result('not-assessable', 'root-kinds-differ', 0, 0, 0, reference, prediction);
  }

  const variables = [...new Set([...collectVariables(reference), ...collectVariables(prediction)])].sort();
  const mismatchCount = {value: 0};
  let validCount = 0;
  let domainSkipCount = 0;
  for (let sample = 0; sample < sampleCount; sample++) {
    const values = createAssignment(variables, sample, seedMaterial);
    let expected;
    let actual;
    let referenceError = null;
    let predictionError = null;
    try {
      expected = evaluate(reference, values);
    } catch (error) {
      if (!(error instanceof EvaluationDomainError)) throw error;
      referenceError = error;
    }
    try {
      actual = evaluate(prediction, values);
    } catch (error) {
      if (!(error instanceof EvaluationDomainError)) throw error;
      predictionError = error;
    }
    if (referenceError || predictionError) {
      if (Boolean(referenceError) !== Boolean(predictionError)) mismatchCount.value++;
      else domainSkipCount++;
      continue;
    }
    try {
      if (referenceKind === 'relation') {
        if (expected !== actual) mismatchCount.value++;
      } else if (!approximatelyEqualValue(expected, actual)) {
        mismatchCount.value++;
      }
      validCount++;
    } catch (error) { throw error; }
  }

  if (validCount < MIN_VALID_SAMPLES) {
    return result('not-assessable', 'insufficient-valid-substitutions', validCount, mismatchCount.value, variables.length, reference, prediction, domainSkipCount);
  }
  if (mismatchCount.value === 0 && !relationsPreserveBoundary(reference, prediction)) {
    return result('not-assessable', 'relation-boundary-unproven', validCount, 0, variables.length, reference, prediction, domainSkipCount);
  }
  return result(
    mismatchCount.value === 0 ? 'sampled-equivalent' : 'sampled-different',
    mismatchCount.value === 0 ? null : 'numeric-substitution-mismatch',
    validCount,
    mismatchCount.value,
    variables.length,
    reference,
    prediction,
    domainSkipCount
  );
}

function relationsPreserveBoundary(reference, prediction) {
  if (reference.type === 'tuple' || prediction.type === 'tuple') {
    return reference.type === 'tuple' && prediction.type === 'tuple' &&
      reference.items.length === prediction.items.length &&
      reference.items.every((item, index) => relationsPreserveBoundary(item, prediction.items[index]));
  }
  if (reference.type !== 'relation' && prediction.type !== 'relation') return true;
  if (reference.type !== 'relation' || prediction.type !== 'relation') return false;
  const direct = reference.operator === prediction.operator &&
    serializeAst(reference.left) === serializeAst(prediction.left) &&
    serializeAst(reference.right) === serializeAst(prediction.right);
  if (direct) return true;
  const reversed = new Map([['=', '='], ['≠', '≠'], ['≈', '≈'], ['≃', '≃'], ['<', '>'], ['>', '<'], ['≤', '≥'], ['≥', '≤']]);
  return reversed.get(reference.operator) === prediction.operator &&
    serializeAst(reference.left) === serializeAst(prediction.right) &&
    serializeAst(reference.right) === serializeAst(prediction.left);
}

export async function compareLatexPair(referenceLatex, predictionLatex, {
  toMathMl,
  adaptor,
  sampleCount = DEFAULT_SAMPLE_COUNT,
  seedMaterial = ''
} = {}) {
  try {
    const [referenceMathMl, predictionMathMl] = await Promise.all([
      toMathMl(referenceLatex), toMathMl(predictionLatex)
    ]);
    const reference = parseMathMl(referenceMathMl, adaptor);
    const prediction = parseMathMl(predictionMathMl, adaptor);
    return compareAstPair(reference, prediction, {sampleCount, seedMaterial});
  } catch (error) {
    return result(
      'not-assessable',
      error instanceof UnsupportedMathError ? error.reason : 'mathjax-parse-error',
      0,
      0,
      0,
      null,
      null,
      0,
      truncate(String(error?.message ?? error))
    );
  }
}

export class UnsupportedMathError extends Error {
  constructor(reason) {
    super(reason);
    this.name = 'UnsupportedMathError';
    this.reason = reason;
  }
}

function parseNode(node) {
  if (node.kind === '#text' || IGNORED_NODES.has(node.kind)) return null;
  const children = mathChildren(node);
  switch (node.kind) {
    case 'math':
    case 'semantics':
    case 'mrow': {
      const semanticType = node.attributes?.['data-semantic-type'];
      const texClass = node.attributes?.['data-mjx-texclass'];
      if (semanticType === 'function' || texClass === 'OP') {
        const operatorName = textContent(node).replace(/\s+/g, '');
        if (SUPPORTED_FUNCTIONS.has(operatorName)) return symbol(operatorName);
      }
      return parseSequence(children.map(parseNode).filter((item) => item !== null));
    }
    case 'mstyle':
    case 'mpadded':
      return parseSequence(children.map(parseNode).filter((item) => item !== null));
    case 'mi': {
      const name = textContent(node).trim();
      if (!name) throw new UnsupportedMathError('empty-identifier');
      const variant = node.attributes?.mathvariant;
      return symbol(variant ? `${variant}:${name}` : name);
    }
    case 'mn': {
      const text = textContent(node).trim().replaceAll(',', '');
      const value = Number(text);
      if (!Number.isFinite(value)) throw new UnsupportedMathError('unsupported-number-literal');
      return number(value);
    }
    case 'mo':
      return {type: 'operator-token', value: normalizeOperator(textContent(node))};
    case 'mfrac':
      requireChildren(node, 2);
      return binary('/', parseRequired(children[0]), parseRequired(children[1]));
    case 'msup':
      requireChildren(node, 2);
      return binary('^', parseRequired(children[0]), parseRequired(children[1]));
    case 'msub':
      requireChildren(node, 2);
      return indexedSymbol(parseRequired(children[0]), parseRequired(children[1]));
    case 'msubsup':
      requireChildren(node, 3);
      return indexedSymbol(parseRequired(children[0]), binary(',', parseRequired(children[1]), parseRequired(children[2])));
    case 'msqrt':
      requireChildren(node, 1);
      return binary('^', parseRequired(children[0]), number(0.5));
    case 'mroot':
      requireChildren(node, 2);
      return binary('^', parseRequired(children[0]), binary('/', number(1), parseRequired(children[1])));
    case 'mover':
      requireChildren(node, 2);
      return decoratedSymbol(parseRequired(children[0]), parseRequired(children[1]));
    case 'mfenced':
      return parseSequence(children.map(parseNode).filter((item) => item !== null));
    default:
      if (WRAPPERS.has(node.kind)) return parseSequence(children.map(parseNode).filter((item) => item !== null));
      throw new UnsupportedMathError(`unsupported-mathml-${node.kind}`);
  }
}

function parseSequence(items) {
  // MathJax keeps sentence punctuation outside the semantic expression. A
  // terminal full stop/semicolon is presentation punctuation, not an operand;
  // strip only terminal punctuation so commas inside tuples remain meaningful.
  while (items.length > 0 && items.at(-1)?.type === 'operator-token' && ['.', ';'].includes(items.at(-1).value)) {
    items = items.slice(0, -1);
  }
  const parser = new SequenceParser(items);
  return parser.parse();
}

class SequenceParser {
  constructor(items) {
    this.items = items;
    this.index = 0;
  }

  parse() {
    if (this.items.length === 0) throw new UnsupportedMathError('empty-expression');
    const values = [this.parseRelation()];
    while (this.operatorAt(this.index) === ',') {
      this.index++;
      values.push(this.parseRelation());
    }
    if (this.index !== this.items.length) {
      throw new UnsupportedMathError(`unsupported-token-${this.operatorAt(this.index) ?? this.items[this.index]?.type ?? 'unknown'}`);
    }
    return values.length === 1 ? values[0] : {type: 'tuple', items: values};
  }

  parseRelation() {
    let left = this.parseAdditive();
    const operator = this.operatorAt(this.index);
    if (RELATIONS.has(operator)) {
      this.index++;
      const right = this.parseAdditive();
      left = {type: 'relation', operator, left, right};
      if (RELATIONS.has(this.operatorAt(this.index))) throw new UnsupportedMathError('chained-relations-unsupported');
    }
    return left;
  }

  parseAdditive() {
    let left = this.parseMultiplicative();
    while (['+', '-', '−'].includes(this.operatorAt(this.index))) {
      const operator = this.operatorAt(this.index++);
      left = binary(operator === '+' ? '+' : '-', left, this.parseMultiplicative());
    }
    return left;
  }

  parseMultiplicative() {
    let left = this.parseUnary();
    while (this.index < this.items.length) {
      const operator = this.operatorAt(this.index);
      if (MULTIPLICATION.has(operator)) {
        this.index++;
        left = binary('*', left, this.parseUnary());
      } else if (operator === '/') {
        this.index++;
        left = binary('/', left, this.parseUnary());
      } else if (canStartImplicitProduct(this.items[this.index])) {
        left = binary('*', left, this.parseUnary());
      } else {
        break;
      }
    }
    return left;
  }

  parseUnary() {
    const operator = this.operatorAt(this.index);
    if (operator === '+' || operator === '-' || operator === '−') {
      this.index++;
      return {type: 'unary', operator: operator === '+' ? '+' : '-', value: this.parseUnary()};
    }
    return this.parseAtom();
  }

  parseAtom() {
    const item = this.items[this.index];
    if (!item) throw new UnsupportedMathError('missing-operand');
    if (item.type === 'operator-token') {
      if (['(', '[', '{'].includes(item.value)) {
        const closing = item.value === '(' ? ')' : item.value === '[' ? ']' : '}';
        this.index++;
        const values = [this.parseRelation()];
        while (this.operatorAt(this.index) === ',') {
          this.index++;
          values.push(this.parseRelation());
        }
        if (this.operatorAt(this.index) !== closing) throw new UnsupportedMathError(`unclosed-${closing === ')' ? 'parenthesis' : 'group'}`);
        this.index++;
        return values.length === 1 ? values[0] : {type: 'tuple', items: values};
      }
      if (SUPPORTED_FUNCTIONS.has(item.value)) {
        this.index++;
        if (APPLY.has(this.operatorAt(this.index))) this.index++;
        return {type: 'call', name: item.value, args: this.parseFunctionArguments()};
      }
      throw new UnsupportedMathError(`unsupported-prefix-${item.value}`);
    }

    this.index++;
    let value = item;
    if (value.type === 'symbol' && APPLY.has(this.operatorAt(this.index))) {
      this.index++;
      const args = this.parseFunctionArguments();
      value = {type: 'call', name: value.name, args};
    }
    if (this.operatorAt(this.index) === '^') {
      this.index++;
      value = binary('^', value, this.parseUnary());
    }
    return value;
  }

  parseFunctionArguments() {
    if (this.operatorAt(this.index) === '(') {
      this.index++;
      const args = [];
      while (this.index < this.items.length) {
        args.push(this.parseRelation());
        const operator = this.operatorAt(this.index);
        if (operator === ')') {
          this.index++;
          return args;
        }
        if (operator !== ',') throw new UnsupportedMathError('malformed-function-arguments');
        this.index++;
      }
      throw new UnsupportedMathError('unclosed-function-call');
    }
    const argument = this.parseUnary();
    // MathJax may encode a function's argument as one MathML child even when
    // that child is a comma-delimited argument list. Keep the list semantics
    // for supported functions such as min/max; unknown functions still fail
    // closed in evaluate().
    return argument.type === 'tuple' ? argument.items : [argument];
  }

  operatorAt(index) {
    const item = this.items[index];
    return item?.type === 'operator-token' ? item.value : null;
  }
}

function evaluate(node, values) {
  switch (node.type) {
    case 'number': return node.value;
    case 'symbol': {
      if (node.name === 'π' || node.name === 'pi') return Math.PI;
      if (node.name === 'ℯ') return Math.E;
      if (!values.has(node.name)) throw new EvaluationDomainError('unbound-symbol');
      return values.get(node.name);
    }
    case 'unary': {
      const value = requireNumber(evaluate(node.value, values));
      return node.operator === '-' ? -value : value;
    }
    case 'binary': {
      const left = requireNumber(evaluate(node.left, values));
      const right = requireNumber(evaluate(node.right, values));
      switch (node.operator) {
        case '+': return left + right;
        case '-': return left - right;
        case '*': return left * right;
        case '/':
          if (Math.abs(right) < 1e-14) throw new EvaluationDomainError('division-by-zero');
          return left / right;
        case '^': return Math.pow(left, right);
        case ',': return [left, right];
        default: throw new EvaluationDomainError(`unsupported-operation-${node.operator}`);
      }
    }
    case 'tuple': return node.items.map((item) => evaluate(item, values));
    case 'relation': {
      const left = requireNumber(evaluate(node.left, values));
      const right = requireNumber(evaluate(node.right, values));
      const tolerance = RELATIVE_TOLERANCE * (1 + Math.max(Math.abs(left), Math.abs(right)));
      switch (node.operator) {
        case '=':
        case '≈':
        case '≃': return Math.abs(left - right) <= tolerance;
        case '≠': return Math.abs(left - right) > tolerance;
        case '<': return left < right - tolerance;
        case '>': return left > right + tolerance;
        case '≤': return left <= right + tolerance;
        case '≥': return left >= right - tolerance;
        default: throw new EvaluationDomainError(`unsupported-relation-${node.operator}`);
      }
    }
    case 'call': {
      const args = node.args.map((arg) => requireNumber(evaluate(arg, values)));
      if (['min', 'max'].includes(node.name)) {
        if (args.length < 2) throw new EvaluationDomainError('min-max-needs-two-arguments');
        return node.name === 'min' ? Math.min(...args) : Math.max(...args);
      }
      if (args.length !== 1) throw new EvaluationDomainError('unary-function-arity');
      const value = args[0];
      switch (node.name) {
        case 'sin': return Math.sin(value);
        case 'cos': return Math.cos(value);
        case 'tan': return Math.tan(value);
        case 'arcsin': return Math.asin(value);
        case 'arccos': return Math.acos(value);
        case 'arctan': return Math.atan(value);
        case 'sinh': return Math.sinh(value);
        case 'cosh': return Math.cosh(value);
        case 'tanh': return Math.tanh(value);
        case 'exp': return Math.exp(value);
        case 'ln':
        case 'log':
          if (value <= 0) throw new EvaluationDomainError('log-domain');
          return Math.log(value);
        case 'abs': return Math.abs(value);
        default: throw new EvaluationDomainError(`unsupported-function-${node.name}`);
      }
    }
    default: throw new EvaluationDomainError(`unsupported-ast-${node.type}`);
  }
}

function createAssignment(variables, sample, seedMaterial) {
  const digest = createHash('sha256').update(String(seedMaterial)).digest();
  const salt = digest.readUInt32LE(0) / 0xFFFFFFFF;
  const result = new Map();
  variables.forEach((variable, index) => {
    if (sample === 0) result.set(variable, 0);
    else if (sample === 1) result.set(variable, 1);
    else if (sample === 2) result.set(variable, -1);
    else if (sample === 3) result.set(variable, 2);
    else if (sample === 4) result.set(variable, -2);
    else if (sample === 5) result.set(variable, 0.5);
    else {
      const phase = (sample + 1) * 0.731 + (index + 1) * 1.217 + salt * 2.39;
      result.set(variable, 1.9 * Math.sin(phase) + 0.73 * Math.cos(phase * 1.618) + 0.13 * (index + 1));
    }
  });
  return result;
}

function collectVariables(node, output = new Set()) {
  if (!node) return output;
  if (node.type === 'symbol' && !['π', 'pi', 'ℯ'].includes(node.name)) output.add(node.name);
  if (node.left) collectVariables(node.left, output);
  if (node.right) collectVariables(node.right, output);
  if (node.value && typeof node.value === 'object') collectVariables(node.value, output);
  for (const item of node.items ?? []) collectVariables(item, output);
  for (const arg of node.args ?? []) collectVariables(arg, output);
  return output;
}

function approximatelyEqual(left, right) {
  if (typeof left !== 'number' || typeof right !== 'number' || !Number.isFinite(left) || !Number.isFinite(right)) return false;
  return Math.abs(left - right) <= RELATIVE_TOLERANCE * (1 + Math.max(Math.abs(left), Math.abs(right)));
}

function approximatelyEqualValue(left, right) {
  if (typeof left === 'boolean' || typeof right === 'boolean') {
    return typeof left === 'boolean' && typeof right === 'boolean' && left === right;
  }
  if (Array.isArray(left) || Array.isArray(right)) {
    return Array.isArray(left) && Array.isArray(right) && left.length === right.length &&
      left.every((value, index) => approximatelyEqualValue(value, right[index]));
  }
  return approximatelyEqual(left, right);
}

function requireNumber(value) {
  if (typeof value !== 'number' || !Number.isFinite(value)) throw new EvaluationDomainError('non-finite-or-non-numeric');
  return value;
}

function symbol(name) { return {type: 'symbol', name}; }
function number(value) { return {type: 'number', value}; }
function binary(operator, left, right) { return {type: 'binary', operator, left, right}; }

function indexedSymbol(base, index) {
  if (base.type === 'symbol') return symbol(`${base.name}_{${serializeAst(index)}}`);
  return symbol(`indexed_{${serializeAst(base)}}_{${serializeAst(index)}}`);
}

function decoratedSymbol(base, mark) {
  if (base.type !== 'symbol') throw new UnsupportedMathError('decorated-non-symbol');
  return symbol(`decorated:${serializeAst(mark)}:${base.name}`);
}

function serializeAst(node) {
  if (!node) return 'null';
  switch (node.type) {
    case 'number': return `n(${node.value})`;
    case 'symbol': return `s(${node.name})`;
    case 'binary': return `b(${node.operator},${serializeAst(node.left)},${serializeAst(node.right)})`;
    case 'tuple': return `t(${node.items.map(serializeAst).join(',')})`;
    case 'unary': return `u(${node.operator},${serializeAst(node.value)})`;
    case 'relation': return `r(${node.operator},${serializeAst(node.left)},${serializeAst(node.right)})`;
    case 'call': return `c(${node.name},${node.args.map(serializeAst).join(',')})`;
    default: return `${node.type}(${node.value ?? ''})`;
  }
}

function result(status, reason, sampleCount, mismatchCount, variableCount, reference, prediction, domainSkipCount = 0, detail = null) {
  return {
    status,
    reason,
    validSubstitutionCount: sampleCount,
    mismatchCount,
    domainSkipCount,
    variableCount,
    referenceAstSha256: reference ? createHash('sha256').update(serializeAst(reference)).digest('hex') : null,
    predictionAstSha256: prediction ? createHash('sha256').update(serializeAst(prediction)).digest('hex') : null,
    detail: detail ? truncate(detail) : null
  };
}

function requireChildren(node, count) {
  const children = mathChildren(node);
  if (children.length !== count) throw new UnsupportedMathError(`expected-${count}-children-in-${node.kind}`);
}

function mathChildren(node) {
  return (node.children ?? []).filter((child) => child.kind !== '#text' && !IGNORED_NODES.has(child.kind));
}

function parseRequired(node) {
  const value = parseNode(node);
  if (!value) throw new UnsupportedMathError('missing-mathml-child');
  return value;
}

function textContent(node) {
  if (node.kind === '#text') return node.value ?? '';
  return (node.children ?? []).map(textContent).join('');
}

function normalizeOperator(value) {
  return String(value).trim().replaceAll('\u2062', '⁢').replaceAll('\u2061', '⁡');
}

function canStartImplicitProduct(item) {
  return item.type === 'number' || item.type === 'symbol' || item.type === 'binary' || item.type === 'call' ||
    (item.type === 'operator-token' && item.value === '(');
}

function truncate(value) { return String(value).slice(0, 240); }

class EvaluationDomainError extends Error {}
