#!/usr/bin/env node

/**
 * 🚀 CODE GENERATOR MCP Server v2.0
 * 
 * Direct access to CodingAgent.Server for multi-language code generation.
 * Connects to the NEW CodingAgent.Server v2.0 architecture.
 * 
 * Tools provided:
 * - orchestrate_task: Start code generation job (background)
 * - get_task_status: Check job progress and get generated files
 * - cancel_task: Stop a running job
 * - list_tasks: See all active jobs
 * - apply_task_files: Get generated files ready for writing
 * 
 * Note: Design tools (design_*) are available through the memory-agent MCP server
 * 
 * Usage: Add to Cursor MCP config:
 * {
 *   "code-generator": {
 *     "command": "node",
 *     "args": ["orchestrator-mcp-wrapper.js", "${workspaceFolder}"]
 *   }
 * }
 * 
 * Server Architecture (v2.0):
 * - CodingAgent.Server (port 5001) - Code generation with Ollama/Claude
 * - ProjectOrchestrator - Template-based scaffolding + LLM generation
 * - JobManager - Background job execution with persistence
 * - Supports: C#, Python, TypeScript, JavaScript, Dart, Flutter, Go, Rust, and more
 */

const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');

// Configuration
const ORCHESTRATOR_HOST = process.env.ORCHESTRATOR_HOST || 'localhost';
const ORCHESTRATOR_PORT = process.env.ORCHESTRATOR_PORT || 5001; // NEW CodingAgent.Server!
const LOG_FILE = process.env.LOG_FILE || path.join(__dirname, 'orchestrator-wrapper.log');

// WebSocket status cache (stores recent events per job)
const jobEvents = new Map(); // jobId -> array of events
const MAX_EVENTS_PER_JOB = 50;

// Try to use @microsoft/signalr if available, otherwise track via polling
let signalR = null;
try {
  signalR = require('@microsoft/signalr');
  log('✅ SignalR available for real-time updates');
} catch (err) {
  log('⚠️ SignalR not available, will use polling for updates');
}

// Detect workspace path from multiple sources
let WORKSPACE_PATH = null;

// 1. Try command-line argument (highest priority)
if (process.argv.length > 2 && process.argv[2] !== '${workspaceFolder}') {
  WORKSPACE_PATH = process.argv[2];
}

// 2. Try environment variable
if (!WORKSPACE_PATH && process.env.WORKSPACE_PATH && process.env.WORKSPACE_PATH !== '${workspaceFolder}') {
  WORKSPACE_PATH = process.env.WORKSPACE_PATH;
}

// 3. Fallback to current directory
if (!WORKSPACE_PATH) {
  WORKSPACE_PATH = process.cwd();
}

// Extract context from workspace path (last directory name)
const CONTEXT_NAME = path.basename(WORKSPACE_PATH).toLowerCase().replace(/[^a-z0-9]/g, '');

function log(message) {
  const timestamp = new Date().toISOString();
  const logMessage = `[${timestamp}] ${message}\n`;
  try {
    fs.appendFileSync(LOG_FILE, logMessage);
  } catch (err) {
    // Ignore logging errors
  }
  // Also log to stderr for debugging
  console.error(`[code-generator] ${message}`);
}

log('🚀 Code Generator MCP Server started');
log(`   Workspace: ${WORKSPACE_PATH}`);
log(`   Context: ${CONTEXT_NAME}`);
log(`   Orchestrator: http://${ORCHESTRATOR_HOST}:${ORCHESTRATOR_PORT}`);

// ═══════════════════════════════════════════════════════════
// WebSocket/SignalR Connection for Real-Time Updates
// ═══════════════════════════════════════════════════════════

let hubConnection = null;

function addJobEvent(jobId, event) {
  if (!jobEvents.has(jobId)) {
    jobEvents.set(jobId, []);
  }
  const events = jobEvents.get(jobId);
  events.push({
    timestamp: new Date().toISOString(),
    ...event
  });
  // Keep only last N events
  if (events.length > MAX_EVENTS_PER_JOB) {
    events.shift();
  }
}

