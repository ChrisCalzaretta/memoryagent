# OLD Multi-Agent System - ARCHIVED 2025-12-20

**Archived:** December 20, 2025 10:30 PM

## What Was Archived

This directory contains the **OLD** multi-agent code generation system that has been **REPLACED** by the new `CodingAgent.Server` with `ProjectOrchestrator`.

### Archived Components:

1. **CodingOrchestrator.Server/** - OLD orchestrator that managed multi-agent workflow
2. **CodingOrchestrator.Server.Tests/** - Tests for old orchestrator
3. **ValidationAgent.Server/** - OLD standalone validation agent
4. **ValidationAgent.Server.Tests/** - Tests for old validation agent

## Why Archived

### Problems with OLD System:
- ❌ Job persistence broken (`/data/jobs` not mounted)
- ❌ MaxSameErrors too low (3 instead of 10)
- ❌ No smart escalation strategy (Deepseek → Claude → back to Deepseek)
- ❌ Separate ValidationAgent.Server (added HTTP overhead)
- ❌ Complex 3-tier architecture (Orchestrator → CodingAgent → ValidationAgent)
- ❌ Stopped at iteration 5 even though maxIterations was 50

### NEW System Advantages:
- ✅ Template-based instant scaffolding (C#, Flutter, etc.)
- ✅ Self-contained in `CodingAgent.Server`
- ✅ Direct Phi4 integration (no HTTP calls)
- ✅ Smart escalation: Deepseek+Phi4 (free) → Claude (paid, only if stuck)
- ✅ 10-attempt retry loop per file
- ✅ Stub generation on failure (never gives up!)
- ✅ Simpler 2-tier architecture

## Migration Path

The new system is in:
- **CodingAgent.Server/** - Contains `ProjectOrchestrator` and `ValidationService`
- **CodingAgent.Server.Tests/** - Integration tests

### What Changed:
1. **Validation is now in-process** - Uses `ValidationService` directly (no HTTP)
2. **Phi4 for thinking** - Planning, validation, build decisions all use Phi4
3. **Template system** - Instant scaffolding for common project types
4. **Better cost control** - Local models first, Claude only when stuck

## Can This Be Restored?

Yes! Just move these directories back to the root:

```bash
cd E:\GitHub\MemoryAgent
mv Archive/OLD_CodingOrchestrator_20251220_223028/* .
```

But we recommend using the NEW system instead.

## Docker Containers

The old Docker containers (`memory-coding-orchestrator`, `memory-coding-agent`) are still running but **should be stopped and removed**:

```bash
docker stop memory-coding-orchestrator memory-coding-agent
docker rm memory-coding-orchestrator memory-coding-agent
```

## MCP Wrapper

The MCP wrapper (`orchestrator-mcp-wrapper.js`) needs to be updated to point to the NEW system.

---

**Status:** ARCHIVED - DO NOT USE
**Replacement:** `CodingAgent.Server/ProjectOrchestrator`



