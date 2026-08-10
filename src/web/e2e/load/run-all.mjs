// QA-13 — runs both load tests back to back and exits non-zero if either
// failed. See docs/development/load-testing.md.

import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const dir = path.dirname(fileURLToPath(import.meta.url));

function run(script) {
  return new Promise((resolve) => {
    const child = spawn(process.execPath, [path.join(dir, script)], { stdio: 'inherit', env: process.env });
    child.on('exit', (code) => resolve(code ?? 1));
  });
}

const readExitCode = await run('read-load.mjs');
const writeExitCode = await run('write-load.mjs');

if (readExitCode !== 0 || writeExitCode !== 0) {
  console.error('\nOne or more load tests failed.');
  process.exit(1);
}

console.log('\nAll load tests passed.');
