#!/usr/bin/env node
// API-14's own named gap: the existing openapi-drift CI job only catches the
// committed docs/openapi/v1.json disagreeing with the live document -- it
// "can't tell an additive change from a removed field, it just refuses to
// let the committed document drift from source at all" (see that job's own
// comment in .github/workflows/ci.yml). This is a real breaking-change
// classifier, deliberately scoped to a conservative, high-confidence subset
// rather than an exhaustive OpenAPI-diff engine -- see the "NOT covered"
// list below. A false positive here would train whoever runs this to
// ignore it; a narrower tool that's always right is worth more than a
// broader one that sometimes cries wolf.
//
// Flags, comparing an old ("base") document against a new one:
//   - an operation (path + method) removed entirely
//   - a parameter that is newly required (didn't exist before, or existed
//     as optional) -- an old client that never sent it now fails validation
//   - a parameter that was optional and became required
//   - a request body that was optional (or had none) and became required
//   - a request body property that is newly required (existed as optional,
//     or didn't exist at all, and is now in `required`)
//   - a 2xx/3xx response status code removed from an operation that had it
//   - a property removed from a 2xx/3xx response's own top-level schema --
//     an existing client reading that field now gets `undefined`
//
// Deliberately NOT covered (real gaps, named rather than implied away):
//   - schema changes more than one $ref level deep (e.g. a field's own
//     nested object type changing shape) -- only the request/response
//     body's own top-level properties are compared
//   - type/format/enum changes on a property that still exists
//   - a request-body property being removed (extra JSON fields an old
//     client still sends are typically ignored by ASP.NET Core's default
//     deserialization, so this is not flagged as breaking)
//   - anything about parameter or property *ordering*
//
// Usage: node infra/scripts/check-breaking-changes.mjs <baseDocPath> <newDocPath>
// Exits 1 and prints every finding if any breaking change is detected.

import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

/** Resolves a schema through at most one `$ref` (or a nullable `oneOf: [{type:null}, {$ref}]` wrapper) into `doc.components.schemas`. */
export function resolveSchema(doc, schema) {
  if (!schema) {
    return schema;
  }
  if (schema.$ref) {
    const name = schema.$ref.replace('#/components/schemas/', '');
    return doc.components?.schemas?.[name] ?? schema;
  }
  if (Array.isArray(schema.oneOf)) {
    const ref = schema.oneOf.find((member) => member.$ref);
    if (ref) {
      return resolveSchema(doc, ref);
    }
  }
  return schema;
}

const HTTP_METHODS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'];

function collectOperations(doc) {
  const operations = new Map();
  for (const [path, methods] of Object.entries(doc.paths ?? {})) {
    for (const [method, operation] of Object.entries(methods)) {
      if (HTTP_METHODS.includes(method)) {
        operations.set(`${method.toUpperCase()} ${path}`, operation);
      }
    }
  }
  return operations;
}

function paramKey(param) {
  return `${param.in}:${param.name}`;
}

function compareParameters(operationKey, baseOp, newOp) {
  const findings = [];
  const baseParams = new Map((baseOp.parameters ?? []).map((p) => [paramKey(p), p]));

  for (const newParam of newOp.parameters ?? []) {
    const key = paramKey(newParam);
    const baseParam = baseParams.get(key);

    if (!baseParam) {
      if (newParam.required === true) {
        findings.push({
          rule: 'new-required-parameter',
          operation: operationKey,
          detail: `New required ${newParam.in} parameter "${newParam.name}" — an existing client never sends it.`,
        });
      }
      continue;
    }

    if (baseParam.required !== true && newParam.required === true) {
      findings.push({
        rule: 'parameter-became-required',
        operation: operationKey,
        detail: `${newParam.in} parameter "${newParam.name}" was optional, is now required.`,
      });
    }
  }

  return findings;
}

function compareRequestBody(operationKey, baseDoc, baseOp, newDoc, newOp) {
  const findings = [];
  const baseBody = baseOp.requestBody;
  const newBody = newOp.requestBody;

  if (!newBody) {
    return findings;
  }

  const baseWasRequired = baseBody?.required === true;
  if (!baseWasRequired && newBody.required === true) {
    findings.push({
      rule: 'request-body-became-required',
      operation: operationKey,
      detail: baseBody
        ? 'The request body was optional, is now required.'
        : 'A request body is now required where none existed before.',
    });
  }

  const baseSchema = resolveSchema(baseDoc, baseBody?.content?.['application/json']?.schema);
  const newSchema = resolveSchema(newDoc, newBody.content?.['application/json']?.schema);
  const baseRequired = new Set(baseSchema?.required ?? []);
  const newRequired = new Set(newSchema?.required ?? []);

  for (const property of newRequired) {
    if (!baseRequired.has(property)) {
      findings.push({
        rule: 'new-required-request-property',
        operation: operationKey,
        detail: `Request body property "${property}" is newly required — an existing client that never sent it now fails validation.`,
      });
    }
  }

  return findings;
}