function connectToSignalR() {
  if (!signalR) {
    log('⚠️ SignalR not available, skipping WebSocket connection');
    return;
  }
  
  try {
    const hubUrl = `http://${ORCHESTRATOR_HOST}:${ORCHESTRATOR_PORT}/conversationHub`;
    log(`🔌 Connecting to SignalR hub: ${hubUrl}`);
    
    hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    
    // Listen for job progress events
    hubConnection.on('JobProgress', (jobId, message, progress) => {
      log(`📊 Job ${jobId}: ${message} (${progress}%)`);
      addJobEvent(jobId, {
        type: 'progress',
        message,
        progress
      });
    });
    
    // Listen for thinking events
    hubConnection.on('ThinkingUpdate', (jobId, message) => {
      log(`🧠 Job ${jobId}: ${message}`);
      addJobEvent(jobId, {
        type: 'thinking',
        message
      });
    });
    
    // Listen for code generation events
    hubConnection.on('CodeGeneration', (jobId, message) => {
      log(`💻 Job ${jobId}: ${message}`);
      addJobEvent(jobId, {
        type: 'coding',
        message
      });
    });
    
    // Listen for validation events
    hubConnection.on('ValidationUpdate', (jobId, message, score) => {
      log(`✅ Job ${jobId}: ${message} (score: ${score})`);
      addJobEvent(jobId, {
        type: 'validation',
        message,
        score
      });
    });
    
    // Listen for error events
    hubConnection.on('ErrorOccurred', (jobId, message) => {
      log(`❌ Job ${jobId}: ${message}`);
      addJobEvent(jobId, {
        type: 'error',
        message
      });
    });
    
    // Listen for completion events
    hubConnection.on('JobCompleted', (jobId, message) => {
      log(`🎉 Job ${jobId}: ${message}`);
      addJobEvent(jobId, {
        type: 'completed',
        message
      });
    });
    
    // Start connection
    hubConnection.start()
      .then(() => {
        log('✅ SignalR connected successfully');
      })
      .catch((err) => {
        log(`⚠️ SignalR connection failed: ${err.message}`);
        hubConnection = null;
      });
      
    // Handle disconnection
    hubConnection.onclose(() => {
      log('⚠️ SignalR disconnected');
    });
    
    hubConnection.onreconnecting(() => {
      log('🔄 SignalR reconnecting...');
    });
    
    hubConnection.onreconnected(() => {
      log('✅ SignalR reconnected');
    });
    
  } catch (err) {
    log(`❌ Failed to set up SignalR: ${err.message}`);
    hubConnection = null;
  }
}

// Connect to SignalR on startup
setTimeout(() => {
  connectToSignalR();
}, 2000); // Give the server time to start

