#!/usr/bin/env node
/**
 * COMPREHENSIVE TEST SUITE
 * Tests the entire code generation workflow:
 * 1. Memory Agent (port 5010) - Search, analyze, validate
 * 2. Code Generator (port 5001) - Generate code
 * 3. End-to-end workflow
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

// Configuration
const MEMORY_ROUTER_URL = 'http://localhost:5010';
const CODING_AGENT_URL = 'http://localhost:5001';
const WORKSPACE_PATH = 'C:\\GitHub\\TestAgent'; // Where to generate code
const CONTEXT = 'testagent';

// Test results
const results = {
  passed: 0,
  failed: 0,
  tests: []
};

// Colors for output
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

// HTTP helper
function httpRequest(url, method = 'GET', data = null) {
  return new Promise((resolve, reject) => {
    const urlObj = new URL(url);
    const options = {
      hostname: urlObj.hostname,
      port: urlObj.port,
      path: urlObj.pathname,
      method: method,
      headers: method === 'POST' ? {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(JSON.stringify(data))
      } : {},
      timeout: 30000
    };
    
    const req = http.request(options, (res) => {
      let responseData = '';
      res.on('data', (chunk) => responseData += chunk);
      res.on('end', () => {
        try {
          resolve({
            statusCode: res.statusCode,
            data: responseData ? JSON.parse(responseData) : null
          });
        } catch (err) {
          resolve({
            statusCode: res.statusCode,
            data: responseData
          });
        }
      });
    });
    
    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy();
      reject(new Error('Request timeout'));
    });
    
    if (data) {
      req.write(JSON.stringify(data));
    }
    req.end();
  });
}

// MCP request helper
async function mcpRequest(baseUrl, method, params = {}) {
  const mcpPayload = {
    jsonrpc: "2.0",
    id: Date.now(),
    method: method,
    params: params
  };
  
  return await httpRequest(`${baseUrl}/api/mcp`, 'POST', mcpPayload);
}

// ===========================================================================
// TEST SUITE 1: MEMORY AGENT (Port 5010)
// ===========================================================================

async function testMemoryAgentHealth() {
  logSection('TEST 1: Memory Agent Health Check');
  
  try {
    const response = await httpRequest(`${MEMORY_ROUTER_URL}/health`);
    
    if (response.statusCode === 200 && response.data.status === 'healthy') {
      logTest('Memory Agent Health', 'PASS', 'Service is healthy');
      return true;
    } else {
      logTest('Memory Agent Health', 'FAIL', `Status: ${response.statusCode}`);
      return false;
    }
  } catch (err) {
    logTest('Memory Agent Health', 'FAIL', err.message);
    return false;
  }
}

async function testMemoryAgentToolsList() {
  logSection('TEST 2: Memory Agent - List Tools');
  
  try {
    const response = await mcpRequest(MEMORY_ROUTER_URL, 'tools/list');
    
    if (response.statusCode === 200 && response.data.result && response.data.result.tools) {
      const toolCount = response.data.result.tools.length;
      logTest('List Tools', 'PASS', `Found ${toolCount} tools`);
      
      // Log some key tools
      const keyTools = ['smartsearch', 'validate', 'get_context', 'analyze_complexity'];
      const foundTools = response.data.result.tools.filter(t => keyTools.includes(t.name));
      log(`  Key tools found: ${foundTools.map(t => t.name).join(', ')}`, 'blue');
      
      return response.data.result.tools;
    } else {
      logTest('List Tools', 'FAIL', 'No tools returned');
      return null;
    }
  } catch (err) {
    logTest('List Tools', 'FAIL', err.message);
    return null;
  }
}

async function testMemoryAgentSearch() {
  logSection('TEST 3: Memory Agent - Smart Search');
  
  try {
    const response = await mcpRequest(MEMORY_ROUTER_URL, 'tools/call', {
      name: 'smartsearch',
      arguments: {
        query: 'authentication patterns',
        context: CONTEXT,
        limit: 5
      }
    });
    
    if (response.statusCode === 200 && response.data.result) {
      logTest('Smart Search', 'PASS', 'Search executed successfully');
      log(`  Result preview: ${JSON.stringify(response.data.result).substring(0, 100)}...`, 'blue');
      return true;
    } else {
      logTest('Smart Search', 'FAIL', `Status: ${response.statusCode}`);
      return false;
    }
  } catch (err) {
    logTest('Smart Search', 'FAIL', err.message);
    return false;
  }
}

async function testMemoryAgentValidate() {
  logSection('TEST 4: Memory Agent - Validate Code');
  
  try {
    const response = await mcpRequest(MEMORY_ROUTER_URL, 'tools/call', {
      name: 'validate',
      arguments: {
        scope: 'best_practices',
        context: CONTEXT,
        minSeverity: 'medium'
      }
    });
    
    if (response.statusCode === 200 && response.data.result) {
      logTest('Validate Code', 'PASS', 'Validation executed');
      return true;
    } else {
      logTest('Validate Code', 'FAIL', `Status: ${response.statusCode}`);
      return false;
    }
  } catch (err) {
    logTest('Validate Code', 'FAIL', err.message);
    return false;
  }
}

async function testMemoryAgentGetContext() {
  logSection('TEST 5: Memory Agent - Get Context');
  
  try {
    const response = await mcpRequest(MEMORY_ROUTER_URL, 'tools/call', {
      name: 'get_context',
      arguments: {
        task: 'implement user authentication',
        context: CONTEXT,
        includePatterns: true,
        includeQA: true
      }
    });
    
    if (response.statusCode === 200 && response.data.result) {
      logTest('Get Context', 'PASS', 'Context retrieved');
      return true;
    } else {
      logTest('Get Context', 'FAIL', `Status: ${response.statusCode}`);
      return false;
    }
  } catch (err) {
    logTest('Get Context', 'FAIL', err.message);
    return false;
  }
}

// ===========================================================================
// TEST SUITE 2: CODE GENERATOR (Port 5001)
// ===========================================================================

async function testCodingAgentHealth() {
  logSection('TEST 6: Code Generator Health Check');
  
  try {
    const response = await httpRequest(`${CODING_AGENT_URL}/health`);
    
    if (response.statusCode === 200) {
      logTest('Code Generator Health', 'PASS', 'Service is healthy');
      return true;
    } else {
      logTest('Code Generator Health', 'FAIL', `Status: ${response.statusCode}`);
      return false;
    }
  } catch (err) {
    logTest('Code Generator Health', 'FAIL', err.message);
    return false;
  }
}

async function testCodingAgentToolsList() {
  logSection('TEST 7: Code Generator - List Jobs');
  
  try {
    const response = await httpRequest(`${CODING_AGENT_URL}/jobs`, 'GET');
    
    if (response.statusCode === 200) {
      const jobs = Array.isArray(response.data) ? response.data : [];
      logTest('List Jobs Endpoint', 'PASS', `Found ${jobs.length} jobs`);
      return jobs;
    } else {
      logTest('List Jobs Endpoint', 'FAIL', `Status: ${response.statusCode}`);
      return null;
    }
  } catch (err) {
    logTest('List Jobs Endpoint', 'FAIL', err.message);
    return null;
  }
}

async function testCodeGeneration() {
  logSection('TEST 8: Code Generator - Generate Calculator');
  
  try {
    log('  Starting code generation...', 'yellow');
    
    const response = await httpRequest(`${CODING_AGENT_URL}/api/orchestrator/orchestrate`, 'POST', {
      task: 'Create a Calculator class with Add, Subtract, Multiply, and Divide methods. Include unit tests.',
      language: 'csharp',
      workspacePath: WORKSPACE_PATH,
      maxIterations: 10
    });
    
    if (response.statusCode === 200 && response.data.jobId) {
      const jobId = response.data.jobId;
      logTest('Start Code Generation', 'PASS', `Job ID: ${jobId}`);
      
      // Monitor job status
      return await monitorJobStatus(jobId);
    } else {
      logTest('Start Code Generation', 'FAIL', `Status: ${response.statusCode}`);
      return null;
    }
  } catch (err) {
    logTest('Start Code Generation', 'FAIL', err.message);
    return null;
  }
}

async function monitorJobStatus(jobId) {
  logSection(`TEST 9: Monitor Job Progress (${jobId})`);
  
  let attempts = 0;
  const maxAttempts = 60; // 5 minutes max
  
  while (attempts < maxAttempts) {
    try {
      const response = await httpRequest(`${CODING_AGENT_URL}/status/${jobId}`, 'GET');
      
      if (response.statusCode === 200 && response.data) {
        const status = response.data.status;
        const score = response.data.validationScore || 0;
        const iteration = response.data.iteration || 0;
        
        log(`  Attempt ${attempts + 1}: Status=${status}, Score=${score}/10, Iteration=${iteration}`, 'yellow');
        
        if (status === 'Completed') {
          if (score >= 8) {
            logTest('Job Completion', 'PASS', `Score: ${score}/10`);
            return response.data;
          } else {
            logTest('Job Completion', 'FAIL', `Low score: ${score}/10`);
            return response.data;
          }
        } else if (status === 'Failed') {
          logTest('Job Completion', 'FAIL', 'Job failed');
          return response.data;
        }
        
        // Wait 5 seconds before next check
        await new Promise(resolve => setTimeout(resolve, 5000));
        attempts++;
      } else {
        logTest('Monitor Job', 'FAIL', `Status: ${response.statusCode}`);
        return null;
      }
    } catch (err) {
      logTest('Monitor Job', 'FAIL', err.message);
      return null;
    }
  }
  
  logTest('Monitor Job', 'FAIL', 'Timeout after 5 minutes');
  return null;
}

async function testGetJobStatus(jobId) {
  logSection(`TEST 10: Get Final Job Status (${jobId})`);
  
  try {
    const response = await httpRequest(`${CODING_AGENT_URL}/status/${jobId}`, 'GET');
    
    if (response.statusCode === 200 && response.data) {
      const data = response.data;
      
      log(`  Status: ${data.status}`, 'blue');
      log(`  Score: ${data.validationScore || 0}/10`, 'blue');
      log(`  Files: ${data.generatedFiles?.length || 0}`, 'blue');
      log(`  Output Path: ${data.outputPath || 'N/A'}`, 'blue');
      
      if (data.generatedFiles && data.generatedFiles.length > 0) {
        logTest('Get Job Status', 'PASS', `${data.generatedFiles.length} files generated`);
        return data;
      } else {
        logTest('Get Job Status', 'FAIL', 'No files generated');
        return null;
      }
    } else {
      logTest('Get Job Status', 'FAIL', `Status: ${response.statusCode}`);
      return null;
    }
  } catch (err) {
    logTest('Get Job Status', 'FAIL', err.message);
    return null;
  }
}

async function testListJobs() {
  logSection('TEST 11: List All Jobs');
  
  try {
    const response = await httpRequest(`${CODING_AGENT_URL}/jobs`, 'GET');
    
    if (response.statusCode === 200 && response.data) {
      const jobs = Array.isArray(response.data) ? response.data : [];
      logTest('List Jobs', 'PASS', `Found ${jobs.length} jobs`);
      
      // Show last 3 jobs
      if (jobs.length > 0) {
        log(`  Recent jobs:`, 'blue');
        jobs.slice(0, 3).forEach(job => {
          log(`    - ${job.jobId}: ${job.status} (Score: ${job.validationScore || 0}/10)`, 'blue');
        });
      }
      
      return jobs;
    } else {
      logTest('List Jobs', 'FAIL', `Status: ${response.statusCode}`);
      return null;
    }
  } catch (err) {
    logTest('List Jobs', 'FAIL', err.message);
    return null;
  }
}

// ===========================================================================
// TEST SUITE 3: END-TO-END WORKFLOW
// ===========================================================================

async function testEndToEndWorkflow() {
  logSection('TEST 12: END-TO-END WORKFLOW');
  
  log('  Step 1: Get context from Memory Agent...', 'yellow');
  const context = await mcpRequest(MEMORY_ROUTER_URL, 'tools/call', {
    name: 'get_context',
    arguments: {
      task: 'create a simple calculator',
      context: CONTEXT
    }
  });
  
  if (context.statusCode === 200) {
    log('  ✅ Context retrieved', 'green');
  } else {
    log('  ❌ Failed to get context', 'red');
  }
  
  log('  Step 2: Generate code with Code Generator...', 'yellow');
  const genResponse = await httpRequest(`${CODING_AGENT_URL}/orchestrate`, 'POST', {
    task: 'Create a simple Calculator class with Add and Subtract methods only',
    language: 'csharp',
    workspacePath: WORKSPACE_PATH,
    maxIterations: 5
  });
  
  if (genResponse.statusCode === 200 && genResponse.data.jobId) {
    const jobId = genResponse.data.jobId;
    log(`  ✅ Code generation started: ${jobId}`, 'green');
    
    log('  Step 3: Monitor job progress...', 'yellow');
    const finalStatus = await monitorJobStatus(jobId);
    
    if (finalStatus && finalStatus.status === 'Completed') {
      log('  ✅ Code generation completed', 'green');
      
      log('  Step 4: Validate generated code with Memory Agent...', 'yellow');
      const validation = await mcpRequest(MEMORY_ROUTER_URL, 'tools/call', {
        name: 'validate',
        arguments: {
          scope: 'best_practices',
          context: CONTEXT
        }
      });
      
      if (validation.statusCode === 200) {
        log('  ✅ Validation completed', 'green');
        logTest('End-to-End Workflow', 'PASS', 'All steps completed successfully');
        return true;
      } else {
        log('  ❌ Validation failed', 'red');
        logTest('End-to-End Workflow', 'FAIL', 'Validation step failed');
        return false;
      }
    } else {
      log('  ❌ Code generation failed', 'red');
      logTest('End-to-End Workflow', 'FAIL', 'Code generation failed');
      return false;
    }
  } else {
    log('  ❌ Failed to start code generation', 'red');
    logTest('End-to-End Workflow', 'FAIL', 'Failed to start generation');
    return false;
  }
}

// ===========================================================================
// MAIN TEST RUNNER
// ===========================================================================

async function runAllTests() {
  log('\n' + '█'.repeat(60), 'cyan');
  log('  🧪 COMPREHENSIVE TEST SUITE', 'cyan');
  log('  Testing Memory Agent + Code Generator', 'cyan');
  log('█'.repeat(60) + '\n', 'cyan');
  
  // Memory Agent Tests
  await testMemoryAgentHealth();
  await testMemoryAgentToolsList();
  await testMemoryAgentSearch();
  await testMemoryAgentValidate();
  await testMemoryAgentGetContext();
  
  // Code Generator Tests
  await testCodingAgentHealth();
  await testCodingAgentToolsList();
  
  // Code Generation Test
  const jobData = await testCodeGeneration();
  
  if (jobData && jobData.jobId) {
    await testGetJobStatus(jobData.jobId);
  }
  
  await testListJobs();
  
  // End-to-End Workflow
  await testEndToEndWorkflow();
  
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

