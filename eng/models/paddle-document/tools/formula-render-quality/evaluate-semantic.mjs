#!/usr/bin/env node

import {createHash} from 'node:crypto';
import {readFile, writeFile, mkdir} from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import {fileURLToPath} from 'node:url';
import {compareLatexPair} from './semantic-evaluator.mjs';
import {initializeMathJax} from './mathjax-runtime.mjs';

const models = [
  'pp-formulanet-plus-s', 'pp-formulanet-plus-m', 'pp-formulanet-plus-l',
  'pp-formulanet-s', 'pp-formulanet-l', 'unimernet'
];
const toolDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(toolDirectory, '..', '..', '..', '..', '..');
const options = parseArguments(process.argv.slice(2));
const manifestPath = path.join(options.datasetRoot, 'realFormula-manifest.jsonl');
const manifestBytes = await readFile(manifestPath);
const manifestRows = manifestBytes.toString('utf8').replace(/^\uFEFF/, '').split(/\r?\n/)
  .filter((line) => line.trim()).map((line) => JSON.parse(line));
const manifest = new Map(manifestRows.map((row) => [row.Image, row]));
if (manifest.size !== 121) throw new Error(`Expected 121 manifest rows, found ${manifest.size}.`);

const mathJax = await initializeMathJax();
const mathMlCache = new Map();
const modelResults = [];
try {
  for (const model of options.vlReport ? ['paddleocr-vl/1.6'] : models) {
    const sourceName = options.vlReport ? path.basename(options.vlReport) : `formula-realformula-${model}-ort-20261007.json`;
    const sourcePath = options.vlReport ?? path.join(options.verificationRoot, sourceName);
    const sourceBytes = await readFile(sourcePath);
    const source = JSON.parse(sourceBytes.toString('utf8').replace(/^\uFEFF/, ''));
    const samples = options.vlReport ? source?.results : source?.modelResults?.[0]?.results;
    if (!Array.isArray(samples) || samples.length !== manifest.size) {
      throw new Error(`Expected ${manifest.size} result rows in ${sourcePath}.`);
    }
    if (options.vlReport && (source.schemaVersion !== 'deploysharp-paddleocr-vl-realformula-quality-v1'
        || source.status !== 'completed' || source.model?.id !== model
        || source.dataset?.manifestSha256 !== sha256Bytes(manifestBytes)
        || source.dataset?.selectedCount !== 121
        || source.summary?.endOfSequenceCount !== 121)) {
      throw new Error('The PaddleOCR-VL source is not a completed, pinned 121-image evaluation.');
    }

    const results = [];
    const seenImages = new Set();
    for (const sample of samples) {
      const truth = manifest.get(sample.image);
      if (!truth) throw new Error(`Missing reference row for ${sample.image}.`);
      if (seenImages.has(sample.image)) throw new Error(`Duplicate prediction row for ${sample.image}.`);
      seenImages.add(sample.image);
      const reference = String(truth.ReferenceLatex ?? '');
      if (options.vlReport && sample.referenceLatex !== reference)
        throw new Error(`Reference text mismatch for ${sample.image}.`);
      const rawPrediction = String(sample.prediction ?? '');
      const prediction = options.vlReport ? stripDisplayDelimiters(rawPrediction) : rawPrediction;
      if (options.vlReport && sample.finishReason !== 'EndOfSequence')
        throw new Error(`Non-terminal PaddleOCR-VL prediction for ${sample.image}.`);
      if (options.vlReport && typeof sample.imageSha256 !== 'string')
        throw new Error(`Missing image SHA for ${sample.image}.`);
      if (sample.imageSha256 && sample.imageSha256.toLowerCase() !== String(truth.ImageSha256).toLowerCase()) {
        throw new Error(`Image SHA mismatch for ${sample.image}.`);
      }
      if (sample.referenceSha256 && sample.referenceSha256.toLowerCase() !== sha256Text(reference)) {
        throw new Error(`Reference SHA mismatch for ${sample.image}.`);
      }
      const diagnostic = await compareLatexPair(reference, prediction, {
        toMathMl: cachedMathMl,
        adaptor: mathJax.adaptor,
        sampleCount: options.sampleCount,
        seedMaterial: `${truth.ImageSha256}|${model}|${sample.image}`
      });
      results.push({
        image: sample.image,
        imageSha256: truth.ImageSha256,
        referenceSha256: sha256Text(reference),
        predictionSha256: sha256Text(prediction),
        ...(options.vlReport ? {rawPredictionSha256: sha256Text(rawPrediction)} : {}),
        ...diagnostic
      });
    }

    const counts = Object.fromEntries(['sampled-equivalent', 'sampled-different', 'not-assessable']
      .map((status) => [status, results.filter((row) => row.status === status).length]));
    modelResults.push({
      model: options.vlReport ? model : `paddle-formula/${model}`,
      backend: options.vlReport ? source.environment.backend : 'onnxruntime-cpu',
      sourceReport: sourceName,
      sourceReportSha256: sha256Bytes(sourceBytes),
      sampleCount: results.length,
      statusCounts: counts,
      assessedCoverage: (counts['sampled-equivalent'] + counts['sampled-different']) / results.length,
      sampledAgreementRate: counts['sampled-equivalent'] + counts['sampled-different'] === 0
        ? null
        : counts['sampled-equivalent'] / (counts['sampled-equivalent'] + counts['sampled-different']),
      results
    });
    console.log(`${model}: ${counts['sampled-equivalent']} sampled-equivalent; ${counts['sampled-different']} sampled-different; ${counts['not-assessable']} not assessable`);
  }

  const report = {
    schemaVersion: 'deploysharp-realformula-numeric-equivalence-diagnostic-v1',
    evaluatorVersion: '0.3.0',
    generatedUtc: new Date().toISOString(),
    runtime: {
      node: process.version,
      mathJax: '4.1.3',
      evaluator: 'restricted presentation-MathML arithmetic AST with deterministic numeric substitutions',
      substitutionCountPerPair: options.sampleCount,
      relativeTolerance: 1e-9,
      minimumValidSubstitutions: 12,
      domains: 'real-valued assignments only; non-finite/domain-invalid points are tracked and skipped only when both sides are undefined'
    },
    dataset: {
      name: 'MathNet realFormula',
      version: 'Zenodo 11296815 v1',
      sampleCount: manifest.size,
      manifestSha256: sha256Bytes(manifestBytes),
      modelTrainingOverlap: 'unknown'
    },
    modelResults,
    boundary: 'sampled-equivalent means only that this restricted evaluator found no difference on the recorded deterministic real-valued substitutions. It is not a proof of mathematical equivalence, does not cover unsupported constructs/domains/complex values, and must not be used as a release-quality pass. The dataset overlap with model training is unknown; an independent validation set and reviewed semantic evaluator remain required.'
  };
  await mkdir(path.dirname(options.outputJson), {recursive: true});
  await mkdir(path.dirname(options.outputMarkdown), {recursive: true});
  await writeFile(options.outputJson, JSON.stringify(report, null, 2) + '\n', 'utf8');
  await writeFile(options.outputMarkdown, markdown(report), 'utf8');
  console.log(`JSON: ${options.outputJson}`);
  console.log(`Markdown: ${options.outputMarkdown}`);
} finally {
  mathJax.done();
}

