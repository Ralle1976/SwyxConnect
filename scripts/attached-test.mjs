#!/usr/bin/env node
/**
 * Attached-Mode Test — connects to CLMgr's existing SwyxIt! session.
 *
 * When SwyxIt! is running and logged in, CLMgr already has a valid session.
 * The bridge creates the COM object and can use it directly (no RegisterUserEx needed).
 *
 * Usage: node scripts/attached-test.mjs
 *
 * Tests: getStatus → getLines → getColleaguePresence → getCallHistory → getVoicemails → getPresence
 */
import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const bridgeDll = join(__dirname, '..', 'bridge', 'SwyxStandalone', 'bin', 'x86', 'Release', 'net8.0-windows', 'SwyxStandalone.dll');
const DOTNET_PATH = 'C:\\Program Files (x86)\\dotnet\\dotnet.exe';

const results = { pass: 0, fail: 0, errors: [] };
function ok(name) { console.log(`  ✅ ${name}`); results.pass++; }
function fail(name, detail) { console.log(`  ❌ ${name}: ${detail}`); results.fail++; results.errors.push(`${name}: ${detail}`); }

let nextId = 1;
const pending = new Map();

console.log('=== SwyxStandalone Attached-Mode Test ===');
console.log('(SwyxIt! must be running and logged in)\n');

const proc = spawn(DOTNET_PATH, [bridgeDll], {
  stdio: ['pipe', 'pipe', 'pipe'],
  windowsHide: true,
});

const stdoutRl = createInterface({ input: proc.stdout });
const stderrRl = createInterface({ input: proc.stderr });
stderrRl.on('line', (line) => { if (line.trim()) console.error(`  [bridge] ${line}`); });
stdoutRl.on('line', (line) => {
  try { handleJsonRpc(JSON.parse(line)); } catch { }
});

function handleJsonRpc(msg) {
  if (msg.jsonrpc !== '2.0') return;
  if (msg.id !== undefined && pending.has(msg.id)) {
    pending.get(msg.id).resolve(msg);
    pending.delete(msg.id);
  } else if (msg.id === undefined || msg.id === null) {
    if (msg.method === 'heartbeat') return; // silent
    console.log(`  [event] ${msg.method}: ${JSON.stringify(msg.params)?.substring(0, 120)}`);
  }
}

function sendRequest(method, params) {
  const id = nextId++;
  const req = { jsonrpc: '2.0', id, method };
  if (params) req.params = params;
  return new Promise((resolve) => {
    pending.set(id, { resolve });
    proc.stdin.write(JSON.stringify(req) + '\n', 'utf8');
  });
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }
function timeout(ms, name) {
  return new Promise((_, reject) => setTimeout(() => reject(new Error(`${name} timed out`)), ms));
}

