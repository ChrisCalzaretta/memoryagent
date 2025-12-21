# Language Server Protocol (LSP) Integration Guide 🔧

## 📚 Table of Contents

1. [What is LSP?](#what-is-lsp)
2. [How LSP Works](#how-lsp-works)
3. [Why LSP for Memory Agent?](#why-lsp-for-memory-agent)
4. [Architecture Overview](#architecture-overview)
5. [Implementation Plan](#implementation-plan)
6. [Protocol Messages](#protocol-messages)
7. [Code Examples](#code-examples)
8. [Integration with Cursor](#integration-with-cursor)
9. [Testing Strategy](#testing-strategy)

---

## What is LSP?

**Language Server Protocol (LSP)** is a **standardized protocol** created by Microsoft (2016) that separates:

```
Language Intelligence          Editor/IDE
(parsing, analysis,      ←→   (UI, editing,
 validation, etc.)             display)
```

### **Before LSP (The Old Way):**

```
Problem: Every editor needs custom integration for every language

VS Code needs:
  ├─ C# plugin
  ├─ Python plugin  
  ├─ TypeScript plugin
  └─ 50+ other plugins (all custom code)

IntelliJ needs:
  ├─ C# plugin (different from VS Code!)
  ├─ Python plugin (different from VS Code!)
  └─ 50+ other plugins (duplicated effort)

Sublime needs:
  ├─ Same thing again...
  └─ Different APIs, different code

Total: N languages × M editors = LOTS of work
```

### **After LSP (The Modern Way):**

```
Solution: One language server works with ALL editors

C# Language Server ─┬─→ VS Code (LSP client)
                    ├─→ IntelliJ (LSP client)
                    ├─→ Sublime (LSP client)
                    ├─→ Vim (LSP client)
                    └─→ ANY editor with LSP support

Total: N language servers + M LSP clients = Much less work!
```

### **What LSP Provides:**

```
✅ Red squiggles under errors
✅ Hover tooltips
✅ Auto-completion
✅ Go to definition
✅ Find references
✅ Rename refactoring
✅ Code actions (Quick Fixes)
✅ Document formatting
✅ And more...
```

---

## How LSP Works

### **The Architecture:**

```
┌─────────────────────────────────────────────────────┐
│                    VS Code / Cursor                  │
│                   (LSP Client)                       │
│                                                      │
│  ┌────────────────────────────────────────────┐    │
│  │          Text Editor                        │    │
│  │  ┌──────────────────────────────────┐     │    │
│  │  │  1  public class User            │     │    │
│  │  │  2  {                             │     │    │
│  │  │  3      _cache.Set(key, value);  │     │    │
│  │  │         ~~~~~~~~~~~~~~~~~~~~~     │     │    │
│  │  │         ⚠️ Missing expiration    │     │    │
│  │  └──────────────────────────────────┘     │    │
│  └────────────────────────────────────────────┘    │
│                        ↕                            │
│                   LSP Protocol                      │
│              (JSON-RPC over stdio/TCP)              │
└─────────────────────────────────────────────────────┘
                         ↕
┌─────────────────────────────────────────────────────┐
│           MemoryAgent.LSP.Server                     │
│              (Language Server)                       │
│                                                      │
│  ┌─────────────────────────────────────────────┐   │
│  │  LSP Protocol Handler                        │   │
│  │  • Receives: textDocument/didSave            │   │
│  │  • Validates code                            │   │
│  │  • Sends: publishDiagnostics                 │   │
│  └─────────────────────────────────────────────┘   │
│                        ↓                            │
│  ┌─────────────────────────────────────────────┐   │
│  │  Pattern Validation Service                  │   │
│  │  (Reuses existing PatternValidationService)  │   │
│  └─────────────────────────────────────────────┘   │
│                        ↓                            │
│  ┌─────────────────────────────────────────────┐   │
│  │  Qdrant + Neo4j                              │   │
│  │  (Existing indexed data)                     │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### **Communication Protocol:**

LSP uses **JSON-RPC 2.0** over:
- **stdio** (standard input/output) - Most common
- **TCP sockets** - For debugging
- **Named pipes** - Windows option

### **Example Message Flow:**

```json
1. User saves file in Cursor
   ↓
2. Cursor (Client) sends:
{
  "jsonrpc": "2.0",
  "method": "textDocument/didSave",
  "params": {
    "textDocument": {
      "uri": "file:///E:/GitHub/MyProject/UserService.cs",
      "version": 5
    }
  }
}
   ↓
3. LSP Server receives, validates code
   ↓
4. LSP Server sends back:
{
  "jsonrpc": "2.0",
  "method": "textDocument/publishDiagnostics",
  "params": {
    "uri": "file:///E:/GitHub/MyProject/UserService.cs",
    "diagnostics": [
      {
        "range": {
          "start": { "line": 15, "character": 8 },
          "end": { "line": 15, "character": 35 }
        },
        "severity": 1,  // 1=Error, 2=Warning, 3=Info
        "code": "MEMAG001",
        "source": "MemoryAgent",
        "message": "Missing cache expiration policy (Score: 4/10)",
        "relatedInformation": [
          {
            "location": {
              "uri": "file:///E:/GitHub/MyProject/UserService.cs",
              "range": { "start": { "line": 15, "character": 8 }, "end": { "line": 15, "character": 35 } }
            },
            "message": "Add: AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)"
          }
        ]
      }
    ]
  }
}
   ↓
5. Cursor shows red squiggle at line 15, chars 8-35
```

---

## Why LSP for Memory Agent?

### **Perfect Fit Because:**

✅ **We already have validation logic**
```
PatternValidationService.ValidatePatternQualityAsync()
  ↓
Returns: Issues with line numbers, severity, fixes
  ↓
LSP: Translate to diagnostics
  ↓
Red squiggles appear!
```

✅ **We already have indexed code**
```
No need to parse files from scratch
Just query what we've already indexed
FAST responses
```

✅ **We can provide Quick Fixes**
```
Auto-fix code we generate
  ↓
LSP CodeAction
  ↓
"Apply fix" in right-click menu
```

✅ **Native IDE experience**
```
Looks like TypeScript/ESLint errors
Users already know how to use it
Professional polish
```

---

## Architecture Overview

### **Components:**

```
MemoryAgent/
├── MemoryAgent.Server/          (Existing MCP server)
│   ├── Services/
│   │   ├── PatternValidationService.cs  (Reuse!)
│   │   ├── PatternIndexingService.cs    (Reuse!)
│   │   └── VectorService.cs              (Reuse!)
│   └── Models/
│       └── PatternQualityResult.cs       (Reuse!)
│
├── MemoryAgent.LSP/              (NEW - LSP Server)
│   ├── Program.cs                (LSP server entry point)
│   ├── LanguageServer.cs         (LSP protocol handler)
│   ├── Handlers/
│   │   ├── TextDocumentHandler.cs
│   │   ├── DiagnosticProvider.cs
│   │   └── CodeActionProvider.cs
│   └── Services/
│       └── ValidationAdapter.cs   (Adapts our validation to LSP)
│
└── .cursor/
    └── extensions/
        └── memoryagent-lsp/       (Cursor extension config)
            ├── package.json
            └── extension.js
```

### **Data Flow:**

```
1. File saved in Cursor
   ↓
2. LSP Client (in Cursor) → textDocument/didSave → LSP Server
   ↓
3. LSP Server:
   a. Extracts file path
   b. Calls PatternIndexingService.SearchPatternsAsync(filePath)
   c. For each pattern:
      - Calls PatternValidationService.ValidatePatternQualityAsync()
   d. Converts results to LSP Diagnostics
   ↓
4. LSP Server → publishDiagnostics → LSP Client
   ↓
5. Cursor shows red squiggles + tooltips
```

### **Reuse Existing Services:**

```csharp
// In LSP Server (NEW)
public class DiagnosticProvider
{
    private readonly PatternValidationService _validationService;  // EXISTING!
    private readonly PatternIndexingService _patternService;       // EXISTING!
    
    public async Task<List<Diagnostic>> GetDiagnosticsAsync(string filePath)
    {
        // 1. Search for patterns in file
        var patterns = await _patternService.SearchPatternsAsync(
            $"file:{filePath}", 
            limit: 100
        );
        
        // 2. Validate each pattern
        var diagnostics = new List<Diagnostic>();
        foreach (var pattern in patterns)
        {
            var validation = await _validationService.ValidatePatternQualityAsync(
                pattern.Id, 
                includeAutoFix: true
            );
            
            // 3. Convert to LSP diagnostic
            if (validation.Score < 7)
            {
                diagnostics.Add(new Diagnostic
                {
                    Range = new Range(pattern.LineNumber, 0, pattern.LineNumber, 100),
                    Severity = GetSeverity(validation.Score),
                    Code = $"MEMAG{pattern.Type}",
                    Source = "MemoryAgent",
                    Message = $"{pattern.Name}: {validation.Summary}",
                    RelatedInformation = validation.Issues.Select(i => new DiagnosticRelatedInformation
                    {
                        Message = i.FixGuidance ?? i.Message
                    }).ToList()
                });
            }
        }
        
        return diagnostics;
    }
    
    private DiagnosticSeverity GetSeverity(int score)
    {
        return score switch
        {
            <= 5 => DiagnosticSeverity.Error,     // F grade - RED
            6 => DiagnosticSeverity.Warning,       // D grade - YELLOW
            7 => DiagnosticSeverity.Information,   // C grade - BLUE
            _ => DiagnosticSeverity.Hint           // B/A grade - SUBTLE
        };
    }
}
```

**NO DUPLICATION!** We reuse everything! 🎉

---

## Implementation Plan

### **Phase 1: Basic LSP Server** (Week 1)

**Goal:** Red squiggles on save

```
Day 1-2: Project Setup
- Create MemoryAgent.LSP project (.NET 9)
- Add OmniSharp.Extensions.LanguageServer NuGet package
- Reference MemoryAgent.Server (reuse services)

Day 3-4: Implement Core Protocol
- Handle: initialize
- Handle: textDocument/didOpen
- Handle: textDocument/didSave
- Send: textDocument/publishDiagnostics

Day 5: Testing
- Test with simple C# file
- Verify squiggles appear
- Test with real patterns
```

**Deliverable:** Basic validation with red squiggles

### **Phase 2: Advanced Features** (Week 2)

**Goal:** Hover tooltips + Quick Fixes

```
Day 1-2: Hover Provider
- Handle: textDocument/hover
- Show pattern details on hover
- Show validation score and issues

Day 3-4: Code Actions (Quick Fixes)
- Handle: textDocument/codeAction
- Provide "Apply fix" actions
- Generate code from auto-fix

Day 5: Polish
- Error handling
- Performance optimization
- Logging
```

**Deliverable:** Full IDE integration

### **Phase 3: Cursor Extension** (Week 2)

**Goal:** Easy activation in Cursor

```
Day 1: Create Extension
- package.json with LSP config
- extension.js for initialization
- Settings for customization

Day 2: Documentation
- Installation guide
- Configuration options
- Troubleshooting

Day 3: Testing
- Test in Cursor
- Test in VS Code
- Verify all features
```

**Deliverable:** Production-ready extension

---

## Protocol Messages

### **Core Messages We'll Implement:**

#### **1. Initialize (Server Capabilities)**

```typescript
Client → Server:
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "processId": 12345,
    "rootUri": "file:///E:/GitHub/MyProject",
    "capabilities": { /* client capabilities */ }
  }
}

Server → Client:
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "capabilities": {
      "textDocumentSync": {
        "openClose": true,
        "change": 2,  // Incremental
        "save": { "includeText": true }
      },
      "hoverProvider": true,
      "codeActionProvider": {
        "codeActionKinds": ["quickfix", "refactor"]
      },
      "diagnosticProvider": {
        "interFileDependencies": false,
        "workspaceDiagnostics": false
      }
    },
    "serverInfo": {
      "name": "MemoryAgent LSP",
      "version": "1.0.0"
    }
  }
}
```

#### **2. Document Open/Save**

```typescript
Client → Server (when file opens):
{
  "jsonrpc": "2.0",
  "method": "textDocument/didOpen",
  "params": {
    "textDocument": {
      "uri": "file:///E:/GitHub/MyProject/UserService.cs",
      "languageId": "csharp",
      "version": 1,
      "text": "/* full file content */"
    }
  }
}

Client → Server (when file saves):
{
  "jsonrpc": "2.0",
  "method": "textDocument/didSave",
  "params": {
    "textDocument": {
      "uri": "file:///E:/GitHub/MyProject/UserService.cs",
      "version": 5
    },
    "text": "/* full file content */"
  }
}
```

#### **3. Publish Diagnostics (Our Validation)**

```typescript
Server → Client (after validation):
{
  "jsonrpc": "2.0",
  "method": "textDocument/publishDiagnostics",
  "params": {
    "uri": "file:///E:/GitHub/MyProject/UserService.cs",
    "version": 5,
    "diagnostics": [
      {
        "range": {
          "start": { "line": 15, "character": 8 },
          "end": { "line": 15, "character": 35 }
        },
        "severity": 1,  // Error
        "code": "MEMAG_CACHE_001",
        "source": "MemoryAgent",
        "message": "CacheAside pattern missing expiration policy (Score: 4/10)",
        "tags": [],
        "relatedInformation": [
          {
            "location": {
              "uri": "file:///E:/GitHub/MyProject/UserService.cs",
              "range": { "start": { "line": 15, "character": 8 }, "end": { "line": 15, "character": 35 } }
            },
            "message": "💡 Fix: Add AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)"
          }
        ],
        "data": {
          "patternId": "cache_aside_123",
          "validationScore": 4,
          "autoFixAvailable": true
        }
      }
    ]
  }
}
```

#### **4. Hover (Show Details)**

```typescript
Client → Server (user hovers over squiggle):
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "textDocument/hover",
  "params": {
    "textDocument": {
      "uri": "file:///E:/GitHub/MyProject/UserService.cs"
    },
    "position": { "line": 15, "character": 20 }
  }
}

Server → Client:
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "contents": {
      "kind": "markdown",
      "value": "## ⚠️ CacheAside Pattern Issues\n\n**Quality Score:** 4/10 (Grade: F)\n\n**Critical Issues:**\n- No expiration policy set\n- Risk of stale data and memory leaks\n\n**High Issues:**\n- Missing null check before caching\n\n**Recommendations:**\n1. Add expiration policy\n2. Add null check\n3. Add concurrency protection\n\n[📚 Learn More](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)"
    },
    "range": {
      "start": { "line": 15, "character": 8 },
      "end": { "line": 15, "character": 35 }
    }
  }
}
```

#### **5. Code Actions (Quick Fixes)**

```typescript
Client → Server (user right-clicks):
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "textDocument/codeAction",
  "params": {
    "textDocument": {
      "uri": "file:///E:/GitHub/MyProject/UserService.cs"
    },
    "range": {
      "start": { "line": 15, "character": 8 },
      "end": { "line": 15, "character": 35 }
    },
    "context": {
      "diagnostics": [/* the diagnostic from above */],
      "triggerKind": 2  // Invoked
    }
  }
}

Server → Client:
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": [
    {
      "title": "✅ Add cache expiration policy",
      "kind": "quickfix",
      "diagnostics": [/* same diagnostic */],
      "edit": {
        "changes": {
          "file:///E:/GitHub/MyProject/UserService.cs": [
            {
              "range": {
                "start": { "line": 15, "character": 35 },
                "end": { "line": 15, "character": 35 }
              },
              "newText": ", new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }"
            }
          ]
        }
      }
    },
    {
      "title": "🔧 Apply all fixes (expiration + null check)",
      "kind": "quickfix.refactor.rewrite",
      "edit": {
        "changes": {
          "file:///E:/GitHub/MyProject/UserService.cs": [
            {
              "range": {
                "start": { "line": 14, "character": 0 },
                "end": { "line": 16, "character": 0 }
              },
              "newText": "if (user != null)\n{\n    _cache.Set(key, user, new MemoryCacheEntryOptions\n    {\n        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)\n    });\n}\n"
            }
          ]
        }
      }
    }
  ]
}
```

---

## Code Examples

### **Main LSP Server (Program.cs):**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;
using MemoryAgent.Server.Services;

namespace MemoryAgent.LSP;

class Program
{
    static async Task Main(string[] args)
    {
        // Create LSP server
        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .ConfigureLogging(x => x
                    .AddLanguageProtocolLogging()
                    .SetMinimumLevel(LogLevel.Debug))
                .WithServices(ConfigureServices)
                .WithHandler<TextDocumentHandler>()
                .WithHandler<CodeActionHandler>()
                .WithHandler<HoverHandler>()
        );

        await server.WaitForExit;
    }

    static void ConfigureServices(IServiceCollection services)
    {
        // Register our existing services (REUSE!)
        services.AddScoped<IPatternValidationService, PatternValidationService>();
        services.AddScoped<IPatternIndexingService, PatternIndexingService>();
        services.AddScoped<IVectorService, VectorService>();
        services.AddScoped<IGraphService, GraphService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        
        // Register LSP-specific adapters
        services.AddScoped<DiagnosticProvider>();
        services.AddScoped<CodeActionProvider>();
    }
}
```