// Tool definitions
const TOOLS = [
  {
    name: 'orchestrate_task',
    description: 'Start a multi-agent coding task. The coding agent generates code and the validation agent reviews it iteratively until quality standards are met (score >= 8/10).',
    inputSchema: {
      type: 'object',
      properties: {
        task: { 
          type: 'string', 
          description: "The coding task to perform (e.g., 'Add caching to UserService', 'Create a REST API endpoint for user registration')" 
        },
        context: { 
          type: 'string', 
          description: 'Project context name for Lightning memory (auto-detected from workspace if not provided)' 
        },
        workspacePath: { 
          type: 'string', 
          description: 'Path to the workspace root (auto-detected if not provided)' 
        },
        background: { 
          type: 'boolean', 
          description: 'Run as background job - returns job ID immediately (default: true)', 
          default: true 
        },
        language: {
          type: 'string',
          description: 'Target language: auto, python, csharp, typescript, javascript, go, rust, java, flutter, dart, etc. Default: auto (detects from task)',
          default: 'auto'
        },
        maxIterations: { 
          type: 'integer', 
          description: 'Maximum iterations (default: 50, no limit - set to 1000+ for complex projects)', 
          default: 50 
        },
        minValidationScore: { 
          type: 'integer', 
          description: 'Minimum score (0-10) required to pass validation (default: 8)', 
          default: 8 
        }
      },
      required: ['task']
    }
  },
  {
    name: 'get_task_status',
    description: 'Get the status of a running or completed coding task. Shows progress, iterations, validation scores, and generated files when complete.',
    inputSchema: {
      type: 'object',
      properties: {
        jobId: { 
          type: 'string', 
          description: 'The job ID returned by orchestrate_task' 
        }
      },
      required: ['jobId']
    }
  },
  {
    name: 'cancel_task',
    description: 'Cancel a running coding task',
    inputSchema: {
      type: 'object',
      properties: {
        jobId: { 
          type: 'string', 
          description: 'The job ID to cancel' 
        }
      },
      required: ['jobId']
    }
  },
  {
    name: 'list_tasks',
    description: 'List all active and recent coding tasks with their status',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'apply_task_files',
    description: 'Get generated files from a completed task in a format ready for writing. Returns explicit instructions for each file. AGENT MUST then use write tool to apply each file.',
    inputSchema: {
      type: 'object',
      properties: {
        jobId: { 
          type: 'string', 
          description: 'The job ID of the completed task' 
        },
        basePath: {
          type: 'string',
          description: 'Base path for file writes (e.g., "E:\\GitHub\\MyProject"). Files will be written relative to this.'
        }
      },
      required: ['jobId', 'basePath']
    }
  },
];

// Send HTTP request to orchestrator
function sendToOrchestrator(endpoint, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: ORCHESTRATOR_HOST,
      port: ORCHESTRATOR_PORT,
      path: endpoint,
      method: method,
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: 120000 // 2 minute timeout for code generation
    };

    if (body) {
      const postData = JSON.stringify(body);
      options.headers['Content-Length'] = Buffer.byteLength(postData);
    }

    const req = http.request(options, (res) => {
      let data = '';
      
      res.on('data', (chunk) => {
        data += chunk;
      });
      
      res.on('end', () => {
        log(`HTTP ${method} ${endpoint} -> ${res.statusCode} (${data.length} bytes)`);
        
        if (res.statusCode >= 400) {
          log(`ERROR: HTTP ${res.statusCode}: ${data}`);
          reject(new Error(`HTTP ${res.statusCode}: ${data}`));
          return;
        }
        
        if (res.statusCode === 204 || data.trim() === '') {
          log(`WARN: Empty response from ${endpoint}`);
          resolve(null);
          return;
        }
        
        try {
          const response = JSON.parse(data);
          log(`Response parsed successfully: ${JSON.stringify(response).substring(0, 200)}...`);
          resolve(response);
        } catch (err) {
          log(`WARN: Could not parse JSON: ${err.message}`);
          resolve({ raw: data });
        }
      });
    });

    req.on('error', (err) => {
      reject(err);
    });

    if (body) {
      req.write(JSON.stringify(body));
    }
    req.end();
  });
}

