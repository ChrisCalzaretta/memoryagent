const http = require('http');

async function callMemoryRouter(method, params = {}) {
  return new Promise((resolve, reject) => {
    const requestBody = JSON.stringify({
      jsonrpc: '2.0',
      id: Date.now(),
      method: method,
      params: params
    });

    console.log('Request body:', requestBody);

    const url = new URL('/api/mcp', 'http://localhost:5010');
    console.log('URL:', url.toString());
    
    const options = {
      hostname: url.hostname,
      port: url.port || 5010,
      path: url.pathname,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(requestBody)
      }
    };

    console.log('Options:', JSON.stringify(options, null, 2));

    const req = http.request(options, (res) => {
      let data = '';

      res.on('data', (chunk) => {
        data += chunk;
      });

      res.on('end', () => {
        console.log('Raw response:', data.substring(0, 300));
        try {
          const response = JSON.parse(data);
          console.log('Parsed response.result.tools:', response.result?.tools?.length);
          resolve(response);
        } catch (error) {
          console.error('Parse error:', error.message);
          reject(new Error(`Failed to parse response: ${error.message}`));
        }
      });
    });

    req.on('error', (error) => {
      console.error('HTTP error:', error.message);
      reject(new Error(`HTTP request failed: ${error.message}`));
    });

    req.write(requestBody);
    req.end();
  });
}

callMemoryRouter('tools/list', {})
  .then(r => {
    console.log('\n✅ SUCCESS!');
    console.log('Tools count:', r.result?.tools?.length);
    console.log('Tool names:', r.result?.tools?.map(t => t.name));
  })
  .catch(e => {
    console.error('\n❌ FAILED:', e.message);
  });


