// Verify Callcenter Dashboard via CDP
const resp = await fetch('http://localhost:9227/json/list');
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
  const result = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  return result?.result?.result?.value;
}

async function getDomText() {
  return await evalJS('document.body.innerText.substring(0, 2000)');
}

ws.addEventListener('open', async () => {
  try {
    await new Promise(r => setTimeout(r, 3000));

    // Navigate to callcenter
    console.log('=== Navigate to #/callcenter ===');
    await evalJS('location.hash = "#/callcenter"');
    await new Promise(r => setTimeout(r, 4000));

    const text = await getDomText();
    console.log('Dashboard text:\n', text);

    // Check specific elements
    const checks = await evalJS(`JSON.stringify({
      hasTeamHeader: document.body.innerText.includes('Team'),
      hasKPI: document.body.innerText.includes('Anrufe heute') || document.body.innerText.includes('heute'),
      hasColleagues: (document.body.innerText.match(/Verfügbar|Verf\u00fcgbar/g) || []).length,
      hasCallEntries: document.body.innerText.includes('Eingehend') || document.body.innerText.includes('Ausgehend'),
      isComSocketError: document.body.innerText.includes('ComSocket nicht verf\u00fcgbar'),
      teamCount: (document.body.innerText.match(/Kollegen/) || []).length,
    })`);
    console.log('\n=== Checks ===');
    console.log(checks);

    // Test Settings page
    console.log('\n=== Navigate to #/settings ===');
    await evalJS('location.hash = "#/settings"');
    await new Promise(r => setTimeout(r, 2000));
    const settingsText = await getDomText();
    const hasUpdateMode = settingsText.includes('Update-Modus') || settingsText.includes('Polling');
    console.log('Settings has Update-Modus section:', hasUpdateMode);
    if (hasUpdateMode) {
      console.log('Found: Daten-Aktualisierung settings section');
    }

    // Check phoneBook store state
    console.log('\n=== Store State ===');
    const storeState = await evalJS(`
      (async () => {
        const r = await window.swyxApi.csGetPhoneBook();
        const entries = r?.entries || [];
        return 'phoneBook entries: ' + entries.length + ', first: ' + (entries[0]?.name || 'none');
      })()
    `);
    console.log(storeState);

    ws.close();
    process.exit(0);
  } catch (e) {
    console.error('Error:', e.message);
    process.exit(1);
  }
});

setTimeout(() => process.exit(1), 30000);
