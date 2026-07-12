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
    if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
    else if (!msg.id && msg.method === 'comSocketState' && msg.params?.connected) setTimeout(runTests, 2000);
  } catch {}
});
proc.stderr.on('data', () => {});

function send(method, params) {
  return new Promise((resolve) => {
    const id = nextId++;
    pending.set(id, resolve);
    proc.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params: params || {} }) + '\n');
  });
}

async function runTests() {
  console.log('=== COMPREHENSIVE DATA INVENTORY ===\n');

  // CallJournal — alle Parts testen
  console.log('--- CallJournal Parts ---');
  for (const part of [0, 1, 2, 3]) {
    const cj = await send('cs.getCallJournal', { part });
    if (Array.isArray(cj.result)) {
      const labels = ['All', 'Missed', 'Outgoing', 'Incoming'];
      console.log(`  Part ${part} (${labels[part] || '?'}): ${cj.result.length} entries`);
      if (cj.result[0] && part === 0) {
        console.log('    Sample fields:', Object.keys(cj.result[0]).join(', '));
        console.log('    kind values:', [...new Set(cj.result.map(e => e.kind))].join(', '));
      }
    }
  }

  // PhoneBook mit allen Feldern
  console.log('\n--- PhoneBook Structure ---');
  const pb = await send('cs.getPhoneBook');
  if (pb.result?.entries?.[0]) {
    console.log('  Fields:', Object.keys(pb.result.entries[0]).join(', '));
    const types = {};
    pb.result.entries.forEach(e => { types[e.entityType] = (types[e.entityType]||0) + 1; });
    console.log('  EntityTypes:', JSON.stringify(types));
    const states = {};
    pb.result.entries.forEach(e => { states[e.curState] = (states[e.curState]||0) + 1; });
    console.log('  Presence states:', JSON.stringify(states));
  }

  // Audio
  console.log('\n--- Audio ---');
  const am = await send('cs.getAudioModes');
  if (am.result) console.log('  AudioModes:', JSON.stringify(am.result));
  const av = await send('cs.getAudioVolumes');
  if (av.result) console.log('  AudioVolumes:', JSON.stringify(av.result));

  // Voicemail
  console.log('\n--- Voicemail ---');
  const vm = await send('cs.getVoiceMessages');
  if (vm.result) console.log('  VoiceMessages:', Array.isArray(vm.result) ? vm.result.length + ' messages' : JSON.stringify(vm.result).substring(0, 100));

  // UserGroups
  console.log('\n--- Groups ---');
  const ug = await send('cs.getUserGroups');
  if (Array.isArray(ug.result)) {
    console.log('  Groups:', ug.result.length);
    ug.result.forEach(g => console.log('    ', g.name || JSON.stringify(g).substring(0, 80)));
  }

  // ForwardingConfig
  console.log('\n--- Forwarding ---');
  const fc = await send('cs.getForwardingConfig');
  if (fc.result) {
    const f = fc.result;
    console.log('  Unconditional:', f.unconditional?.status ? 'ON → ' + f.unconditional.number : 'OFF');
    console.log('  Busy:', f.busy?.status ? 'ON → ' + f.busy.number : 'OFF');
    console.log('  NoReply:', f.noReply?.status ? `ON → ${f.noReply.number} (${f.noReply.timeout}s)` : 'OFF');
  }

  // SpeedDials mit Live-State
  console.log('\n--- SpeedDials (named, with state) ---');
  const sd = await send('cs.getSpeedDials');
  if (Array.isArray(sd.result)) {
    const named = sd.result.filter(s => s.name);
    named.forEach(s => {
      const states = { 0: 'Offline', 1: 'Offline', 2: 'Available', 3: 'Busy', 4: 'DND', 5: 'Away' };
      const masked = s.name[0] + '. (' + s.name.length + ')';
      console.log(`  [${s.keyIndex ?? '?'}] ${masked} num=${s.number} state=${states[s.state] || s.state}`);
    });
  }

  // VersionInfo
  console.log('\n--- Version ---');
  const vi = await send('cs.getVersionInfo');
  if (vi.result) {
    console.log('  SwyxIt:', vi.result.swyxItVersion);
    console.log('  ComSocket:', vi.result.comSocketVersion);
    console.log('  Interface v:', vi.result.comSocketInterfaceVersion);
  }

  console.log('\n=== INVENTORY COMPLETE ===');
  proc.kill();
  process.exit(0);
}

setTimeout(() => { console.log('Timeout'); proc.kill(); process.exit(1); }, 25000);
