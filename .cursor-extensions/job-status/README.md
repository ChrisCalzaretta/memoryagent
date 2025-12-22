# Cursor Job Status Extension

Real-time status bar extension for monitoring code generation jobs in Cursor.

## Features

- **Real-time status bar** showing active jobs with progress
- **Desktop notifications** when jobs complete or fail
- **Detailed job view** with progress, iterations, and validation scores
- **Multiple job tracking** - monitors both CodingOrchestrator and MemoryRouter
- **Quick actions** - View details, cancel jobs, refresh status

## Status Bar Display

### Single Job
```
🔄 UserService (60%) | ⏱️ 2m 15s
```

### Multiple Jobs
```
🔄 2 jobs | UserService (60%)
```

### No Jobs
```
💤 No active jobs
```

## Commands

- **Show Job Details** - Click status bar or `Ctrl+Shift+P` → "Show Job Details"
- **Refresh Job Status** - Force refresh current jobs
- **Cancel Job** - Cancel a running job

## Configuration

Access settings via `File` → `Preferences` → `Settings` → Search for "Job Status":

| Setting | Default | Description |
|---------|---------|-------------|
| `jobStatus.pollingInterval` | 3000 | Polling interval in milliseconds |
| `jobStatus.orchestratorUrl` | http://localhost:5001 | CodingOrchestrator URL |
| `jobStatus.memoryRouterUrl` | http://localhost:5010 | MemoryRouter URL |
| `jobStatus.showNotifications` | true | Show desktop notifications |

## Installation

### Option 1: Install Script (Recommended)
```powershell
.\install-job-status-extension.ps1
```

### Option 2: Manual Installation
```powershell
# Copy extension to Cursor's extensions directory
cp -r .cursor-extensions/job-status "$env:USERPROFILE\.cursor\extensions\job-status"

# Reload Cursor
```

## Requirements

- Node.js (v14 or higher)
- CodingOrchestrator running on port 5001
- MemoryRouter running on port 5010 (optional)

## How It Works

1. Extension polls job endpoints every 3 seconds (configurable)
2. Updates status bar with current job progress
3. Shows notifications when jobs complete/fail
4. Click status bar to see detailed view of all jobs

## Troubleshooting

**Status bar shows no jobs but jobs are running:**
- Check if services are running: `curl http://localhost:5001/health`
- Verify URLs in settings
- Check extension output: `View` → `Output` → Select "Cursor Job Status"

**Extension not loading:**
- Restart Cursor: `Ctrl+Shift+P` → "Developer: Reload Window"
- Check if extension is installed: `C:\Users\[USERNAME]\.cursor\extensions\job-status`

**Notifications not showing:**
- Enable in settings: `jobStatus.showNotifications = true`
- Check Windows notification settings

## Version History

### 1.0.0 (2024-12-21)
- Initial release
- Real-time status bar
- Desktop notifications
- Multi-job tracking
- Detailed job view
- Cancel job support



