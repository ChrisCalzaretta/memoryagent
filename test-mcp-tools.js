#!/usr/bin/env node
/**
 * TEST MCP TOOLS - Test the actual MCP tools exposed to Cursor
 * 
 * This tests what Cursor actually sees and uses:
 * - @memory-agent tools (via memory-router-mcp-wrapper.js)
 * - @code-generator tools (via orchestrator-mcp-wrapper.js)
 */

const { spawn } = require('child_process');
const path = require('path');

const WORKSPACE = process.cwd();

// Test results
const results = { passed: 0, failed: 0, tests: [] };

// Colors
const colors = {
  reset: '\x1b[0m',
  green: '\x1b[32m',
  red: '\x1b[31m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m'
};

function log(message, color = 'reset') {
  console.log(`${colors[color]}${message}${colors.reset}`);
}

function logTest(testName, status, message = '') {
  const symbol = status === 'PASS' ? '✅' : '❌';
  const color = status === 'PASS' ? 'green' : 'red';
  log(`${symbol} ${testName}${message ? ': ' + message : ''}`, color);
  
  results.tests.push({ testName, status, message });
  if (status === 'PASS') results.passed++;
  else results.failed++;
}

function logSection(title) {
  log('\n' + '═'.repeat(60), 'cyan');
  log(`  ${title}`, 'cyan');
  log('═'.repeat(60), 'cyan');
}

/**
 * Send MCP request to wrapper and get response
 */
async function callMcpTool(wrapperScript, method, params = {}) {
  return new Promise((resolve, reject) => {
    const wrapper = spawn('node', [wrapperScript, WORKSPACE], {
      stdio: ['pipe', 'pipe', 'pipe']
    });
    
    let stdout = '';
    let stderr = '';
    
    wrapper.stdout.on('data', (data) => {
      stdout += data.toString();
    });
    
    wrapper.stderr.on('data', (data) => {
      stderr += data.toString();
    });
    
    wrapper.on('close', () => {
      // Parse JSON-RPC response from stdout
      const lines = stdout.trim().split('\n');
      const responses = lines.map(line => {
        try {
          return JSON.parse(line);
        } catch {
          return null;
        }
      }).filter(r => r !== null);
      
      resolve({ responses, stderr });
    });
    
    wrapper.on('error', reject);
    
    // Send JSON-RPC request
    const request = {
      jsonrpc: "2.0",
      id: Date.now(),
      method: method,
      params: params
    };
    
    wrapper.stdin.write(JSON.stringify(request) + '\n');
    
    // Give it time to process
    setTimeout(() => {
      wrapper.stdin.end();
    }, 3000);
  });
}

// ===========================================================================
// TEST SUITE 1: MEMORY AGENT MCP TOOLS
// ===========================================================================

async function testMemoryAgentInitialize() {
  logSection('TEST 1: Memory Agent - Initialize');
  
  try {
    const result = await callMcpTool(
      'memory-router-mcp-wrapper.js',
      'initialize',
      {
        protocolVersion: "2024-11-05",
        capabilities: {},
        clientInfo: { name: "test-client", version: "1.0.0" }
      }
    );
    
    const response = result.responses.find(r => r.result);
    
    if (response && response.result && response.result.protocolVersion) {
      logTest('Memory Agent Initialize', 'PASS', `Protocol: ${response.result.protocolVersion}`);
      return true;
    } else {
      logTest('Memory Agent Initialize', 'FAIL', 'No valid response');
      return false;
    }
  } catch (err) {
    logTest('Memory Agent Initialize', 'FAIL', err.message);
    return false;
  }
}

async function testMemoryAgentListTools() {
  logSection('TEST 2: Memory Agent - List Tools');
  
  try {
    const result = await callMcpTool(
      'memory-router-mcp-wrapper.js',
      'tools/list'
    );
    
    const response = result.responses.find(r => r.result && r.result.tools);
    
    if (response && response.result && response.result.tools) {
      const tools = response.result.tools;
      logTest('Memory Agent List Tools', 'PASS', `Found ${tools.length} tools`);
      
      // Show tools
      log('  Tools:', 'blue');
      tools.forEach(tool => {
        log(`    - ${tool.name}: ${tool.description.substring(0, 60)}...`, 'blue');
      });
      
      return tools;
    } else {
      logTest('Memory Agent List Tools', 'FAIL', 'No tools found');
      return null;
    }
  } catch (err) {
    logTest('Memory Agent List Tools', 'FAIL', err.message);
    return null;
  }
}

