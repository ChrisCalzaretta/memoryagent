#!/usr/bin/env node
/**
 * MCP Wrapper for Memory Code Agent
 * This script:
 * 1. Starts Docker containers if not running
 * 2. Connects to the MCP server via SSE
 * 3. Proxies MCP protocol between Cursor (STDIO) and Docker (SSE)
 */

const { spawn } = require('child_process');
const http = require('http');
const EventSource = require('eventsource');
const path = require('path');

const PROJECT_PATH = 'E:\\GitHub\\MemoryAgent';
const MCP_URL = 'http://localhost:5000';
const HEALTH_URL = `${MCP_URL}/api/health`;
const SSE_URL = `${MCP_URL}/sse`;
const MCP_POST_URL = `${MCP_URL}/mcp`;

// Logging
function log(message) {
  console.error(`[MCP-Wrapper] ${message}`);
}

// Check if Docker containers are running
async function checkContainersRunning() {
  return new Promise((resolve) => {
    const ps = spawn('docker-compose', ['ps', '--services', '--filter', 'status=running'], {
      cwd: PROJECT_PATH,
      shell: true
    });
    
    let output = '';
    ps.stdout.on('data', (data) => output += data.toString());
    ps.on('close', (code) => {
      resolve(output.includes('mcp-server'));
    });
  });
}

// Start Docker containers
async function startContainers() {
  log('Starting Docker containers...');
  return new Promise((resolve, reject) => {
    const compose = spawn('docker-compose', ['up', '-d'], {
      cwd: PROJECT_PATH,
      shell: true
    });
    
    compose.on('close', (code) => {
      if (code === 0) {
        log('Containers started');
        resolve();
      } else {
        reject(new Error(`docker-compose failed with code ${code}`));
      }
    });
  });
}

// Wait for MCP server to be ready
async function waitForServer(maxAttempts = 30) {
  log('Waiting for MCP server to be ready...');
  
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const response = await fetch(HEALTH_URL);
      const data = await response.json();
      if (data.status === 'healthy') {
        log('MCP server is ready!');
        return true;
      }
    } catch (err) {
      // Server not ready yet
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  
  throw new Error('MCP server did not become ready in time');
}

// Send MCP request to server
async function sendMcpRequest(request) {
  const response = await fetch(MCP_POST_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  });
  
  return await response.json();
}

// Handle MCP protocol over STDIO
async function handleStdio() {
  let buffer = '';
  
  process.stdin.on('data', async (chunk) => {
    buffer += chunk.toString();
    
    // Try to parse complete JSON-RPC messages
    const lines = buffer.split('\n');
    buffer = lines.pop() || ''; // Keep incomplete line in buffer
    
    for (const line of lines) {
      if (line.trim()) {
        try {
          const request = JSON.parse(line);
          log(`Received request: ${request.method}`);
          
          const response = await sendMcpRequest(request);
          
          // Send response back to Cursor via STDOUT
          process.stdout.write(JSON.stringify(response) + '\n');
        } catch (err) {
          log(`Error processing request: ${err.message}`);
          // Send error response
          const errorResponse = {
            jsonrpc: "2.0",
            id: null,
            error: {
              code: -32603,
              message: err.message
            }
          };
          process.stdout.write(JSON.stringify(errorResponse) + '\n');
        }
      }
    }
  });
  
  process.stdin.on('end', () => {
    log('STDIN closed, exiting');
    process.exit(0);
  });
}

// Main
async function main() {
  try {
    log('Memory Code Agent MCP Wrapper starting...');
    
    // Check if containers are running
    const running = await checkContainersRunning();
    
    if (!running) {
      // Start containers
      await startContainers();
    } else {
      log('Containers already running');
    }
    
    // Wait for server to be ready
    await waitForServer();
    
    // Start handling STDIO
    log('Ready to handle MCP requests');
    await handleStdio();
    
  } catch (err) {
    log(`Fatal error: ${err.message}`);
    process.exit(1);
  }
}

main();


