#!/usr/bin/env node

/**
 * MCP STDIO Wrapper for CodingOrchestrator
 * Bridges Cursor's STDIO transport to the CodingOrchestrator HTTP API
 * Provides tools: orchestrate_task, get_task_status, cancel_task, list_tasks
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

// Configuration
const ORCHESTRATOR_PORT = process.env.ORCHESTRATOR_PORT || 5003;
const LOG_FILE = process.env.LOG_FILE || 'E:\\GitHub\\MemoryAgent\\orchestrator-wrapper.log';

// Detect workspace path from multiple sources
let WORKSPACE_PATH = null;

// 1. Try command-line argument (highest priority)
if (process.argv.length > 2 && process.argv[2] !== '${workspaceFolder}') {
  WORKSPACE_PATH = process.argv[2];
  log(`Using workspace from command-line argument: ${WORKSPACE_PATH}`);
}

// 2. Try environment variable
if (!WORKSPACE_PATH && process.env.WORKSPACE_PATH && process.env.WORKSPACE_PATH !== '${workspaceFolder}') {
  WORKSPACE_PATH = process.env.WORKSPACE_PATH;
  log(`Using workspace from environment variable: ${WORKSPACE_PATH}`);
}

// 3. Fallback
if (!WORKSPACE_PATH) {
  WORKSPACE_PATH = 'E:\\GitHub';
  log('⚠️ Using default workspace path');
}

// Extract context from workspace path (last directory name)
const CONTEXT_NAME = path.basename(WORKSPACE_PATH).toLowerCase();

function log(message) {
  const timestamp = new Date().toISOString();
  const logMessage = `[${timestamp}] ${message}\n`;
  try {
    fs.appendFileSync(LOG_FILE, logMessage);
  } catch (err) {
    // Ignore logging errors
  }
}

log('Orchestrator MCP Wrapper started');
log(`  Workspace: ${WORKSPACE_PATH}`);
log(`  Context: ${CONTEXT_NAME}`);
log(`  Orchestrator Port: ${ORCHESTRATOR_PORT}`);

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
  
  // ═══════════════════════════════════════════════════════════
  // 🎨 DESIGN TOOLS - Brand guidelines and validation
  // ═══════════════════════════════════════════════════════════
  
  {
    name: 'design_questionnaire',
    description: 'Get the brand builder questionnaire. Returns questions to answer for creating a complete brand system with colors, typography, components, and guidelines.',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'design_create_brand',
    description: 'Create a complete brand system from questionnaire answers. Returns design tokens, components, themes, voice guidelines, and accessibility requirements.',
    inputSchema: {
      type: 'object',
      properties: {
        brand_name: { type: 'string', description: 'Name of the brand/product' },
        tagline: { type: 'string', description: 'Optional tagline' },
        description: { type: 'string', description: '1-2 sentence product description' },
        target_audience: { type: 'string', description: 'Who is the target audience?' },
        industry: { type: 'string', description: 'Industry: SaaS, E-commerce, Finance, Health, Education, Entertainment, Enterprise, Consumer, Other' },
        personality_traits: { type: 'array', items: { type: 'string' }, description: '3-5 traits: Professional, Playful, Trustworthy, Bold, Minimal, etc.' },
        brand_voice: { type: 'string', description: 'Voice: Encouraging coach, Trusted advisor, Friendly helper, Expert authority, Playful friend, Calm guide' },
        theme_preference: { type: 'string', description: 'Theme: Dark mode, Light mode, Both' },
        visual_style: { type: 'string', description: 'Style: Minimal, Rich, Bold, Soft, Technical' },
        platforms: { type: 'array', items: { type: 'string' }, description: 'Platforms: Web, iOS, Android, Desktop' },
        frameworks: { type: 'array', items: { type: 'string' }, description: 'Frameworks: Blazor, React, Vue, SwiftUI, Flutter, etc.' }
      },
      required: ['brand_name', 'description', 'industry', 'personality_traits', 'brand_voice', 'visual_style', 'platforms', 'frameworks']
    }
  },
  {
    name: 'design_get_brand',
    description: 'Get an existing brand definition by context name. Returns full brand with tokens, components, themes, voice, and accessibility.',
    inputSchema: {
      type: 'object',
      properties: {
        context: { type: 'string', description: 'Brand context name (e.g., "fittrack-pro")' }
      },
      required: ['context']
    }
  },
  {
    name: 'design_list_brands',
    description: 'List all available brand definitions',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'design_validate',
    description: 'Validate code against brand guidelines. Checks colors, typography, spacing, components, and accessibility. Returns score, grade, and issues with fixes.',
    inputSchema: {
      type: 'object',
      properties: {
        context: { type: 'string', description: 'Brand context name' },
        code: { type: 'string', description: 'Code to validate (HTML, CSS, Blazor, React, etc.)' }
      },
      required: ['context', 'code']
    }
  },
  {
    name: 'design_update_brand',
    description: 'Update an existing brand settings (colors, fonts, etc.)',
    inputSchema: {
      type: 'object',
      properties: {
        context: { type: 'string', description: 'Brand context name to update' },
        primary_color: { type: 'string', description: 'New primary color (hex)' },
        font_family: { type: 'string', description: 'New font family' },
        theme_preference: { type: 'string', description: 'Theme: Dark mode, Light mode, Both' }
      },
      required: ['context']
    }
  }
];

// Send HTTP request to orchestrator
function sendToOrchestrator(endpoint, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: 'localhost',
      port: ORCHESTRATOR_PORT,
      path: endpoint,
      method: method,
      headers: {
        'Content-Type': 'application/json'
      }
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
        if (res.statusCode === 204 || data.trim() === '') {
          resolve(null);
          return;
        }
        
        try {
          const response = JSON.parse(data);
          resolve(response);
        } catch (err) {
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
        context: args.context,
        workspacePath: args.workspacePath,
        background: args.background !== false,
        maxIterations: args.maxIterations || 50,
        minValidationScore: args.minValidationScore || 8
      };
      
      const result = await sendToOrchestrator('/api/orchestrator/task', 'POST', body);
      
      return `🚀 **Multi-Agent Coding Task Started**

**Job ID:** \`${result.jobId}\`
**Task:** ${args.task}
**Language:** ${body.language}
**Context:** ${args.context}
**Status:** ${result.status}

The CodingAgent and ValidationAgent are now working on your task.

**Progress:**
- Max iterations: ${body.maxIterations}
- Min validation score: ${body.minValidationScore}/10

**To check status:** Call \`get_task_status\` with jobId: \`${result.jobId}\``;
    }
    
    case 'get_task_status': {
      const result = await sendToOrchestrator(`/api/orchestrator/task/${args.jobId}`);
      
      if (result.error) {
        return `❌ Job \`${args.jobId}\` not found`;
      }
      
      // Convert numeric status to string
      const statusNames = ['Queued', 'Running', 'Complete', 'Failed', 'Cancelled', 'TimedOut'];
      const statusName = typeof result.status === 'number' ? statusNames[result.status] : result.status;
      const statusIcon = {
        'Queued': '⏳',
        'Running': '🔄',
        'Complete': '✅',
        'Failed': '❌',
        'Cancelled': '🚫',
        'TimedOut': '⏱️'
      }[statusName] || '❓';
      
      let output = `📊 **Task Status: ${statusIcon} ${statusName}**

**Job ID:** \`${result.jobId}\`
**Progress:** ${result.progress}%
**Current Phase:** ${result.currentPhase || 'N/A'}
**Iteration:** ${result.iteration}/${result.maxIterations}
`;

      if (result.timeline && result.timeline.length > 0) {
        output += '\n**Timeline:**\n';
        for (const phase of result.timeline) {
          const duration = phase.durationMs ? ` (${phase.durationMs}ms)` : '';
          const iter = phase.iteration ? ` [iter ${phase.iteration}]` : '';
          output += `- ✅ ${phase.name}${iter}${duration}\n`;
        }
      }
      
      // TaskState enum: Queued=0, Running=1, Complete=2, Failed=3, Cancelled=4, TimedOut=5
      const isComplete = result.status === 2 || result.status === 'Complete';
      const isFailed = result.status === 3 || result.status === 'Failed';
      
      // DEBUG: Log what we're working with
      log(`DEBUG: status=${result.status}, isComplete=${isComplete}, isFailed=${isFailed}`);
      log(`DEBUG: result.result exists=${!!result.result}`);
      if (result.result) {
        log(`DEBUG: result.result.files type=${typeof result.result.files}, isArray=${Array.isArray(result.result.files)}, length=${result.result.files?.length}`);
      }
      if (result.error?.partialResult) {
        log(`DEBUG: partialResult.files type=${typeof result.error.partialResult.files}, isArray=${Array.isArray(result.error.partialResult.files)}, length=${result.error.partialResult.files?.length}`);
      }
      
      if (isComplete && result.result) {
        output += `\n**✅ COMPLETED**
- Validation Score: ${result.result.validationScore}/10
- Total Iterations: ${result.result.totalIterations}
- Duration: ${result.result.totalDurationMs}ms
- Files Generated: ${result.result.files?.length || 0}
- Summary: ${result.result.summary || 'N/A'}
`;
        
        if (result.result.files && result.result.files.length > 0) {
          log(`Returning ${result.result.files.length} files in response`);
          for (const file of result.result.files) {
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
            
            output += `\n---\n### 📄 ${file.path}\n**Change Type:** ${file.changeType}\n`;
            if (file.reason) {
              output += `**Reason:** ${file.reason}\n`;
            }
            output += `\n\`\`\`${lang}\n${file.content || '// Empty content'}\n\`\`\`\n`;
          }
        } else {
          output += '\n⚠️ **No files in result** - Check orchestrator logs for parsing errors\n';
        }
      } else if (isFailed && result.error) {
        output += `\n**❌ FAILED**
- Error: ${result.error.message}
- Type: ${result.error.type}
- Can retry: ${result.error.canRetry ? 'Yes' : 'No'}`;
        
        // Include partial result if available
        if (result.error.partialResult?.files?.length > 0) {
          output += `\n\n**Partial Results (${result.error.partialResult.files.length} files):**\n`;
          for (const file of result.error.partialResult.files) {
            output += `\n**${file.path}**\n\`\`\`\n${file.content}\n\`\`\`\n`;
          }
        }
      }
      
      return output;
    }
    
    case 'cancel_task': {
      try {
        await sendToOrchestrator(`/api/orchestrator/task/${args.jobId}`, 'DELETE');
        return `✅ Job \`${args.jobId}\` has been cancelled`;
      } catch (err) {
        return `❌ Could not cancel job \`${args.jobId}\` - ${err.message}`;
      }
    }
    
    case 'list_tasks': {
      const tasks = await sendToOrchestrator('/api/orchestrator/tasks');
      
      if (!tasks || tasks.length === 0) {
        return 'No active tasks';
      }
      
      const statusNames = ['Queued', 'Running', 'Complete', 'Failed', 'Cancelled', 'TimedOut'];
      const iconMap = {
        'Queued': '⏳',
        'Running': '🔄',
        'Complete': '✅',
        'Failed': '❌',
        'Cancelled': '🚫',
        'TimedOut': '⏱️'
      };
      
      let output = '**Active Tasks:**\n\n';
      for (const task of tasks) {
        const statusName = typeof task.status === 'number' ? statusNames[task.status] : task.status;
        const icon = iconMap[statusName] || '❓';
        
        output += `${icon} \`${task.jobId}\` - ${statusName} (${task.progress}%) - ${task.currentPhase || 'N/A'}\n`;
      }
      
      return output;
    }
    
    case 'apply_task_files': {
      const result = await sendToOrchestrator(`/api/orchestrator/task/${args.jobId}`);
      
      if (result.error && !result.error.partialResult) {
        return `❌ Job \`${args.jobId}\` not found or has no files`;
      }
      
      const isComplete = result.status === 2 || result.status === 'Complete';
      const isFailed = result.status === 3 || result.status === 'Failed';
      
      let files = [];
      if (isComplete && result.result?.files) {
        files = result.result.files;
      } else if (isFailed && result.error?.partialResult?.files) {
        files = result.error.partialResult.files;
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
    // 🎨 DESIGN TOOL HANDLERS
    // ═══════════════════════════════════════════════════════════
    
    case 'design_questionnaire': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_questionnaire', arguments: {} });
      return result.content?.[0]?.text || 'Error getting questionnaire';
    }
    
    case 'design_create_brand': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_create_brand', arguments: args });
      return result.content?.[0]?.text || 'Error creating brand';
    }
    
    case 'design_get_brand': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_get_brand', arguments: args });
      return result.content?.[0]?.text || 'Error getting brand';
    }
    
    case 'design_list_brands': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_list_brands', arguments: {} });
      return result.content?.[0]?.text || 'Error listing brands';
    }
    
    case 'design_validate': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_validate', arguments: args });
      return result.content?.[0]?.text || 'Error validating';
    }
    
    case 'design_update_brand': {
      const result = await sendToOrchestrator('/api/mcp/call', 'POST', { name: 'design_update_brand', arguments: args });
      return result.content?.[0]?.text || 'Error updating brand';
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
      return {
        jsonrpc: '2.0',
        id,
        result: {
          protocolVersion: '2024-11-05',
          capabilities: {
            tools: {}
          },
          serverInfo: {
            name: 'coding-orchestrator',
            version: '1.0.0'
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


