#!/usr/bin/env node
// Regenerates docs/openapi/v1.json from a running API (API-14).
//
// Node, not PowerShell, is what writes the file. Windows PowerShell 5.1's
// ConvertTo-Json has no standard-indent option -- it produces a "staircase"
// format where each nesting level's indent grows by the accumulated length
// of every enclosing key, not a fixed 2 spaces per level. For a document
// this deeply nested that bloated a semantically-unchanged ~120KB document
// to ~540KB, and its indentation never matches CI's own regeneration step
// (`jq 'del(.servers)'` in .github/workflows/ci.yml, which uses jq's
// ordinary 2-space pretty-print). The result: every PowerShell-regenerated
// commit failed the openapi-drift CI job even when the API contract hadn't
// actually changed at all -- confirmed by diffing a docs-only commit's own
// "drift" and finding it semantically identical to what was already
// committed, just reformatted. Node's JSON.stringify(obj, null, 2) matches
// jq's own default formatting because both converge on the same ordinary
// 2-space convention .NET's own OpenApi middleware already emits -- the
// only real transformation needed is deleting `servers`.
//
// Usage: node infra/scripts/regenerate-openapi.mjs [apiBaseUrl] [outputPath]
// apiBaseUrl defaults to http://localhost:5216, the "http" launch profile.
// outputPath defaults to docs/openapi/v1.json; verify.ps1 passes a temp
// path instead, so a drift *check* never leaves an unstaged working-tree
// change behind the way overwriting the real file in place would.

import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const apiBaseUrl = process.argv[2] ?? 'http://localhost:5216';
const outputPath = process.argv[3]
  ? pathToFileURL(resolve(process.argv[3]))
  : new URL('../../docs/openapi/v1.json', import.meta.url);

const response = await fetch(`${apiBaseUrl}/openapi/v1.json`);
if (!response.ok) {
  throw new Error(`GET ${apiBaseUrl}/openapi/v1.json failed: ${response.status} ${response.statusText}`);
}

const doc = await response.json();

// ASP.NET Core fills servers[] from whatever host generated the document --
// localhost:<port> locally, something else everywhere else -- so committing
// it would make every regeneration a noisy diff implying a URL this
// document doesn't actually promise. See docs/openapi/README.md.
delete doc.servers;

await writeFile(outputPath, JSON.stringify(doc, null, 2) + '\n', 'utf8');

console.log(`Wrote ${fileURLToPath(outputPath)}`);
