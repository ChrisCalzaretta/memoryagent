#!/usr/bin/env node

/**
 * 🧠 MEMORY AGENT MCP Server
 * 
 * AI-powered semantic search, code analysis, and knowledge management.
 * Routes through MemoryRouter with FunctionGemma for intelligent tool selection.
 * 
 * Tools provided (33+):
 * - Search: semantic_search, smart_search, graph_search
 * - Analysis: explain_code, impact_analysis, complexity_analysis
 * - Knowledge: store_qa, get_insights, start_session
 * - Planning: create_plan, estimate_complexity
 * - Indexing: index_workspace, reindex_all
 * 
 * Usage: Add to Cursor MCP config:
 * {
 *   "memory-agent": {
 *     "command": "node",
 *     "args": ["memory-router-mcp-wrapper.js", "${workspaceFolder}"]
 *   }
 * }
 * 
 * For code generation, use the separate 'code-generator' MCP server.
 */

const http = require('http');
const path = require('path');

// Configuration
const MEMORY_ROUTER_URL = process.env.MEMORY_ROUTER_URL || 'http://localhost:5010';
const WORKSPACE_FOLDER = process.argv[2] || process.cwd();
const WORKSPACE_NAME = path.basename(WORKSPACE_FOLDER);

// Parse workspace folder to extract context name
const context = WORKSPACE_NAME.toLowerCase().replace(/[^a-z0-9]/g, '');

// Log configuration (to stderr so it doesn't interfere with STDIO protocol)
console.error(`🧠 Memory Agent MCP Server Starting...`);
console.error(`   Workspace: ${WORKSPACE_FOLDER}`);
console.error(`   Context: ${context}`);
console.error(`   Router URL: ${MEMORY_ROUTER_URL}`);
console.error(`   💡 For code generation, use 'code-generator' MCP server`);

/**
 * Call MemoryRouter HTTP endpoint
 */
async function callMemoryRouter(method, params = {}) {
  return new Promise((resolve, reject) => {
    const requestBody = JSON.stringify({
      jsonrpc: '2.0',
      id: Date.now(),
      method: method,
      params: params
    });

    const url = new URL('/api/mcp', MEMORY_ROUTER_URL);
    
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

    const req = http.request(options, (res) => {
      let data = '';

      res.on('data', (chunk) => {
        data += chunk;
      });

      res.on('end', () => {
        try {
          const response = JSON.parse(data);
          resolve(response);
        } catch (error) {
          reject(new Error(`Failed to parse response: ${error.message}`));
        }
      });
    });

    req.on('error', (error) => {
      reject(new Error(`HTTP request failed: ${error.message}`));
    });

    req.write(requestBody);
    req.end();
  });
}

/**
 * Handle MCP requests from Cursor
 */
async function handleRequest(request) {
  try {
    const { method, params } = request;

    // Route based on MCP method
    switch (method) {
      case 'initialize':
        return {
          jsonrpc: '2.0',
          id: request.id,
          result: {
            protocolVersion: '2024-11-05',
            capabilities: {
              tools: {}
            },
            serverInfo: {
              name: 'memory-agent',
              version: '2.0.0',
              description: 'AI-powered semantic search and code analysis'
            }
          }
        };

      case 'tools/list':
        // Get tools from MemoryRouter
        const toolsResponse = await callMemoryRouter('tools/list', {});
        return {
          jsonrpc: '2.0',
          id: request.id,
          result: toolsResponse.result || { tools: [] }
        };

      case 'tools/call':
        // Call tool via MemoryRouter
        const toolName = params.name;
        const toolArgs = params.arguments || {};

        // Automatically inject context and workspace for relevant tools
        if (!toolArgs.context && (toolName === 'execute_task' || toolName === 'list_available_tools')) {
          // For execute_task, context is usually implicit in the request
          // But we can add workspace info to the request
          if (toolName === 'execute_task' && toolArgs.request) {
            // Optionally enhance request with workspace context
            toolArgs.workspaceFolder = WORKSPACE_FOLDER;
            toolArgs.workspaceName = WORKSPACE_NAME;
            toolArgs.context = context;
          }
        }

        const callResponse = await callMemoryRouter('tools/call', {
          name: toolName,
          arguments: toolArgs
        });

        return {
          jsonrpc: '2.0',
          id: request.id,
          result: callResponse.result || {},
          error: callResponse.error
        };

      case 'notifications/initialized':
        // Acknowledge initialization
        return null; // No response needed for notifications

      default:
        return {
          jsonrpc: '2.0',
          id: request.id,
          error: {
            code: -32601,
            message: `Method not found: ${method}`
          }
        };
    }
  } catch (error) {
    console.error(`Error handling request: ${error.message}`, error);
    return {
      jsonrpc: '2.0',
      id: request.id,
      error: {
        code: -32603,
        message: `Internal error: ${error.message}`
      }
    };
  }
}

