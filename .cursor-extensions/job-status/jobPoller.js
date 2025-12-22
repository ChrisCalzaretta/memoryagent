const http = require('http');

class JobPoller {
    constructor(orchestratorUrl, memoryRouterUrl) {
        this.orchestratorUrl = orchestratorUrl;
        this.memoryRouterUrl = memoryRouterUrl;
        this.pollingInterval = null;
        this.listeners = [];
    }

    /**
     * Make HTTP GET request
     */
    async makeRequest(url) {
        return new Promise((resolve, reject) => {
            const urlObj = new URL(url);
            const options = {
                hostname: urlObj.hostname,
                port: urlObj.port,
                path: urlObj.pathname + urlObj.search,
                method: 'GET',
                timeout: 5000
            };

            const req = http.request(options, (res) => {
                let data = '';
                res.on('data', (chunk) => data += chunk);
                res.on('end', () => {
                    try {
                        resolve(JSON.parse(data));
                    } catch (err) {
                        resolve(null);
                    }
                });
            });

            req.on('error', (err) => {
                resolve(null); // Silent fail
            });

            req.on('timeout', () => {
                req.destroy();
                resolve(null);
            });

            req.end();
        });
    }

    /**
     * Poll for coding jobs from CodingOrchestrator
     */
    async pollCodingJobs() {
        try {
            const response = await this.makeRequest(`${this.orchestratorUrl}/api/orchestrator/jobs`);
            if (!response || !Array.isArray(response)) return [];

            return response.map(job => {
                // Parse status - it comes as "running (attempt 1/100) - solo thinking"
                let statusText = job.status || 'unknown';
                let cleanStatus = statusText.split(' ')[0].toLowerCase(); // Get first word
                
                return {
                    id: job.jobId,
                    type: 'coding',
                    task: job.task || 'Code Generation',
                    status: cleanStatus, // 'running', 'completed', 'failed', etc.
                    progress: job.progress || 0,
                    phase: statusText.includes('-') ? statusText.split('-')[1].trim() : 'processing',
                    iteration: 0,
                    maxIterations: 0,
                    score: 0,
                    duration: this.formatDuration(job.startedAt),
                    startedAt: job.startedAt,
                    completedAt: job.completedAt
                };
            });
        } catch (err) {
            console.error('[JobPoller] Error polling coding jobs:', err);
            return [];
        }
    }

    /**
     * Poll for workflow jobs from MemoryRouter
     */
    async pollWorkflows() {
        try {
            const response = await this.makeRequest(`${this.memoryRouterUrl}/api/workflows/list`);
            if (!response || !response.workflows) return [];

            return response.workflows
                .filter(w => w.status === 'running' || w.status === 'queued')
                .map(workflow => ({
                    id: workflow.workflowId,
                    type: 'workflow',
                    task: workflow.request || 'Workflow',
                    status: workflow.status,
                    progress: workflow.progress || 0,
                    phase: workflow.currentStep || 'initializing',
                    duration: this.formatDuration(workflow.startedAt),
                    startedAt: workflow.startedAt
                }));
        } catch (err) {
            console.error('[JobPoller] Error polling workflows:', err);
            return [];
        }
    }

    /**
     * Poll all job sources
     */
    async pollAll() {
        const [codingJobs, workflows] = await Promise.all([
            this.pollCodingJobs(),
            this.pollWorkflows()
        ]);

        const allJobs = [...codingJobs, ...workflows];
        this.notifyListeners(allJobs);
        return allJobs;
    }

    /**
     * Start polling
     */
    startPolling(intervalMs = 3000) {
        if (this.pollingInterval) {
            clearInterval(this.pollingInterval);
        }

        console.log('[JobPoller] Starting polling every', intervalMs, 'ms');
        
        // Poll immediately
        this.pollAll();

        // Then poll at interval
        this.pollingInterval = setInterval(() => {
            this.pollAll();
        }, intervalMs);
    }

    /**
     * Stop polling
     */
    stopPolling() {
        if (this.pollingInterval) {
            clearInterval(this.pollingInterval);
            this.pollingInterval = null;
            console.log('[JobPoller] Stopped polling');
        }
    }

    /**
     * Add listener for job updates
     */
    onUpdate(callback) {
        this.listeners.push(callback);
    }

    /**
     * Notify all listeners
     */
    notifyListeners(jobs) {
        this.listeners.forEach(listener => {
            try {
                listener(jobs);
            } catch (err) {
                console.error('[JobPoller] Listener error:', err);
            }
        });
    }

    /**
     * Format duration from start time
     */
    formatDuration(startTime) {
        if (!startTime) return '0s';
        
        const start = new Date(startTime);
        const now = new Date();
        const diff = Math.floor((now - start) / 1000); // seconds

        if (diff < 60) return `${diff}s`;
        if (diff < 3600) return `${Math.floor(diff / 60)}m ${diff % 60}s`;
        return `${Math.floor(diff / 3600)}h ${Math.floor((diff % 3600) / 60)}m`;
    }

    /**
     * Get detailed job status
     */
    async getJobDetails(jobId, type = 'coding') {
        try {
            const url = type === 'coding'
                ? `${this.orchestratorUrl}/api/orchestrator/status/${jobId}`
                : `${this.memoryRouterUrl}/api/workflows/status/${jobId}`;
            
            return await this.makeRequest(url);
        } catch (err) {
            console.error('[JobPoller] Error getting job details:', err);
            return null;
        }
    }

    /**
     * Cancel a job
     */
    async cancelJob(jobId, type = 'coding') {
        return new Promise((resolve, reject) => {
            const url = type === 'coding'
                ? `${this.orchestratorUrl}/api/orchestrator/cancel/${jobId}`
                : `${this.memoryRouterUrl}/api/workflows/cancel/${jobId}`;

            const urlObj = new URL(url);
            const options = {
                hostname: urlObj.hostname,
                port: urlObj.port,
                path: urlObj.pathname,
                method: 'POST',
                timeout: 5000
            };

            const req = http.request(options, (res) => {
                let data = '';
                res.on('data', (chunk) => data += chunk);
                res.on('end', () => {
                    try {
                        resolve(JSON.parse(data));
                    } catch (err) {
                        resolve({ success: false });
                    }
                });
            });

            req.on('error', (err) => {
                reject(err);
            });

            req.on('timeout', () => {
                req.destroy();
                reject(new Error('Request timeout'));
            });

            req.end();
        });
    }
}

module.exports = JobPoller;