### **Text Document Handler:**

```csharp
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace MemoryAgent.LSP.Handlers;

public class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ILanguageServerFacade _router;
    private readonly DiagnosticProvider _diagnosticProvider;
    private readonly ILogger<TextDocumentHandler> _logger;

    public TextDocumentHandler(
        ILanguageServerFacade router,
        DiagnosticProvider diagnosticProvider,
        ILogger<TextDocumentHandler> logger)
    {
        _router = router;
        _diagnosticProvider = diagnosticProvider;
        _logger = logger;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "csharp");
    }

    // Called when file is opened
    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document opened: {Uri}", request.TextDocument.Uri);
        return Unit.Task;
    }

    // Called when file is saved - VALIDATE HERE!
    public override async Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document saved: {Uri}", request.TextDocument.Uri);
        
        // Get file path from URI
        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        
        // Validate using our existing services!
        var diagnostics = await _diagnosticProvider.GetDiagnosticsAsync(filePath, cancellationToken);
        
        // Publish diagnostics to client (show squiggles!)
        _router.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>(diagnostics)
        });
        
        return Unit.Value;
    }

    // Called when file changes (optional - for real-time validation)
    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        // For now, we only validate on save
        // Could add debounced real-time validation here
        return Unit.Task;
    }

    // Called when file is closed
    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document closed: {Uri}", request.TextDocument.Uri);
        
        // Clear diagnostics for closed file
        _router.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>()
        });
        
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, 
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = new DocumentSelector(
                new DocumentFilter { Pattern = "**/*.cs" },
                new DocumentFilter { Pattern = "**/*.py" },
                new DocumentFilter { Pattern = "**/*.vb" }
            ),
            Change = TextDocumentSyncKind.Incremental,
            Save = new SaveOptions { IncludeText = true }
        };
    }
}
```

