#!/usr/bin/env node
/**
 * Test script for MCP wrapper
 * Simulates Cursor sending MCP requests
 */

const { spawn } = require('child_process');

console.log('🧪 Testing MCP Wrapper...\n');

// Start the wrapper
const wrapper = spawn('node', ['mcp-wrapper-expanded.js'], {
  stdio: ['pipe', 'pipe', 'pipe']
});

// Listen to STDERR (logs)
wrapper.stderr.on('data', (data) => {
  console.log(`[LOG] ${data.toString().trim()}`);
});

// Listen to STDOUT (responses)
wrapper.stdout.on('data', (data) => {
  console.log(`[RESPONSE] ${data.toString().trim()}`);
});

// Wait for initialization
setTimeout(() => {
  console.log('\n📤 Test 1: Starting code generation...\n');
  
  // Test 1: Start code generation
  const request1 = {
    jsonrpc: "2.0",
    method: "codingagent/generate",
    params: {
      task: "Create a simple Calculator class with Add and Subtract methods",
      language: "csharp"
    },
    id: 1
  };
  
  wrapper.stdin.write(JSON.stringify(request1) + '\n');
  
  // Test 2: Check status after 5 seconds
  setTimeout(() => {
    console.log('\n📤 Test 2: Checking job status...\n');
    
    const request2 = {
      jsonrpc: "2.0",
      method: "codingagent/status",
      params: {},
      id: 2
    };
    
    wrapper.stdin.write(JSON.stringify(request2) + '\n');
  }, 5000);
  
  // Test 3: Get Lightning prompt after 8 seconds
  setTimeout(() => {
    console.log('\n📤 Test 3: Getting AI Lightning prompt...\n');
    
    const request3 = {
      jsonrpc: "2.0",
      method: "lightning/get_prompt",
      params: {
        prompt_id: "agentic_coding_system_v1"
      },
      id: 3
    };
    
    wrapper.stdin.write(JSON.stringify(request3) + '\n');
  }, 8000);
  
  // Cleanup after 15 seconds
  setTimeout(() => {
    console.log('\n✅ Tests complete, shutting down...\n');
    wrapper.kill('SIGTERM');
    process.exit(0);
  }, 15000);
  
}, 10000); // Wait 10 seconds for initialization

// Handle errors
wrapper.on('error', (err) => {
  console.error(`❌ Error: ${err.message}`);
  process.exit(1);
});

wrapper.on('exit', (code) => {
  console.log(`\n🏁 Wrapper exited with code ${code}`);
});


