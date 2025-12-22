# Job Status Extension - User Guide

## 🎯 Quick Start (2 Minutes)

### Step 1: Activate Extension
```
Ctrl+Shift+P → "Developer: Reload Window"
```

### Step 2: Check Status Bar
Look at bottom-left corner:
```
💤 No active jobs
```

### Step 3: Start a Job
Use the `orchestrate_task` MCP tool in chat:
```
orchestrate_task(task: "Create a Calculator class", language: "csharp")
```

### Step 4: Watch It Update!
```
🔄 Create a Calculator... (0%) | ⏱️ 2s
🔄 Create a Calculator... (30%) | ⏱️ 45s
🔄 Create a Calculator... (60%) | ⏱️ 1m 32s
✅ Create a Calculator complete! 🎉
```

---

## 📊 Status Bar States

### No Jobs
```
💤 No active jobs
```
*Tooltip: No code generation jobs running*

### Single Job Running
```
🔄 UserService (60%) | ⏱️ 2m 15s
```
*Updates every 3 seconds*

### Multiple Jobs
```
🔄 2 jobs | UserService (60%)
```
*Click to see all jobs*

### Job Complete
```
✅ UserService complete! (Click to review) 🎉
```
*Shows for 10 seconds, then returns to idle*

### Job Failed
```
❌ UserService failed (Score: 6/10) - Click to retry
```
*Red background, click for details*

---

## 🖱️ Interactive Features

### Click Status Bar
Opens detailed job picker:
```
┌─────────────────────────────────────────────┐
│ Select a job to view details:              │
├─────────────────────────────────────────────┤
│ 🔄 UserService                              │
│    60% | 2m 15s                             │
│    running | validation_agent | Score: 7/10│
├─────────────────────────────────────────────┤
│ 🔄 Calculator                               │
│    30% | 45s                                │
│    running | coding_agent | Score: N/A     │
└─────────────────────────────────────────────┘
```

Select a job → Opens detailed panel with:
- Full job information
- Progress bar
- Iteration count
- Validation score
- Timeline
- Quick actions

### Hover Status Bar
Shows tooltip:
```
┌─────────────────────────────────────┐
│ Job: UserService.cs                 │
│ Status: running                     │
│ Progress: 60%                       │
│ Phase: validation_agent             │
│ Iteration: 3/5                      │
│ Score: 7/10                         │
│ Duration: 2m 15s                    │
│                                     │
│ 💡 Click for details                │
└─────────────────────────────────────┘
```

### Right-Click Status Bar
(Future feature - not yet implemented)
```
┌─────────────────────────────┐
│ 📋 Copy Job ID              │
│ 📄 View Generated Files     │
│ 🔍 Show Detailed Logs       │
│ ❌ Cancel Job               │
│ 🔄 Retry Job                │
└─────────────────────────────┘
```

---

## 🔔 Desktop Notifications

### Job Complete
```
┌────────────────────────────────────────────┐
│  Cursor - Code Generation Complete  ✅     │
├────────────────────────────────────────────┤
│  UserService.cs generated successfully!    │
│  Score: 9/10 | Files: 3                   │
│                                            │
│  [View Details]  [Dismiss]                 │
└────────────────────────────────────────────┘
```

### Job Failed
```
┌────────────────────────────────────────────┐
│  Cursor - Code Generation Failed  ❌       │
├────────────────────────────────────────────┤
│  UserService.cs validation failed          │
│  Score: 6/10 | Issues: Missing tests      │
│                                            │
│  [View Errors]  [Retry]  [Dismiss]         │
└────────────────────────────────────────────┘
```

---

## ⚙️ Configuration

### Open Settings
```
File → Preferences → Settings → Search "Job Status"
```

### Available Settings

#### Polling Interval
```json
"jobStatus.pollingInterval": 3000
```
- Default: 3000ms (3 seconds)
- Range: 1000-10000ms
- Lower = More responsive, higher CPU
- Higher = Less responsive, lower CPU

#### Orchestrator URL
```json
"jobStatus.orchestratorUrl": "http://localhost:5001"
```
Change if running on different port/host

#### Memory Router URL
```json
"jobStatus.memoryRouterUrl": "http://localhost:5010"
```
Optional - for workflow tracking

#### Show Notifications
```json
"jobStatus.showNotifications": true
```
Enable/disable desktop notifications

### Example settings.json
```json
{
  "jobStatus.pollingInterval": 5000,
  "jobStatus.orchestratorUrl": "http://localhost:5001",
  "jobStatus.memoryRouterUrl": "http://localhost:5010",
  "jobStatus.showNotifications": true
}
```