/**
 * Main STDIO loop
 */
async function main() {
  let buffer = '';

  // Health check MemoryRouter before starting
  try {
    const health = await new Promise((resolve, reject) => {
      const req = http.get(`${MEMORY_ROUTER_URL}/health`, (res) => {
        let data = '';
        res.on('data', chunk => data += chunk);
        res.on('end', () => resolve(JSON.parse(data)));
      });
      req.on('error', reject);
      req.setTimeout(5000, () => reject(new Error('Health check timeout')));
    });
    
    if (health.status === 'healthy') {
      console.error(`✅ MemoryRouter is healthy`);
    }
  } catch (error) {
    console.error(`⚠️  Warning: Could not reach MemoryRouter: ${error.message}`);
    console.error(`   Make sure MemoryRouter is running at ${MEMORY_ROUTER_URL}`);
  }

  // Process STDIN line by line
  process.stdin.setEncoding('utf8');

  // Track pending requests to prevent premature exit
  let pendingRequests = 0;
  let stdinEnded = false;

  async function processLine(line) {
    if (line.trim() === '') return;

    pendingRequests++;
    try {
      const request = JSON.parse(line);
      console.error(`📥 Received: ${request.method}`);

      const response = await handleRequest(request);
      
      if (response) {
        const responseStr = JSON.stringify(response);
        console.log(responseStr); // Send to STDOUT for Cursor
        console.error(`📤 Sent: ${response.result ? 'success' : response.error ? 'error' : 'response'}`);
      }
    } catch (error) {
      console.error(`❌ Error processing line: ${error.message}`);
      console.error(`   Line: ${line}`);
      
      // Try to send error response
      try {
        const errorResponse = {
          jsonrpc: '2.0',
          id: null,
          error: {
            code: -32700,
            message: `Parse error: ${error.message}`
          }
        };
        console.log(JSON.stringify(errorResponse));
      } catch (e) {
        console.error(`   Failed to send error response: ${e.message}`);
      }
    } finally {
      pendingRequests--;
      // Exit if stdin ended and no pending requests
      if (stdinEnded && pendingRequests === 0) {
        console.error('🛑 All requests processed, exiting...');
        process.exit(0);
      }
    }
  }

  process.stdin.on('data', (chunk) => {
    buffer += chunk;
    const lines = buffer.split('\n');
    buffer = lines.pop() || ''; // Keep the incomplete line in buffer

    // Process each complete line
    for (const line of lines) {
      processLine(line); // Fire and forget, but tracked via pendingRequests
    }
  });

  process.stdin.on('end', () => {
    console.error('🛑 STDIN closed, waiting for pending requests...');
    stdinEnded = true;
    
    // Only exit if no pending requests
    if (pendingRequests === 0) {
      console.error('🛑 No pending requests, exiting...');
      process.exit(0);
    }
    // Otherwise, processLine will exit when the last request completes
  });

  process.on('SIGINT', () => {
    console.error('🛑 Received SIGINT, exiting...');
    process.exit(0);
  });

  console.error('✅ Wrapper ready, waiting for requests...');
}

// Start the wrapper
main().catch((error) => {
  console.error(`💥 Fatal error: ${error.message}`);
  process.exit(1);
});





