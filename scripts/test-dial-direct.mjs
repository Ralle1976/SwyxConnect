// Test dial directly via bridge and monitor what happens
import { spawn } from 'child_process';
import { createInterface } from 'readline';
import { fileURLToPath } from 'url';
import * as path from 'path';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const bridgeExe = path.join(__dirname, '..', 'out', 'bridge', 'SwyxMessenger.exe');

console.log(`Starting bridge: ${bridgeExe}`);
const proc = spawn(bridgeExe, [], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });

proc.stderr.on('data', (d) => {
  const lines = d.toString().split('\n').filter(l => l.trim());
  lines.forEach(l => console.log(`[BRIDGE] ${l}`));
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
    setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(new Error('timeout')); } }, 10000);
  });
}

async function run() {
  // Wait for bridge to attach
  await new Promise(r => setTimeout(r, 5000));

  console.log('\n=== Status vor Dial ===');
  const status = await send('getSystemInfo');
  console.log('isCtiMaster:', status.result?.isCtiMaster);
  console.log('isServerUp:', status.result?.isServerUp);
  console.log('numberOfLines:', status.result?.numberOfLines);

  const linesBefore = await send('getLines');
  console.log('Lines before:', JSON.stringify(linesBefore.result).substring(0, 200));

  console.log('\n=== DIAL TEST: Wähle "99" (Voicemail/Weiterleitung) ===');
  console.log('Überwache 10 Sekunden auf lineStateChanged-Events...');
  const dialResult = await send('dial', { number: '99' });
  console.log('Dial result:', JSON.stringify(dialResult).substring(0, 300));

  // Poll line state every second for 10 seconds
  for (let i = 1; i <= 10; i++) {
    await new Promise(r => setTimeout(r, 1000));
    const lines = await send('getLines');
    const lineState = lines.result?.lines?.[0]?.state || '?';
    const callerNum = lines.result?.lines?.[0]?.callerNumber || '';
    console.log(`  ${i}s: Line0=${lineState} caller=${callerNum}`);
  }

  console.log('\n=== HANGUP TEST ===');
  const hangupResult = await send('hangup', { lineId: 0 });
  console.log('Hangup result:', JSON.stringify(hangupResult).substring(0, 200));

  await new Promise(r => setTimeout(r, 2000));
  const linesFinal = await send('getLines');
  console.log('\n=== Lines nach Hangup ===');
  console.log('Lines final:', JSON.stringify(linesFinal.result).substring(0, 300));

  proc.kill();
  process.exit(0);
}

run().catch(e => { console.error('Error:', e.message); process.exit(1); });
setTimeout(() => { console.log('Timeout'); proc.kill(); process.exit(1); }, 40000);
