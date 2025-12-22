#!/usr/bin/env node
/**
 * EXPANDED MCP Wrapper for Memory Code Agent + CodingAgent
 * 
 * Features:
 * 1. Starts Docker containers if not running
 * 2. Connects to BOTH MemoryAgent (MCP) and CodingAgent (WebSocket)
 * 3. Proxies MCP protocol between Cursor (STDIO) and Docker (SSE)
 * 4. Handles real-time code generation with progress updates
 * 5. Interactive Q&A in Cursor's chat
 * 6. Access to AI Lightning for prompts and learning
 */

const { spawn } = require('child_process');
const http = require('http');
const EventSource = require('eventsource');
const path = require('path');

// Configuration
// MEMORYAGENT_PATH: Where MemoryAgent repo is (for docker-compose)
const MEMORYAGENT_PATH = path.dirname(__filename); // Directory where this script is
// WORKSPACE_PATH: User's current workspace (from Cursor env variable)
const WORKSPACE_PATH = process.env.PROJECT_PATH || process.cwd();

const MEMORY_AGENT_URL = 'http://localhost:5000';
const CODING_AGENT_URL = 'http://localhost:5001';
const WEBSOCKET_URL = 'ws://localhost:5001/hubs/codingagent';

// URLs
const MEMORY_HEALTH_URL = `${MEMORY_AGENT_URL}/api/health`;
const CODING_HEALTH_URL = `${CODING_AGENT_URL}/health`;
const MCP_POST_URL = `${MEMORY_AGENT_URL}/mcp`;
const ORCHESTRATE_URL = `${CODING_AGENT_URL}/api/orchestrate`;
const JOB_STATUS_URL = (jobId) => `${CODING_AGENT_URL}/api/jobs/${jobId}`;

// State
let currentJob = null;
let jobStatusInterval = null;