// Handle MCP tool calls
async function handleToolCall(toolName, args) {
  log(`Tool call: ${toolName} with args: ${JSON.stringify(args)}`);
  
  // Auto-inject context and workspace path
  if (!args.context) {
    args.context = CONTEXT_NAME;
  }
  if (!args.workspacePath) {
    args.workspacePath = WORKSPACE_PATH;
  }
  
  switch (toolName) {
    case 'orchestrate_task': {
      const body = {
        task: args.task,
        language: args.language || 'auto',
        maxIterations: Math.max(args.maxIterations || 50, 50), // Minimum 50 iterations
        workspacePath: args.workspacePath // Pass workspace path to CodingAgent
      };
      
      log(`Sending to /api/orchestrator/orchestrate: ${JSON.stringify(body)}`);
      const result = await sendToOrchestrator('/api/orchestrator/orchestrate', 'POST', body);
      
      if (!result || !result.jobId) {
        log(`ERROR: Orchestrator returned null or missing jobId. Result: ${JSON.stringify(result)}`);
        throw new Error(`CodingAgent did not return a valid jobId. Check if the service is running on port ${ORCHESTRATOR_PORT}. Response: ${JSON.stringify(result)}`);
      }
      
      log(`✅ Got jobId: ${result.jobId}`);
      
      const monitorUrl = `http://localhost:5001/job-status.html?jobId=${result.jobId}`;
      
      return `🚀 **Multi-Agent Coding Task Started**

**Job ID:** \`${result.jobId}\`
**Task:** ${args.task}
**Language:** ${body.language}
**Message:** ${result.message || 'Job started successfully'}

The CodingAgent is now working on your task.

**Progress:**
- Max iterations: ${body.maxIterations}

**📊 Live Monitor:** Open this URL in your browser for real-time WebSocket updates:
${monitorUrl}

**To check status:** Call \`get_task_status\` with jobId: \`${result.jobId}\``;
    }
    
    case 'get_task_status': {
      const result = await sendToOrchestrator(`/api/orchestrator/status/${args.jobId}`);
      
      if (!result || !result.jobId) {
        return `❌ Job \`${args.jobId}\` not found`;
      }
      
      // Status is a string: "running", "completed", "failed", "cancelled"
      const statusName = result.status;
      const statusIcon = {
        'running': '🔄',
        'completed': '✅',
        'failed': '❌',
        'cancelled': '🚫'
      }[statusName.toLowerCase()] || '❓';
      
      const monitorUrl = `http://localhost:5001/job-status.html?jobId=${args.jobId}`;
      
      let output = `📊 **Task Status: ${statusIcon} ${statusName.toUpperCase()}**

**Job ID:** \`${result.jobId}\`
**Task:** ${result.task}
**Progress:** ${result.progress}%
**Started:** ${result.startedAt}

**📊 Live Monitor:** ${monitorUrl}
`;

      if (result.completedAt) {
        output += `**Completed:** ${result.completedAt}\n`;
      }
      
      // Add real-time events from WebSocket
      const events = jobEvents.get(args.jobId);
      if (events && events.length > 0) {
        output += `\n## 📡 **Real-Time Updates** (Last ${events.length} events)\n\n`;
        
        // Show last 10 events
        const recentEvents = events.slice(-10);
        for (const event of recentEvents) {
          const eventIcon = {
            'progress': '📊',
            'thinking': '🧠',
            'coding': '💻',
            'validation': '✅',
            'error': '❌',
            'completed': '🎉'
          }[event.type] || '📝';
          
          const time = new Date(event.timestamp).toLocaleTimeString();
          output += `${eventIcon} **[${time}]** ${event.message}`;
          
          if (event.progress !== undefined) {
            output += ` (${event.progress}%)`;
          }
          if (event.score !== undefined) {
            output += ` (score: ${event.score}/10)`;
          }
          output += '\n';
        }
        output += '\n';
      } else if (signalR && !hubConnection) {
        output += `\n⚠️ **WebSocket disconnected** - Real-time updates unavailable\n\n`;
      } else if (!signalR) {
        output += `\n💡 **Tip:** Install @microsoft/signalr for real-time updates: \`npm install @microsoft/signalr\`\n\n`;
      }
      
      const isComplete = result.status === 'completed';
      const isFailed = result.status === 'failed';
      
      if (isComplete && result.result) {
        output += `\n**✅ COMPLETED**
- Success: ${result.result.success}
- Model Used: ${result.result.modelUsed || 'N/A'}
- Tokens Used: ${result.result.tokensUsed || 0}
- Files Generated: ${result.result.fileChanges?.length || 0}
- Explanation: ${result.result.explanation || 'N/A'}
`;
        
        if (result.result.fileChanges && result.result.fileChanges.length > 0) {
          log(`Returning ${result.result.fileChanges.length} files in response`);
          for (const file of result.result.fileChanges) {
            // Detect language from file extension
            const ext = file.path.split('.').pop()?.toLowerCase() || '';
            const langMap = {
              'cs': 'csharp',
              'ts': 'typescript',
              'tsx': 'typescript',
              'js': 'javascript',
              'jsx': 'javascript',
              'py': 'python',
              'sql': 'sql',
              'json': 'json',
              'yaml': 'yaml',
              'yml': 'yaml',
              'xml': 'xml',
              'html': 'html',
              'css': 'css',
              'md': 'markdown'
            };
            const lang = langMap[ext] || '';
            
            output += `\n---\n### 📄 ${file.path}\n**Type:** ${file.type}\n`;
            if (file.reason) {
              output += `**Reason:** ${file.reason}\n`;
            }
            output += `\n\`\`\`${lang}\n${file.content || '// Empty content'}\n\`\`\`\n`;
          }
        } else {
          output += '\n⚠️ **No files in result** - Check orchestrator logs\n';
        }
      } else if (isFailed) {
        output += `\n**❌ FAILED**
- Error: ${result.error || 'Unknown error'}`;
        
        // Check if there's a partial result in result.result (not result.error.partialResult)
        if (result.result?.fileChanges?.length > 0) {
          output += `\n\n**Partial Results (${result.result.fileChanges.length} files):**\n`;
          for (const file of result.result.fileChanges) {
            output += `\n**${file.path}**\n\`\`\`\n${file.content}\n\`\`\`\n`;
          }
        }
      }
      
      return output;
    }
    
    case 'cancel_task': {
      try {
        await sendToOrchestrator(`/api/orchestrator/cancel/${args.jobId}`, 'POST');
        return `✅ Job \`${args.jobId}\` has been cancelled`;
      } catch (err) {
        return `❌ Could not cancel job \`${args.jobId}\` - ${err.message}`;
      }
    }
    
    case 'list_tasks': {
      const tasks = await sendToOrchestrator('/api/orchestrator/jobs');
      
      if (!tasks || tasks.length === 0) {
        return 'No active tasks';
      }
      
      const iconMap = {
        'running': '🔄',
        'completed': '✅',
        'failed': '❌',
        'cancelled': '🚫'
      };
      
      let output = '**Active Tasks:**\n\n';
      for (const task of tasks) {
        const icon = iconMap[task.status.toLowerCase()] || '❓';
        
        output += `${icon} \`${task.jobId}\` - ${task.status.toUpperCase()} (${task.progress}%) - ${task.task}\n`;
      }
      
      return output;
    }
    
    case 'apply_task_files': {
      const result = await sendToOrchestrator(`/api/orchestrator/status/${args.jobId}`);
      
      if (!result || !result.jobId) {
        return `❌ Job \`${args.jobId}\` not found`;
      }
      
      const isComplete = result.status === 'completed';
      const isFailed = result.status === 'failed';
      
      let files = [];
      if (result.result?.fileChanges) {
        files = result.result.fileChanges;
      }
      
      if (!files || files.length === 0) {
        return `❌ Job \`${args.jobId}\` has no files to apply`;
      }
      
      const basePath = args.basePath.replace(/\\/g, '/');
      
      let output = `## 🚨 AGENT ACTION REQUIRED: Write ${files.length} file(s)\n\n`;
      output += `**Base Path:** \`${args.basePath}\`\n`;
      output += `**Job ID:** \`${args.jobId}\`\n`;
      output += `**Status:** ${isComplete ? '✅ Complete' : '⚠️ Failed (partial results)'}\n\n`;
      output += `---\n\n`;
      output += `**YOU MUST NOW USE THE \`write\` TOOL FOR EACH FILE BELOW:**\n\n`;
      
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const fullPath = `${args.basePath}\\${file.path.replace(/\//g, '\\\\')}`;
        
        output += `### File ${i + 1}/${files.length}: ${file.path}\n\n`;
        output += `**Action:** \`write(file_path: "${fullPath}", contents: <code below>)\`\n\n`;
        
        const ext = file.path.split('.').pop()?.toLowerCase() || '';
        const langMap = {
          'cs': 'csharp', 'ts': 'typescript', 'tsx': 'typescript',
          'js': 'javascript', 'jsx': 'javascript', 'py': 'python',
          'json': 'json', 'yaml': 'yaml', 'yml': 'yaml', 'xml': 'xml'
        };
        const lang = langMap[ext] || '';
        
        output += `\`\`\`${lang}\n${file.content}\n\`\`\`\n\n`;
        output += `---\n\n`;
      }
      
      output += `## ✅ After writing all files, confirm to user that code has been applied.\n`;
      
      return output;
    }
    
    // ═══════════════════════════════════════════════════════════
    // 🎨 DESIGN TOOL HANDLERS - Route to DesignAgent via MemoryRouter
    // ═══════════════════════════════════════════════════════════
    
    case 'design_questionnaire':
    case 'design_create_brand':
    case 'design_get_brand':
    case 'design_list_brands':
    case 'design_validate':
    case 'design_update_brand': {
      return `⚠️ Design tools must be called through the memory-agent MCP server, not the code-generator server.
      
**To use design tools:**
1. Ensure you have the memory-agent MCP server configured in your MCP settings
2. Call the design tools through that server instead

**Available design tools in memory-agent:**
- design_questionnaire
- design_create_brand
- design_get_brand
- design_list_brands
- design_validate
- design_update_brand

The code-generator server focuses on code generation only.`;
    }
    
    default:
      return `Unknown tool: ${toolName}`;
  }
}

