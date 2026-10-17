#!/usr/bin/env node

import {createHash} from 'node:crypto';
import {readFile, writeFile, mkdir} from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import sharp from 'sharp';

global.MathJax = {
  loader: {
    paths: {mathjax: '@mathjax/src/bundle'},
    load: ['adaptors/liteDOM'],
    require: (file) => import(file)
  },
  svg: {fontCache: 'local'}
};

await import('@mathjax/src/bundle/tex-svg.js');
await MathJax.startup.promise;

const models = [
  'pp-formulanet-plus-s',
  'pp-formulanet-plus-m',
  'pp-formulanet-plus-l',
  'pp-formulanet-s',
  'pp-formulanet-l',
  'unimernet'
];

const options = parseArguments(process.argv.slice(2));
const manifestPath = path.join(options.datasetRoot, 'realFormula-manifest.jsonl');
const manifestBytes = await readFile(manifestPath);
const manifestRows = manifestBytes.toString('utf8').replace(/^\uFEFF/, '').split(/\r?\n/)
  .filter((line) => line.trim().length > 0)
  .map((line) => JSON.parse(line));
const manifest = new Map(manifestRows.map((row) => [row.Image, row]));
if (manifest.size !== 121) throw new Error(`Expected 121 manifest rows, found ${manifest.size}.`);

