import { spawn } from 'child_process';
import { createInterface } from 'readline';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const EXE = path.join(__dirname, '..', 'out', 'bridge', 'SwyxMessenger.exe');
const proc = spawn(EXE, [], { stdio: ['pipe', 'pipe', 'pipe'] });

let nextId = 1;
const pending = new Map();

const rl = createInterface({ input: proc.stdout });
rl.on('line', (line) => {
  try {
    const msg = JSON.parse(line);
    if (msg.id && pending.has(msg.id)) {
      pending.get(msg.id)(msg);
      pending.delete(msg.id);
    } else if (!msg.id) {
      if (msg.method === 'bridgeState') {
        console.log('Bridge:', msg.params.state, msg.params.mode || '');
      } else if (msg.method === 'comSocketState') {
        console.log('ComSocket:', msg.params.connected ? 'CONNECTED port=' + msg.params.port : 'FAILED');
        if (msg.params.connected) setTimeout(runTests, 2000);
      } else if (msg.method === 'cs.lineStateChanged') {
        console.log('[EVENT] lineStateChanged');
      } else if (msg.method?.startsWith('cs.')) {
        console.log('[EVENT]', msg.method);
      }
    }
  } catch {}
});

proc.stderr.on('data', (d) => {
  const s = d.toString().trim();
  if (s) console.error('[bridge]', s.substring(0, 150));
});

function send(method, params) {
  return new Promise((resolve) => {
    const id = nextId++;
    pending.set(id, resolve);
    proc.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params: params || {} }) + '\n');
  });
}

async function runTests() {
  console.log('\n=== COMSOCKET LIVE TESTS ===');

  console.log('\n--- cs.getPhoneBook (ALL colleagues) ---');
  const pb = await send('cs.getPhoneBook');
  if (pb.result?.entries) {
    const entries = pb.result.entries;
    const withPresence = entries.filter(e => e.curState > 0).length;
    console.log(`  ✅ PhoneBook: ${entries.length} entries, ${withPresence} with live presence`);
    entries.filter(e => e.curState > 0).slice(0, 5).forEach(e => {
      const masked = e.name ? e.name[0] + '. (' + e.name.length + ')' : '?';
      console.log(`    ${masked} ext=${e.number} state=${e.curState} desc=${e.description || ''}`);
    });
  } else if (pb.error) {
    console.log(`  ❌ Error: ${pb.error.message}`);
  }

  console.log('\n--- cs.getCallJournal ---');
  const cj = await send('cs.getCallJournal');
  if (Array.isArray(cj.result)) {
    console.log(`  ✅ CallJournal: ${cj.result.length} entries`);
  } else if (cj.error) {
    console.log(`  ❌ Error: ${cj.error.message}`);
  }

  console.log('\n--- cs.getSpeedDials ---');
  const sd = await send('cs.getSpeedDials');
  if (Array.isArray(sd.result)) {
    const named = sd.result.filter(s => s.name).length;
    console.log(`  ✅ SpeedDials: ${named} named / ${sd.result.length} total`);
  } else if (sd.error) {
    console.log(`  ❌ Error: ${sd.error.message}`);
  }

  console.log('\n--- cs.getVersionInfo ---');
  const vi = await send('cs.getVersionInfo');
  if (vi.result?.swyxItVersion) {
    console.log(`  ✅ SwyxIt: ${vi.result.swyxItVersion}`);
  } else if (vi.error) {
    console.log(`  ❌ Error: ${vi.error.message}`);
  }

  console.log('\n--- cs.getForwardingConfig ---');
  const fc = await send('cs.getForwardingConfig');
  if (fc.result) {
    console.log(`  ✅ Forwarding: ${JSON.stringify(fc.result).substring(0, 100)}`);
  } else if (fc.error) {
    console.log(`  ❌ Error: ${fc.error.message}`);
  }

  console.log('\n=== TEST COMPLETE ===');
  proc.kill();
  process.exit(0);
}

setTimeout(() => {
  console.log('Timeout — bridge did not connect ComSocket in time');
  runTests();
}, 20000);
