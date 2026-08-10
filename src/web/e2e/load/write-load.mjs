// QA-13 — write-path load test: N concurrent "terminals" cycling a real
// service flow (open table -> ring up an item -> close, issuing a real
// fiscal document -> clear the table) for a fixed duration.
//
// Unlike read-load.mjs, this can't just be handed to a generic HTTP
// benchmarking tool: each step depends on the previous one's response (the
// order id from opening, reused to add a line and to close), and the
// seeded floor plan only has 16 tables -- hammering it with more concurrent
// "terminals" than there are tables would just produce 409 table-conflict
// noise that looks like a failure but is really a fixture-size limit, not
// a performance one. Each terminal claims exactly one table for the whole
// run and cycles it, the same way one real terminal serves one table
// through a shift.
//
// See docs/development/load-testing.md for what this does and doesn't prove.

const apiBaseUrl = process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216';
const apiBase = `${apiBaseUrl}/api/v1`;
const durationSeconds = Number(process.env.LOAD_DURATION ?? 10);
const requestedTerminals = Number(process.env.LOAD_TERMINALS ?? 10);

function idempotencyKey() {
  return crypto.randomUUID();
}

/** Every call returns { ok, status, elapsedMs, body } -- body is always the parsed JSON, on success or failure. */
async function api(method, path, jsonBody) {
  const headers = { Accept: 'application/json' };
  if (jsonBody !== undefined) headers['Content-Type'] = 'application/json';
  if (method !== 'GET') headers['Idempotency-Key'] = idempotencyKey();

  const start = performance.now();
  const response = await fetch(`${apiBase}${path}`, {
    method,
    headers,
    body: jsonBody === undefined ? undefined : JSON.stringify(jsonBody),
  });
  const elapsedMs = performance.now() - start;

  let body = null;
  if (response.status !== 204) {
    body = await response.json().catch(() => null);
  }

  return { ok: response.ok, status: response.status, elapsedMs, body };
}

function percentile(sortedValues, p) {
  if (sortedValues.length === 0) return 0;
  const index = Math.min(sortedValues.length - 1, Math.ceil((p / 100) * sortedValues.length) - 1);
  return sortedValues[index];
}

function summarize(name, samples, failureCount) {
  const sorted = [...samples].sort((a, b) => a - b);
  console.log(
    `  ${name.padEnd(12)} n=${String(samples.length).padStart(5)} failures=${String(failureCount).padStart(3)} ` +
      `p50=${percentile(sorted, 50).toFixed(0)}ms p95=${percentile(sorted, 95).toFixed(0)}ms p99=${percentile(sorted, 99).toFixed(0)}ms`,
  );
}

async function main() {
  const menu = await (await fetch(`${apiBase}/menu`)).json();
  const item = menu.flatMap((c) => c.items).find((i) => i.modifierGroups.length === 0);
  if (!item) throw new Error('No modifier-free menu item found in the seeded menu.');

  const floor = await (await fetch(`${apiBase}/floor`)).json();
  const freeTables = floor.flatMap((r) => r.tables).filter((t) => t.state === 'Free');

  const terminalCount = Math.min(requestedTerminals, freeTables.length);
  if (terminalCount === 0) {
    throw new Error('No free tables available to run the write-load test -- clear the floor plan first.');
  }
  if (terminalCount < requestedTerminals) {
    console.log(
      `Only ${freeTables.length} free tables available -- running ${terminalCount} terminals instead of the requested ${requestedTerminals}.`,
    );
  }

  console.log(`\n=== Write-path load: ${terminalCount} terminals, ${durationSeconds}s, item "${item.name}" ===`);

  const latencies = { open: [], addLine: [], close: [], clear: [] };
  const failures = { open: 0, addLine: 0, close: 0, clear: 0 };
  let cyclesCompleted = 0;
  const endAt = performance.now() + durationSeconds * 1000;

  async function cycleTerminal(tableId) {
    while (performance.now() < endAt) {
      const opened = await api('POST', '/orders', { tableId, coverCount: 2 });
      latencies.open.push(opened.elapsedMs);
      if (!opened.ok) {
        failures.open++;
        // Table may still be mid-clear from this same terminal's previous
        // cycle under real concurrency elsewhere -- brief backoff, not a
        // hard stop.
        await new Promise((resolve) => setTimeout(resolve, 20));
        continue;
      }
      const orderId = opened.body.id;

      const added = await api('POST', `/orders/${orderId}/lines`, {
        menuItemId: item.id,
        quantity: 1,
        selectedModifierIds: [],
      });
      latencies.addLine.push(added.elapsedMs);
      if (!added.ok) failures.addLine++;

      const closed = await api('POST', `/orders/${orderId}/close`);
      latencies.close.push(closed.elapsedMs);
      if (!closed.ok) {
        failures.close++;
        continue;
      }

      const cleared = await api('POST', `/tables/${tableId}/clear`);
      latencies.clear.push(cleared.elapsedMs);
      if (!cleared.ok) {
        failures.clear++;
        continue;
      }

      cyclesCompleted++;
    }
  }

  await Promise.all(freeTables.slice(0, terminalCount).map((t) => cycleTerminal(t.id)));

  console.log(`  ${cyclesCompleted} full open->add->close->clear cycles completed\n`);
  summarize('open', latencies.open, failures.open);
  summarize('addLine', latencies.addLine, failures.addLine);
  summarize('close', latencies.close, failures.close);
  summarize('clear', latencies.clear, failures.clear);

  const totalFailures = failures.open + failures.addLine + failures.close + failures.clear;
  const p95Target = 200;
  const worstP95 = Math.max(
    ...Object.values(latencies).map((samples) => percentile([...samples].sort((a, b) => a - b), 95)),
  );

  if (totalFailures > 0) {
    console.error(`\nFAIL -- ${totalFailures} request(s) failed across the write path.`);
    process.exitCode = 1;
    return;
  }

  if (worstP95 > p95Target) {
    console.error(`\nFAIL -- worst p95 (${worstP95.toFixed(0)}ms) exceeded the ${p95Target}ms target.`);
    process.exitCode = 1;
    return;
  }

  console.log(`\nPASS -- ${terminalCount} concurrent terminals, all operations stayed under the ${p95Target}ms p95 target.`);
}

await main();