const renderCache = new Map();
const modelResults = [];
try {
  for (const model of models) {
    const sourceName = `formula-realformula-${model}-ort-20261007.json`;
    const sourcePath = path.join(options.verificationRoot, sourceName);
    const sourceBytes = await readFile(sourcePath);
    const source = JSON.parse(sourceBytes.toString('utf8').replace(/^\uFEFF/, ''));
    const samples = source?.modelResults?.[0]?.results;
    if (!Array.isArray(samples) || samples.length !== 121) {
      throw new Error(`Expected 121 result rows in ${sourcePath}.`);
    }

    const rows = [];
    for (const sample of samples) {
      const truth = manifest.get(sample.image);
      if (!truth) throw new Error(`Missing reference row for ${sample.image}.`);
      const reference = String(truth.ReferenceLatex ?? '');
      const prediction = String(sample.prediction ?? '');
      if (sample.imageSha256 && sample.imageSha256.toLowerCase() !== String(truth.ImageSha256).toLowerCase()) {
        throw new Error(`Image SHA mismatch for ${sample.image}.`);
      }
      if (sample.referenceSha256 && sample.referenceSha256.toLowerCase() !== sha256Text(reference)) {
        throw new Error(`Reference SHA mismatch for ${sample.image}.`);
      }
      const expected = await renderFormula(reference);
      const actual = await renderFormula(prediction);
      let visual = null;
      if (expected.succeeded && actual.succeeded) {
        visual = compareRaster(expected.raster, actual.raster);
      }
      rows.push({
        image: sample.image,
        imageSha256: truth.ImageSha256,
        referenceSha256: sha256Text(reference),
        predictionSha256: sha256Text(prediction),
        referenceRenderSucceeded: expected.succeeded,
        predictionRenderSucceeded: actual.succeeded,
        referenceRenderError: expected.error,
        predictionRenderError: actual.error,
        referenceMathmlStructuralSha256: expected.mathmlStructuralSha256,
        predictionMathmlStructuralSha256: actual.mathmlStructuralSha256,
        mathmlStructuralMatch: expected.mathmlStructuralSha256 !== null && expected.mathmlStructuralSha256 === actual.mathmlStructuralSha256,
        visualForegroundIou: visual?.foregroundIou ?? null,
        visualPixelSimilarity: visual?.pixelSimilarity ?? null
      });
    }

    const comparable = rows.filter((row) => row.visualForegroundIou !== null);
    modelResults.push({
      model: `paddle-formula/${model}`,
      backend: 'onnxruntime-cpu',
      sourceReport: sourceName,
      sourceReportSha256: sha256Bytes(sourceBytes),
      sampleCount: rows.length,
      referenceRenderSucceeded: rows.filter((row) => row.referenceRenderSucceeded).length,
      predictionRenderSucceeded: rows.filter((row) => row.predictionRenderSucceeded).length,
      comparableRenderPairs: comparable.length,
      mathmlComparablePairs: rows.filter((row) => row.referenceMathmlStructuralSha256 !== null && row.predictionMathmlStructuralSha256 !== null).length,
      mathmlStructuralMatches: rows.filter((row) => row.mathmlStructuralMatch).length,
      mathmlStructuralMatchRate: rows.filter((row) => row.referenceMathmlStructuralSha256 !== null && row.predictionMathmlStructuralSha256 !== null).length === 0 ? null : rows.filter((row) => row.mathmlStructuralMatch).length / rows.filter((row) => row.referenceMathmlStructuralSha256 !== null && row.predictionMathmlStructuralSha256 !== null).length,
      exactPredictionMatches: rows.filter((row) => row.referenceSha256 === row.predictionSha256).length,
      foregroundIouP05: percentile(comparable.map((row) => row.visualForegroundIou), 0.05),
      foregroundIouP50: percentile(comparable.map((row) => row.visualForegroundIou), 0.50),
      pixelSimilarityP05: percentile(comparable.map((row) => row.visualPixelSimilarity), 0.05),
      pixelSimilarityP50: percentile(comparable.map((row) => row.visualPixelSimilarity), 0.50),
      results: rows
    });
    console.log(`${model}: renderable predictions ${modelResults.at(-1).predictionRenderSucceeded}/${rows.length}`);
  }

  const report = {
    schemaVersion: 'deploysharp-realformula-render-quality-v2',
    generatedUtc: new Date().toISOString(),
    runtime: {
      node: process.version,
      mathJax: '4.1.3',
      sharp: '0.35.5',
      renderer: 'MathJax tex-svg with local font cache',
      rasterComparison: `grayscale foreground IoU and pixel similarity after aspect-preserving fit into ${options.rasterWidth}x${options.rasterHeight}`,
      mathmlComparison: 'MathJax tex2mml output after removing renderer metadata attributes and inter-tag whitespace; structural diagnostic only.'
    },
    dataset: {
      name: 'MathNet realFormula',
      version: 'Zenodo 11296815 v1',
      sampleCount: manifest.size,
      manifestSha256: sha256Bytes(manifestBytes)
    },
    modelResults,
    boundary: 'MathJax render success and raster-shape similarity are renderer compatibility and visual-structure diagnostics. They do not prove mathematical equivalence, semantic correctness, TeX-engine portability, model accuracy, or any backend other than the checked-in ONNX Runtime CPU predictions.'
  };
  await mkdir(path.dirname(options.outputJson), {recursive: true});
  await mkdir(path.dirname(options.outputMarkdown), {recursive: true});
  await writeFile(options.outputJson, JSON.stringify(report, null, 2) + '\n', 'utf8');
  await writeFile(options.outputMarkdown, markdown(report), 'utf8');
  console.log(`JSON: ${options.outputJson}`);
  console.log(`Markdown: ${options.outputMarkdown}`);
} finally {
  MathJax.done();
}

async function renderFormula(formula) {
  const key = formula;
  if (renderCache.has(key)) return renderCache.get(key);
  const promise = renderFormulaUncached(formula);
  renderCache.set(key, promise);
  return promise;
}

