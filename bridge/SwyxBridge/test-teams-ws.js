const WebSocket = require('ws');

async function testPort(port) {
    console.log(`Testing port ${port}...`);
    const url = `ws://127.0.0.1:${port}?token=&protocol-version=2.0.0&manufacturer=Tester&device=TestDevice&app=SwyIt&app-version=1.0.0`;

    return new Promise((resolve) => {
        const ws = new WebSocket(url);

        ws.on('open', () => {
            console.log(`Connected to Teams on port ${port}!`);
            ws.send(JSON.stringify({
                action: 'query-state',
                parameters: {
                    apiVersion: '1.0'
                }
            }));
        });

        ws.on('message', (data) => {
            console.log(`Data from ${port}:`, data.toString());
            try {
                const parsed = JSON.parse(data.toString());
                if (parsed.tokenRefresh) {
                    console.log(`Token received: ${parsed.tokenRefresh}`);
                }
            } catch (e) { }
        });

        ws.on('error', (err) => {
            console.log(`Port ${port} error:`, err.message);
            resolve(false);
        });

        ws.on('close', () => {
            console.log(`Port ${port} closed.`);
            resolve(true);
        });

        // Timeout
        setTimeout(() => {
            ws.close();
            resolve(true);
        }, 5000);
    });
}

async function run() {
    for (let p = 8124; p <= 8130; p++) {
        await testPort(p);
    }
}

run();
