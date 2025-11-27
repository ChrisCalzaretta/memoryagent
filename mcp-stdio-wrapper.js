#!/usr/bin/env node

/**
 * MCP STDIO Wrapper for Memory Code Agent
 * Bridges Cursor's STDIO transport to our HTTP MCP server
 * Supports multi-workspace with automatic context injection
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

// Configuration
const WORKSPACE_PATH = process.env.WORKSPACE_PATH;  // From Cursor via ${workspaceFolder}
const MCP_PORT = 5000;  // Always port 5000 for shared stack
const LOG_FILE = process.env.MCP_LOG_FILE || 'E:\\GitHub\\MemoryAgent\\mcp-wrapper.log';

// Extract context from workspace path
const CONTEXT_NAME = WORKSPACE_PATH ? path.basename(WORKSPACE_PATH) : 'default';

function log(message) {
  const timestamp = new Date().toISOString();
  const logMessage = `[${timestamp}] ${message}\n`;
  try {
    fs.appendFileSync(LOG_FILE, logMessage);
  } catch (err) {
    // Ignore logging errors
  }
}

log('MCP Wrapper started (multi-workspace mode)');
log(`  Workspace: ${WORKSPACE_PATH || 'not set'}`);
log(`  Context: ${CONTEXT_NAME}`);
log(`  MCP Port: ${MCP_PORT}`);

// Function to send HTTP request to MCP server
function sendToMcpServer(jsonRpcRequest) {
  return new Promise((resolve, reject) => {
    
    // Auto-inject context into tool calls (except workspace registration)
    if (jsonRpcRequest.method === 'tools/call' && 
        jsonRpcRequest.id !== 'register-workspace' &&
        jsonRpcRequest.id !== 'unregister-workspace') {
      const params = jsonRpcRequest.params;
      if (params && params.arguments && !params.arguments.context && CONTEXT_NAME !== 'default') {
        params.arguments.context = CONTEXT_NAME;
        log(`Auto-injected context: ${CONTEXT_NAME}`);
      }
    }
    
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

// Register workspace on startup
async function registerWorkspace() {
  if (!WORKSPACE_PATH) {
    log('⚠️ WORKSPACE_PATH not set, skipping registration');
    return;
  }
  
  try {
    const request = {
      jsonrpc: '2.0',
      id: 'register-workspace',
      method: 'tools/call',
      params: {
        name: 'register_workspace',
        arguments: {
          workspacePath: WORKSPACE_PATH,
          context: CONTEXT_NAME
        }
      }
    };
    
    await sendToMcpServer(request);
    log(`✅ Workspace registered: ${WORKSPACE_PATH} → ${CONTEXT_NAME}`);
  } catch (err) {
    log(`⚠️ Failed to register workspace: ${err.message}`);
  }
}

// Unregister workspace on shutdown
async function unregisterWorkspace() {
  if (!WORKSPACE_PATH) {
    return;
  }
  
  try {
    const request = {
      jsonrpc: '2.0',
      id: 'unregister-workspace',
      method: 'tools/call',
      params: {
        name: 'unregister_workspace',
        arguments: {
          workspacePath: WORKSPACE_PATH
        }
      }
    };
    
    await sendToMcpServer(request);
    log(`✅ Workspace unregistered: ${WORKSPACE_PATH}`);
  } catch (err) {
    log(`⚠️ Failed to unregister workspace: ${err.message}`);
  }
}

// Wait a bit for MCP server to be ready, then register
setTimeout(() => {
  registerWorkspace();
}, 1000);

process.stdin.on('end', () => {
  process.exit(0);
});

// Handle process signals
process.on('SIGINT', async () => {
  await unregisterWorkspace();
  process.exit(0);
});

process.on('SIGTERM', async () => {
  await unregisterWorkspace();
  process.exit(0);
});
