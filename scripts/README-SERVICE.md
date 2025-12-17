# Ollama Dual GPU Windows Service

This service runs two Ollama instances on separate GPUs in the background, starting automatically with Windows.

## Features

- ✅ **Automatic Startup**: Starts when Windows boots
- ✅ **Background Process**: No console windows
- ✅ **Auto-Restart**: Automatically restarts on failure
- ✅ **Logging**: Detailed logs at `C:\Logs\OllamaDualGPU`
- ✅ **Dual GPU**: GPU 0 (3090) for pinned models, GPU 1 (5070 Ti) for swap models

## GPU Configuration

### GPU 0 (RTX 3090 - 24GB) - Port 11434
**PINNED** - Models stay in VRAM forever for instant inference

- `mxbai-embed-large:latest` (~0.7GB) - Embeddings
- `phi4:latest` (~2.5GB) - Fast validation
- `deepseek-coder-v2:16b` (~11GB) - Code generation
- **Total: ~14.2GB VRAM**

### GPU 1 (RTX 5070 Ti - 16GB) - Port 11435
**SWAP** - Models auto-unload after 5 minutes idle

- Available for: `qwen2.5-coder:14b`, `codellama:13b`, etc.

## Installation

### Prerequisites

1. **Administrator Access**: Required to install Windows services
2. **Ollama Installed**: Must be at `C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe`
3. **PowerShell**: PowerShell 5.1+ or PowerShell Core 7+

### Install Steps

1. **Open PowerShell as Administrator**
   ```powershell
   # Right-click PowerShell → Run as Administrator
   ```

2. **Navigate to scripts directory**
   ```powershell
   cd E:\GitHub\MemoryAgent\scripts
   ```

3. **Run installer**
   ```powershell
   .\install-ollama-service.ps1
   ```

4. **Follow prompts**
   - The installer will download NSSM if needed
   - Choose 'y' to start the service immediately
   - Wait ~30 seconds for models to load

### What Gets Installed

- **Service Name**: `OllamaDualGPU`
- **Display Name**: "Ollama Dual GPU Service"
- **Startup Type**: Automatic (starts with Windows)
- **Account**: LocalSystem
- **NSSM**: Installed to `C:\Tools\nssm\` (if not already present)
- **Logs**: `C:\Logs\OllamaDualGPU\`

## Usage

### Service Management

```powershell
# Check service status
Get-Service -Name OllamaDualGPU

# Start service
Start-Service -Name OllamaDualGPU

# Stop service
Stop-Service -Name OllamaDualGPU

# Restart service
Restart-Service -Name OllamaDualGPU

# View logs (live tail)
Get-Content C:\Logs\OllamaDualGPU\service-*.log -Tail 50 -Wait
```

### API Endpoints

Once the service is running:

```powershell
# Primary GPU (3090) - Use this for all production requests
http://localhost:11434/api/generate
http://localhost:11434/api/embeddings

# Swap GPU (5070 Ti) - For alternative models
http://localhost:11435/api/generate
```

### Test the Service

```powershell
# Test GPU 0 (3090)
Invoke-RestMethod -Uri "http://localhost:11434/api/tags"

# Test GPU 1 (5070 Ti)
Invoke-RestMethod -Uri "http://localhost:11435/api/tags"

