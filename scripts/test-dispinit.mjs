#!/usr/bin/env node
/**
 * Test: Does DispInit("172.18.3.202") on CLMgr COM trigger the RemoteConnector tunnel?
 * 
 * Preconditions:
 *   - SwyxIt!.exe is NOT running
 *   - CLMgr.exe IS running
 *   - Port 9094 is CLOSED
 * 
 * Expected: After DispInit, CLMgr establishes RemoteConnector tunnel → port 9094 opens
 */
import { spawn, execSync } from 'child_process';
import { setTimeout as sleep } from 'timers/promises';

const BRIDGE_PATH = 'C:\\temp\\SwyxBridge\\SwyxBridge.exe';
const SERVER = '172.18.3.202';

function checkPort(port) {
  try {
    const out = execSync(
      `powershell.exe -Command "Test-NetConnection -ComputerName 127.0.0.1 -Port ${port} -WarningAction SilentlyContinue | Select -ExpandProperty TcpTestSucceeded"`,
      { encoding: 'utf-8', timeout: 15000 }
    ).trim();
    return out === 'True';
  } catch {
    return false;
  }
}

function sendRpc(proc, id, method, params = {}) {
  const msg = JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n';
  console.log(`>>> ${method} (id=${id})`);
  proc.stdin.write(msg);
}

async function main() {
  console.log('=== DispInit Standalone Test ===');
  console.log(`Server: ${SERVER}`);
  console.log(`SwyxIt!: NOT running`);
  console.log(`Port 9094 before: ${checkPort(9094) ? 'OPEN' : 'CLOSED'}`);
  console.log('');
  
  // Spawn SwyxBridge
  console.log('Spawning SwyxBridge...');
  const bridge = spawn('powershell.exe', ['-Command', BRIDGE_PATH], {
    stdio: ['pipe', 'pipe', 'pipe']
  });
  
  let responses = [];
  
  bridge.stdout.on('data', (data) => {
    const lines = data.toString().split('\n').filter(l => l.trim());
    for (const line of lines) {
      try {
        const msg = JSON.parse(line);
        if (msg.id) {
          responses.push(msg);
          console.log(`<<< Response id=${msg.id}:`, JSON.stringify(msg.result || msg.error, null, 2));
        } else if (msg.method) {
          console.log(`<<< Event: ${msg.method}`, JSON.stringify(msg.params));
        }
      } catch {
        // not JSON, skip
      }
    }
  });
  
  bridge.stderr.on('data', (data) => {
    const lines = data.toString().split('\n').filter(l => l.trim());
    for (const line of lines) {
      if (line.includes('DispInit') || line.includes('LoggedIn') || line.includes('ServerUp') || 
          line.includes('tunnel') || line.includes('RemoteConnector') || line.includes('Standalone') ||
          line.includes('Auto-detect') || line.includes('E_NOTIMPL') || line.includes('NICHT') ||
          line.includes('connected') || line.includes('COM') || line.includes('Error') ||
          line.includes('failed') || line.includes('erfolgreich')) {
        console.log(`[BRIDGE] ${line.trim()}`);
      }
    }
  });
  
  // Wait for bridge to initialize
  await sleep(3000);
  
  // Step 1: Connect with server name (triggers DispInit)
  console.log('\n--- Step 1: DispInit with server name ---');
  sendRpc(bridge, 1, 'connect', { serverName: SERVER });
  
  // Wait for DispInit to complete and potentially establish tunnel
  await sleep(8000);
  
  // Step 2: Check port 9094
  console.log('\n--- Step 2: Check port 9094 after DispInit ---');
  const port9094After = checkPort(9094);
  console.log(`Port 9094 after DispInit: ${port9094After ? 'OPEN ✅' : 'CLOSED ❌'}`);
  
  // Step 3: Check connection info
  console.log('\n--- Step 3: Get connection info ---');
  sendRpc(bridge, 2, 'getConnectionInfo');
  await sleep(2000);
  
  // Step 4: If port 9094 is open, try CDS connect
  if (port9094After) {
    console.log('\n--- Step 4: Port open! Trying CDS Connect ---');
    sendRpc(bridge, 3, 'cdsConnect', {
      host: '127.0.0.1',
      port: 9094,
      username: 'Ralf Arnold',
      password: 'w9P1sK5h'
    });
    await sleep(10000);
  } else {
    console.log('\n--- Step 4: Port still closed. Waiting 15s more in case tunnel is slow... ---');
    await sleep(15000);
    const port9094Retry = checkPort(9094);
    console.log(`Port 9094 after 15s wait: ${port9094Retry ? 'OPEN ✅' : 'STILL CLOSED ❌'}`);
    
    if (port9094Retry) {
      console.log('\n--- Port opened after delay! Trying CDS Connect ---');
      sendRpc(bridge, 3, 'cdsConnect', {
        host: '127.0.0.1',
        port: 9094,
        username: 'Ralf Arnold',
        password: 'w9P1sK5h'
      });
      await sleep(10000);
    }
  }
  
  // Step 5: Also check other ports
  console.log('\n--- Step 5: Port scan ---');
  for (const port of [5060, 9094, 9100, 12042]) {
    const open = checkPort(port);
    console.log(`  Port ${port}: ${open ? 'OPEN' : 'CLOSED'}`);
  }
  
  // Cleanup
  console.log('\n--- Cleanup ---');
  sendRpc(bridge, 99, 'disconnect');
  await sleep(1000);
  bridge.kill();
  
  console.log('\n=== Test Complete ===');
}

main().catch(console.error);
