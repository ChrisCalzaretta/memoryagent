#!/usr/bin/env node
/**
 * MCP PASSTHROUGH Wrapper for Memory Agent
 * 
 * This exposes ALL MemoryAgent tools directly (33 tools).
 * Used by Cursor's AI for background context and intelligent assistance.
 * 
 * For USER commands, use mcp-wrapper-router.js instead.
 */

const { spawn } = require('child_process');
const http = require('http');
const path = require('path');

// Configuration
const MEMORYAGENT_PATH = path.dirname(__filename);
const WORKSPACE_PATH = process.env.PROJECT_PATH || process.cwd();
const MCP_URL = 'http://localhost:5000';
const HEALTH_URL = `${MCP_URL}/api/health`;
const MCP_POST_URL = `${MCP_URL}/mcp`;

// Logging
function log(message, level = 'INFO') {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}] [MCP-Passthrough] [${level}] ${message}`);
}

// HTTP helper for POST requests
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
          reject(new Error(`Failed to parse response: ${err.message}`));
        }
      });
    });
    
    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy();
      reject(new Error('Request timeout'));
    });
    req.write(postData);
    req.end();
  });
}

// HTTP helper for GET requests
function httpGet(url) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    const options = {
      hostname: urlObj.hostname,
      port: urlObj.port,
      path: urlObj.pathname,
      method: 'GET',
      timeout: 5000
    };
    
    const req = http.request(options, (res) => {
      let responseData = '';
      res.on('data', (chunk) => responseData += chunk);
      res.on('end', () => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          try {
            resolve(JSON.parse(responseData));
          } catch (err) {
            resolve({ status: 'ok' });
          }
        } else {
          reject(new Error(`HTTP ${res.statusCode}`));
        }
      });
    });
    
    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy();
      reject(new Error('Timeout'));
    });
    req.end();
  });
}

// Check if Docker containers are running
async function checkContainersRunning() {
  return new Promise((resolve) => {
    const ps = spawn('docker-compose', ['-f', 'docker-compose-shared-Calzaretta.yml', 'ps', '--services', '--filter', 'status=running'], {
      cwd: MEMORYAGENT_PATH,
      shell: true
    });
    
    let output = '';
    ps.stdout.on('data', (data) => output += data.toString());
    ps.on('close', () => {
      const running = output.split('\n').filter(s => s.trim());
      resolve(running.includes('mcp-server'));
    });
  });
}

// Start Docker containers
async function startContainers() {
  return new Promise((resolve, reject) => {
    const compose = spawn('docker-compose', ['-f', 'docker-compose-shared-Calzaretta.yml', 'up', '-d', 'mcp-server'], {
      cwd: MEMORYAGENT_PATH,
      shell: true
    });
    
    compose.on('close', (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(new Error(`docker-compose exited with code ${code}`));
      }
    });
  });
}

// Wait for MCP server to be ready
async function waitForServer(maxAttempts = 30) {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      await httpGet(HEALTH_URL);
      log('✅ MCP server is healthy');
      return true;
    } catch (err) {
      log(`Waiting for MCP server... (${i + 1}/${maxAttempts})`);
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
  }
  throw new Error('MCP server did not become healthy in time');
}

// Send MCP request to MemoryAgent
async function sendMcpRequest(request) {
  return await httpPost(MCP_POST_URL, request);
}

// Handle MCP protocol over STDIO
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
          
          const msgType = isNotification ? 'notification' : 'request';
          log(`📥 ${msgType}: ${request.method || 'unknown'}`);
          
          if (isNotification) {
            // Notifications - forward but don't wait for response
            sendMcpRequest(request).catch(err => {
              log(`Error forwarding notification: ${err.message}`, 'ERROR');
            });
          } else {
            // Requests - forward and send response
            const response = await sendMcpRequest(request);
            
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
    log('STDIN closed, cleaning up...');
    process.exit(0);
  });
}

// Main
async function main() {
  try {
    log('═══════════════════════════════════════════════════════');
    log('🧠 MemoryAgent MCP - PASSTHROUGH MODE');
    log('   Version: 2.0.0 (Direct access to all 33 tools)');
    log('═══════════════════════════════════════════════════════');
    log('📁 Paths:');
    log(`   MemoryAgent: ${MEMORYAGENT_PATH}`);
    log(`   Workspace: ${WORKSPACE_PATH}`);
    log('═══════════════════════════════════════════════════════');
    
    const running = await checkContainersRunning();
    
    if (!running) {
      log('Starting Docker container...');
      await startContainers();
      await new Promise(resolve => setTimeout(resolve, 5000));
    } else {
      log('✅ Container already running');
    }
    
    log('Waiting for MCP server...');
    await waitForServer();
    
    log('═══════════════════════════════════════════════════════');
    log('✅ Ready! Exposing ALL MemoryAgent tools:');
    log('   - smartsearch, get_context, validate, etc.');
    log('   - For AI context and background intelligence');
    log('   - Use @memory-agent to access these tools');
    log('═══════════════════════════════════════════════════════');
    
    await handleStdio();
    
  } catch (err) {
    log(`Fatal error: ${err.message}`, 'ERROR');
    process.exit(1);
  }
}

main();


