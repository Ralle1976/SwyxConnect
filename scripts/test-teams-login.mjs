// Test Teams Graph login flow
const r = await fetch('http://localhost:9240/json/list');
const p = (await r.json())[0];
const ws = new WebSocket(p.webSocketDebuggerUrl);
let id=1;
const pending=new Map();
ws.onmessage = (e) => { try { const m=JSON.parse(e.data); if(m.id&&pending.has(m.id)){pending.get(m.id)(m);pending.delete(m.id);} } catch(_){} };
function send(method, params={}) { return new Promise((res,rej)=>{ const i=id++; pending.set(i,res); ws.send(JSON.stringify({jsonrpc:'2.0',id:i,method,params})); setTimeout(()=>{if(pending.has(i)){pending.delete(i);rej('timeout');}},30000); }); }

ws.onopen = async () => {
  await new Promise(r=>setTimeout(r,2000));

  // Step 1: Check current Teams Graph status
  console.log('=== Teams Graph Status (before login) ===');
  const statusBefore = await send('Runtime.evaluate', {
    expression: `(async()=>{ try { const r = await window.swyxApi.teamsGraphGetStatus(); return JSON.stringify(r); } catch(e) { return 'error: '+e.message; } })()`,
    awaitPromise: true, returnByValue: true
  });
  console.log('Status:', statusBefore.result?.result?.value);

  // Step 2: Try Teams Graph login
  console.log('\n=== Teams Graph Login Attempt ===');
  const loginResult = await send('Runtime.evaluate', {
    expression: `(async()=>{ try { const r = await window.swyxApi.teamsGraphLogin(); return JSON.stringify(r); } catch(e) { return 'error: '+e.message; } })()`,
    awaitPromise: true, returnByValue: true
  });
  console.log('Login result:', loginResult.result?.result?.value);

  // Wait for OAuth to complete
  await new Promise(r=>setTimeout(r,8000));

  // Step 3: Check status after login attempt
  console.log('\n=== Teams Graph Status (after login) ===');
  const statusAfter = await send('Runtime.evaluate', {
    expression: `(async()=>{ try { const r = await window.swyxApi.teamsGraphGetStatus(); return JSON.stringify(r); } catch(e) { return 'error: '+e.message; } })()`,
    awaitPromise: true, returnByValue: true
  });
  console.log('Status:', statusAfter.result?.result?.value);

  // Step 4: Start polling
  console.log('\n=== Start Presence Polling ===');
  const pollResult = await send('Runtime.evaluate', {
    expression: `(async()=>{ try { await window.swyxApi.teamsGraphStartPolling(); return 'polling started'; } catch(e) { return 'error: '+e.message; } })()`,
    awaitPromise: true, returnByValue: true
  });
  console.log('Polling:', pollResult.result?.result?.value);

  // Wait and check presence
  await new Promise(r=>setTimeout(r,5000));
  const presence = await send('Runtime.evaluate', {
    expression: `(async()=>{ try { const r = await window.swyxApi.teamsGraphGetStatus(); return JSON.stringify(r); } catch(e) { return 'error: '+e.message; } })()`,
    awaitPromise: true, returnByValue: true
  });
  console.log('\nFinal status with presence:', presence.result?.result?.value);

  ws.close();
  process.exit(0);
};
setTimeout(()=>process.exit(1),45000);
