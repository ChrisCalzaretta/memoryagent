# Auto-Reindex Feature

## Overview

The Memory Agent now includes an **automatic file watcher** that monitors your project for code changes and automatically triggers smart reindexing. When you save a file, the system detects the change and updates the vector database (Qdrant) and knowledge graph (Neo4j) automatically.

## How It Works

```
File Change Detected
├── Debounce (wait 3 seconds for batched changes)
├── Trigger Smart Reindex
│   ├── Compare changed files vs database
│   ├── Delete old vectors & nodes
│   ├── Parse updated code
│   ├── Generate embeddings (Ollama)
│   └── Store in Qdrant + Neo4j
└── Log completion
```

## Configuration

### Per-Project Settings

Auto-reindex is configured **per project** when you start it:

```powershell
.\start-project.ps1 -projectName myproject -projectPath "E:\GitHub\MyProject"
```

The auto-reindex service will:
- **Watch**: Only the specific project path (e.g., `/workspace/MyProject`)
- **Context**: Use the project's context name for isolation
- **Debounce**: Wait 3 seconds after file changes before reindexing

### Environment Variables (docker-compose)

```yaml
environment:
  - AutoReindex__Enabled=true                    # Enable/disable auto-reindex
  - AutoReindex__WorkspacePath=/workspace/MyProject  # Project-specific path
  - AutoReindex__Context=MyProject               # Project context name
```

### Application Settings

**appsettings.Development.json** (enabled by default in dev):
```json
{
  "AutoReindex": {
    "Enabled": true,
    "WorkspacePath": "/workspace",
    "Context": "default"
  }
}
```

**appsettings.json** (disabled by default in production):
```json
{
  "AutoReindex": {
    "Enabled": false,
    "WorkspacePath": "/workspace",
    "Context": "default"
  }
}
```

## Monitored File Types

The watcher monitors these file extensions:
- **C#**: `*.cs`, `*.cshtml`, `*.razor`
- **VB.NET**: `*.vb`
- **Python**: `*.py`
- **Markdown**: `*.md`
- **Styles**: `*.css`, `*.scss`, `*.less`
- **JavaScript/TypeScript**: `*.js`, `*.jsx`, `*.ts`, `*.tsx`

## Features

### ✅ What's Included

- **Debouncing**: Waits 3 seconds after changes to batch multiple file saves
- **Parallel Processing**: Processes up to 8 files simultaneously
- **Smart Reindex**: Only reindexes changed/new/deleted files
- **Per-Project Isolation**: Each project watches only its own path
- **Automatic Cleanup**: Removes stale data for deleted files
- **Error Handling**: Continues watching even if reindex fails
- **Detailed Logging**: Shows what files changed and reindex results

### ❌ What's NOT Included

- **Smart filtering** of `.git`, `node_modules`, `bin`, `obj` - These are handled by the reindex service itself
- **Pattern-based exclusions** - The file watcher watches everything; filtering happens during indexing

## Logs

The auto-reindex service provides detailed logging:

```
🔍 Auto-reindex ENABLED
   Watching: /workspace/MyProject
   Context: MyProject
   Debounce: 3 seconds

File change detected: Changed - /workspace/MyProject/Services/UserService.cs
🔄 Auto-reindex triggered for 1 file(s)
✅ Auto-reindex completed: +0 -0 ~1 files in 2.3s
```

## Disabling Auto-Reindex

### Per Project

Set environment variable before starting:
```powershell
$env:AUTO_REINDEX_ENABLED = "false"
.\start-project.ps1 -projectName myproject -projectPath "E:\GitHub\MyProject"
```

### Globally

Edit `appsettings.Development.json`:
```json
{
  "AutoReindex": {
    "Enabled": false
  }
}
```

## Performance Impact

### CPU Usage
- **Idle**: Minimal (<1% CPU)
- **File Change**: Spikes during reindex (uses up to 8 cores in parallel)
- **Debouncing**: Prevents constant reindexing on rapid changes

### Memory Usage
- **File Watcher**: ~10-20 MB
- **Reindex Process**: Depends on file size (typically 100-500 MB during active reindex)

## Troubleshooting

### Auto-reindex not triggering

1. Check if enabled:
   ```bash
   docker logs memory-agent-server | grep "Auto-reindex"
   ```

2. Verify environment variables:
   ```bash
   docker exec memory-agent-server printenv | grep AutoReindex
   ```

3. Check file watcher is active:
   ```bash
   docker logs memory-agent-server | grep "File watcher started"
   ```

### Too many reindexes

- Increase debounce delay in `AutoReindexService.cs`:
  ```csharp
  private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(5); // Increase from 3 to 5
  ```

### Missing file changes

- Verify the file type is monitored (see "Monitored File Types" above)
- Check file path is within the watched workspace
- Review logs for errors

## Architecture

### Components

1. **AutoReindexService** (`FileWatcher/AutoReindexService.cs`)
   - Background hosted service
   - Monitors file system changes
   - Implements debouncing logic
   - Triggers reindex operations

2. **FileSystemWatcher** (.NET built-in)
   - Watches for file changes
   - Raises events for Created/Changed/Deleted/Renamed

3. **ReindexService** (`Services/ReindexService.cs`)
   - Smart reindex logic
   - Parallel file processing
   - Database cleanup

### Flow Diagram

```
┌─────────────────────────┐
│  File System Change     │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  FileSystemWatcher      │
│  (per file pattern)     │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  AutoReindexService     │
│  - Add to pending queue │
│  - Track timestamp      │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Debounce Timer         │
│  (check every 1 second) │
└───────────┬─────────────┘
            │
            ▼ (after 3s delay)
┌─────────────────────────┐
│  Trigger Smart Reindex  │
│  - ReindexService       │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Parallel Processing    │
│  (8 files at once)      │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Update Qdrant + Neo4j  │
└─────────────────────────┘
```

## Best Practices

1. **Development**: Keep auto-reindex **enabled** for immediate feedback
2. **Large Changes**: Consider manual reindex for bulk operations (faster)
3. **Testing**: Disable auto-reindex to control timing
4. **Production**: Disable auto-reindex (use manual triggers)

## Manual Reindex

If you prefer manual control or need to force a full reindex:

```powershell
# Using REST API
$body = @{
    context = "MyProject"
    path = "E:\GitHub\MyProject"
    removeStale = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5098/api/index/reindex" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

## Future Enhancements

Potential improvements:
- [ ] Configurable debounce delay per project
- [ ] File pattern exclusions (regex-based)
- [ ] Pause/Resume auto-reindex via API
- [ ] Real-time status updates (SignalR)
- [ ] Reindex queue viewer
- [ ] Performance metrics dashboard

