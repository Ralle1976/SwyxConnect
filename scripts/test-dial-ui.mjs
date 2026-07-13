// Test: Dial via the Electron app's API and check if UI shows the active call
const resp = await fetch('http://localhost:9226/json/list');
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

async function evalJS(expression) {
  const result = await send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
  });
  return result?.result?.result?.value;
}

async function getDomText() {
  return await evalJS(`document.body.innerText.substring(0, 1000)`);
}

ws.addEventListener('open', async () => {
  try {
    await new Promise(r => setTimeout(r, 2000));

    console.log('=== Vor Dial: UI Text ===');
    console.log(await getDomText());

    console.log('\n=== Führe Dial über GUI-API aus (Nummer "99") ===');
    const dialResult = await evalJS(`
      (async () => {
        try {
          await window.swyxApi.dial('99');
          return 'dial ok';
        } catch(e) { return 'dial error: ' + e.message; }
      })()
    `);
    console.log('Dial result:', dialResult);

    // Wait 2 seconds, then check if UI shows active call
    await new Promise(r => setTimeout(r, 2000));
    console.log('\n=== 2s nach Dial: UI Text ===');
    console.log(await getDomText());

    // Check line store state
    const lineState = await evalJS(`
      (function() {
        // Can't directly access Zustand store from outside, but can check DOM
        const hasActiveCall = document.body.innerText.includes('Aktiv') || document.body.innerText.includes('Klingelt') || document.body.innerText.includes('Alerting');
        const hasHangupButton = !!document.querySelector('button') && document.body.innerText.includes('Auflegen');
        return JSON.stringify({hasActiveCall, hasHangupButton});
      })()
    `);
    console.log('\n=== UI State ===');
    console.log(lineState);

    // Wait 3 more seconds
    await new Promise(r => setTimeout(r, 3000));
    console.log('\n=== 5s nach Dial: UI Text ===');
    console.log(await getDomText());

    // Try hangup via API
    console.log('\n=== Hangup via API ===');
    const hangupResult = await evalJS(`
      (async () => {
        try {
          await window.swyxApi.hangup(0);
          return 'hangup ok';
        } catch(e) { return 'hangup error: ' + e.message; }
      })()
    `);
    console.log('Hangup result:', hangupResult);

    await new Promise(r => setTimeout(r, 2000));
    console.log('\n=== Nach Hangup: UI Text ===');
    console.log(await getDomText());

    ws.close();
    process.exit(0);
  } catch (e) {
    console.error('Error:', e.message);
    process.exit(1);
  }
});

setTimeout(() => process.exit(1), 30000);
