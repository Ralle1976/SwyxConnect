#!/usr/bin/env node
/**
 * Login-Test for SwyxStandalone Bridge — requires Swyx credentials.
 *
 * Usage: node scripts/login-test.mjs <server> <username> <password>
 * Example: node scripts/login-test.mjs [SWYX_SERVER] "[USER_EMAIL]" "yourpassword"
 *
 * Tests: login → getStatus → getLines → getColleaguePresence → getCallHistory → getVoicemails
 * Does NOT test: dial (live call), hangup, hold, transfer — those need a live call partner.
 *
 * SECURITY: Password is passed as CLI arg (process arg, not persisted). The bridge
 * receives it in-memory via JSON-RPC. It is NOT written to disk, logs, or commits.
 * If you prefer, run this in a private terminal and clear history afterwards.
 */
import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const bridgeDll = join(__dirname, '..', 'bridge', 'SwyxStandalone', 'bin', 'x86', 'Release', 'net8.0-windows', 'SwyxStandalone.dll');
const DOTNET_PATH = 'C:\\Program Files (x86)\\dotnet\\dotnet.exe';

// Parse args
const [server, username, password] = process.argv.slice(2);
if (!server || !username) {
  console.error('Usage: node scripts/login-test.mjs <server> <username> [password]');
  console.error('Example: node scripts/login-test.mjs [SWYX_SERVER] "user@domain" "secret"');
  process.exit(1);
}
const pass = password ?? '';

const results = { pass: 0, fail: 0, errors: [] };
function ok(name) { console.log(`  ✅ ${name}`); results.pass++; }
function fail(name, detail) { console.log(`  ❌ ${name}: ${detail}`); results.fail++; results.errors.push(`${name}: ${detail}`); }

let nextId = 1;
const pending = new Map();
let loginSucceeded = false;
let loginFailed = false;
let loginErrorCode = null;

console.log('=== SwyxStandalone Login Test ===\n');
console.log(`Server: ${server}`);
console.log(`User: ${username}\n`);

const proc = spawn(DOTNET_PATH, [bridgeDll], {
  stdio: ['pipe', 'pipe', 'pipe'],
  windowsHide: true,
});

const stdoutRl = createInterface({ input: proc.stdout });
const stderrRl = createInterface({ input: proc.stderr });

stderrRl.on('line', (line) => {
  if (line.includes('Passwort') || line.includes('password') || line.includes('Password')) return; // never log passwords
  if (line.trim()) console.error(`  [bridge] ${line}`);
});

stdoutRl.on('line', (line) => {
  try {
    const msg = JSON.parse(line);
    handleJsonRpc(msg);
  } catch { /* non-JSON */ }
});

function handleJsonRpc(msg) {
  if (msg.jsonrpc !== '2.0') return;

  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve } = pending.get(msg.id);
    pending.delete(msg.id);
    resolve(msg);
    return;
  }

  if (msg.id === undefined || msg.id === null) {
    if (msg.method === 'loginSucceeded') {
      loginSucceeded = true;
      console.log(`  [event] loginSucceeded: ${JSON.stringify(msg.params)}`);
    } else if (msg.method === 'loginFailed') {
      loginFailed = true;
      loginErrorCode = msg.params?.errorCode;
      console.log(`  [event] loginFailed: ${JSON.stringify(msg.params)}`);
    } else if (msg.method === 'bridgeState') {
      console.log(`  [event] bridgeState: ${msg.params?.state}`);
    } else if (msg.method === 'heartbeat') {
      // silent
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
    pending.set(id, { resolve });
    proc.stdin.write(JSON.stringify(req) + '\n', 'utf8');
  });
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }
function timeout(ms, name) {
  return new Promise((_, reject) => setTimeout(() => reject(new Error(`${name} timed out`)), ms));
}