async function runTests() {
  console.log('--- Starting bridge ---');
  await sleep(2000);

  // getStatus — in attached mode, connected=true but loggedIn=false
  // (the bridge doesn't know about SwyxIt!'s session, but COM calls will work)
  console.log('\n--- Test 1: getStatus ---');
  try {
    const resp = await Promise.race([sendRequest('getStatus'), timeout(5000, 'getStatus')]);
    if (resp.error) fail('getStatus', resp.error.message);
    else ok(`getStatus → connected=${resp.result.connected}, loggedIn=${resp.result.loggedIn}, lines=${resp.result.lineCount}`);
  } catch (e) { fail('getStatus', e.message); }

  // setLines — should work since COM is available
  console.log('\n--- Test 2: setLines(4) ---');
  try {
    const resp = await Promise.race([sendRequest('setLines', { count: 4 }), timeout(5000, 'setLines')]);
    if (resp.error) fail('setLines', resp.error.message);
    else ok(`setLines(4) → ok=${resp.result.ok}`);
  } catch (e) { fail('setLines', e.message); }

  // getLines — THE KEY TEST: should return real line data
  console.log('\n--- Test 3: getLines (should return real lines) ---');
  try {
    const resp = await Promise.race([sendRequest('getLines'), timeout(10000, 'getLines')]);
    if (resp.error) fail('getLines', resp.error.message);
    else if (resp.result?.lines && Array.isArray(resp.result.lines)) {
      const lines = resp.result.lines;
      ok(`getLines → ${lines.length} lines`);
      lines.forEach(l => {
        console.log(`    Line ${l.id}: state=${l.state}, caller=${l.callerName || '—'} (${l.callerNumber || '—'}), selected=${l.isSelected}`);
      });
    } else fail('getLines', `unexpected: ${JSON.stringify(resp.result)?.substring(0, 100)}`);
  } catch (e) { fail('getLines', e.message); }

  // getColleaguePresence — THE MAIN BUG FIX TEST
  console.log('\n--- Test 4: getColleaguePresence (main bug fix!) ---');
  try {
    const resp = await Promise.race([sendRequest('getColleaguePresence'), timeout(15000, 'getColleaguePresence')]);
    if (resp.error) fail('getColleaguePresence', resp.error.message);
    else {
      const colleagues = resp.result?.colleagues;
      if (Array.isArray(colleagues)) {
        ok(`getColleaguePresence → ${colleagues.length} colleagues`);
        colleagues.slice(0, 15).forEach(c => {
          console.log(`    ${c.name}: status=${c.status}${c.statusText ? ' ('+c.statusText+')' : ''} ext=${c.extension || '—'}`);
        });
        if (colleagues.length > 15) console.log(`    ... and ${colleagues.length - 15} more`);
      } else fail('getColleaguePresence', `unexpected: ${JSON.stringify(resp.result)?.substring(0, 100)}`);
    }
  } catch (e) { fail('getColleaguePresence', e.message); }

  // getCallHistory
  console.log('\n--- Test 5: getCallHistory ---');
  try {
    const resp = await Promise.race([sendRequest('getCallHistory'), timeout(15000, 'getCallHistory')]);
    if (resp.error) fail('getCallHistory', resp.error.message);
    else if (Array.isArray(resp.result)) {
      ok(`getCallHistory → ${resp.result.length} entries`);
      resp.result.slice(0, 5).forEach(h => {
        const date = h.timestamp ? new Date(h.timestamp * 1000).toLocaleString('de-DE') : '—';
        console.log(`    ${h.callerName || '—'} (${h.callerNumber || '—'}): ${h.direction} at ${date}`);
      });
    } else fail('getCallHistory', `unexpected: ${JSON.stringify(resp.result)?.substring(0, 100)}`);
  } catch (e) { fail('getCallHistory', e.message); }

  // getVoicemails
  console.log('\n--- Test 6: getVoicemails ---');
  try {
    const resp = await Promise.race([sendRequest('getVoicemails'), timeout(15000, 'getVoicemails')]);
    if (resp.error) fail('getVoicemails', resp.error.message);
    else {
      const vm = resp.result;
      ok(`getVoicemails → ${(vm?.messages || []).length} messages, ${vm?.newCount || 0} new`);
    }
  } catch (e) { fail('getVoicemails', e.message); }

  // getPresence (own status)
  console.log('\n--- Test 7: getPresence (own) ---');
  try {
    const resp = await Promise.race([sendRequest('getPresence'), timeout(5000, 'getPresence')]);
    if (resp.error) fail('getPresence', resp.error.message);
    else ok(`getPresence → status=${resp.result.status}`);
  } catch (e) { fail('getPresence', e.message); }

  // getConnectionInfo
  console.log('\n--- Test 8: getConnectionInfo ---');
  try {
    const resp = await Promise.race([sendRequest('getConnectionInfo'), timeout(5000, 'getConnectionInfo')]);
    if (resp.error) fail('getConnectionInfo', resp.error.message);
    else ok(`getConnectionInfo → ${JSON.stringify(resp.result)}`);
  } catch (e) { fail('getConnectionInfo', e.message); }

  cleanup(results.fail > 0 ? 1 : 0);
}

function cleanup(exitCode) {
  console.log('\n=== Attached-Mode Test Summary ===');
  console.log(`  Passed: ${results.pass}`);
  console.log(`  Failed: ${results.fail}`);
  if (results.errors.length > 0) {
    console.log('  Errors:');
    results.errors.forEach(e => console.log(`    - ${e}`));
  }
  proc.kill();
  setTimeout(() => process.exit(exitCode), 1000);
}

proc.on('error', (err) => { console.error('Process error:', err.message); process.exit(1); });
setTimeout(runTests, 1000);
