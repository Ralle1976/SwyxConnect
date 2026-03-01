// Bridge test script - sends JSON-RPC commands and prints responses
import { spawn } from 'child_process';
import { createInterface } from 'readline';

const bridgePath = process.platform === 'win32' ? 'C:\\Users\\tango\\Desktop\\SwyIt-byRalle1976\\out\\bridge\\SwyxBridge.exe' : '/mnt/c/Users/tango/Desktop/SwyIt-byRalle1976/out/bridge/SwyxBridge.exe';

const isWSL = process.platform === 'linux';
  const proc = isWSL
  ? spawn('powershell.exe', ['-Command', '& "C:\\Users\\tango\\Desktop\\SwyIt-byRalle1976\\out\\bridge\\SwyxBridge.exe"'], { stdio: ['pipe', 'pipe', 'pipe'] })
  : spawn(bridgePath, [], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });

// Collect stderr for debugging
// Collect stderr (bridge logs) - show lines containing HistoryHandler
const stderrRl = createInterface({ input: proc.stderr });
stderrRl.on('line', (line) => {
  if (line.includes('HistoryHandler')) {
    console.log(`  [LOG] ${line}`);
  }
});

const rl = createInterface({ input: proc.stdout });

const responses = new Map();
let resolvers = new Map();

rl.on('line', (line) => {
  try {
    const obj = JSON.parse(line);
    if (obj.id !== undefined) {
      console.log(`[id=${obj.id}] ${line}`);
      if (resolvers.has(obj.id)) {
        resolvers.get(obj.id)(obj);
        resolvers.delete(obj.id);
      }
    }
  } catch {}
});

function send(id, method, params) {
  return new Promise((resolve) => {
    resolvers.set(id, resolve);
    const msg = params 
      ? JSON.stringify({ jsonrpc: '2.0', id, method, params })
      : JSON.stringify({ jsonrpc: '2.0', id, method });
    proc.stdin.write(msg + '\n');
    // Timeout after 8 seconds
    setTimeout(() => {
      if (resolvers.has(id)) {
        resolvers.delete(id);
        console.log(`[id=${id}] TIMEOUT`);
        resolve(null);
      }
    }, 8000);
  });
}

async function runTests() {
  // Wait for bridge to connect
  await new Promise(r => setTimeout(r, 6000));
  console.log('\n=== Bridge connected, running tests ===\n');

  console.log('--- TEST 1: Set DND ---');
  await send(1, 'setPresence', { status: 'DND' });
  await new Promise(r => setTimeout(r, 2000));

  console.log('\n--- TEST 2: Verify DND ---');
  await send(2, 'getPresence');

  console.log('\n--- TEST 3: Set Available ---');
  await send(3, 'setPresence', { status: 'Available' });
  await new Promise(r => setTimeout(r, 2000));

  console.log('\n--- TEST 4: Verify Available ---');
  await send(4, 'getPresence');

  console.log('\n--- TEST 5: Call History ---');
  await send(5, 'getCallHistory');

  console.log('\n--- TEST 6: Voicemails ---');
  await send(6, 'getVoicemails');

  console.log('\n=== ALL TESTS COMPLETE ===');
  
  proc.kill();
  process.exit(0);
}

runTests().catch(err => {
  console.error('Error:', err);
  proc.kill();
  process.exit(1);
});
