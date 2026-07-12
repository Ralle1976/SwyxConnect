#!/usr/bin/env node
/**
 * Smoke-Test for SwyxStandalone Bridge (no login required).
 *
 * Verifies:
 *   1. Bridge process starts and emits bridgeState:ready
 *   2. Heartbeat events arrive within timeout
 *   3. JSON-RPC ping/getStatus/setLines/getLines work
 *   4. Method-not-found returns proper error code
 *
 * Does NOT test: login (needs credentials), dial (needs live call), presence (needs session).
 * Those are Phase 4.1b — requires user-provided Swyx credentials.
 *
 * Usage: node scripts/smoke-test.mjs
 */
import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Find bridge DLL — UseAppHost=false means we run via dotnet
const bridgeDll = join(__dirname, '..', 'bridge', 'SwyxStandalone', 'bin', 'x86', 'Release', 'net8.0-windows', 'SwyxStandalone.dll');

const DOTNET_PATH = 'C:\\Program Files (x86)\\dotnet\\dotnet.exe';

const results = { pass: 0, fail: 0, errors: [] };
function ok(name) { console.log(`  ✅ ${name}`); results.pass++; }
function fail(name, detail) { console.log(`  ❌ ${name}: ${detail}`); results.fail++; results.errors.push(`${name}: ${detail}`); }

let nextId = 1;
const pending = new Map();
let gotReady = false;
let gotHeartbeat = false;

console.log('=== SwyxStandalone Smoke Test ===\n');
console.log(`Bridge DLL: ${bridgeDll}\n`);

// Spawn via dotnet (UseAppHost=false)
const proc = spawn(DOTNET_PATH, [bridgeDll], {
  stdio: ['pipe', 'pipe', 'pipe'],
  windowsHide: true,
});

const stdoutRl = createInterface({ input: proc.stdout });
const stderrRl = createInterface({ input: proc.stderr });

stderrRl.on('line', (line) => {
  // Bridge logs to stderr — show for debugging
  if (line.trim()) console.error(`  [bridge stderr] ${line}`);
});

stdoutRl.on('line', (line) => {
  try {
    const msg = JSON.parse(line);
    handleJsonRpc(msg);
  } catch {
    // Non-JSON stdout — ignore
  }
});

function handleJsonRpc(msg) {
  if (msg.jsonrpc !== '2.0') return;

  // Response to a request
  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve, name } = pending.get(msg.id);
    pending.delete(msg.id);
    resolve(msg);
    return;
  }

  // Notification (event, no id)
  if (msg.id === undefined || msg.id === null) {
    if (msg.method === 'bridgeState') {
      const state = msg.params?.state;
      console.log(`  [event] bridgeState: ${state}`);
      if (state === 'ready') gotReady = true;
    } else if (msg.method === 'heartbeat') {
      if (!gotHeartbeat) {
        gotHeartbeat = true;
        console.log(`  [event] heartbeat: uptime=${msg.params?.uptime}s`);
      }
    } else {
      console.log(`  [event] ${msg.method}`);
    }
  }
}

function sendRequest(method, params) {
  const id = nextId++;
  const req = { jsonrpc: '2.0', id, method };
  if (params) req.params = params;
  return new Promise((resolve) => {
    pending.set(id, { resolve, name: method });
    proc.stdin.write(JSON.stringify(req) + '\n', 'utf8');
  });
}

proc.on('error', (err) => {
  fail('process spawn', err.message);
  process.exit(1);
});

proc.on('exit', (code) => {
  console.log(`\nBridge exited with code ${code}`);
});

