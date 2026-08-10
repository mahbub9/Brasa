// QA-13 — read-path load test. GET /menu and GET /floor are what every
// terminal polls constantly through a shift (menu render, floor-plan
// refresh) — unlike the write path below, there's no correlated state to
// track between requests, so autocannon's own connection pool handles this
// directly rather than a hand-rolled harness.
//
// Target: p95 < 200ms (docs/product/plan.md's own "Load, before launch"
// verification bullet). See docs/development/load-testing.md for what this
// does and doesn't prove.

import autocannon from 'autocannon';

const apiBaseUrl = process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216';
const connections = Number(process.env.LOAD_CONNECTIONS ?? 50);
const durationSeconds = Number(process.env.LOAD_DURATION ?? 10);

// autocannon's built-in latency buckets skip straight from p90 to p97_5 --
// there is no exact p95. p97_5 is used as a deliberately conservative
// stand-in: it's a stricter (higher) percentile than p95, so a result that
// passes the target at p97_5 would certainly pass at the real p95 too.
// Labelled "p95 (p97.5)" below rather than silently claiming an exact p95
// this tool doesn't report.
function p95Proxy(result) {
  return result.latency.p97_5 ?? result.latency.p99;
}

async function run(name, path) {
  const result = await autocannon({
    url: `${apiBaseUrl}${path}`,
    connections,
    duration: durationSeconds,
    headers: { Accept: 'application/json' },
  });

  console.log(`\n=== ${name} (${path}) ===`);
  console.log(`  requests: ${result.requests.total}, errors: ${result.errors}, non-2xx: ${result.non2xx}`);
  console.log(`  latency (ms) — p50: ${result.latency.p50}, p95 (p97.5): ${p95Proxy(result)}, p99: ${result.latency.p99}, max: ${result.latency.max}`);
  console.log(`  throughput: ${result.requests.average.toFixed(1)} req/s average`);

  return result;
}

const menuResult = await run('GET /menu', '/api/v1/menu');
const floorResult = await run('GET /floor', '/api/v1/floor');

const p95Target = 200;
const failures = [menuResult, floorResult].filter((r) => p95Proxy(r) > p95Target);

if (menuResult.errors > 0 || menuResult.non2xx > 0 || floorResult.errors > 0 || floorResult.non2xx > 0) {
  console.error('\nFAIL — one or more requests errored or returned a non-2xx status.');
  process.exit(1);
}

if (failures.length > 0) {
  console.error(`\nFAIL — p95 latency exceeded the ${p95Target}ms target for ${failures.length} endpoint(s).`);
  process.exit(1);
}

console.log(`\nPASS — both endpoints stayed under the ${p95Target}ms p95 target with ${connections} concurrent connections.`);