async function cachedMathMl(formula) {
  if (!mathMlCache.has(formula)) mathMlCache.set(formula, mathJax.toMathMl(formula));
  return mathMlCache.get(formula);
}

function markdown(report) {
  const lines = [
    options.vlReport ? '# PaddleOCR-VL realFormula restricted numeric-equivalence diagnostic' : '# realFormula restricted numeric-equivalence diagnostic (2026-10-18)',
    '',
    options.vlReport
      ? 'This is an exploratory, fail-closed diagnostic over 121 MathNet realFormula labels and one external PaddleOCR-VL prediction set. Only outer display delimiters are removed. “Sampled-equivalent” is neither a proof of mathematical equivalence nor a release-quality score.'
      : 'This is an exploratory, fail-closed diagnostic over the existing 121 MathNet realFormula labels and six checked-in ORT CPU prediction sets. “Sampled-equivalent” is not a proof of mathematical equivalence and is not a release-quality score.',
    '',
    '| Model | Samples | Sampled-equivalent | Sampled-different | Not assessable | Assessed coverage | Numeric agreement among assessed |',
    '|---|---:|---:|---:|---:|---:|---:|'
  ];
  for (const row of report.modelResults) {
    const counts = row.statusCounts;
    lines.push(`| \`${row.model}\` | ${row.sampleCount} | ${counts['sampled-equivalent']} | ${counts['sampled-different']} | ${counts['not-assessable']} | ${formatRate(row.assessedCoverage)} | ${formatRate(row.sampledAgreementRate)} |`);
  }
  lines.push(
    '',
    '## Protocol',
    '',
    `- MathJax ${report.runtime.mathJax} converts both strings to presentation MathML; evaluator version ${report.evaluatorVersion} supports a restricted real-arithmetic AST (identifiers, numeric literals, arithmetic, fractions, powers, square roots, selected elementary functions, simple relations and comma-delimited numeric tuples).`,
    `- Each pair is checked on ${report.runtime.substitutionCountPerPair} deterministic substitutions with relative tolerance ${report.runtime.relativeTolerance}; the image hash, model ID and sample ordinal seed the assignments. At least ${report.runtime.minimumValidSubstitutions} valid points are required.`,
    '- The JSON stores hashes and outcomes only; source formula text is not copied into this report.',
    '',
    '## Boundary',
    '',
    '- Numerical agreement on a finite set of values is not a symbolic proof and can miss counterexamples; real-only evaluation omits complex semantics, binding, units, sets, matrices, summations, integrals, piecewise expressions and many other constructs.',
    '- Unsupported or insufficient-coverage pairs are `not-assessable`, never counted as equivalent.',
    '- The 121-sample dataset is legally attributed as CC-BY-4.0, but overlap with model training is unknown. This diagnostic does not close the independent semantic evaluator or release-quality gate.',
    options.vlReport
      ? '- Only the external PaddleOCR-VL report identified by SHA-256 is evaluated; no other backend quality is inferred.'
      : '- Only the checked-in ONNX Runtime CPU predictions are evaluated; no other backend quality is inferred.'
  );
  return lines.join('\n') + '\n';
}