// Run tests after bridge has time to start
async function runTests() {
  // Wait for bridge to be ready (max 10s)
  console.log('\n--- Test 1: Bridge starts and emits ready ---');
  for (let i = 0; i < 20; i++) {
    if (gotReady) break;
    await sleep(500);
  }
  if (gotReady) ok('bridgeState:ready received');
  else fail('bridge ready timeout', 'no ready event within 10s');

  // Wait for heartbeat
  console.log('\n--- Test 2: Heartbeat ---');
  for (let i = 0; i < 12; i++) {
    if (gotHeartbeat) break;
    await sleep(500);
  }
  if (gotHeartbeat) ok('heartbeat received within timeout');
  else fail('heartbeat timeout', 'no heartbeat within 6s');

  // ping
  console.log('\n--- Test 3: ping ---');
  try {
    const pingResp = await Promise.race([
      sendRequest('ping'),
      timeout(5000, 'ping'),
    ]);
    if (pingResp.error) fail('ping', pingResp.error.message);
    else if (pingResp.result?.pong === true) ok('ping → pong=true');
    else fail('ping', `unexpected result: ${JSON.stringify(pingResp.result)}`);
  } catch (e) { fail('ping', e.message); }

  // getStatus (should show loggedIn: false since no login)
  console.log('\n--- Test 4: getStatus (pre-login) ---');
  try {
    const statusResp = await Promise.race([
      sendRequest('getStatus'),
      timeout(5000, 'getStatus'),
    ]);
    if (statusResp.error) fail('getStatus', statusResp.error.message);
    else {
      const s = statusResp.result;
      if (s?.loggedIn === false) ok(`getStatus → connected=${s.connected}, loggedIn=false`);
      else fail('getStatus', `expected loggedIn=false, got: ${JSON.stringify(s)}`);
    }
  } catch (e) { fail('getStatus', e.message); }

  // setLines
  console.log('\n--- Test 5: setLines(2) ---');
  try {
    const linesResp = await Promise.race([
      sendRequest('setLines', { count: 2 }),
      timeout(5000, 'setLines'),
    ]);
    if (linesResp.error) fail('setLines', linesResp.error.message);
    else if (linesResp.result?.ok === true) ok(`setLines(2) → ok=true`);
    else fail('setLines', `unexpected: ${JSON.stringify(linesResp.result)}`);
  } catch (e) { fail('setLines', e.message); }

  // getLines (may fail since not logged in — that's OK, just check structure)
  console.log('\n--- Test 6: getLines (structure check) ---');
  try {
    const glResp = await Promise.race([
      sendRequest('getLines'),
      timeout(5000, 'getLines'),
    ]);
    if (glResp.error) {
      // COM error before login is expected
      ok(`getLines → error (expected pre-login): ${glResp.error.message?.substring(0, 60)}`);
    } else if (glResp.result?.lines && Array.isArray(glResp.result.lines)) {
      ok(`getLines → ${glResp.result.lines.length} lines returned`);
    } else {
      fail('getLines', `unexpected structure: ${JSON.stringify(glResp.result)?.substring(0, 100)}`);
    }
  } catch (e) { fail('getLines', e.message); }

  // unknown method → MethodNotFound error
  console.log('\n--- Test 7: unknown method → MethodNotFound ---');
  try {
    const unkResp = await Promise.race([
      sendRequest('nonexistentMethod'),
      timeout(5000, 'unknown'),
    ]);
    if (unkResp.error && unkResp.error.code === -32601) ok('unknown method → -32601 MethodNotFound');
    else fail('unknown method', `expected -32601, got: ${JSON.stringify(unkResp)}`);
  } catch (e) { fail('unknown method', e.message); }

  // Summary
  console.log('\n=== Smoke Test Summary ===');
  console.log(`  Passed: ${results.pass}`);
  console.log(`  Failed: ${results.fail}`);
  if (results.errors.length > 0) {
    console.log('  Errors:');
    results.errors.forEach(e => console.log(`    - ${e}`));
  }
  console.log('');

  // Cleanup
  proc.kill();
  process.exit(results.fail > 0 ? 1 : 0);
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }
function timeout(ms, name) {
  return new Promise((_, reject) => setTimeout(() => reject(new Error(`${name} timed out after ${ms}ms`)), ms));
}

// Start tests after bridge initializes
setTimeout(runTests, 2000);
