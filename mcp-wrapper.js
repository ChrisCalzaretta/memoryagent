#!/usr/bin/env node
/**
 * ROUTER-BASED MCP Wrapper for Memory Code Agent
 * 
 * Instead of exposing 33 low-level tools, this exposes 8 HIGH-LEVEL workflows:
 * 1. generate_code - Multi-model code generation
 * 2. search_code - Semantic code search
 * 3. ask_question - Q&A with learning
 * 4. validate_code - Code validation
 * 5. analyze_project - Project analysis
 * 6. test_code - Code testing
 * 7. refactor_code - Code refactoring
 * 8. get_context - Context retrieval
 */

const { spawn } = require('child_process');
const http = require('http');
const path = require('path');

// Configuration
const MEMORYAGENT_PATH = path.dirname(__filename);
const WORKSPACE_PATH = process.env.PROJECT_PATH || process.cwd();
const MEMORY_AGENT_URL = 'http://localhost:5000';
const CODING_AGENT_URL = 'http://localhost:5001';

// URLs
const MEMORY_HEALTH_URL = `${MEMORY_AGENT_URL}/api/health`;
const CODING_HEALTH_URL = `${CODING_AGENT_URL}/health`;
const MCP_POST_URL = `${MEMORY_AGENT_URL}/mcp`;
const ORCHESTRATE_URL = `${CODING_AGENT_URL}/api/orchestrate`;
const JOB_STATUS_URL = (jobId) => `${CODING_AGENT_URL}/api/jobs/${jobId}`;

let currentJob = null;
let jobStatusInterval = null;

// Logging helper
function log(message, level = 'INFO') {
  const timestamp = new Date().toISOString();
  console.error(`[${timestamp}] [MCP-Router] [${level}] ${message}`);
}

// HIGH-LEVEL TOOL DEFINITIONS (what Cursor sees)
const ROUTER_TOOLS = [
  {
    name: "generate_code",
    description: "Generate code using multi-model AI (Qwen, Gemma, Phi4, Codestral) with automatic validation and retry. Supports .NET, Python, Flutter, etc.",
    inputSchema: {
      type: "object",
      properties: {
        task: {
          type: "string",
          description: "Description of what to generate (e.g., 'Create a Calculator class with Add, Subtract, Multiply, Divide methods')"
        },
        language: {
          type: "string",
          description: "Programming language (csharp, python, flutter, typescript, etc.)",
          default: "csharp"
        },
        maxIterations: {
          type: "number",
          description: "Maximum validation/retry iterations (default: 10)",
          default: 10
        }
      },
      required: ["task"]
    }
  },
  {
    name: "search_code",
    description: "Semantic search across the codebase using AI Lightning. Finds code by meaning, not exact text. Uses Qdrant vector DB and Neo4j graph.",
    inputSchema: {
      type: "object",
      properties: {
        query: {
          type: "string",
          description: "Natural language question (e.g., 'How do we handle authentication?', 'classes that implement IRepository')"
        },
        context: {
          type: "string",
          description: "Project context (defaults to workspace folder name)"
        },
        limit: {
          type: "number",
          description: "Maximum results to return (default: 20)",
          default: 20
        }
      },
      required: ["query"]
    }
  },
  {
    name: "ask_question",
    description: "Ask questions about the codebase. First checks if similar questions were asked before (instant recall), then searches, stores answer for future learning.",
    inputSchema: {
      type: "object",
      properties: {
        question: {
          type: "string",
          description: "Your question about the codebase"
        },
        context: {
          type: "string",
          description: "Project context (defaults to workspace folder name)"
        }
      },
      required: ["question"]
    }
  },
  {
    name: "validate_code",
    description: "Validate code against best practices, security rules, and patterns. Provides recommendations and auto-fix suggestions.",
    inputSchema: {
      type: "object",
      properties: {
        scope: {
          type: "string",
          description: "Validation scope: 'best_practices', 'security', 'anti_patterns', 'project' (comprehensive)",
          enum: ["best_practices", "security", "anti_patterns", "project"],
          default: "best_practices"
        },
        context: {
          type: "string",
          description: "Project context (defaults to workspace folder name)"
        },
        minSeverity: {
          type: "string",
          description: "Minimum severity to report (low, medium, high, critical)",
          enum: ["low", "medium", "high", "critical"],
          default: "medium"
        }
      },
      required: []
    }
  },
  {
    name: "analyze_project",
    description: "Get comprehensive project insights: important files, patterns, recommendations, health score, architecture analysis.",
    inputSchema: {
      type: "object",
      properties: {
        context: {
          type: "string",
          description: "Project context (defaults to workspace folder name)"
        },
        includeRecommendations: {
          type: "boolean",
          description: "Include architecture recommendations (default: true)",
          default: true
        }
      },
      required: []
    }
  },
  {
    name: "test_code",
    description: "Test generated code by compiling and running it. For web apps, can also run browser-based E2E tests.",
    inputSchema: {
      type: "object",
      properties: {
        jobId: {
          type: "string",
          description: "Job ID from code generation"
        },
        testType: {
          type: "string",
          description: "Type of test: 'compile', 'run', 'browser'",
          enum: ["compile", "run", "browser"],
          default: "compile"
        }
      },
      required: ["jobId"]
    }
  },
  {
    name: "refactor_code",
    description: "Refactor and modernize code. Can transform legacy patterns, modernize CSS, extract components, learn patterns from examples.",
    inputSchema: {
      type: "object",
      properties: {
        type: {
          type: "string",
          description: "Transformation type: 'page' (Blazor/Razor), 'css', 'learn_pattern', 'apply_pattern'",
          enum: ["page", "css", "learn_pattern", "apply_pattern"]
        },
        sourcePath: {
          type: "string",
          description: "Path to source file/directory"
        },
        context: {
          type: "string",
          description: "Project context"
        }
      },
      required: ["type", "sourcePath"]
    }
  },
  {
    name: "get_context",
    description: "Get relevant context before starting a task: related files, patterns, previous Q&A, co-edited files.",
    inputSchema: {
      type: "object",
      properties: {
        task: {
          type: "string",
          description: "Description of the task you're about to do"
        },
        context: {
          type: "string",
          description: "Project context (defaults to workspace folder name)"
        },
        includePatterns: {
          type: "boolean",
          description: "Include relevant patterns (default: true)",
          default: true
        },
        includeQA: {
          type: "boolean",
          description: "Include similar Q&A (default: true)",
          default: true
        }
      },
      required: ["task"]
    }
  }
];

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

