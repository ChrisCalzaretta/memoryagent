#!/usr/bin/env node

/**
 * Real-time Job Monitor via WebSocket + API Polling
 * Usage: node watch-job.js <jobId>
 */

const http = require('http');
const WebSocket = require('ws');

const jobId = process.argv[2] || 'job_20251222004923_606dbfd342a849b99355c5bee0e20a74';
const apiUrl = `http://localhost:5001/api/orchestrator/status/${jobId}`;
const wsUrl = 'ws://localhost:5001/ws/coding';

console.clear();
console.log('\n🔍 LIVE JOB MONITOR\n');
console.log('═'.repeat(80));
console.log(`📋 Job ID: ${jobId}`);
console.log('═'.repeat(80));

// WebSocket connection for real-time events
const ws = new WebSocket(wsUrl);
let wsConnected = false;

ws.on('open', () => {
    wsConnected = true;
    console.log('\n✅ WebSocket Connected - Real-time updates active');
    
    // Subscribe to job events
    ws.send(JSON.stringify({
        action: 'subscribe',
        jobId: jobId
    }));
});

ws.on('message', (data) => {
    try {
        const event = JSON.parse(data.toString());
        const timestamp = new Date().toLocaleTimeString();
        
        // Format event based on type
        let icon = '📡';
        if (event.type === 'progress') icon = '📊';
        if (event.type === 'status') icon = '⚡';
        if (event.type === 'error') icon = '❌';
        if (event.type === 'complete') icon = '✅';
        
        console.log(`\n[${timestamp}] ${icon} ${event.type.toUpperCase()}`);
        console.log(JSON.stringify(event, null, 2));
        console.log('─'.repeat(80));
    } catch (err) {
        console.log('📨 Raw message:', data.toString());
    }
});

ws.on('error', (err) => {
    console.log('\n⚠️  WebSocket Error:', err.message);
    wsConnected = false;
});

ws.on('close', () => {
    console.log('\n⚠️  WebSocket Disconnected');
    wsConnected = false;
});

// API polling as fallback
let lastStatus = null;
let lastProgress = -1;

function pollStatus() {
    const req = http.get(apiUrl, (res) => {
        let data = '';
        
        res.on('data', (chunk) => {
            data += chunk;
        });
        
        res.on('end', () => {
            try {
                const status = JSON.parse(data);
                
                // Only log if something changed
                if (status.status !== lastStatus || status.progress !== lastProgress) {
                    const timestamp = new Date().toLocaleTimeString();
                    console.log(`\n[${timestamp}] 📊 STATUS UPDATE`);
                    console.log(`Status: ${status.status}`);
                    console.log(`Progress: ${status.progress}%`);
                    
                    if (status.error) {
                        console.log(`❌ Error: ${status.error}`);
                    }
                    
                    console.log('─'.repeat(80));
                    
                    lastStatus = status.status;
                    lastProgress = status.progress;
                    
                    // Exit if complete or failed
                    if (status.status === 'completed' || status.status === 'failed') {
                        console.log('\n🏁 Job finished!');
                        if (status.result) {
                            console.log('\n📄 Generated Files:');
                            console.log(JSON.stringify(status.result.files?.map(f => f.path), null, 2));
                        }
                        setTimeout(() => process.exit(0), 2000);
                    }
                }
            } catch (err) {
                console.error('❌ Failed to parse API response:', err.message);
            }
        });
    });
    
    req.on('error', (err) => {
        console.error('❌ API Error:', err.message);
    });
    
    req.end();
}

// Poll every 5 seconds
console.log('\n⏱️  Polling every 5 seconds...');
console.log('🔌 ' + (wsConnected ? 'WebSocket CONNECTED' : 'WebSocket DISCONNECTED'));
console.log('═'.repeat(80));

pollStatus(); // Initial poll
const interval = setInterval(pollStatus, 5000);

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n\n👋 Shutting down monitor...');
    clearInterval(interval);
    ws.close();
    process.exit(0);
});