# Check what's loaded in VRAM
Invoke-RestMethod -Uri "http://localhost:11434/api/ps"
```

## Uninstallation

```powershell
# Run as Administrator
cd E:\GitHub\MemoryAgent\scripts
.\uninstall-ollama-service.ps1
```

Options during uninstall:
- Keep or delete log files
- Keep or delete NSSM

## Troubleshooting

### Service Won't Start

1. **Check logs**:
   ```powershell
   Get-Content C:\Logs\OllamaDualGPU\service-*.log -Tail 50
   Get-Content C:\Logs\OllamaDualGPU\stderr.log -Tail 50
   ```

2. **Verify Ollama path**:
   ```powershell
   Test-Path "C:\Users\chris\AppData\Local\Programs\Ollama\ollama.exe"
   ```

3. **Check GPU availability**:
   ```powershell
   nvidia-smi
   ```

4. **Verify ports are free**:
   ```powershell
   netstat -ano | findstr "11434"
   netstat -ano | findstr "11435"
   ```

### Models Not Loading

1. **Check if models are installed**:
   ```powershell
   # Test from command line first
   ollama list
   ```

2. **Pull missing models**:
   ```powershell
   ollama pull deepseek-coder-v2:16b
   ollama pull phi4:latest
   ollama pull mxbai-embed-large:latest
   ```

3. **Check VRAM usage**:
   ```powershell
   nvidia-smi
   ```

### Service Keeps Restarting

The service will automatically restart up to 5 times within a 5-minute window. Check logs for errors:

```powershell
Get-Content C:\Logs\OllamaDualGPU\service-*.log | Select-String "ERROR"
```

Common causes:
- Insufficient VRAM
- Model not installed
- Port already in use
- GPU driver issues

### High Memory/CPU Usage

This is normal during model loading. After ~2 minutes:
- **GPU 0 VRAM**: ~14-15GB used
- **GPU 1 VRAM**: Minimal (idle)
- **CPU**: Low (<10%)
- **System RAM**: ~500MB per Ollama instance

## Advanced Configuration

### Change Default Models

Edit `ollama-dual-gpu-service.ps1`:

```powershell
$PrimaryModel = "your-model:tag"
$ValidationModel = "your-validation-model:tag"
$EmbeddingModel = "your-embedding-model:tag"
```

Then reinstall the service:
```powershell
.\uninstall-ollama-service.ps1
.\install-ollama-service.ps1
```

### Change Ports

Edit `ollama-dual-gpu-service.ps1`:

```powershell
$gpu0Port = 11434  # Change to your preferred port
$gpu1Port = 11435  # Change to your preferred port
```

### Adjust Keep-Alive Times

Edit environment variables in `ollama-dual-gpu-service.ps1`:

```powershell
# GPU 0 - PINNED (never unload)
$startInfo0.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "-1"

# GPU 1 - SWAP (unload after 5 minutes)
$startInfo1.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "300"  # seconds
```

### Enable Verbose Logging

The service logs to:
- `C:\Logs\OllamaDualGPU\service-YYYY-MM-DD.log` - Main service log
- `C:\Logs\OllamaDualGPU\stdout.log` - Standard output
- `C:\Logs\OllamaDualGPU\stderr.log` - Error output

Logs rotate automatically at 10MB.

## Integration with Docker

Update your `docker-compose.yml`:

```yaml
services:
  mcp-server:
    environment:
      - Ollama__Url=http://10.0.0.20:11434  # Your host IP
      - Gpu__DualGpu=true
      - Gpu__PinnedPort=11434
      - Gpu__SwapPort=11435
```

## Performance Tips

1. **First request after boot takes longer** (~30 seconds for model loading)
2. **Subsequent requests are instant** (pinned models in VRAM)
3. **GPU 1 will unload after 5 minutes idle** - first request takes longer
4. **Restart the service after Windows updates** to ensure GPU drivers are fresh

## Security Notes

- Service runs as **LocalSystem** (high privileges)
- Ollama binds to `0.0.0.0` (accessible from network)
- Consider firewall rules if exposing to network
- No authentication by default - use reverse proxy if needed

## Comparison with Manual Start

| Feature | Manual Script | Windows Service |
|---------|--------------|-----------------|
| Auto-start on boot | ❌ No | ✅ Yes |
| Console window | ✅ Visible | ❌ Hidden |
| Survives logout | ❌ No | ✅ Yes |
| Auto-restart on crash | ❌ No | ✅ Yes (5 attempts) |
| Windows integration | ❌ No | ✅ Full |
| Log rotation | ❌ Manual | ✅ Automatic |

## License

MIT License - Same as parent project