function isSuccessStatus(status) {
  return /^[23]\d\d$/.test(status);
}

function compareResponses(operationKey, baseDoc, baseOp, newDoc, newOp) {
  const findings = [];
  const baseResponses = baseOp.responses ?? {};
  const newResponses = newOp.responses ?? {};

  for (const [status, baseResponse] of Object.entries(baseResponses)) {
    if (!isSuccessStatus(status)) {
      continue;
    }

    const newResponse = newResponses[status];
    if (!newResponse) {
      findings.push({
        rule: 'response-status-removed',
        operation: operationKey,
        detail: `Response status ${status} was removed — a client branching on it now gets no match.`,
      });
      continue;
    }

    const baseSchema = resolveSchema(baseDoc, baseResponse.content?.['application/json']?.schema);
    const newSchema = resolveSchema(newDoc, newResponse.content?.['application/json']?.schema);
    const baseProperties = Object.keys(baseSchema?.properties ?? {});
    const newProperties = new Set(Object.keys(newSchema?.properties ?? {}));

    for (const property of baseProperties) {
      if (!newProperties.has(property)) {
        findings.push({
          rule: 'response-property-removed',
          operation: operationKey,
          detail: `Response ${status}'s property "${property}" was removed — an existing client reading it now gets undefined.`,
        });
      }
    }
  }

  return findings;
}

/** @returns {{rule: string, operation: string, detail: string}[]} */
export function findBreakingChanges(baseDoc, newDoc) {
  const findings = [];
  const baseOps = collectOperations(baseDoc);
  const newOps = collectOperations(newDoc);

  for (const [operationKey, baseOp] of baseOps) {
    const newOp = newOps.get(operationKey);
    if (!newOp) {
      findings.push({
        rule: 'removed-operation',
        operation: operationKey,
        detail: 'This operation was removed entirely.',
      });
      continue;
    }

    findings.push(...compareParameters(operationKey, baseOp, newOp));
    findings.push(...compareRequestBody(operationKey, baseDoc, baseOp, newDoc, newOp));
    findings.push(...compareResponses(operationKey, baseDoc, baseOp, newDoc, newOp));
  }

  return findings;
}

// PowerShell's `>` redirection (verify.ps1 uses it for `git show HEAD:... >
// $previousDocPath`) writes a UTF-8 byte-order-mark that Node's JSON.parse
// treats as an invalid leading token -- the same class of PowerShell-
// encoding trap this codebase has already hit more than once (see
// docs/ai/README.md). Stripped defensively here regardless of which tool
// produced the file, rather than relying on every caller to know not to use
// `>` in PowerShell.
function stripBom(text) {
  return text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
}

export async function loadDoc(path) {
  return JSON.parse(stripBom(await readFile(path, 'utf8')));
}

async function main() {
  const [baseDocPath, newDocPath] = process.argv.slice(2);
  if (!baseDocPath || !newDocPath) {
    console.error('Usage: node infra/scripts/check-breaking-changes.mjs <baseDocPath> <newDocPath>');
    process.exitCode = 2;
    return;
  }

  const baseDoc = await loadDoc(baseDocPath);
  const newDoc = await loadDoc(newDocPath);
  const findings = findBreakingChanges(baseDoc, newDoc);

  if (findings.length === 0) {
    console.log('No breaking changes detected (see this script\'s own header for what is and isn\'t covered).');
    return;
  }

  console.error(`${findings.length} potential breaking change(s) detected:\n`);
  for (const finding of findings) {
    console.error(`  [${finding.rule}] ${finding.operation}\n    ${finding.detail}`);
  }
  console.error(
    '\nIf this is intentional (e.g. a deliberate /api/v2), see docs/architecture/api-contract.md §3. ' +
      'Otherwise, this is a real breaking change to a released contract (hard rule 11).',
  );
  process.exitCode = 1;
}

// Only run as a CLI entry point -- not when imported for its exported
// functions (check-breaking-changes.test.mjs).
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  await main();
}