async function testMemoryAgentCallTool(toolName, args) {
  logSection(`TEST: Memory Agent - Call Tool: ${toolName}`);
  
  try {
    const result = await callMcpTool(
      'memory-router-mcp-wrapper.js',
      'tools/call',
      {
        name: toolName,
        arguments: args
      }
    );
    
    const response = result.responses.find(r => r.result || r.error);
    
    if (response && response.result) {
      logTest(`Call Tool: ${toolName}`, 'PASS', 'Tool executed successfully');
      log(`  Result preview: ${JSON.stringify(response.result).substring(0, 100)}...`, 'blue');
      return response.result;
    } else if (response && response.error) {
      logTest(`Call Tool: ${toolName}`, 'FAIL', response.error.message);
      return null;
    } else {
      logTest(`Call Tool: ${toolName}`, 'FAIL', 'No response');
      return null;
    }
  } catch (err) {
    logTest(`Call Tool: ${toolName}`, 'FAIL', err.message);
    return null;
  }
}

// ===========================================================================
// TEST SUITE 2: CODE GENERATOR MCP TOOLS
// ===========================================================================

async function testCodeGeneratorInitialize() {
  logSection('TEST: Code Generator - Initialize');
  
  try {
    const result = await callMcpTool(
      'orchestrator-mcp-wrapper.js',
      'initialize',
      {
        protocolVersion: "2024-11-05",
        capabilities: {},
        clientInfo: { name: "test-client", version: "1.0.0" }
      }
    );
    
    const response = result.responses.find(r => r.result);
    
    if (response && response.result && response.result.protocolVersion) {
      logTest('Code Generator Initialize', 'PASS', `Protocol: ${response.result.protocolVersion}`);
      return true;
    } else {
      logTest('Code Generator Initialize', 'FAIL', 'No valid response');
      return false;
    }
  } catch (err) {
    logTest('Code Generator Initialize', 'FAIL', err.message);
    return false;
  }
}

async function testCodeGeneratorListTools() {
  logSection('TEST: Code Generator - List Tools');
  
  try {
    const result = await callMcpTool(
      'orchestrator-mcp-wrapper.js',
      'tools/list'
    );
    
    const response = result.responses.find(r => r.result && r.result.tools);
    
    if (response && response.result && response.result.tools) {
      const tools = response.result.tools;
      logTest('Code Generator List Tools', 'PASS', `Found ${tools.length} tools`);
      
      // Show tools
      log('  Tools:', 'blue');
      tools.forEach(tool => {
        log(`    - ${tool.name}: ${tool.description.substring(0, 60)}...`, 'blue');
      });
      
      return tools;
    } else {
      logTest('Code Generator List Tools', 'FAIL', 'No tools found');
      return null;
    }
  } catch (err) {
    logTest('Code Generator List Tools', 'FAIL', err.message);
    return null;
  }
}

async function testCodeGeneratorOrchestrate() {
  logSection('TEST: Code Generator - Orchestrate Task');
  
  try {
    log('  Starting code generation (this will take a few minutes)...', 'yellow');
    
    const result = await callMcpTool(
      'orchestrator-mcp-wrapper.js',
      'tools/call',
      {
        name: 'orchestrate_task',
        arguments: {
          task: 'Create a simple Calculator class with Add and Subtract methods',
          language: 'csharp',
          maxIterations: 5
        }
      }
    );
    
    const response = result.responses.find(r => r.result || r.error);
    
    if (response && response.result) {
      logTest('Orchestrate Task', 'PASS', 'Code generation started');
      log(`  Result: ${JSON.stringify(response.result).substring(0, 200)}...`, 'blue');
      
      // Try to extract job ID
      const resultStr = JSON.stringify(response.result);
      const jobIdMatch = resultStr.match(/job_\w+/);
      
      if (jobIdMatch) {
        return jobIdMatch[0];
      }
      
      return response.result;
    } else if (response && response.error) {
      logTest('Orchestrate Task', 'FAIL', response.error.message);
      return null;
    } else {
      logTest('Orchestrate Task', 'FAIL', 'No response');
      return null;
    }
  } catch (err) {
    logTest('Orchestrate Task', 'FAIL', err.message);
    return null;
  }
}

