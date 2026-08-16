// Run with: node --test infra/scripts/check-breaking-changes.test.mjs
// Node's own built-in test runner (18+) -- no new dependency for one script,
// consistent with this codebase's own minimal-footprint instinct elsewhere
// (ExcelDataReader over ClosedXML, no icon-font library, etc.).

import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { findBreakingChanges, loadDoc } from './check-breaking-changes.mjs';

/** A single GET operation with no parameters/body, for tests that only care about operation presence. */
function minimalDoc(overrides = {}) {
  return {
    openapi: '3.1.1',
    paths: {
      '/api/v1/widgets': {
        get: {
          operationId: 'GetWidgets',
          responses: { 200: { description: 'OK' } },
          ...overrides,
        },
      },
    },
    components: { schemas: {} },
    ...overrides.rootOverrides,
  };
}

test('an operation removed entirely is flagged', () => {
  const base = minimalDoc();
  const next = { openapi: '3.1.1', paths: {}, components: { schemas: {} } };

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'removed-operation');
  assert.equal(findings[0].operation, 'GET /api/v1/widgets');
});

test('a brand new operation is not flagged', () => {
  const base = { openapi: '3.1.1', paths: {}, components: { schemas: {} } };
  const next = minimalDoc();

  assert.deepEqual(findBreakingChanges(base, next), []);
});

test('a new required query parameter is flagged', () => {
  const base = minimalDoc({ parameters: [] });
  const next = minimalDoc({
    parameters: [{ name: 'siteId', in: 'query', required: true, schema: { type: 'string' } }],
  });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'new-required-parameter');
});

test('a new OPTIONAL query parameter is not flagged', () => {
  const base = minimalDoc({ parameters: [] });
  const next = minimalDoc({
    parameters: [{ name: 'siteId', in: 'query', required: false, schema: { type: 'string' } }],
  });

  assert.deepEqual(findBreakingChanges(base, next), []);
});

test('an optional parameter becoming required is flagged', () => {
  const base = minimalDoc({
    parameters: [{ name: 'take', in: 'query', required: false, schema: { type: 'integer' } }],
  });
  const next = minimalDoc({
    parameters: [{ name: 'take', in: 'query', required: true, schema: { type: 'integer' } }],
  });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'parameter-became-required');
});

test('removing a parameter entirely is not flagged (extra params an old client sends are harmless)', () => {
  const base = minimalDoc({
    parameters: [{ name: 'take', in: 'query', required: false, schema: { type: 'integer' } }],
  });
  const next = minimalDoc({ parameters: [] });

  assert.deepEqual(findBreakingChanges(base, next), []);
});

function docWithRequestBody(requestSchema, { bodyRequired = true } = {}) {
  return {
    openapi: '3.1.1',
    paths: {
      '/api/v1/widgets': {
        post: {
          operationId: 'CreateWidget',
          requestBody: {
            required: bodyRequired,
            content: { 'application/json': { schema: { $ref: '#/components/schemas/CreateWidgetRequest' } } },
          },
          responses: { 200: { description: 'OK' } },
        },
      },
    },
    components: { schemas: { CreateWidgetRequest: requestSchema } },
  };
}

test('a request-body property that is newly required is flagged', () => {
  const base = docWithRequestBody({ type: 'object', required: ['name'], properties: { name: {}, note: {} } });
  const next = docWithRequestBody({ type: 'object', required: ['name', 'note'], properties: { name: {}, note: {} } });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'new-required-request-property');
  assert.match(findings[0].detail, /"note"/);
});

test('a brand new OPTIONAL request-body property is not flagged', () => {
  const base = docWithRequestBody({ type: 'object', required: ['name'], properties: { name: {} } });
  const next = docWithRequestBody({ type: 'object', required: ['name'], properties: { name: {}, note: {} } });

  assert.deepEqual(findBreakingChanges(base, next), []);
});

test('removing an optional request-body property is not flagged', () => {
  const base = docWithRequestBody({ type: 'object', required: ['name'], properties: { name: {}, note: {} } });
  const next = docWithRequestBody({ type: 'object', required: ['name'], properties: { name: {} } });

  assert.deepEqual(findBreakingChanges(base, next), []);
});