// Handle JSON-RPC requests
async function handleRequest(request) {
  const { method, params, id } = request;
  
  log(`Handling method: ${method}`);
  
  switch (method) {
    case 'initialize':
      // Health check orchestrator on init
      try {
        await sendToOrchestrator('/health');
        log('✅ CodingOrchestrator is healthy');
      } catch (err) {
        log(`⚠️ CodingOrchestrator not reachable: ${err.message}`);
      }
      
      return {
        jsonrpc: '2.0',
        id,
        result: {
          protocolVersion: '2024-11-05',
          capabilities: {
            tools: {}
          },
          serverInfo: {
            name: 'code-generator',
            version: '2.0.0',
            description: 'Multi-language code generation via CodingAgent.Server v2.0'
          }
        }
      };
    
    case 'notifications/initialized':
      // No response needed for notifications
      return null;
    
    case 'tools/list':
      return {
        jsonrpc: '2.0',
        id,
        result: {
          tools: TOOLS
        }
      };
    
    case 'tools/call':
      try {
        const result = await handleToolCall(params.name, params.arguments || {});
        return {
          jsonrpc: '2.0',
          id,
          result: {
            content: [{ type: 'text', text: result }]
          }
        };
      } catch (err) {
        log(`Error in tool call: ${err.message}`);
        return {
          jsonrpc: '2.0',
          id,
          result: {
            content: [{ type: 'text', text: `Error: ${err.message}` }],
            isError: true
          }
        };
      }
    
    default:
      return {
        jsonrpc: '2.0',
        id,
        error: {
          code: -32601,
          message: `Method not found: ${method}`
        }
      };
  }
}

// Read from STDIN line by line
let buffer = '';

process.stdin.on('data', async (chunk) => {
  buffer += chunk.toString();
  
  let newlineIndex;
  while ((newlineIndex = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, newlineIndex).trim();
    buffer = buffer.slice(newlineIndex + 1);
    
    if (line) {
      try {
        const request = JSON.parse(line);
        log(`Received: ${request.method} (id: ${request.id})`);
        
        const response = await handleRequest(request);
        
        if (response !== null) {
          process.stdout.write(JSON.stringify(response) + '\n');
          log(`Sent response for ${request.method}`);
        }
      } catch (err) {
        log(`Error processing request: ${err.message}`);
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
    }
  }
});

process.stdin.on('end', () => {
  log('STDIN closed, exiting');
  process.exit(0);
});

process.on('SIGINT', () => {
  log('SIGINT received, exiting');
  process.exit(0);
});

process.on('SIGTERM', () => {
  log('SIGTERM received, exiting');
  process.exit(0);
});