### **Diagnostic Provider (Adapts our validation to LSP):**

```csharp
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using MemoryAgent.Server.Services;
using MemoryAgent.Server.Models;

namespace MemoryAgent.LSP;

public class DiagnosticProvider
{
    private readonly IPatternValidationService _validationService;
    private readonly IPatternIndexingService _patternService;
    private readonly ILogger<DiagnosticProvider> _logger;

    public DiagnosticProvider(
        IPatternValidationService validationService,
        IPatternIndexingService patternService,
        ILogger<DiagnosticProvider> logger)
    {
        _validationService = validationService;
        _patternService = patternService;
        _logger = logger;
    }

    public async Task<List<Diagnostic>> GetDiagnosticsAsync(
        string filePath, 
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<Diagnostic>();

        try
        {
            // 1. Search for patterns in this file
            var patterns = await _patternService.SearchPatternsAsync(
                $"file:{filePath}",
                limit: 100,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Found {Count} patterns in {File}", patterns.Count, filePath);

            // 2. Validate each pattern
            foreach (var pattern in patterns)
            {
                var validation = await _validationService.ValidatePatternQualityAsync(
                    pattern.Id,
                    includeAutoFix: true,
                    cancellationToken: cancellationToken
                );

                // 3. Only create diagnostic if there are issues (score < 9)
                if (validation.Score < 9 && validation.Issues.Any())
                {
                    diagnostics.Add(ConvertToDiagnostic(pattern, validation));
                }
            }

            _logger.LogInformation("Created {Count} diagnostics for {File}", diagnostics.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diagnostics for {File}", filePath);
        }

        return diagnostics;
    }

    private Diagnostic ConvertToDiagnostic(CodePattern pattern, PatternQualityResult validation)
    {
        // Determine severity based on score
        var severity = validation.Score switch
        {
            <= 5 => DiagnosticSeverity.Error,      // F grade
            6 => DiagnosticSeverity.Warning,        // D grade
            7 => DiagnosticSeverity.Information,    // C grade
            8 => DiagnosticSeverity.Hint,           // B grade
            _ => DiagnosticSeverity.Hint            // A grade (shouldn't reach here)
        };

        // Build message
        var message = $"{pattern.Name}: {validation.Summary}";
        
        // Add critical issues to message
        var criticalIssues = validation.Issues
            .Where(i => i.Severity == IssueSeverity.Critical)
            .ToList();
        
        if (criticalIssues.Any())
        {
            message += "\n🚨 " + string.Join("\n🚨 ", criticalIssues.Select(i => i.Message));
        }

        return new Diagnostic
        {
            Range = new Range(
                new Position(pattern.LineNumber - 1, 0),  // LSP uses 0-based line numbers
                new Position(pattern.LineNumber - 1, 100)
            ),
            Severity = severity,
            Code = $"MEMAG_{pattern.Type}",
            Source = "MemoryAgent",
            Message = message,
            Tags = validation.Score <= 5 
                ? new Container<DiagnosticTag>(DiagnosticTag.Deprecated) 
                : null,
            RelatedInformation = validation.Issues
                .Where(i => !string.IsNullOrEmpty(i.FixGuidance))
                .Select(i => new DiagnosticRelatedInformation
                {
                    Location = new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(pattern.FilePath),
                        Range = new Range(
                            new Position(pattern.LineNumber - 1, 0),
                            new Position(pattern.LineNumber - 1, 100)
                        )
                    },
                    Message = $"💡 {i.FixGuidance}"
                }).ToContainer(),
            Data = new
            {
                patternId = pattern.Id,
                validationScore = validation.Score,
                autoFixCode = validation.AutoFixCode,
                recommendations = validation.Recommendations
            }
        };
    }
}
```

