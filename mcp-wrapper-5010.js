#!/usr/bin/env node
/**
 * MCP Wrapper for MemoryRouter (Port 5010)
 * 
 * Connects to the C# MemoryRouter service that:
 * - Auto-discovers MemoryAgent tools
 * - Categorizes them intelligently  
 * - Provides smart routing
 */

const http = require('http');

const MEMORY_ROUTER_URL = 'http://localhost:5010';
const MCP_URL = `${MEMORY_ROUTER_URL}/api/mcp`;

function log(message, level = 'INFO') {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}] [MCP-5010] [${level}] ${message}`);
}

function httpPost(url, data) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    const postData = JSON.stringify(data);
    
    const options = {
      hostname: urlObj.hostname,
      port: urlObj.port,
      path: urlObj.pathname,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(postData)
      },
      timeout: 30000
    };
    
    const req = http.request(options, (res) => {
      let responseData = '';
      res.on('data', (chunk) => responseData += chunk);
      res.on('end', () => {
        if (!responseData || responseData.trim() === '') {
          resolve(null);
          return;
        }
        try {
          resolve(JSON.parse(responseData));
        } catch (err) {
          reject(new Error(`Failed to parse: ${err.message}`));
        }
      });
    });
    
    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy();
      reject(new Error('Timeout'));
    });
    req.write(postData);
    req.end();
  });
}

async function handleStdio() {
  let buffer = '';
  
  process.stdin.on('data', async (chunk) => {
    buffer += chunk.toString();
    
    const lines = buffer.split('\n');
    buffer = lines.pop() || '';
    
    for (const line of lines) {
      if (line.trim()) {
        let requestId = null;
        let isNotification = false;
        
        try {
          const request = JSON.parse(line);
          requestId = request.id;
          isNotification = (request.id === undefined || request.id === null);
          
          log(`📥 ${isNotification ? 'notification' : 'request'}: ${request.method}`);
          
          if (isNotification) {
            // Fire and forget
            httpPost(MCP_URL, request).catch(err => {
              log(`Error: ${err.message}`, 'ERROR');
            });
          } else {
            // Wait for response
            const response = await httpPost(MCP_URL, request);
            
            if (response) {
              process.stdout.write(JSON.stringify(response) + '\n');
            }
          }
          
        } catch (err) {
          log(`Error: ${err.message}`, 'ERROR');
          
          if (!isNotification) {
            const errorResponse = {
              jsonrpc: "2.0",
              id: requestId,
              error: {
                code: -32603,
                message: err.message || 'Internal error'
              }
            };
            process.stdout.write(JSON.stringify(errorResponse) + '\n');
          }
        }
      }
    }
  });
  
  process.stdin.on('end', () => {
    log('STDIN closed');
    process.exit(0);
  });
}

async function main() {
  log('═══════════════════════════════════════════════════════');
  log('🧠 MemoryRouter MCP Wrapper (Port 5010)');
  log('   Connects to C# MemoryRouter service');
  log('   Smart categorization & routing');
  log('═══════════════════════════════════════════════════════');
  log('✅ Ready!');
  
  await handleStdio();
}

main();