async function testCodeGeneratorGetStatus(jobId) {
  logSection(`TEST: Code Generator - Get Task Status (${jobId})`);
  
  try {
    const result = await callMcpTool(
      'orchestrator-mcp-wrapper.js',
      'tools/call',
      {
        name: 'get_task_status',
        arguments: {
          jobId: jobId
        }
      }
    );
    
    const response = result.responses.find(r => r.result || r.error);
    
    if (response && response.result) {
      logTest('Get Task Status', 'PASS', 'Status retrieved');
      log(`  Result: ${JSON.stringify(response.result).substring(0, 200)}...`, 'blue');
      return response.result;
    } else if (response && response.error) {
      logTest('Get Task Status', 'FAIL', response.error.message);
      return null;
    } else {
      logTest('Get Task Status', 'FAIL', 'No response');
      return null;
    }
  } catch (err) {
    logTest('Get Task Status', 'FAIL', err.message);
    return null;
  }
}

async function testCodeGeneratorListTasks() {
  logSection('TEST: Code Generator - List Tasks');
  
  try {
    const result = await callMcpTool(
      'orchestrator-mcp-wrapper.js',
      'tools/call',
      {
        name: 'list_tasks',
        arguments: {}
      }
    );
    
    const response = result.responses.find(r => r.result || r.error);
    
    if (response && response.result) {
      logTest('List Tasks', 'PASS', 'Tasks listed');
      log(`  Result: ${JSON.stringify(response.result).substring(0, 200)}...`, 'blue');
      return response.result;
    } else if (response && response.error) {
      logTest('List Tasks', 'FAIL', response.error.message);
      return null;
    } else {
      logTest('List Tasks', 'FAIL', 'No response');
      return null;
    }
  } catch (err) {
    logTest('List Tasks', 'FAIL', err.message);
    return null;
  }
}

// ===========================================================================
// MAIN TEST RUNNER
// ===========================================================================

async function runAllTests() {
  log('\n' + '█'.repeat(60), 'cyan');
  log('  🧪 MCP TOOLS TEST SUITE', 'cyan');
  log('  Testing actual MCP tools exposed to Cursor', 'cyan');
  log('█'.repeat(60) + '\n', 'cyan');
  
  log('Workspace: ' + WORKSPACE, 'blue');
  log('');
  
  // Memory Agent Tests
  log('🧠 TESTING MEMORY AGENT (@memory-agent)', 'cyan');
  await testMemoryAgentInitialize();
  const memoryTools = await testMemoryAgentListTools();
  
  // Test a few key tools if we got the list
  if (memoryTools && memoryTools.length > 0) {
    // Test first tool
    const firstTool = memoryTools[0];
    await testMemoryAgentCallTool(firstTool.name, {});
  }
  
  // Code Generator Tests
  log('\n🤖 TESTING CODE GENERATOR (@code-generator)', 'cyan');
  await testCodeGeneratorInitialize();
  const codeTools = await testCodeGeneratorListTools();
  
  // Test list_tasks
  await testCodeGeneratorListTasks();
  
  // Test orchestrate (long-running)
  log('\n⏳ LONG-RUNNING TEST: Code Generation', 'yellow');
  log('This will take 2-5 minutes...', 'yellow');
  const jobId = await testCodeGeneratorOrchestrate();
  
  if (jobId && typeof jobId === 'string' && jobId.startsWith('job_')) {
    // Wait a bit then check status
    log('\n⏳ Waiting 10 seconds before checking status...', 'yellow');
    await new Promise(resolve => setTimeout(resolve, 10000));
    
    await testCodeGeneratorGetStatus(jobId);
  }
  
  // Summary
  logSection('TEST SUMMARY');
  log(`  Total Tests: ${results.passed + results.failed}`, 'blue');
  log(`  ✅ Passed: ${results.passed}`, 'green');
  log(`  ❌ Failed: ${results.failed}`, 'red');
  log(`  Success Rate: ${((results.passed / (results.passed + results.failed)) * 100).toFixed(1)}%`, 'yellow');
  
  if (results.failed > 0) {
    log('\n  Failed Tests:', 'red');
    results.tests.filter(t => t.status === 'FAIL').forEach(t => {
      log(`    - ${t.testName}: ${t.message}`, 'red');
    });
  }
  
  log('\n' + '█'.repeat(60), 'cyan');
  log(`  ${results.failed === 0 ? '🎉 ALL TESTS PASSED!' : '⚠️  SOME TESTS FAILED'}`, results.failed === 0 ? 'green' : 'red');
  log('█'.repeat(60) + '\n', 'cyan');
  
  process.exit(results.failed > 0 ? 1 : 0);
}

// Run tests
runAllTests().catch(err => {
  log(`\n❌ Fatal error: ${err.message}`, 'red');
  console.error(err.stack);
  process.exit(1);
});