---

## Integration with Cursor

### **Cursor Extension (package.json):**

```json
{
  "name": "memoryagent-lsp",
  "displayName": "Memory Agent - Code Intelligence",
  "description": "AI-powered pattern validation and recommendations",
  "version": "1.0.0",
  "publisher": "memoryagent",
  "engines": {
    "vscode": "^1.80.0"
  },
  "categories": ["Linters", "Programming Languages"],
  "activationEvents": ["onLanguage:csharp", "onLanguage:python", "onLanguage:vb"],
  "main": "./extension.js",
  "contributes": {
    "configuration": {
      "type": "object",
      "title": "Memory Agent",
      "properties": {
        "memoryAgent.lsp.enable": {
          "type": "boolean",
          "default": true,
          "description": "Enable Memory Agent LSP server"
        },
        "memoryAgent.lsp.serverPath": {
          "type": "string",
          "default": "E:\\GitHub\\MemoryAgent\\MemoryAgent.LSP\\bin\\Release\\net9.0\\MemoryAgent.LSP.exe",
          "description": "Path to MemoryAgent LSP server executable"
        },
        "memoryAgent.validation.onSave": {
          "type": "boolean",
          "default": true,
          "description": "Validate patterns on file save"
        },
        "memoryAgent.validation.minScore": {
          "type": "number",
          "default": 7,
          "description": "Minimum pattern quality score (0-10)"
        }
      }
    }
  }
}
```