---

## 🎮 Commands

### Show Job Details
```
Ctrl+Shift+P → "Show Job Details"
```
Opens job picker (same as clicking status bar)

### Refresh Job Status
```
Ctrl+Shift+P → "Refresh Job Status"
```
Force immediate refresh (normally polls every 3s)

### Cancel Job
```
Ctrl+Shift+P → "Cancel Job"
```
Shows list of running jobs to cancel

---

## 🐛 Troubleshooting

### Status Bar Shows "No Jobs" But Jobs Are Running

**Check Services:**
```powershell
curl http://localhost:5001/health
curl http://localhost:5010/health
```

**Check Extension Logs:**
1. `View` → `Output`
2. Select "Cursor Job Status" from dropdown
3. Look for errors

**Verify Settings:**
```
Settings → Extensions → Job Status
```
Make sure URLs are correct

### Extension Not Loading

**Reload Window:**
```
Ctrl+Shift+P → "Developer: Reload Window"
```

**Check Installation:**
```powershell
Test-Path "$env:USERPROFILE\.cursor\extensions\cursor-job-status-1.0.0"
```

**Reinstall:**
```powershell
.\install-job-status-extension.ps1
```

### Notifications Not Showing

**Enable in Extension:**
```
Settings → Extensions → Job Status
jobStatus.showNotifications = true
```

**Check Windows Settings:**
```
Settings → System → Notifications
Make sure notifications are enabled for Cursor
```

### Status Bar Not Updating

**Check Polling Interval:**
```
Settings → Extensions → Job Status
jobStatus.pollingInterval (default: 3000ms)
```

**Force Refresh:**
```
Ctrl+Shift+P → "Refresh Job Status"
```

**Check Network:**
```powershell
curl http://localhost:5001/api/orchestrator/list
```

---

## 💡 Pro Tips

### Tip 1: Multiple Monitors
Move detailed job panel to second monitor while working

### Tip 2: Keyboard Shortcut
Create custom keybinding for "Show Job Details":
```json
{
  "key": "ctrl+shift+j",
  "command": "jobStatus.showDetails"
}
```

### Tip 3: Reduce Noise
Disable notifications for completed jobs, keep for failures:
```
(Feature coming soon)
```

### Tip 4: Check Logs
Extension logs all activity to Output panel - great for debugging

### Tip 5: Remote Development
Update URLs in settings if running services remotely:
```json
{
  "jobStatus.orchestratorUrl": "http://remote-server:5001"
}
```

---

## 🔥 Power User Features

### Track Multiple Workspaces
Extension works per-window, so you can track different projects simultaneously

### Custom Polling Strategies
- **Fast (1s)**: Development/testing
- **Normal (3s)**: Daily use
- **Slow (10s)**: Battery saving

### Integration with MCP
Extension reads same endpoints as MCP tools:
- `list_tasks` → `/api/orchestrator/list`
- `get_task_status` → `/api/orchestrator/status/{id}`

So you can use both interchangeably!

---

## 📈 What's Next?

Future enhancements we're planning:
- [ ] Right-click context menu
- [ ] Retry failed jobs
- [ ] Export job logs
- [ ] Custom notification rules
- [ ] Job history view
- [ ] Keyboard shortcuts
- [ ] Integration with Git commits

---

## 🆘 Support

**Found a bug?**
Create an issue in the repo with:
- Extension version
- Cursor version
- Extension logs (View → Output → Cursor Job Status)
- Steps to reproduce

**Need help?**
Check `.cursor-extensions/job-status/README.md`

---

## 🎓 How It Works Internally

```
┌─────────────────────────────────────────────┐
│  Extension Activation                       │
│  (onStartupFinished)                        │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│  JobPoller                                  │
│  - Polls /api/orchestrator/list (3s)       │
│  - Polls /api/workflows/list (3s)          │
└────────────────┬────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────┐
│  StatusBarManager                           │
│  - Updates status bar text                 │
│  - Shows notifications                      │
│  - Manages detailed views                   │
└─────────────────────────────────────────────┘
```

**Why every 3 seconds?**
- Fast enough to feel responsive
- Slow enough not to hammer the server
- Configurable if you want different

**Why HTTP polling vs WebSocket?**
- Simpler implementation
- Works with existing REST API
- No server changes needed
- Configurable interval

Future: We may add WebSocket support for instant updates!

---

**Enjoy your real-time job monitoring! 🚀**



