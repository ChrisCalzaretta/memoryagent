const vscode = require('vscode');
const JobPoller = require('./jobPoller');
const StatusBarManager = require('./statusBar');

let jobPoller;
let statusBarManager;

/**
 * Activate extension
 */
function activate(context) {
    console.log('[Job Status] Extension activating...');

    // Get configuration
    const config = vscode.workspace.getConfiguration('jobStatus');
    const orchestratorUrl = config.get('orchestratorUrl', 'http://localhost:5001');
    const memoryRouterUrl = config.get('memoryRouterUrl', 'http://localhost:5010');
    const pollingInterval = config.get('pollingInterval', 3000);

    // Initialize components
    statusBarManager = new StatusBarManager();
    jobPoller = new JobPoller(orchestratorUrl, memoryRouterUrl);

    // Connect poller to status bar
    jobPoller.onUpdate((jobs) => {
        statusBarManager.update(jobs);
    });

    // Start polling
    jobPoller.startPolling(pollingInterval);

    // Register commands
    const showDetailsCommand = vscode.commands.registerCommand('jobStatus.showDetails', () => {
        statusBarManager.showDetails();
    });

    const refreshCommand = vscode.commands.registerCommand('jobStatus.refresh', async () => {
        vscode.window.showInformationMessage('Refreshing job status...');
        await jobPoller.pollAll();
    });

    const cancelJobCommand = vscode.commands.registerCommand('jobStatus.cancelJob', async () => {
        // Show quick pick to select job to cancel
        const jobs = statusBarManager.currentJobs.filter(j => 
            j.status === 'running' || j.status === 'queued'
        );

        if (jobs.length === 0) {
            vscode.window.showInformationMessage('No jobs to cancel');
            return;
        }

        const items = jobs.map(job => ({
            label: job.task,
            description: `${job.status} | ${job.progress}%`,
            job: job
        }));

        const selected = await vscode.window.showQuickPick(items, {
            placeHolder: 'Select a job to cancel'
        });

        if (selected) {
            try {
                await jobPoller.cancelJob(selected.job.id, selected.job.type);
                vscode.window.showInformationMessage(`Job "${selected.job.task}" cancelled`);
                await jobPoller.pollAll(); // Refresh immediately
            } catch (err) {
                vscode.window.showErrorMessage(`Failed to cancel job: ${err.message}`);
            }
        }
    });

    // Watch for configuration changes
    const configWatcher = vscode.workspace.onDidChangeConfiguration(e => {
        if (e.affectsConfiguration('jobStatus')) {
            const newConfig = vscode.workspace.getConfiguration('jobStatus');
            const newInterval = newConfig.get('pollingInterval', 3000);
            
            // Restart polling with new interval
            jobPoller.stopPolling();
            jobPoller.startPolling(newInterval);
            
            vscode.window.showInformationMessage('Job Status settings updated');
        }
    });

    // Add to subscriptions for cleanup
    context.subscriptions.push(
        statusBarManager.statusBarItem,
        showDetailsCommand,
        refreshCommand,
        cancelJobCommand,
        configWatcher
    );

    console.log('[Job Status] Extension activated successfully!');
    vscode.window.showInformationMessage('✅ Job Status extension is now active!');
}

/**
 * Deactivate extension
 */
function deactivate() {
    console.log('[Job Status] Extension deactivating...');
    
    if (jobPoller) {
        jobPoller.stopPolling();
    }
    
    if (statusBarManager) {
        statusBarManager.dispose();
    }
    
    console.log('[Job Status] Extension deactivated');
}

module.exports = {
    activate,
    deactivate
};



