const vscode = require('vscode');

class StatusBarManager {
    constructor() {
        this.statusBarItem = vscode.window.createStatusBarItem(
            vscode.StatusBarAlignment.Left,
            100
        );
        this.statusBarItem.command = 'jobStatus.showDetails';
        this.currentJobs = [];
        this.lastNotifiedJobs = new Set();
    }

    /**
     * Update status bar with current jobs
     */
    update(jobs) {
        this.currentJobs = jobs;

        if (jobs.length === 0) {
            this.statusBarItem.text = '$(pulse) No active jobs';
            this.statusBarItem.tooltip = 'No code generation jobs running';
            this.statusBarItem.backgroundColor = undefined;
            this.statusBarItem.show();
            return;
        }

        // Show the most relevant job (running > queued > others)
        const runningJobs = jobs.filter(j => j.status === 'running');
        const primaryJob = runningJobs[0] || jobs[0];

        const icon = this.getStatusIcon(primaryJob.status);
        const color = this.getStatusColor(primaryJob.status);

        // Format: 🔄 UserService (60%) | ⏱️ 2m 15s
        if (jobs.length === 1) {
            const taskName = this.truncateTask(primaryJob.task, 20);
            this.statusBarItem.text = `${icon} ${taskName} (${primaryJob.progress}%) | ⏱️ ${primaryJob.duration}`;
            
            this.statusBarItem.tooltip = this.createTooltip(primaryJob);
        } else {
            // Multiple jobs: 🔄 2 jobs | UserService (60%)
            const taskName = this.truncateTask(primaryJob.task, 15);
            this.statusBarItem.text = `${icon} ${jobs.length} jobs | ${taskName} (${primaryJob.progress}%)`;
            
            this.statusBarItem.tooltip = this.createMultiJobTooltip(jobs);
        }

        this.statusBarItem.backgroundColor = color;
        this.statusBarItem.show();

        // Check for completed/failed jobs and notify
        this.checkForNotifications(jobs);
    }

    /**
     * Get status icon
     */
    getStatusIcon(status) {
        const icons = {
            'running': '$(sync~spin)',
            'completed': '$(check)',
            'failed': '$(error)',
            'cancelled': '$(circle-slash)',
            'queued': '$(clock)'
        };
        return icons[status] || '$(question)';
    }

    /**
     * Get status color
     */
    getStatusColor(status) {
        if (status === 'failed') {
            return new vscode.ThemeColor('statusBarItem.errorBackground');
        }
        if (status === 'completed') {
            return new vscode.ThemeColor('statusBarItem.warningBackground');
        }
        return undefined;
    }

    /**
     * Create tooltip for single job
     */
    createTooltip(job) {
        let tooltip = `Job: ${job.task}\n`;
        tooltip += `Status: ${job.status}\n`;
        tooltip += `Progress: ${job.progress}%\n`;
        
        if (job.phase) {
            tooltip += `Phase: ${job.phase}\n`;
        }
        
        if (job.iteration && job.maxIterations) {
            tooltip += `Iteration: ${job.iteration}/${job.maxIterations}\n`;
        }
        
        if (job.score) {
            tooltip += `Score: ${job.score}/10\n`;
        }
        
        tooltip += `Duration: ${job.duration}\n`;
        tooltip += `\n💡 Click for details`;
        
        return tooltip;
    }

    /**
     * Create tooltip for multiple jobs
     */
    createMultiJobTooltip(jobs) {
        let tooltip = `Active Jobs (${jobs.length}):\n\n`;
        
        jobs.slice(0, 5).forEach((job, index) => {
            const icon = this.getStatusIcon(job.status);
            const taskName = this.truncateTask(job.task, 25);
            tooltip += `${icon} ${taskName} (${job.progress}%)\n`;
        });
        
        if (jobs.length > 5) {
            tooltip += `\n... and ${jobs.length - 5} more\n`;
        }
        
        tooltip += `\n💡 Click for details`;
        
        return tooltip;
    }

    /**
     * Truncate task name
     */
    truncateTask(task, maxLength) {
        if (task.length <= maxLength) return task;
        return task.substring(0, maxLength - 3) + '...';
    }

    /**
     * Check for jobs that completed/failed and show notifications
     */
    checkForNotifications(jobs) {
        const config = vscode.workspace.getConfiguration('jobStatus');
        const showNotifications = config.get('showNotifications', true);
        
        if (!showNotifications) return;

        jobs.forEach(job => {
            const wasNotified = this.lastNotifiedJobs.has(job.id);
            const isTerminal = job.status === 'completed' || job.status === 'failed';
            
            if (isTerminal && !wasNotified) {
                this.showNotification(job);
                this.lastNotifiedJobs.add(job.id);
            }
        });

        // Clean up old notifications (keep last 50)
        if (this.lastNotifiedJobs.size > 50) {
            const entries = Array.from(this.lastNotifiedJobs);
            this.lastNotifiedJobs = new Set(entries.slice(-50));
        }
    }