async function renderFormulaUncached(formula) {
  if (!formula.trim()) return {succeeded: false, error: 'empty formula', raster: null, mathmlStructuralSha256: null};
  try {
    const mathml = await MathJax.tex2mmlPromise(formula, {display: true});
    if (!mathml || /data-mml-node=["']merror["']|data-mjx-error/i.test(mathml)) {
      const detail = extractMathJaxError(mathml);
      return {succeeded: false, error: detail ? `MathJax: ${detail}` : 'MathJax emitted an merror node', raster: null, mathmlStructuralSha256: null};
    }
    const mathmlStructuralSha256 = sha256Text(canonicalMathml(mathml));
    const node = await MathJax.tex2svgPromise(formula, {
      display: true,
      em: 16,
      ex: 8,
      containerWidth: 1280
    });
    const adaptor = MathJax.startup.adaptor;
    const svgNode = adaptor.tags(node, 'svg')[0];
    if (!svgNode) return {succeeded: false, error: 'renderer returned no SVG', raster: null, mathmlStructuralSha256};
    const svg = adaptor.serializeXML(svgNode);
    if (/data-mml-node=["']merror["']|data-mjx-error/i.test(svg)) {
      const detail = extractMathJaxError(svg);
      return {succeeded: false, error: detail ? `MathJax: ${detail}` : 'MathJax emitted an merror node', raster: null, mathmlStructuralSha256};
    }
    const raster = await sharp(Buffer.from(svg), {density: 192})
      .flatten({background: '#ffffff'})
      .trim({background: '#ffffff'})
      .resize({width: options.rasterWidth, height: options.rasterHeight, fit: 'inside', withoutEnlargement: false})
      .grayscale()
      .raw()
      .toBuffer({resolveWithObject: true});
    if (raster.info.width < 1 || raster.info.height < 1) {
      return {succeeded: false, error: 'renderer returned an empty raster', raster: null, mathmlStructuralSha256};
    }
    return {succeeded: true, error: null, raster, mathmlStructuralSha256};
  } catch (error) {
    return {succeeded: false, error: truncateError(error), raster: null, mathmlStructuralSha256: null};
  }
}

function canonicalMathml(value) {
  return String(value)
    .replace(/\s(?:data-(?:latex|semantic-[^=\s]+)|id|class)=("[^"]*"|'[^']*')/gi, '')
    .replace(/>\s+</g, '><')
    .trim();
}

function compareRaster(expected, actual) {
  const width = Math.max(expected.info.width, actual.info.width);
  const height = Math.max(expected.info.height, actual.info.height);
  const expectedLeft = Math.floor((width - expected.info.width) / 2);
  const expectedTop = Math.floor((height - expected.info.height) / 2);
  const actualLeft = Math.floor((width - actual.info.width) / 2);
  const actualTop = Math.floor((height - actual.info.height) / 2);
  let intersection = 0;
  let union = 0;
  let absoluteDifference = 0;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const expectedValue = rasterValue(expected, x - expectedLeft, y - expectedTop);
      const actualValue = rasterValue(actual, x - actualLeft, y - actualTop);
      const expectedForeground = expectedValue < 245;
      const actualForeground = actualValue < 245;
      if (expectedForeground && actualForeground) intersection++;
      if (expectedForeground || actualForeground) union++;
      absoluteDifference += Math.abs(expectedValue - actualValue);
    }
  }
  return {
    foregroundIou: union === 0 ? 1 : intersection / union,
    pixelSimilarity: 1 - absoluteDifference / (255 * width * height)
  };
}

function rasterValue(raster, x, y) {
  if (x < 0 || y < 0 || x >= raster.info.width || y >= raster.info.height) return 255;
  return raster.data[y * raster.info.width + x];
}

function percentile(values, probability) {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const position = (sorted.length - 1) * probability;
  const lower = Math.floor(position);
  const upper = Math.ceil(position);
  return lower === upper ? sorted[lower] : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

function markdown(report) {
  const lines = [
    '# Formula realFormula MathJax render and MathML diagnostics (2026-10-17)',
    '',
    'This report renders the 121 MathNet references and six checked-in ONNX Runtime CPU prediction sets with the locked MathJax renderer, compares paired grayscale rasters, and records normalized MathML structural fingerprints. It is a renderability/structure diagnostic, not a mathematical-semantic score.',
    '',
    '| Model | Samples | Reference rendered | Prediction rendered | Comparable pairs | MathML structural match | Exact text | Foreground IoU P05 / P50 | Pixel similarity P05 / P50 |',
    '|---|---:|---:|---:|---:|---:|---:|---:|---:|'
  ];
  for (const row of report.modelResults) {
    lines.push(`| \`${row.model}\` | ${row.sampleCount} | ${row.referenceRenderSucceeded}/${row.sampleCount} | ${row.predictionRenderSucceeded}/${row.sampleCount} | ${row.comparableRenderPairs}/${row.sampleCount} | ${row.mathmlStructuralMatches}/${row.mathmlComparablePairs} | ${row.exactPredictionMatches}/${row.sampleCount} | ${format(row.foregroundIouP05)} / ${format(row.foregroundIouP50)} | ${format(row.pixelSimilarityP05)} / ${format(row.pixelSimilarityP50)} |`);
  }
  lines.push(
    '',
    '## Protocol',
    '',
    `- Renderer: MathJax ${report.runtime.mathJax} \`tex-svg\` plus \`tex2mml\`, local SVG font cache; Node ${report.runtime.node}.`,
    `- Rasterizer: sharp ${report.runtime.sharp}; each formula is fitted without distortion into ${options.rasterWidth}x${options.rasterHeight}, centered on a shared white canvas, and compared as grayscale pixels.`,
    `- Dataset: ${report.dataset.name}, ${report.dataset.version}, ${report.dataset.sampleCount} references; manifest SHA-256 \`${report.dataset.manifestSha256}\`.`,
    '- The machine report retains per-image hashes, MathML structural hashes, render outcomes, foreground IoU and pixel similarity without copying reference or prediction text.',
    '- MathML structural matches are normalized renderer-structure matches only; they are not mathematical-semantic equivalence.',
    '',
    '## Boundary',
    '',
    '- A successful MathJax render only proves compatibility with this locked renderer and configuration. It does not prove compatibility with a full TeX distribution.',
    '- Pixel similarity, foreground IoU and normalized MathML structure are diagnostics; they cannot establish mathematical equivalence or semantic correctness.',
    '- The predictions come from the checked-in ONNX Runtime CPU reports. OpenVINO, OpenCV DNN and TensorRT quality are not represented.',
    '- No release-quality threshold is inferred from these exploratory distributions.'
  );
  return lines.join('\n') + '\n';
}

function parseArguments(args) {
  const values = new Map();
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || value === undefined) throw new Error(`Invalid argument near ${name ?? '<end>'}.`);
    values.set(name.slice(2), value);
  }
  const cwd = process.cwd();
  const rasterWidth = parseBoundedInteger(values.get('raster-width') ?? '2048', 'raster-width', 256, 4096);
  const rasterHeight = parseBoundedInteger(values.get('raster-height') ?? '128', 'raster-height', 32, 512);
  return {
    datasetRoot: path.resolve(values.get('dataset-root') ?? 'F:/OCRBenchmarkTesting/datasets/realFormula-zenodo-11296815/extracted/realFormula-public'),
    verificationRoot: path.resolve(values.get('verification-root') ?? path.join(cwd, 'eng/models/paddle-document/verification')),
    outputJson: path.resolve(values.get('output-json') ?? path.join(cwd, 'eng/models/paddle-document/verification/formula-realformula-render-quality-20261013.json')),
    outputMarkdown: path.resolve(values.get('output-markdown') ?? path.join(cwd, 'eng/models/paddle-document/verification/formula-realformula-render-quality-20261013.md')),
    rasterWidth,
    rasterHeight
  };
}

function parseBoundedInteger(value, name, minimum, maximum) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed < minimum || parsed > maximum) {
    throw new Error(`--${name} must be between ${minimum} and ${maximum}.`);
  }
  return parsed;
}

function sha256Text(value) {
  return sha256Bytes(Buffer.from(value, 'utf8'));
}

function sha256Bytes(value) {
  return createHash('sha256').update(value).digest('hex');
}

function truncateError(error) {
  const value = error instanceof Error ? error.message : String(error);
  return value.length <= 500 ? value : value.slice(0, 500);
}

function extractMathJaxError(svg) {
  const match = svg.match(/data-mjx-error=(?:"([^"]*)"|'([^']*)')/i);
  if (!match) return null;
  return (match[1] ?? match[2])
    .replaceAll('&quot;', '"')
    .replaceAll('&apos;', "'")
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll('&amp;', '&');
}

function format(value) {
  return value === null ? '-' : value.toFixed(4);
}