function parseArguments(args) {
  const options = {
    datasetRoot: 'F:\\OCRBenchmarkTesting\\datasets\\realFormula-zenodo-11296815\\extracted\\realFormula-public',
    verificationRoot: path.join(repositoryRoot, 'eng', 'models', 'paddle-document', 'verification'),
    outputJson: path.join(repositoryRoot, 'eng', 'models', 'paddle-document', 'verification', 'formula-realformula-numeric-equivalence-20261018.json'),
    outputMarkdown: path.join(repositoryRoot, 'eng', 'models', 'paddle-document', 'verification', 'formula-realformula-numeric-equivalence-20261018.md'),
    sampleCount: 24,
    vlReport: null
  };
  for (let index = 0; index < args.length; index++) {
    const key = args[index];
    const value = args[++index];
    if (!value) throw new Error(`Missing value for ${key}.`);
    if (key === '--dataset-root') options.datasetRoot = value;
    else if (key === '--verification-root') options.verificationRoot = value;
    else if (key === '--output-json') options.outputJson = value;
    else if (key === '--output-markdown') options.outputMarkdown = value;
    else if (key === '--vl-report') options.vlReport = path.resolve(value);
    else if (key === '--samples') {
      options.sampleCount = Number(value);
      if (!Number.isInteger(options.sampleCount) || options.sampleCount < 12 || options.sampleCount > 256) {
        throw new Error('--samples must be an integer from 12 to 256.');
      }
    } else {
      throw new Error(`Unknown argument: ${key}`);
    }
  }
  return options;
}

function stripDisplayDelimiters(value) {
  const trimmed = value.trim();
  return trimmed.startsWith('\\[') && trimmed.endsWith('\\]') ? trimmed.slice(2, -2).trim() : trimmed;
}

function sha256Text(value) { return createHash('sha256').update(String(value), 'utf8').digest('hex'); }
function sha256Bytes(value) { return createHash('sha256').update(value).digest('hex'); }
function formatRate(value) { return value === null ? 'n/a' : `${(value * 100).toFixed(2)}%`; }
