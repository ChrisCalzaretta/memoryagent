#!/usr/bin/env node

/**
 * MCP STDIO Wrapper for Memory Code Agent (DEBUG VERSION)
 */

const http = require('http');

console.error('[DEBUG] Wrapper started');

// Function to send HTTP request to MCP server
function sendToMcpServer(jsonRpcRequest) {
  console.error('[DEBUG] Sending request to MCP server:', JSON.stringify(jsonRpcRequest));
  
  return new Promise((resolve, reject) => {
    const postData = JSON.stringify(jsonRpcRequest);
    
    const options = {
      hostname: 'localhost',
      port: 5000,
      path: '/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(postData)
      }
    };

    const req = http.request(options, (res) => {
      let data = '';
      
      res.on('data', (chunk) => {
        data += chunk;
      });
      
      res.on('end', () => {
        console.error('[DEBUG] Received response from server');
        try {
          const response = JSON.parse(data);
          resolve(response);
        } catch (err) {
          console.error('[DEBUG] Parse error:', err.message);
          reject(new Error(`Failed to parse response: ${err.message}`));
        }
      });
    });

    req.on('error', (err) => {
      console.error('[DEBUG] HTTP error:', err.message);
      reject(err);
    });

    req.write(postData);
    req.end();
  });
}

// Read from STDIN
let buffer = '';

process.stdin.on('data', (chunk) => {
  console.error('[DEBUG] Received data chunk:', chunk.toString());
  buffer += chunk.toString();
  
  // Process complete lines
  let newlineIndex;
  while ((newlineIndex = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, newlineIndex).trim();
    buffer = buffer.slice(newlineIndex + 1);
    
    console.error('[DEBUG] Processing line:', line);
    
    if (line) {
      (async () => {
        try {
          const request = JSON.parse(line);
          const response = await sendToMcpServer(request);
          
          console.error('[DEBUG] Sending response to stdout');
          process.stdout.write(JSON.stringify(response) + '\n');
        } catch (err) {
          console.error('[DEBUG] Error:', err.message);
          const errorResponse = {
            jsonrpc: '2.0',
            id: null,
            error: {
              code: -32603,
              message: `Internal error: ${err.message}`
            }
          };
          process.stdout.write(JSON.stringify(errorResponse) + '\n');
        }
      })();
    }
  }
});

process.stdin.on('end', () => {
  console.error('[DEBUG] STDIN ended');
  setTimeout(() => process.exit(0), 1000); // Give async operations time to complete
});

process.on('SIGINT', () => {
  console.error('[DEBUG] SIGINT received');
  process.exit(0);
});

process.on('SIGTERM', () => {
  console.error('[DEBUG] SIGTERM received');
  process.exit(0);
});

console.error('[DEBUG] Waiting for input...');

