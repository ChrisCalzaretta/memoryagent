using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CodingAgent.Server.Controllers;

[ApiController]
public class JobStatusPageController : ControllerBase
{
    private readonly ILogger<JobStatusPageController> _logger;

    public JobStatusPageController(ILogger<JobStatusPageController> logger)
    {
        _logger = logger;
    }

    [HttpGet("/job-status.html")]
    public IActionResult GetJobStatusPage([FromQuery] string? jobId)
    {
        var html = GenerateJobStatusHtml(jobId ?? "");
        return Content(html, "text/html");
    }

    [HttpGet("/api/logs")]
    public async Task<IActionResult> GetRecentLogs([FromQuery] int lines = 500)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "logs memory-coding-agent --tail " + lines,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return StatusCode(500, new { error = "Failed to start docker logs process" });
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var allLogs = output + errors;
            var logLines = allLogs.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            return Ok(new { logs = logLines, count = logLines.Length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch docker logs");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    
    [HttpGet("/api/todos")]
    public async Task<IActionResult> GetJobTodos([FromQuery] string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest(new { error = "jobId is required" });
        }
        
        try
        {
            // Get job status from orchestrator (which includes todos)
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://localhost:5001/api/orchestrator/status/{jobId}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch job status for {JobId}: {Status}", jobId, response.StatusCode);
                return Ok(new { todos = new List<object>(), message = "Job not found" });
            }
            
            var jobStatus = await response.Content.ReadFromJsonAsync<JobStatusResponse>();
            
            if (jobStatus?.Todos != null && jobStatus.Todos.Count > 0)
            {
                return Ok(new { todos = jobStatus.Todos, count = jobStatus.Todos.Count });
            }
            
            return Ok(new { todos = new List<object>(), message = "Plan not yet created" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch todos for {JobId}", jobId);
            return Ok(new { todos = new List<object>(), error = ex.Message });
        }
    }
    
    private class JobStatusResponse
    {
        public string? JobId { get; set; }
        public string? Status { get; set; }
        public int Progress { get; set; }
        public List<TodoItem>? Todos { get; set; }
    }
    
    private class TodoItem
    {
        public string? Id { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
        public int Complexity { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
    }
    
    private string GenerateJobStatusHtml(string jobId)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Job Status - {jobId}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            padding: 30px;
        }}
        h1 {{
            color: #667eea;
            margin-bottom: 10px;
            font-size: 2.5em;
        }}
        .job-id {{
            font-family: 'Courier New', monospace;
            background: #f0f0f0;
            padding: 10px 15px;
            border-radius: 5px;
            display: inline-block;
            margin: 10px 0 20px 0;
            font-size: 0.9em;
        }}
        .status-card {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 25px;
            border-radius: 10px;
            margin: 20px 0;
        }}
        .status-card h2 {{
            margin-bottom: 15px;
            font-size: 1.8em;
        }}
        .status-item {{
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid rgba(255,255,255,0.2);
        }}
        .status-item:last-child {{ border-bottom: none; }}
        .progress-bar {{
            width: 100%;
            height: 30px;
            background: #f0f0f0;
            border-radius: 15px;
            overflow: hidden;
            margin: 20px 0;
        }}
        .progress-fill {{
            height: 100%;
            background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
            transition: width 0.3s ease;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: bold;
        }}
        .events {{
            background: #f9f9f9;
            padding: 20px;
            border-radius: 10px;
            margin-top: 20px;
            max-height: 500px;
            overflow-y: auto;
        }}
        .event {{
            padding: 10px;
            margin: 5px 0;
            background: white;
            border-left: 4px solid #667eea;
            border-radius: 5px;
            animation: slideIn 0.3s ease;
        }}
        @keyframes slideIn {{
            from {{ opacity: 0; transform: translateX(-20px); }}
            to {{ opacity: 1; transform: translateX(0); }}
        }}
        .timestamp {{
            color: #999;
            font-size: 0.85em;
        }}
        .ws-status {{
            display: inline-block;
            padding: 5px 15px;
            border-radius: 20px;
            font-size: 0.9em;
            font-weight: bold;
            margin-left: 10px;
        }}
        .ws-connected {{ background: #4CAF50; color: white; }}
        .ws-disconnected {{ background: #f44336; color: white; }}
        .ws-connecting {{ background: #FF9800; color: white; }}
        .refresh-notice {{
            background: #fff3cd;
            border: 1px solid #ffc107;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
        }}
        .logs-section {{
            background: #1e1e1e;
            color: #d4d4d4;
            padding: 20px;
            border-radius: 10px;
            margin-top: 20px;
            max-height: 600px;
            overflow-y: auto;
            font-family: 'Courier New', monospace;
            font-size: 0.85em;
        }}
        .logs-section h3 {{
            color: #4ec9b0;
            margin-bottom: 15px;
            font-size: 1.2em;
        }}
        .log-entry {{
            padding: 5px 0;
            border-bottom: 1px solid #333;
            line-height: 1.6;
        }}
        .log-entry:last-child {{ border-bottom: none; }}
        .log-level-info {{ color: #4ec9b0; }}
        .log-level-warn {{ color: #dcdcaa; }}
        .log-level-error {{ color: #f48771; }}
        .log-level-debug {{ color: #9cdcfe; }}
        .log-timestamp {{
            color: #858585;
            margin-right: 10px;
        }}
        .status-completed {{
            background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%) !important;
        }}
        .status-failed {{
            background: linear-gradient(135deg, #f44336 0%, #d32f2f 100%) !important;
        }}
        .status-stopped {{
            background: linear-gradient(135deg, #9E9E9E 0%, #616161 100%) !important;
        }}
        .status-cancelled {{
            background: linear-gradient(135deg, #FF9800 0%, #F57C00 100%) !important;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🚀 Live Job Monitor</h1>
        <div class=""job-id"">Job ID: {jobId}</div>
        
        <div class=""refresh-notice"">
            ⏱️ <strong>Auto-refreshing every 2 seconds</strong> - <span id=""last-update"">Just started...</span>
        </div>

        <div class=""status-card"">
            <h2>📊 Current Status</h2>
            <div class=""status-item"">
                <span><strong>Status:</strong></span>
                <span id=""status"">Loading...</span>
            </div>
            <div class=""status-item"">
                <span><strong>Progress:</strong></span>
                <span id=""progress-text"">0%</span>
            </div>
            <div class=""status-item"">
                <span><strong>Current Phase:</strong></span>
                <span id=""phase"">Initializing...</span>
            </div>
            <div class=""status-item"">
                <span><strong>Workflow Step:</strong></span>
                <span id=""workflow-step"">-</span>
            </div>
            <div class=""status-item"">
                <span><strong>Started:</strong></span>
                <span id=""started"">-</span>
            </div>
            <div class=""status-item"">
                <span><strong>Duration:</strong></span>
                <span id=""duration"">-</span>
            </div>
        </div>

        <div class=""status-card"" style=""margin-top: 20px; background: linear-gradient(135deg, #4CAF50 0%, #45a049 100%);"">
            <h2>🔄 Workflow Pipeline</h2>
            <div id=""workflow-timeline"" style=""color: white; padding: 10px 0;"">
                Loading workflow...
            </div>
        </div>

        <div class=""progress-bar"">
            <div class=""progress-fill"" id=""progress-bar"" style=""width: 0%"">0%</div>
        </div>
        
        <div class=""status-card"" style=""margin-top: 20px; background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%);"" id=""todos-section"">
            <h2>✅ Project Plan & Todos</h2>
            <div id=""todos-list"" style=""color: white; padding: 10px 0;"">
                <div style=""opacity: 0.7;"">Loading project plan...</div>
            </div>
        </div>

        <div class=""events"">
            <h3>📋 Updates</h3>
            <div id=""events-list""></div>
        </div>

        <div class=""logs-section"">
            <h3>🔍 Live CodingAgent Logs <span style=""float: right; font-size: 0.8em; color: #858585;"" id=""log-status"">Connecting...</span></h3>
            <div id=""logs-container"">
                <div class=""log-entry"">
                    <span class=""log-timestamp"">[Initializing...]</span>
                    <span>Waiting for logs...</span>
                </div>
            </div>
        </div>
    </div>

    <script>
        const jobId = '{jobId}';
        let lastStatus = '';
        let startTime = null;

        // Workflow phases mapping (order matters - more specific first!)
        const workflowPhases = [
            {{ name: 'Self-Review', icon: '🔍', keywords: ['self-review', 'reviewing'] }},
            {{ name: 'LLM Thinking', icon: '🤖', keywords: ['calling ollama', 'llm', 'gemma', 'qwen', 'phi4', 'generating', 'deserialized'] }},
            {{ name: 'Validation', icon: '✅', keywords: ['validating', 'validation'] }},
            {{ name: 'Building', icon: '🔨', keywords: ['building', 'compiling', 'dotnet build'] }},
            {{ name: 'Testing', icon: '🧪', keywords: ['testing', 'running tests'] }},
            {{ name: 'Code Generation', icon: '💻', keywords: ['agentic', 'tool-augmented', 'multi-model', 'single-model', 'parsed', 'files from generated'] }},
            {{ name: 'Strategic Planning', icon: '🧠', keywords: ['strategic', 'planning'] }},
            {{ name: 'Duo-Debate', icon: '🤝', keywords: ['duo-debate'] }},
            {{ name: 'Scaffolding', icon: '🏗️', keywords: ['scaffolding'] }},
            {{ name: 'Initialization', icon: '🚀', keywords: ['queued', 'starting', 'initializing', 'started:'] }},
            {{ name: 'Indexing', icon: '📇', keywords: ['indexing'] }},
            {{ name: 'Completed', icon: '🎯', keywords: ['completed', 'success'] }},
            {{ name: 'Stopped', icon: '🛑', keywords: ['stopped', 'interrupted'] }},
            {{ name: 'Cancelled', icon: '⛔', keywords: ['cancelled', 'canceled'] }},
            {{ name: 'Failed', icon: '❌', keywords: ['failed'] }}
        ];
        
        let recentLogs = [];

        function parseStatus(statusStr) {{
            const lower = (statusStr || '').toLowerCase();
            
            // Extract attempt number (e.g. attempt 4/50)
            const attemptMatch = statusStr.match(/attempt (\d+)\/(\d+)/i);
            const attempt = attemptMatch ? 'Attempt ' + attemptMatch[1] + ' of ' + attemptMatch[2] : null;
            
            // First, try to match from status string
            let currentPhase = 'Processing';
            let phaseIcon = '⚙️';
            let matchedKeyword = '';
            
            for (const phase of workflowPhases) {{
                for (const kw of phase.keywords) {{
                    if (lower.includes(kw.toLowerCase())) {{
                        currentPhase = phase.name;
                        phaseIcon = phase.icon;
                        matchedKeyword = kw;
                        break;
                    }}
                }}
                if (matchedKeyword) break;
            }}
            
            // If no match in status, check recent logs for better accuracy
            if (!matchedKeyword && recentLogs.length > 0) {{
                const recentLogsText = recentLogs.slice(0, 5).join(' ').toLowerCase();
                console.log('[Checking Recent Logs]', recentLogsText.substring(0, 200));
                
                for (const phase of workflowPhases) {{
                    for (const kw of phase.keywords) {{
                        if (recentLogsText.includes(kw.toLowerCase())) {{
                            currentPhase = phase.name;
                            phaseIcon = phase.icon;
                            matchedKeyword = kw + ' (from logs)';
                            break;
                        }}
                    }}
                    if (matchedKeyword) break;
                }}
            }}
            
            // Extract specific activity (after the dash)
            const dashIndex = statusStr.indexOf('-');
            const activity = dashIndex > 0 ? statusStr.substring(dashIndex + 1).trim() : '';
            
            // Debug logging
            console.log('[Status Parse]', {{
                raw: statusStr,
                phase: currentPhase,
                icon: phaseIcon,
                keyword: matchedKeyword,
                attempt: attempt,
                activity: activity
            }});
            
            return {{ attempt, currentPhase, phaseIcon, activity, raw: statusStr }};
        }}

        function formatDuration(startDateStr) {{
            if (!startDateStr) return '-';
            const start = new Date(startDateStr);
            const now = new Date();
            const diffMs = now - start;
            
            const hours = Math.floor(diffMs / 3600000);
            const minutes = Math.floor((diffMs % 3600000) / 60000);
            const seconds = Math.floor((diffMs % 60000) / 1000);
            
            if (hours > 0) return hours + 'h ' + minutes + 'm ' + seconds + 's';
            if (minutes > 0) return minutes + 'm ' + seconds + 's';
            return seconds + 's';
        }}

        function updateWorkflowTimeline(statusInfo) {{
            const timeline = document.getElementById('workflow-timeline');
            let html = '<div style=""display: flex; justify-content: space-around; flex-wrap: wrap; gap: 15px;"">';
            
            for (const phase of workflowPhases) {{
                const isCurrent = phase.name === statusInfo.currentPhase;
                const opacity = isCurrent ? '1' : '0.5';
                const border = isCurrent ? '3px solid #FFF' : '2px solid rgba(255,255,255,0.3)';
                const scale = isCurrent ? 'transform: scale(1.1);' : '';
                
                html += '<div style=""' +
                    'opacity: ' + opacity + ';' +
                    'border: ' + border + ';' +
                    'padding: 10px 15px;' +
                    'border-radius: 10px;' +
                    'background: rgba(255,255,255,0.2);' +
                    'text-align: center;' +
                    'min-width: 120px;' +
                    scale +
                    'transition: all 0.3s ease;' +
                    '"">' +
                    '<div style=""font-size: 2em; margin-bottom: 5px;"">' + phase.icon + '</div>' +
                    '<div style=""font-size: 0.85em; font-weight: ' + (isCurrent ? 'bold' : 'normal') + ';"">' + phase.name + '</div>' +
                    '</div>';
            }}
            
            html += '</div>';
            timeline.innerHTML = html;
        }}

        async function updateStatus() {{
            try {{
                const response = await fetch('http://localhost:5001/api/orchestrator/status/' + jobId);
                
                if (!response.ok) {{
                    console.error('[Status Fetch] HTTP error:', response.status, response.statusText);
                    throw new Error('HTTP ' + response.status + ': ' + response.statusText);
                }}
                
                const data = await response.json();
                console.log('[Status Data]', {{ status: data.status, progress: data.progress }});

                if (!startTime && data.startedAt) {{
                    startTime = data.startedAt;
                }}

                const statusInfo = parseStatus(data.status);
                const statusLower = (data.status || '').toLowerCase();
                
                // Update main status
                document.getElementById('status').textContent = statusInfo.raw || 'Unknown';
                document.getElementById('progress-text').textContent = (data.progress || 0) + '%';
                document.getElementById('phase').innerHTML = statusInfo.phaseIcon + ' ' + statusInfo.currentPhase;
                document.getElementById('workflow-step').textContent = statusInfo.attempt || statusInfo.activity || '-';
                document.getElementById('started').textContent = data.startedAt ? new Date(data.startedAt).toLocaleString() : '-';
                document.getElementById('duration').textContent = formatDuration(startTime);

                // Update progress bar
                const progressBar = document.getElementById('progress-bar');
                const progress = data.progress || 0;
                progressBar.style.width = progress + '%';
                progressBar.textContent = progress + '%';

                // Update workflow timeline
                updateWorkflowTimeline(statusInfo);

                // Update status card styling based on terminal state
                const statusCards = document.querySelectorAll('.status-card');
                if (statusCards.length > 0) {{
                    const mainCard = statusCards[0];
                    mainCard.classList.remove('status-completed', 'status-failed', 'status-cancelled', 'status-stopped');
                    
                    if (data.status === 'completed') {{
                        mainCard.classList.add('status-completed');
                    }} else if (data.status === 'failed' || statusLower.includes('error')) {{
                        mainCard.classList.add('status-failed');
                    }} else if (statusLower.includes('stopped') || statusLower.includes('interrupted')) {{
                        mainCard.classList.add('status-stopped');
                    }} else if (statusLower.includes('cancelled') || statusLower.includes('canceled')) {{
                        mainCard.classList.add('status-cancelled');
                    }}
                }}

                // Update last refresh time
                document.getElementById('last-update').textContent = 'Last updated: ' + new Date().toLocaleTimeString();

                // Add event if status changed
                if (data.status !== lastStatus) {{
                    addEvent(statusInfo.phaseIcon + ' ' + statusInfo.currentPhase + ': ' + (statusInfo.activity || statusInfo.attempt || 'Processing'), progress);
                    lastStatus = data.status;
                }}

                // Check if job is in a terminal state
                const isTerminal = data.status === 'completed' || 
                                 data.status === 'failed' || 
                                 statusLower.includes('cancelled') ||
                                 statusLower.includes('canceled') ||
                                 statusLower.includes('stopped') ||
                                 statusLower.includes('interrupted');
                
                if (isTerminal) {{
                    console.log('Job finished with status: ' + data.status);
                    
                    if (data.status === 'completed' && data.result && data.result.files) {{
                        addEvent('✅ Job Completed - Generated ' + data.result.files.length + ' files', 100);
                    }} else if (data.status === 'failed' || statusLower.includes('error')) {{
                        addEvent('❌ Job Failed - See logs for details', data.progress || 0);
                    }} else if (statusLower.includes('stopped') || statusLower.includes('interrupted')) {{
                        addEvent('🛑 Job Stopped - Interrupted by container restart', data.progress || 0);
                    }} else if (statusLower.includes('cancelled') || statusLower.includes('canceled')) {{
                        addEvent('⛔ Job Cancelled - Stopped by user', data.progress || 0);
                    }}
                    
                    document.getElementById('last-update').textContent = 'Final status at ' + new Date().toLocaleTimeString();
                    return; // Don't schedule next update
                }}

                // Schedule next update (faster polling - 2 seconds)
                setTimeout(updateStatus, 2000);
            }} catch (err) {{
                console.error('[Status Fetch ERROR]', {{
                    error: err,
                    message: err.message,
                    stack: err.stack,
                    jobId: jobId,
                    url: 'http://localhost:5001/api/orchestrator/status/' + jobId
                }});
                
                // Don't overwrite status if we already have one
                const currentStatus = document.getElementById('status').textContent;
                if (!currentStatus || currentStatus === 'Loading...') {{
                    document.getElementById('status').textContent = 'Connection issue - retrying...';
                }}
                
                const errorMsg = err.message || 'Unknown error';
                document.getElementById('last-update').textContent = '⚠️ Error: ' + errorMsg + ' (retrying...)';
                addEvent('⚠️ Connection error: ' + errorMsg, 0);
                setTimeout(updateStatus, 3000); // Retry after 3 seconds on error
            }}
        }}

        function addEvent(message, progress) {{
            const eventsList = document.getElementById('events-list');
            const event = document.createElement('div');
            event.className = 'event';
            event.innerHTML = '<div class=""timestamp"">' + new Date().toLocaleTimeString() + '</div>' +
                '<div>' + message + ' (Progress: ' + progress + '%)</div>';
            eventsList.insertBefore(event, eventsList.firstChild);
            
            // Keep only last 20 events
            while (eventsList.children.length > 20) {{
                eventsList.removeChild(eventsList.lastChild);
            }}
        }}

        async function updateTodos() {{
            try {{
                const response = await fetch('http://localhost:5001/api/todos?jobId=' + jobId);
                const data = await response.json();
                
                if (data.todos && data.todos.length > 0) {{
                    const todosContainer = document.getElementById('todos-list');
                    let html = '<div style=""display: flex; flex-direction: column; gap: 10px;"">';
                    
                    data.todos.forEach((todo, idx) => {{
                        let icon = '⏳';
                        let opacity = '0.6';
                        let checkmark = '';
                        
                        if (todo.status === 'completed') {{
                            icon = '✅';
                            opacity = '1';
                            checkmark = '<span style=""color: #4CAF50; font-weight: bold;"">✓</span> ';
                        }} else if (todo.status === 'in_progress') {{
                            icon = '🔄';
                            opacity = '1';
                            checkmark = '<span style=""color: #FF9800; font-weight: bold;"">➤</span> ';
                        }}
                        
                        html += '<div style=""' +
                            'padding: 12px 15px;' +
                            'background: rgba(255,255,255,0.2);' +
                            'border-radius: 8px;' +
                            'opacity: ' + opacity + ';' +
                            'border-left: 4px solid ' + (todo.status === 'completed' ? '#4CAF50' : todo.status === 'in_progress' ? '#FF9800' : '#FFF') + ';' +
                            '"">' +
                            '<span style=""font-size: 1.2em; margin-right: 10px;"">' + icon + '</span>' +
                            checkmark +
                            '<span style=""font-weight: ' + (todo.status === 'in_progress' ? 'bold' : 'normal') + ';"">' + todo.description + '</span>' +
                            '</div>';
                    }});
                    
                    html += '</div>';
                    todosContainer.innerHTML = html;
                }} else {{
                    const todosContainer = document.getElementById('todos-list');
                    todosContainer.innerHTML = '<div style=""opacity: 0.7;"">No plan available yet (plan created after job starts)</div>';
                }}
            }} catch (err) {{
                console.error('Failed to fetch todos:', err);
                const todosContainer = document.getElementById('todos-list');
                todosContainer.innerHTML = '<div style=""opacity: 0.7;"">Error loading todos</div>';
            }}
        }}
        
        async function updateLogs() {{
            try {{
                const response = await fetch('http://localhost:5001/api/logs?lines=500');
                const data = await response.json();
                
                if (data.logs && data.logs.length > 0) {{
                    // Update recent logs for phase detection
                    recentLogs = data.logs.slice(0, 50); // Keep last 50 logs for phase detection
                    
                    const logsContainer = document.getElementById('logs-container');
                    logsContainer.innerHTML = '';
                    
                    // Parse and display logs
                    data.logs.forEach(logLine => {{
                        if (!logLine.trim()) return;
                        
                        const entry = document.createElement('div');
                        entry.className = 'log-entry';
                        
                        // Determine log level
                        let levelClass = 'log-level-info';
                        if (logLine.toLowerCase().includes('error') || logLine.toLowerCase().includes('fail')) {{
                            levelClass = 'log-level-error';
                        }} else if (logLine.toLowerCase().includes('warn')) {{
                            levelClass = 'log-level-warn';
                        }} else if (logLine.toLowerCase().includes('debug')) {{
                            levelClass = 'log-level-debug';
                        }}
                        
                        // Extract timestamp if present
                        const timestampMatch = logLine.match(/\d{{4}}-\d{{2}}-\d{{2}}T\d{{2}}:\d{{2}}:\d{{2}}/);
                        let timestamp = '';
                        let message = logLine;
                        
                        if (timestampMatch) {{
                            timestamp = timestampMatch[0].substring(11); // Just time
                            message = logLine.substring(timestampMatch.index + timestampMatch[0].length);
                        }}
                        
                        // Truncate very long messages
                        if (message.length > 200) {{
                            message = message.substring(0, 200) + '...';
                        }}
                        
                        entry.innerHTML = '<span class=""log-timestamp"">[' + (timestamp || 'N/A') + ']</span>' +
                            '<span class=""' + levelClass + '"">' + message.trim() + '</span>';
                        
                        logsContainer.appendChild(entry);
                    }});
                    
                    document.getElementById('log-status').textContent = 'Updated ' + new Date().toLocaleTimeString();
                    
                    // Auto-scroll to bottom
                    logsContainer.scrollTop = logsContainer.scrollHeight;
                }} else {{
                    document.getElementById('log-status').textContent = 'No logs available';
                }}
                
                // Schedule next log update (every 2 seconds for better phase tracking)
                setTimeout(updateLogs, 2000);
            }} catch (err) {{
                console.error('Failed to fetch logs:', err);
                document.getElementById('log-status').textContent = 'Error fetching logs';
                setTimeout(updateLogs, 5000); // Retry after 5 seconds on error
            }}
        }}

        // Start monitoring - fetch logs first for better phase detection
        async function startMonitoring() {{
            await updateTodos(); // Fetch todos/plan first
            await updateLogs(); // Fetch logs
            await updateStatus(); // Then status (uses logs for phase detection)
            addEvent('🔍 Monitoring started', 0);
            
            // Update todos periodically (every 5 seconds - less frequent than logs/status)
            setInterval(updateTodos, 5000);
        }}
        
        startMonitoring();
    </script>
</body>
</html>";
    }
}