async function runTests() {
  // Wait for ready
  console.log('--- Starting bridge ---');
  await sleep(2000);

  // LOGIN
  console.log('\n--- Test 1: login ---');
  try {
    const loginResp = await Promise.race([
      sendRequest('login', { server, username, password: pass, authMode: 1, ctiMaster: false }),
      timeout(30000, 'login'),
    ]);
    if (loginResp.error) {
      fail('login', loginResp.error.message);
      console.log('\nLogin failed — cannot continue with live tests.');
      cleanup(1);
      return;
    }
    if (loginResp.result?.ok === true) {
      ok(`login → ok=true, server=${loginResp.result.server}, user=${loginResp.result.username}`);
    } else {
      fail('login', `unexpected: ${JSON.stringify(loginResp.result)}`);
      cleanup(1);
      return;
    }
  } catch (e) {
    fail('login', e.message);
    cleanup(1);
    return;
  }

  // Wait for login events
  await sleep(3000);

  // getStatus (should show loggedIn: true)
  console.log('\n--- Test 2: getStatus (post-login) ---');
  try {
    const resp = await Promise.race([sendRequest('getStatus'), timeout(5000, 'getStatus')]);
    if (resp.error) fail('getStatus', resp.error.message);
    else {
      const s = resp.result;
      if (s?.loggedIn === true) ok(`getStatus → connected=${s.connected}, loggedIn=true, lines=${s.lineCount}`);
      else fail('getStatus', `expected loggedIn=true: ${JSON.stringify(s)}`);
    }
  } catch (e) { fail('getStatus', e.message); }

  // getLines
  console.log('\n--- Test 3: getLines ---');
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

  // getColleaguePresence
  console.log('\n--- Test 4: getColleaguePresence ---');
  try {
    const resp = await Promise.race([sendRequest('getColleaguePresence'), timeout(15000, 'getColleaguePresence')]);
    if (resp.error) fail('getColleaguePresence', resp.error.message);
    else {
      const colleagues = resp.result?.colleagues;
      if (Array.isArray(colleagues)) {
        ok(`getColleaguePresence → ${colleagues.length} colleagues`);
        colleagues.slice(0, 10).forEach(c => {
          console.log(`    ${c.name}: status=${c.status}${c.statusText ? ' ('+c.statusText+')' : ''} ext=${c.extension || '—'}`);
        });
        if (colleagues.length > 10) console.log(`    ... and ${colleagues.length - 10} more`);
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
        console.log(`    ${h.callerName || '—'} (${h.callerNumber || '—'}): ${h.direction} at ${new Date(h.timestamp * 1000).toLocaleString()}`);
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
      const msgs = vm?.messages;
      if (Array.isArray(msgs)) {
        ok(`getVoicemails → ${msgs.length} messages, ${vm.newCount} new`);
      } else fail('getVoicemails', `unexpected: ${JSON.stringify(vm)?.substring(0, 100)}`);
    }
  } catch (e) { fail('getVoicemails', e.message); }

  // getPresence (own)
  console.log('\n--- Test 7: getPresence (own status) ---');
  try {
    const resp = await Promise.race([sendRequest('getPresence'), timeout(5000, 'getPresence')]);
    if (resp.error) fail('getPresence', resp.error.message);
    else if (resp.result?.status) {
      ok(`getPresence → status=${resp.result.status}`);
    } else fail('getPresence', `unexpected: ${JSON.stringify(resp.result)}`);
  } catch (e) { fail('getPresence', e.message); }

  // Logout
  console.log('\n--- Test 8: logout ---');
  try {
    const resp = await Promise.race([sendRequest('logout'), timeout(10000, 'logout')]);
    if (resp.error) fail('logout', resp.error.message);
    else if (resp.result?.ok === true) ok('logout → ok=true');
    else fail('logout', `unexpected: ${JSON.stringify(resp.result)}`);
  } catch (e) { fail('logout', e.message); }

  cleanup(results.fail > 0 ? 1 : 0);
}

function cleanup(exitCode) {
  console.log('\n=== Login Test Summary ===');
  console.log(`  Passed: ${results.pass}`);
  console.log(`  Failed: ${results.fail}`);
  if (results.errors.length > 0) {
    console.log('  Errors:');
    results.errors.forEach(e => console.log(`    - ${e}`));
  }

  proc.kill();
  setTimeout(() => process.exit(exitCode), 1000);
}

proc.on('error', (err) => { fail('process', err.message); process.exit(1); });
setTimeout(runTests, 1000);