// Check if containers are running
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
      resolve(running.includes('mcp-server') && running.includes('coding-agent'));
    });
  });
}

// Start Docker containers
async function startContainers() {
  return new Promise((resolve, reject) => {
    const compose = spawn('docker-compose', ['-f', 'docker-compose-shared-Calzaretta.yml', 'up', '-d', 'mcp-server', 'coding-agent'], {
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

// Wait for services to be healthy
async function waitForServices() {
  for (let i = 0; i < 30; i++) {
    try {
      await httpGet(MEMORY_HEALTH_URL);
      await httpGet(CODING_HEALTH_URL);
      log('✅ Both services are healthy');
      return true;
    } catch (err) {
      log(`Waiting for services... (${i + 1}/30)`);
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
  }
  throw new Error('Services did not become healthy in time');
}

// Call MemoryAgent MCP tool
async function callMemoryAgentTool(toolName, args) {
  const mcpRequest = {
    jsonrpc: "2.0",
    method: "tools/call",
    params: {
      name: toolName,
      arguments: args
    },
    id: Date.now()
  };
  
  return await httpPost(MCP_POST_URL, mcpRequest);
}

// Start code generation
async function startCodeGeneration(task, language = 'csharp', maxIterations = 10) {
  log(`🚀 Starting code generation: ${task}`);
  
  const data = await httpPost(ORCHESTRATE_URL, {
    task,
    language,
    workspacePath: WORKSPACE_PATH,
    maxIterations
  });
  
  currentJob = data.jobId;
  log(`✅ Job started: ${currentJob}`);
  
  return {
    jobId: currentJob,
    status: 'started',
    message: '🤖 Code generation started. Files will be auto-written to workspace/Generated/ on completion.'
  };
}

// ROUTER: Route high-level tool calls to the right backend
async function routeTool(toolName, args) {
  const context = args.context || path.basename(WORKSPACE_PATH);
  
  log(`🔀 Routing tool: ${toolName}`);
  
  switch (toolName) {
    case 'generate_code':
      return await startCodeGeneration(args.task, args.language, args.maxIterations);
      
    case 'search_code':
      return await callMemoryAgentTool('smartsearch', {
        query: args.query,
        context: context,
        limit: args.limit || 20
      });
      
    case 'ask_question':
      // First check for similar questions
      const similarResult = await callMemoryAgentTool('find_similar_questions', {
        question: args.question,
        context: context
      });
      
      if (similarResult && similarResult.result && similarResult.result.length > 0) {
        log('✅ Found similar question in memory');
        return similarResult;
      }
      
      // Otherwise search
      return await callMemoryAgentTool('smartsearch', {
        query: args.question,
        context: context
      });
      
    case 'validate_code':
      return await callMemoryAgentTool('validate', {
        scope: args.scope || 'best_practices',
        context: context,
        minSeverity: args.minSeverity || 'medium'
      });
      
    case 'analyze_project':
      const insights = await callMemoryAgentTool('get_insights', {
        category: 'all',
        context: context
      });
      
      if (args.includeRecommendations !== false) {
        const recommendations = await callMemoryAgentTool('get_recommendations', {
          context: context
        });
        
        return {
          insights,
          recommendations
        };
      }
      
      return insights;
      
    case 'test_code':
      if (!args.jobId) {
        throw new Error('jobId is required for test_code');
      }
      
      return await httpGet(JOB_STATUS_URL(args.jobId));
      
    case 'refactor_code':
      return await callMemoryAgentTool('transform', {
        type: args.type,
        sourcePath: args.sourcePath,
        context: context
      });
      
    case 'get_context':
      return await callMemoryAgentTool('get_context', {
        task: args.task,
        context: context,
        includePatterns: args.includePatterns !== false,
        includeQA: args.includeQA !== false
      });
      
    default:
      throw new Error(`Unknown tool: ${toolName}`);
  }
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
          log(`📥 Received ${msgType}: ${request.method || 'unknown'}`);
          
          let response;
          
          // Handle MCP protocol methods
          if (request.method === 'initialize') {
            response = {
              jsonrpc: "2.0",
              id: request.id,
              result: {
                protocolVersion: "2024-11-05",
                serverInfo: {
                  name: "memory-code-agent-router",
                  version: "2.0.0"
                },
                capabilities: {
                  tools: {}
                }
              }
            };
          } else if (request.method === 'tools/list') {
            response = {
              jsonrpc: "2.0",
              id: request.id,
              result: {
                tools: ROUTER_TOOLS
              }
            };
          } else if (request.method === 'tools/call') {
            const toolName = request.params.name;
            const args = request.params.arguments || {};
            
            try {
              const result = await routeTool(toolName, args);
              response = {
                jsonrpc: "2.0",
                id: request.id,
                result: result
              };
            } catch (err) {
              response = {
                jsonrpc: "2.0",
                id: request.id,
                error: {
                  code: -32603,
                  message: err.message
                }
              };
            }
          } else if (isNotification) {
            // Notifications - don't respond
            log(`✅ Handled notification: ${request.method}`);
            continue;
          } else {
            // Unknown method
            response = {
              jsonrpc: "2.0",
              id: request.id,
              error: {
                code: -32601,
                message: `Method not found: ${request.method}`
              }
            };
          }
          
          // Send response for requests only
          if (!isNotification && response) {
            process.stdout.write(JSON.stringify(response) + '\n');
          }
          
        } catch (err) {
          log(`Error processing ${isNotification ? 'notification' : 'request'}: ${err.message}`, 'ERROR');
          
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

// Main startup
async function main() {
  try {
    log('═══════════════════════════════════════════════════════');
    log('🤖 Memory Code Agent - ROUTER MODE');
    log('   Version: 2.0.0 (High-level workflow router)');
    log('═══════════════════════════════════════════════════════');
    log('📁 Paths:');
    log(`   MemoryAgent: ${MEMORYAGENT_PATH}`);
    log(`   Workspace: ${WORKSPACE_PATH}`);
    log('═══════════════════════════════════════════════════════');
    
    const running = await checkContainersRunning();
    
    if (!running) {
      log('Starting Docker containers...');
      await startContainers();
      await new Promise(resolve => setTimeout(resolve, 5000));
    } else {
      log('✅ Containers already running');
    }
    
    log('Waiting for services to be healthy...');
    await waitForServices();
    
    log('═══════════════════════════════════════════════════════');
    log('✅ Ready! Exposing 8 HIGH-LEVEL tools:');
    log('   1. generate_code - Multi-model code generation');
    log('   2. search_code - Semantic code search');
    log('   3. ask_question - Q&A with learning');
    log('   4. validate_code - Code validation');
    log('   5. analyze_project - Project analysis');
    log('   6. test_code - Code testing');
    log('   7. refactor_code - Code refactoring');
    log('   8. get_context - Context retrieval');
    log('═══════════════════════════════════════════════════════');
    
    await handleStdio();
    
  } catch (err) {
    log(`Fatal error: ${err.message}`, 'ERROR');
    process.exit(1);
  }
}

main();