### **Extension Code (extension.js):**

```javascript
const vscode = require('vscode');
const { LanguageClient } = require('vscode-languageclient/node');

let client;

function activate(context) {
    const config = vscode.workspace.getConfiguration('memoryAgent.lsp');
    
    if (!config.get('enable')) {
        console.log('Memory Agent LSP is disabled');
        return;
    }

    const serverPath = config.get('serverPath');
    
    // Define LSP client options
    const serverOptions = {
        run: { command: serverPath, args: [] },
        debug: { command: serverPath, args: ['--debug'] }
    };

    const clientOptions = {
        documentSelector: [
            { scheme: 'file', language: 'csharp' },
            { scheme: 'file', language: 'python' },
            { scheme: 'file', language: 'vb' }
        ],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.{cs,py,vb}')
        }
    };

    // Create and start the client
    client = new LanguageClient(
        'memoryAgentLsp',
        'Memory Agent LSP',
        serverOptions,
        clientOptions
    );

    client.start();
    
    console.log('Memory Agent LSP client started');
}

function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}

module.exports = { activate, deactivate };
```

---

## Testing Strategy

### **Unit Tests:**

```csharp
[Test]
public async Task DiagnosticProvider_ValidatesPatternCorrectly()
{
    // Arrange
    var mockValidation = new Mock<IPatternValidationService>();
    mockValidation
        .Setup(x => x.ValidatePatternQualityAsync(It.IsAny<string>(), true, default))
        .ReturnsAsync(new PatternQualityResult
        {
            Score = 4,
            Issues = new List<ValidationIssue>
            {
                new() { 
                    Severity = IssueSeverity.Critical,
                    Message = "Missing expiration",
                    FixGuidance = "Add expiration policy"
                }
            }
        });

    var provider = new DiagnosticProvider(mockValidation.Object, ...);

    // Act
    var diagnostics = await provider.GetDiagnosticsAsync("test.cs");

    // Assert
    Assert.That(diagnostics, Has.Count.EqualTo(1));
    Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
    Assert.That(diagnostics[0].Message, Contains.Substring("Missing expiration"));
}
```

