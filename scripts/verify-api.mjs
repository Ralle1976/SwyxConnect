// Direct API verification — call swyxApi methods and inspect results
const resp = await fetch('http://localhost:9224/json/list');
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

ws.addEventListener('open', async () => {
  try {
    // Test 1: cs.getPhoneBook — call and stringify result INSIDE the eval
    console.log('=== cs.getPhoneBook ===');
    const r1 = await send('Runtime.evaluate', {
      expression: `(async () => {
        const r = await window.swyxApi.csGetPhoneBook();
        const entries = r?.entries || [];
        return 'count=' + entries.length + ', first=' + (entries[0]?.name || 'none') + ', firstState=' + entries[0]?.curState;
      })()`,
      awaitPromise: true,
      returnByValue: true,
    });
    console.log('Result:', r1?.result?.result?.value);

    // Test 2: cs.getCallJournal
    console.log('\n=== cs.getCallJournal ===');
    const r2 = await send('Runtime.evaluate', {
      expression: `(async () => {
        const r = await window.swyxApi.csGetCallJournal(0);
        const arr = Array.isArray(r) ? r : [];
        return 'count=' + arr.length + ', first=' + (arr[0]?.number || arr[0]?.id || 'none');
      })()`,
      awaitPromise: true,
      returnByValue: true,
    });
    console.log('Result:', r2?.result?.result?.value);

    // Test 3: cs.getForwarding
    console.log('\n=== cs.getForwarding ===');
    const r3 = await send('Runtime.evaluate', {
      expression: `(async () => {
        try {
          const r = await window.swyxApi.csGetForwarding();
          return JSON.stringify(r).substring(0, 300);
        } catch(e) { return 'ERROR: ' + e.message; }
      })()`,
      awaitPromise: true,
      returnByValue: true,
    });
    console.log('Result:', r3?.result?.result?.value);

    // Test 4: getLines (COM)
    console.log('\n=== getLines (COM) ===');
    const r4 = await send('Runtime.evaluate', {
      expression: `(async () => {
        try {
          const r = await window.swyxApi.getLines();
          const lines = Array.isArray(r) ? r : (r?.lines || []);
          return 'count=' + lines.length + ', state0=' + lines[0]?.state;
        } catch(e) { return 'ERROR: ' + e.message; }
      })()`,
      awaitPromise: true,
      returnByValue: true,
    });
    console.log('Result:', r4?.result?.result?.value);

    // Test 5: getSystemInfo
    console.log('\n=== getSystemInfo ===');
    const r5 = await send('Runtime.evaluate', {
      expression: `(async () => {
        try {
          const r = await window.swyxApi.getSystemInfo();
          return JSON.stringify(r).substring(0, 300);
        } catch(e) { return 'ERROR: ' + e.message; }
      })()`,
      awaitPromise: true,
      returnByValue: true,
    });
    console.log('Result:', r5?.result?.result?.value);

    ws.close();
    process.exit(0);
  } catch (e) {
    console.error('Error:', e.message);
    process.exit(1);
  }
});

setTimeout(() => process.exit(1), 30000);
