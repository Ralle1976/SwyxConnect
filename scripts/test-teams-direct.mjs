// Simpler Teams test - just trigger login and see what happens
const r = await fetch('http://localhost:9240/json/list');
const p = (await r.json())[0];
const ws = new WebSocket(p.webSocketDebuggerUrl);
let id=1;
const pending=new Map();

ws.onmessage = (e) => {
  try {
    const m = JSON.parse(e.data);
    if (m.id !== undefined && pending.has(m.id)) {
      pending.get(m.id)(m);
      pending.delete(m.id);
    }
  } catch(_) {}
};

function evalJS(expr, timeoutMs = 30000) {
  return new Promise((resolve, reject) => {
    const i = id++;
    pending.set(i, resolve);
    ws.send(JSON.stringify({ id:i, method:'Runtime.evaluate', params:{
      expression: expr, awaitPromise: true, returnByValue: true
    }}));
    setTimeout(() => { if (pending.has(i)) { pending.delete(i); reject(new Error('timeout')); } }, timeoutMs);
  });
}

ws.onopen = async () => {
  try {
    await new Promise(r=>setTimeout(r,1000));

    // Step 1: Just check status first (no login)
    console.log('=== Step 1: Teams status (no login) ===');
    const r1 = await evalJS(`(async()=>{
      try {
        const r = await window.swyxApi.teamsGraphGetStatus();
        return JSON.stringify({loggedIn: r.loggedIn, userName: r.userName, hasPresence: !!r.presence});
      } catch(e) { return 'ERROR: ' + e.message; }
    })()`);
    console.log('Result:', r1?.result?.result?.value);

    // Step 2: Trigger login (this opens a browser window!)
    console.log('\n=== Step 2: Teams Login (opens browser) ===');
    console.log('A browser window should open for Microsoft login...');
    const r2 = await evalJS(`(async()=>{
      try {
        const r = await window.swyxApi.teamsGraphLogin();
        return JSON.stringify(r);
      } catch(e) { return 'ERROR: ' + e.message; }
    })()`, 60000); // 60s timeout for OAuth
    console.log('Login result:', r2?.result?.result?.value);

    // Step 3: Check status after login
    console.log('\n=== Step 3: Status after login ===');
    const r3 = await evalJS(`(async()=>{
      try {
        const r = await window.swyxApi.teamsGraphGetStatus();
        return JSON.stringify({loggedIn: r.loggedIn, userName: r.userName, presence: r.presence});
      } catch(e) { return 'ERROR: ' + e.message; }
    })()`);
    console.log('Result:', r3?.result?.result?.value);

  } catch(e) {
    console.log('Test error:', e.message);
  }

  ws.close();
  process.exit(0);
};

setTimeout(() => { console.log('GLOBAL TIMEOUT'); process.exit(1); }, 90000);