    /**
     * Show notification for completed/failed job
     */
    showNotification(job) {
        if (job.status === 'completed') {
            const message = `✅ ${job.task} completed! Score: ${job.score || 'N/A'}/10`;
            vscode.window.showInformationMessage(message, 'View Details', 'Dismiss')
                .then(selection => {
                    if (selection === 'View Details') {
                        vscode.commands.executeCommand('jobStatus.showDetails');
                    }
                });
        } else if (job.status === 'failed') {
            const message = `❌ ${job.task} failed! Score: ${job.score || 'N/A'}/10`;
            vscode.window.showErrorMessage(message, 'View Details', 'Retry', 'Dismiss')
                .then(selection => {
                    if (selection === 'View Details') {
                        vscode.commands.executeCommand('jobStatus.showDetails');
                    } else if (selection === 'Retry') {
                        // TODO: Implement retry
                        vscode.window.showInformationMessage('Retry not yet implemented');
                    }
                });
        }
    }

    /**
     * Show detailed job panel
     */
    showDetails() {
        if (this.currentJobs.length === 0) {
            vscode.window.showInformationMessage('No active jobs');
            return;
        }

        // Create quick pick items
        const items = this.currentJobs.map(job => ({
            label: `${this.getStatusIcon(job.status)} ${job.task}`,
            description: `${job.progress}% | ${job.duration}`,
            detail: `${job.status} | ${job.phase || 'N/A'} | Score: ${job.score || 'N/A'}/10`,
            job: job
        }));

        vscode.window.showQuickPick(items, {
            placeHolder: 'Select a job to view details',
            matchOnDescription: true,
            matchOnDetail: true
        }).then(selected => {
            if (selected) {
                this.showJobDetails(selected.job);
            }
        });
    }

    /**
     * Show detailed information for a specific job
     */
    showJobDetails(job) {
        const panel = vscode.window.createWebviewPanel(
            'jobDetails',
            `Job: ${job.task}`,
            vscode.ViewColumn.Two,
            { enableScripts: true }
        );

        panel.webview.html = this.getJobDetailsHtml(job);
    }

    /**
     * Generate HTML for job details
     */
    getJobDetailsHtml(job) {
        const statusColor = {
            'running': '#4CAF50',
            'completed': '#2196F3',
            'failed': '#f44336',
            'cancelled': '#9E9E9E',
            'queued': '#FF9800'
        }[job.status] || '#666';

        return `
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <style>
        body {
            font-family: var(--vscode-font-family);
            padding: 20px;
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
        }
        .header {
            border-bottom: 2px solid ${statusColor};
            padding-bottom: 10px;
            margin-bottom: 20px;
        }
        .status {
            display: inline-block;
            padding: 5px 15px;
            border-radius: 5px;
            background-color: ${statusColor};
            color: white;
            font-weight: bold;
            text-transform: uppercase;
        }
        .progress {
            width: 100%;
            height: 30px;
            background-color: var(--vscode-input-background);
            border-radius: 5px;
            overflow: hidden;
            margin: 10px 0;
        }
        .progress-bar {
            height: 100%;
            background-color: ${statusColor};
            transition: width 0.3s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
        }
        .info-grid {
            display: grid;
            grid-template-columns: 150px 1fr;
            gap: 10px;
            margin: 20px 0;
        }
        .info-label {
            font-weight: bold;
            color: var(--vscode-descriptionForeground);
        }
        .info-value {
            color: var(--vscode-foreground);
        }
        .section {
            margin: 30px 0;
            padding: 15px;
            background-color: var(--vscode-editor-background);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 5px;
        }
        .section-title {
            font-size: 1.2em;
            margin-bottom: 10px;
            color: var(--vscode-foreground);
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>${job.task}</h1>
        <span class="status">${job.status}</span>
    </div>

    <div class="progress">
        <div class="progress-bar" style="width: ${job.progress}%">
            ${job.progress}%
        </div>
    </div>

    <div class="section">
        <div class="section-title">Job Information</div>
        <div class="info-grid">
            <div class="info-label">Job ID:</div>
            <div class="info-value"><code>${job.id}</code></div>
            
            <div class="info-label">Type:</div>
            <div class="info-value">${job.type === 'coding' ? 'Code Generation' : 'Workflow'}</div>
            
            <div class="info-label">Status:</div>
            <div class="info-value">${job.status}</div>
            
            <div class="info-label">Progress:</div>
            <div class="info-value">${job.progress}%</div>
            
            ${job.phase ? `
            <div class="info-label">Current Phase:</div>
            <div class="info-value">${job.phase}</div>
            ` : ''}
            
            ${job.iteration ? `
            <div class="info-label">Iteration:</div>
            <div class="info-value">${job.iteration}/${job.maxIterations}</div>
            ` : ''}
            
            ${job.score ? `
            <div class="info-label">Validation Score:</div>
            <div class="info-value">${job.score}/10</div>
            ` : ''}
            
            <div class="info-label">Duration:</div>
            <div class="info-value">${job.duration}</div>
            
            <div class="info-label">Started:</div>
            <div class="info-value">${new Date(job.startedAt).toLocaleString()}</div>
            
            ${job.completedAt ? `
            <div class="info-label">Completed:</div>
            <div class="info-value">${new Date(job.completedAt).toLocaleString()}</div>
            ` : ''}
        </div>
    </div>

    <div class="section">
        <div class="section-title">💡 Tips</div>
        <ul>
            <li>Use <code>get_task_status</code> MCP tool to get detailed information</li>
            <li>Use <code>apply_task_files</code> to retrieve generated files when complete</li>
            <li>Check the AI chat for full code output and validation details</li>
        </ul>
    </div>
</body>
</html>
        `;
    }

    /**
     * Dispose resources
     */
    dispose() {
        this.statusBarItem.dispose();
    }
}

module.exports = StatusBarManager;



