// Live call monitor — watches line state before/during/after a call
const resp = await fetch('http://localhost:9225/json/list');
const pages = await resp.json();
const page = pages[0];

const ws = new WebSocket(page.webSocketDebuggerUrl);
let nextId = 1;
const pending = new Map();

ws.addEventListener('message', (event) => {
  try {
    const msg = JSON.parse(event.data);
    if (msg.id !== undefined && pending.has(msg.id)) {
      pending.get(msg.id).resolve(msg);
      pending.delete(msg.id);
    } else if (msg.method === 'Runtime.consoleAPICalled') {
      const args = msg.params?.args?.map(a => a.value || a.description).join(' ');
      if (args) console.log(`[CONSOLE] ${args}`);
    }
  } catch (e) {}
});

function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params }));
    setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(new Error('timeout')); } }, 15000);
  });
}

async function evalJS(expression) {
  const result = await send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
  });
  return result?.result?.result?.value;
}

ws.addEventListener('open', async () => {
  try {
    console.log('=== Call Monitor bereit ===');
    console.log('Starte einen Anruf im SwyxConnect GUI. Ich überwache alles.\n');

    // Initial state
    console.log('--- Vor Anruf ---');
    const linesBefore = await evalJS(`
      (async () => {
        const r = await window.swyxApi.getLines();
        const lines = Array.isArray(r) ? r : (r?.lines || []);
        return lines.map(l => 'Line ' + l.id + ': ' + l.state).join(', ') || 'keine Lines';
      })()
    `);
    console.log('Lines:', linesBefore);

    const sysInfo = await evalJS(`
      (async () => {
        const r = await window.swyxApi.getSystemInfo();
        return 'isCtiMaster=' + r?.isCtiMaster + ', serverUp=' + r?.isServerUp + ', lines=' + r?.numberOfLines;
      })()
    `);
    console.log('SystemInfo:', sysInfo);

    // Monitor for 60 seconds — poll line state every 2 seconds
    let pollCount = 0;
    const pollInterval = setInterval(async () => {
      pollCount++;
      try {
        const lines = await evalJS(`
          (async () => {
            const r = await window.swyxApi.getLines();
            const lines = Array.isArray(r) ? r : (r?.lines || []);
            return lines.map(l => l.id + ':' + l.state + (l.callerName ? '(' + l.callerName + ')' : '')).join(' | ') || 'none';
          })()
        `);
        const ts = new Date().toLocaleTimeString('de-DE');
        console.log(`[${ts}] Lines: ${lines}`);

        if (pollCount >= 30) {
          clearInterval(pollInterval);
          console.log('\n=== Monitor beendet (60s) ===');
          ws.close();
          process.exit(0);
        }
      } catch (e) {
        console.log(`[poll ${pollCount}] error: ${e.message}`);
      }
    }, 2000);

  } catch (e) {
    console.error('Monitor error:', e.message);
    process.exit(1);
  }
});

setTimeout(() => { console.log('Timeout'); process.exit(1); }, 70000);
