// Test: Kill SwyxIt! then login directly via RegisterUserEx
// Reads credentials from .env (never prints them)
import { readFileSync } from 'fs';
import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { fileURLToPath } from 'url';
import * as path from 'path';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '..');

// Parse .env manually
const envContent = readFileSync(path.join(projectRoot, '.env'), 'utf8');
const env = {};
for (const line of envContent.split('\n')) {
  const trimmed = line.trim();
  if (!trimmed || trimmed.startsWith('#')) continue;
  const eqIdx = trimmed.indexOf('=');
  if (eqIdx < 0) continue;
  const key = trimmed.substring(0, eqIdx).trim();
  const value = trimmed.substring(eqIdx + 1).trim();
  if (key) env[key] = value;
}

const username = env.SWYX_USERNAME;
const password = env.SWYX_PASSWORD;

if (!username || !password) {
  console.error('ERROR: SWYX_USERNAME or SWYX_PASSWORD not found in .env');
  process.exit(1);
}
console.log(`Credentials loaded: user=<hidden>, pass length=${password.length}`);

// Also check for server/backup
const server = env.SWYX_SERVER || '127.0.0.1';
const backupServer = env.SWYX_BACKUP_SERVER || '';
console.log(`Server: ${server}`);

// Step 1: Kill SwyxIt! to free the CLMgr session
console.log('\n=== Step 1: Killing SwyxIt! ===');
const { execSync } = await import('child_process');
try {
  execSync('taskkill /F /IM SwyxIt!.exe', { stdio: 'ignore' });
  console.log('SwyxIt! killed.');
} catch {
  console.log('SwyxIt! was not running.');
}
await new Promise(r => setTimeout(r, 2000));

// Step 2: Start bridge with login args
console.log('\n=== Step 2: Starting bridge with direct login ===');
const bridgeExe = path.join(projectRoot, 'out', 'bridge', 'SwyxMessenger.exe');
const proc = spawn(bridgeExe, [
  '--server', server,
  '--user', username,
  '--password', password,
  ...(backupServer ? ['--backup-server', backupServer] : []),
  '--auth-mode', '1',
], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });

proc.stderr.on('data', (d) => {
  const lines = d.toString().split('\n').filter(l => l.trim());
  lines.forEach(l => {
    // Mask any credential values that might appear in logs
    let safe = l;
    if (safe.includes(username)) safe = safe.replaceAll(username, '<USER>');
    if (safe.includes(password)) safe = safe.replaceAll(password, '<PASS>');
    console.log(`[BRIDGE] ${safe}`);
  });
});

let nextId = 1;
const pending = new Map();
const rl = createInterface({ input: proc.stdout });
rl.on('line', (line) => {
  try {
    const obj = JSON.parse(line);
    if (obj.id !== undefined && pending.has(obj.id)) {
      pending.get(obj.id).resolve(obj);
      pending.delete(obj.id);
    } else if (obj.method) {
      console.log(`[EVENT] ${obj.method}: ${JSON.stringify(obj.params).substring(0, 150)}`);
    }
  } catch {}
});

function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    proc.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');
    setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(new Error('timeout')); } }, 15000);
  });
}

// Wait for login to complete
await new Promise(r => setTimeout(r, 8000));

console.log('\n=== Step 3: Check login result ===');
const status = await send('getSystemInfo');
console.log('isServerUp:', status.result?.isServerUp);
console.log('isCtiMaster:', status.result?.isCtiMaster);
console.log('numberOfLines:', status.result?.numberOfLines);

const linesBefore = await send('getLines');
console.log('Lines:', JSON.stringify(linesBefore.result).substring(0, 200));

if (status.result?.isServerUp) {
  console.log('\n=== LOGIN SUCCESSFUL! Testing dial... ===');
  const dialResult = await send('dial', { number: '99' });
  console.log('Dial:', JSON.stringify(dialResult).substring(0, 150));

  for (let i = 1; i <= 8; i++) {
    await new Promise(r => setTimeout(r, 1000));
    const lines = await send('getLines');
    const s = lines.result?.lines?.[0];
    console.log(`  ${i}s: state=${s?.state} caller=${s?.callerNumber || ''}`);
  }

  const hangup = await send('hangup', { lineId: 0 });
  console.log('Hangup:', JSON.stringify(hangup).substring(0, 100));
} else {
  console.log('\n=== LOGIN FAILED ===');
}

console.log('\n=== Step 4: SwyxIt! check (should NOT be running) ===');
try {
  execSync('tasklist /FI "IMAGENAME eq SwyxIt!.exe" 2>nul | findstr SwyxIt', { stdio: 'pipe' });
  console.log('WARNING: SwyxIt! is running!');
} catch {
  console.log('SwyxIt! is NOT running. ✅ Headless mode confirmed.');
}

proc.kill();
process.exit(0);