// Logging
function log(message, level = 'INFO') {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}] [MCP-Wrapper] [${level}] ${message}`);
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
    ps.on('close', (code) => {
      const hasMcp = output.includes('mcp-server'); // Correct service name!
      const hasCoding = output.includes('coding-agent');
      resolve({ memoryAgent: hasMcp, codingAgent: hasCoding });
    });
  });
}

// Start Docker containers
async function startContainers() {
  log('Starting Docker containers...');
  return new Promise((resolve, reject) => {
    const compose = spawn('docker-compose', ['-f', 'docker-compose-shared-Calzaretta.yml', 'up', '-d', 'mcp-server', 'coding-agent'], {
      cwd: MEMORYAGENT_PATH,
      shell: true
    });
    
    compose.stdout.on('data', (data) => log(data.toString().trim(), 'DOCKER'));
    compose.stderr.on('data', (data) => log(data.toString().trim(), 'DOCKER'));
    
    compose.on('close', (code) => {
      if (code === 0) {
        log('Containers started successfully');
        resolve();
      } else {
        reject(new Error(`docker-compose failed with code ${code}`));
      }
    });
  });
}

// Wait for servers to be ready
async function waitForServers(maxAttempts = 30) {
  log('Waiting for servers to be ready...');
  
  for (let i = 0; i < maxAttempts; i++) {
    try {
      // Check MemoryAgent
      const memoryHealthy = await checkHealth(MEMORY_HEALTH_URL);
      
      // Check CodingAgent  
      const codingHealthy = await checkHealth(CODING_HEALTH_URL);
      
      if (memoryHealthy && codingHealthy) {
        log('✅ Both servers are ready!');
        log(`   mcp-server: ${MEMORY_HEALTH_URL}`);
        log(`   coding-agent: ${CODING_HEALTH_URL}`);
        return true;
      }
    } catch (err) {
      // Servers not ready yet
      log(`  Attempt ${i + 1}/${maxAttempts}... (${err.message})`, 'DEBUG');
    }
    
    await new Promise(resolve => setTimeout(resolve, 2000));
  }
  
  throw new Error('Servers did not become ready in time');
}

// Check if a health endpoint is responding
function checkHealth(url) {
  return new Promise((resolve) => {
    const urlObj = new URL(url);
    const options = {
      hostname: urlObj.hostname,
      port: urlObj.port,
      path: urlObj.pathname,
      method: 'GET',
      timeout: 1000
    };
    
    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => data += chunk);
      res.on('end', () => {
        try {
          const json = JSON.parse(data);
          // Accept both "healthy" and "Healthy"
          resolve(json.status && json.status.toLowerCase() === 'healthy');
        } catch {
          resolve(false);
        }
      });
    });
    
    req.on('error', () => resolve(false));
    req.on('timeout', () => {
      req.destroy();
      resolve(false);
    });
    
    req.end();
  });
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
      }
    };
    
    const req = http.request(options, (res) => {
      let responseData = '';
      res.on('data', (chunk) => responseData += chunk);
      res.on('end', () => {
        // Handle empty responses (common for notifications)
        if (!responseData || responseData.trim() === '') {
          resolve(null);
          return;
        }
        
        try {
          resolve(JSON.parse(responseData));
        } catch (err) {
          reject(new Error(`Failed to parse response: ${err.message} (Response: ${responseData.substring(0, 100)})`));
        }
      });
    });
    
    req.on('error', reject);
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
      method: 'GET'
    };
    
    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => data += chunk);
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (err) {
          reject(new Error(`Failed to parse response: ${err.message}`));
        }
      });
    });
    
    req.on('error', reject);
    req.end();
  });
}

// Send MCP request to MemoryAgent
async function sendMcpRequest(request) {
  return await httpPost(MCP_POST_URL, request);
}

// Start code generation job
async function startCodeGeneration(task, language = 'csharp') {
  log(`🚀 Starting code generation: ${task}`);
  
  const data = await httpPost(ORCHESTRATE_URL, {
    task,
    language,
    workspacePath: WORKSPACE_PATH,
    maxIterations: 10
  });
  
  currentJob = data.jobId;
  
  log(`✅ Job started: ${currentJob}`);
  
  // Send initial response to Cursor
  sendCursorMessage({
    type: 'job_started',
    jobId: currentJob,
    message: `🤖 CodingAgent: Job started (${currentJob})\n\n🔍 Exploring codebase...`
  });
  
  // Start monitoring job status
  startJobMonitoring();
  
  return data;
}

// Monitor job status and send updates to Cursor
function startJobMonitoring() {
  if (jobStatusInterval) {
    clearInterval(jobStatusInterval);
  }
  
  jobStatusInterval = setInterval(async () => {
    if (!currentJob) {
      clearInterval(jobStatusInterval);
      return;
    }
    
    try {
      const status = await httpGet(JOB_STATUS_URL(currentJob));
      
      // Send status update to Cursor
      let message = '';
      
      switch (status.status) {
        case 'thinking':
          message = `💭 ${status.currentStep || 'Thinking...'}`;
          break;
        case 'generating':
          message = `⚙️ Generating code... (iteration ${status.iteration}/${status.maxIterations})`;
          break;
        case 'validating':
          message = `📊 Validating code...`;
          break;
        case 'completed':
          message = `✅ Complete! Score: ${status.score}/10\n\n📄 Generated ${status.files?.length || 0} files:\n${status.files?.map(f => `- ${f}`).join('\n')}`;
          clearInterval(jobStatusInterval);
          currentJob = null;
          break;
        case 'failed':
          message = `❌ Job failed: ${status.error}`;
          clearInterval(jobStatusInterval);
          currentJob = null;
          break;
      }
      
      if (message) {
        sendCursorMessage({
          type: 'progress_update',
          jobId: currentJob,
          status: status.status,
          message
        });
      }
      
    } catch (err) {
      log(`Error checking job status: ${err.message}`, 'ERROR');
    }
  }, 2000); // Poll every 2 seconds
}

// Send message to Cursor (via STDOUT)
function sendCursorMessage(data) {
  // MCP-compatible message format
  const message = {
    jsonrpc: "2.0",
    method: "notifications/message",
    params: {
      level: "info",
      message: typeof data === 'string' ? data : data.message,
      data: typeof data === 'object' ? data : undefined
    }
  };
  
  process.stdout.write(JSON.stringify(message) + '\n');
  log(`📤 Sent to Cursor: ${message.params.message.substring(0, 100)}...`);
}

// Handle special MCP methods for code generation
async function handleCustomMethod(method, params) {
  switch (method) {
    case 'codingagent/generate':
      return await startCodeGeneration(params.task, params.language);
      
    case 'codingagent/status':
      if (currentJob) {
        return await httpGet(JOB_STATUS_URL(currentJob));
      }
      return { status: 'no_active_job' };
      
    case 'codingagent/cancel':
      if (currentJob) {
        log(`🛑 Cancelling job: ${currentJob}`);
        clearInterval(jobStatusInterval);
        currentJob = null;
        return { success: true, message: 'Job cancelled' };
      }
      return { success: false, message: 'No active job' };
      
    case 'lightning/get_prompt':
      // Get best prompt from AI Lightning via MemoryAgent
      const mcpRequest = {
        jsonrpc: "2.0",
        method: "tools/call",
        params: {
          name: "manage_prompts",
          arguments: {
            action: "get_best",
            prompt_id: params.prompt_id
          }
        },
        id: Date.now()
      };
      return await sendMcpRequest(mcpRequest);
      
    default:
      throw new Error(`Unknown method: ${method}`);
  }
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
        let requestId = null;
        let isNotification = false;
        
        try {
          const request = JSON.parse(line);
          requestId = request.id; // Capture ID for error responses
          
          // Check if this is a notification (no id or id is null)
          isNotification = (request.id === undefined || request.id === null);
          
          const msgType = isNotification ? 'notification' : 'request';
          log(`📥 Received ${msgType}: ${request.method || 'unknown'}`);
          
          let response;
          
          // Handle based on message type
          if (isNotification) {
            // Notifications: forward but don't wait for response
            if (request.method && (request.method.startsWith('codingagent/') || request.method.startsWith('lightning/'))) {
              // Custom notification - handle but don't respond
              await handleCustomMethod(request.method, request.params).catch(err => {
                log(`Error handling notification: ${err.message}`, 'ERROR');
              });
            } else {
              // Forward notification to MCP server (fire and forget)
              sendMcpRequest(request).catch(err => {
                log(`Error forwarding notification: ${err.message}`, 'ERROR');
              });
            }
          } else {
            // Requests: handle and send response
            if (request.method && (request.method.startsWith('codingagent/') || request.method.startsWith('lightning/'))) {
              const result = await handleCustomMethod(request.method, request.params);
              response = {
                jsonrpc: "2.0",
                id: request.id,
                result
              };
            } else {
              // Forward to MemoryAgent MCP server
              response = await sendMcpRequest(request);
            }
            
            // Send response back to Cursor
            if (response) {
              process.stdout.write(JSON.stringify(response) + '\n');
            }
          }
          
        } catch (err) {
          log(`Error processing ${isNotification ? 'notification' : 'request'}: ${err.message}`, 'ERROR');
          
          // Only send error response for requests (not notifications)
          if (!isNotification) {
            const errorResponse = {
              jsonrpc: "2.0",
              id: requestId, // Use captured ID (null if parsing failed)
              error: {
                code: -32603, // Internal error
                message: err.message || 'Internal error',
                data: {
                  stack: err.stack
                }
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
    if (jobStatusInterval) {
      clearInterval(jobStatusInterval);
    }
    process.exit(0);
  });
}

// Main
async function main() {
  try {
    log('═══════════════════════════════════════════════════════');
    log('🤖 Memory Code Agent + CodingAgent MCP Wrapper');
    log('   Version: 2.0.0 (Expanded with CodingAgent support)');
    log('═══════════════════════════════════════════════════════');
    log('📁 Paths:');
    log(`   MemoryAgent: ${MEMORYAGENT_PATH}`);
    log(`   Workspace: ${WORKSPACE_PATH}`);
    log('═══════════════════════════════════════════════════════');
    
    // Check if containers are running
    const running = await checkContainersRunning();
    
    if (!running.memoryAgent || !running.codingAgent) {
      log(`Status: mcp-server=${running.memoryAgent}, coding-agent=${running.codingAgent}`);
      log('Starting missing containers...');
      await startContainers();
    } else {
      log('✅ All containers already running');
    }
    
    // Wait for servers to be ready
    await waitForServers();
    
    log('═══════════════════════════════════════════════════════');
    log('✅ Ready to handle requests!');
    log('');
    log('Connected services:');
    log('  - mcp-server (port 5000) - MCP tools, AI Lightning');
    log('  - coding-agent (port 5001) - Code generation');
    log('');
    log('Available methods:');
    log('  - Standard MCP tools (via mcp-server)');
    log('  - codingagent/generate - Start code generation');
    log('  - codingagent/status - Check current job');
    log('  - codingagent/cancel - Cancel current job');
    log('  - lightning/get_prompt - Get AI Lightning prompt');
    log('═══════════════════════════════════════════════════════');
    
    // Start handling STDIO
    await handleStdio();
    
  } catch (err) {
    log(`Fatal error: ${err.message}`, 'ERROR');
    log(err.stack, 'ERROR');
    process.exit(1);
  }
}

// Handle graceful shutdown
process.on('SIGINT', () => {
  log('Received SIGINT, shutting down gracefully...');
  if (jobStatusInterval) {
    clearInterval(jobStatusInterval);
  }
  process.exit(0);
});

process.on('SIGTERM', () => {
  log('Received SIGTERM, shutting down gracefully...');
  if (jobStatusInterval) {
    clearInterval(jobStatusInterval);
  }
  process.exit(0);
});

main();

