#!/usr/bin/env node

/**
 * MCP STDIO Wrapper for Memory Code Agent
 * Bridges Cursor's STDIO transport to our HTTP MCP server
 */

const http = require('http');
const fs = require('fs');

const MCP_PORT = process.env.MCP_PORT || 5098;
const LOG_FILE = process.env.MCP_LOG_FILE || 'E:\\GitHub\\MemoryAgent\\mcp-wrapper.log';

function log(message) {
  const timestamp = new Date().toISOString();
  const logMessage = `[${timestamp}] ${message}\n`;
  try {
    fs.appendFileSync(LOG_FILE, logMessage);
  } catch (err) {
    // Ignore logging errors
  }
}

log('MCP Wrapper started');

// Function to send HTTP request to MCP server
function sendToMcpServer(jsonRpcRequest) {
  return new Promise((resolve, reject) => {
    const postData = JSON.stringify(jsonRpcRequest);
    
    const options = {
      hostname: 'localhost',
      port: MCP_PORT,
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
        // Handle 204 No Content (notifications)
        if (res.statusCode === 204 || data.trim() === '') {
          resolve(null);
          return;
        }
        
        try {
          const response = JSON.parse(data);
          resolve(response);
        } catch (err) {
          reject(new Error(`Failed to parse response: ${err.message}`));
        }
      });
    });

    req.on('error', (err) => {
      reject(err);
    });

    req.write(postData);
    req.end();
  });
}

// Read from STDIN line by line
let buffer = '';

process.stdin.on('data', async (chunk) => {
  buffer += chunk.toString();
  
  // Process complete lines
  let newlineIndex;
  while ((newlineIndex = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, newlineIndex).trim();
    buffer = buffer.slice(newlineIndex + 1);
    
    if (line) {
      let requestId = null;
      try {
        // Parse the JSON-RPC request
        const request = JSON.parse(line);
        requestId = request.id; // Preserve the request ID
        
        log(`Received request: ${request.method} (id: ${requestId})`);
        
        // Forward to MCP server
        const response = await sendToMcpServer(request);
        
        log(`Got response for ${request.method}: ${response ? 'success' : 'null (notification)'}`);
        
        // Only write response if not null (notifications return null)
        if (response !== null) {
          process.stdout.write(JSON.stringify(response) + '\n');
        }
      } catch (err) {
        log(`Error processing request: ${err.message}`);
        // Send JSON-RPC error response with the original request ID
        const errorResponse = {
          jsonrpc: '2.0',
          id: requestId, // Use the ID from the original request
          error: {
            code: -32603,
            message: `Internal error: ${err.message}`
          }
        };
        process.stdout.write(JSON.stringify(errorResponse) + '\n');
      }
    }
  }
});

process.stdin.on('end', () => {
  process.exit(0);
});

// Handle process signals
process.on('SIGINT', () => {
  process.exit(0);
});

process.on('SIGTERM', () => {
  process.exit(0);
});
