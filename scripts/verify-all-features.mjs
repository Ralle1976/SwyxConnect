// Comprehensive feature verification via CDP
// Tests EVERY claim from the README and reports actual results
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
  } catch (e) {
    // Non-JSON, ignore
  }
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
    returnByValue: true,
  });
  // CDP response structure: {id, result: {result: {type, value}}}
  return result?.result?.result?.value;
}

const results = [];

function record(name, passed, detail) {
  results.push({ name, passed, detail });
  const icon = passed ? '✅' : '❌';
  console.log(`${icon} ${name}: ${detail}`);
}

ws.addEventListener('open', async () => {
  try {
    await new Promise(r => setTimeout(r, 2000));

    console.log('\n=== TEST 1: Auto-Attach / Login Bypass ===');
    const bodyText = await evalJS('document.body.innerText.substring(0, 500)');
    const isLogin = bodyText?.includes('Server') && bodyText?.includes('Passwort') && !bodyText?.includes('Telefon');
    record('Auto-Attach (skip login)', !isLogin, isLogin ? 'Login form shown!' : 'Main UI shown, no login form');

    console.log('\n=== TEST 2: Presence View — All Colleagues ===');
    await evalJS('location.hash = "#/presence"');
    await new Promise(r => setTimeout(r, 3000));
    const presenceText = await evalJS('document.body.innerText');
    const phonebookMatch = presenceText?.match(/TELEFONBUCH\D*(\d+)/);
    const colleagueCount = phonebookMatch ? parseInt(phonebookMatch[1]) : 0;
    record('PhoneBook loaded via ComSocket', colleagueCount > 6, `${colleagueCount} colleagues (COM-only would show ~6)`);

    // Check for actual colleague names
    const hasNames = presenceText?.includes('Stephan') || presenceText?.includes('Heine') || presenceText?.includes('Rieger');
    record('Colleague names visible', hasNames, hasNames ? 'Real names found in DOM' : 'No names detected');

    console.log('\n=== TEST 3: History View — Call Journal ===');
    await evalJS('location.hash = "#/history"');
    await new Promise(r => setTimeout(r, 3000));
    const historyText = await evalJS('document.body.innerText');
    const journalMatch = historyText?.match(/COMSOCKET\D*(\d+)/);
    const journalCount = journalMatch ? parseInt(journalMatch[1]) : 0;
    record('CallJournal loaded via ComSocket', journalCount > 0, `${journalCount} entries`);

    const hasTabs = historyText?.includes('Alle') && historyText?.includes('Eingehend') && historyText?.includes('Verpasst');
    record('Journal filter tabs', hasTabs, hasTabs ? 'Alle/Eingehend/Ausgehend/Verpasst tabs present' : 'Tabs missing');

    console.log('\n=== TEST 4: Phone View — Dialer ===');
    await evalJS('location.hash = "#/"');
    await new Promise(r => setTimeout(r, 2000));
    const phoneText = await evalJS('document.body.innerText');
    const hasDialer = phoneText?.includes('Wählen') || phoneText?.includes('Anrufen');
    const hasLines = phoneText?.includes('Leitung');
    record('Phone dialer present', hasDialer, hasDialer ? 'Dialpad + Anrufen button' : 'Dialer missing');
    record('Line status visible', hasLines, hasLines ? 'Leitung 1/2 shown' : 'Lines missing');

    console.log('\n=== TEST 5: Settings View ===');
    await evalJS('location.hash = "#/settings"');
    await new Promise(r => setTimeout(r, 2000));
    const settingsText = await evalJS('document.body.innerText');
    const hasSettings = settingsText?.includes('Einstellungen') || settingsText?.includes('Allgemein');
    record('Settings view renders', hasSettings, hasSettings ? 'Settings sections visible' : 'Settings missing');

    console.log('\n=== TEST 6: ComSocket Connection Status ===');
    // Check if swyxApi exists and comsocket is reported
    const comSocketStatus = await evalJS(`
      typeof window.swyxApi !== 'undefined' ? 'swyxApi exists' : 'swyxApi MISSING'
    `);
    record('Preload API exposed', comSocketStatus === 'swyxApi exists', comSocketStatus);

    console.log('\n=== TEST 7: Actual API calls ===');
    // Try cs.getPhoneBook via the exposed API
    const phoneBookResult = await evalJS(`
      (async () => {
        try {
          const r = await window.swyxApi.csGetPhoneBook();
          const entries = r?.entries || [];
          return JSON.stringify({ok: true, count: entries.length, first: entries[0]?.name || 'none'});
        } catch(e) { return JSON.stringify({ok: false, error: (e.message || String(e)).substring(0, 200)}); }
      })()
    `);
    let pb = {};
    try { pb = JSON.parse(phoneBookResult || '{}'); } catch (e) {
      console.log('phoneBook raw result:', String(phoneBookResult).substring(0, 300));
    }
    record('cs.getPhoneBook API call works', pb.ok && pb.count > 0, pb.ok ? `${pb.count} entries, first: ${pb.first}` : `FAILED: ${pb.error || phoneBookResult}`);

    const journalResult = await evalJS(`
      (async () => {
        try {
          const r = await window.swyxApi.csGetCallJournal(0);
          const arr = Array.isArray(r) ? r : [];
          return JSON.stringify({ok: true, count: arr.length, first: arr[0]?.id || arr[0]?.number || 'none'});
        } catch(e) { return JSON.stringify({ok: false, error: (e.message || String(e)).substring(0, 200)}); }
      })()
    `);
    let cj = {};
    try { cj = JSON.parse(journalResult || '{}'); } catch (e) {
      console.log('journal raw result:', String(journalResult).substring(0, 300));
    }
    record('cs.getCallJournal API call works', cj.ok && cj.count > 0, cj.ok ? `${cj.count} entries, first: ${cj.first}` : `FAILED: ${cj.error || journalResult}`);

    console.log('\n=== SUMMARY ===');
    const passed = results.filter(r => r.passed).length;
    const failed = results.filter(r => !r.passed).length;
    console.log(`\n${passed}/${results.length} checks passed, ${failed} failed`);

    if (failed > 0) {
      console.log('\nFAILED CHECKS:');
      results.filter(r => !r.passed).forEach(r => console.log(`  ❌ ${r.name}: ${r.detail}`));
    }

    ws.close();
    process.exit(failed > 0 ? 1 : 0);
  } catch (e) {
    console.error('Verification error:', e.message);
    process.exit(1);
  }
});

setTimeout(() => { console.log('TIMEOUT'); process.exit(1); }, 60000);