test('a request body becoming required where none existed before is flagged', () => {
  const base = {
    openapi: '3.1.1',
    paths: { '/api/v1/widgets': { post: { operationId: 'CreateWidget', responses: { 200: { description: 'OK' } } } } },
    components: { schemas: {} },
  };
  const next = docWithRequestBody({ type: 'object', properties: {} }, { bodyRequired: true });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'request-body-became-required');
});

function docWithResponse(status, responseSchema) {
  return {
    openapi: '3.1.1',
    paths: {
      '/api/v1/widgets/{id}': {
        get: {
          operationId: 'GetWidget',
          responses: {
            [status]: {
              description: 'OK',
              content: { 'application/json': { schema: { $ref: '#/components/schemas/WidgetDto' } } },
            },
          },
        },
      },
    },
    components: { schemas: { WidgetDto: responseSchema } },
  };
}

test('a response property removed is flagged', () => {
  const base = docWithResponse('200', {
    type: 'object',
    required: ['id', 'legacyCode'],
    properties: { id: {}, legacyCode: {} },
  });
  const next = docWithResponse('200', { type: 'object', required: ['id'], properties: { id: {} } });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'response-property-removed');
  assert.match(findings[0].detail, /"legacyCode"/);
});

test('a response property added is not flagged', () => {
  const base = docWithResponse('200', { type: 'object', required: ['id'], properties: { id: {} } });
  const next = docWithResponse('200', {
    type: 'object',
    required: ['id', 'newField'],
    properties: { id: {}, newField: {} },
  });

  assert.deepEqual(findBreakingChanges(base, next), []);
});

test('a success response status code removed is flagged', () => {
  const base = {
    openapi: '3.1.1',
    paths: {
      '/api/v1/widgets': {
        post: { operationId: 'CreateWidget', responses: { 200: { description: 'OK' }, 201: { description: 'Created' } } },
      },
    },
    components: { schemas: {} },
  };
  const next = {
    openapi: '3.1.1',
    paths: { '/api/v1/widgets': { post: { operationId: 'CreateWidget', responses: { 201: { description: 'Created' } } } } },
    components: { schemas: {} },
  };

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'response-status-removed');
  assert.match(findings[0].detail, /200/);
});

test('a nullable $ref (oneOf [null, $ref]) response schema still resolves for property comparison', () => {
  const doc = (schema) => ({
    openapi: '3.1.1',
    paths: {
      '/api/v1/widgets/{id}': {
        get: {
          operationId: 'GetWidget',
          responses: {
            200: {
              description: 'OK',
              content: {
                'application/json': {
                  schema: { oneOf: [{ type: 'null' }, { $ref: '#/components/schemas/WidgetDto' }] },
                },
              },
            },
          },
        },
      },
    },
    components: { schemas: { WidgetDto: schema } },
  });

  const base = doc({ type: 'object', required: ['id', 'code'], properties: { id: {}, code: {} } });
  const next = doc({ type: 'object', required: ['id'], properties: { id: {} } });

  const findings = findBreakingChanges(base, next);

  assert.equal(findings.length, 1);
  assert.equal(findings[0].rule, 'response-property-removed');
});

test('loadDoc strips a leading UTF-8 BOM (PowerShell\'s `>` redirection writes one, verify.ps1 uses exactly this)', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'brasa-openapi-bom-'));
  const path = join(dir, 'with-bom.json');
  try {
    await writeFile(path, '﻿' + JSON.stringify({ openapi: '3.1.1', paths: {}, components: { schemas: {} } }), 'utf8');
    const doc = await loadDoc(path);
    assert.equal(doc.openapi, '3.1.1');
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('comparing the real committed OpenAPI document against itself never finds a breaking change', async () => {
  const raw = await readFile(new URL('../../docs/openapi/v1.json', import.meta.url), 'utf8');
  const doc = JSON.parse(raw);

  // A guard against false positives on real, non-synthetic data: every rule
  // above must agree that "nothing changed" really does mean zero findings
  // across all 68 real route mappings, not just the small hand-built
  // fixtures elsewhere in this file.
  assert.deepEqual(findBreakingChanges(doc, doc), []);
});