### **Integration Tests:**

```csharp
[Test]
public async Task LSP_EndToEnd_ShowsDiagnostics()
{
    // 1. Start LSP server
    var server = await StartLspServer();

    // 2. Send initialize request
    var initResponse = await server.SendRequest("initialize", new { ... });
    Assert.That(initResponse.capabilities, Is.Not.Null);

    // 3. Open document
    await server.SendNotification("textDocument/didOpen", new
    {
        textDocument = new
        {
            uri = "file:///test.cs",
            languageId = "csharp",
            version = 1,
            text = "/* code with pattern issues */"
        }
    });

    // 4. Save document (triggers validation)
    await server.SendNotification("textDocument/didSave", new
    {
        textDocument = new { uri = "file:///test.cs" }
    });

    // 5. Wait for diagnostics
    var diagnostics = await WaitForDiagnostics("file:///test.cs");

    // 6. Verify
    Assert.That(diagnostics, Has.Count.GreaterThan(0));
    Assert.That(diagnostics[0].source, Is.EqualTo("MemoryAgent"));
}
```

---

## Summary

**LSP gives us:**

✅ **Native IDE integration** - Red squiggles, hover tooltips  
✅ **Professional polish** - Looks like TypeScript/ESLint  
✅ **Reuses existing code** - No duplication, just adapters  
✅ **Works in any editor** - VS Code, Cursor, IntelliJ, Vim  
✅ **Industry standard** - Well-documented, mature protocol  

**Next Steps:**

1. ✅ Review this guide
2. Create MemoryAgent.LSP project
3. Implement basic protocol
4. Test with simple file
5. Add code actions
6. Create Cursor extension
7. Ship it! 🚀

**Questions? Let's discuss!**
























