# What's Needed to Ship: Flutter + C# Code Generation Agent

## 📊 Current Status Analysis

### ✅ **ALREADY HAVE (Working)**

| Component | Status | Notes |
|-----------|--------|-------|
| **Docker Configs** | ✅ | C# (`memoryagent-dotnet-multi:latest`) + Flutter (`ghcr.io/cirruslabs/flutter:stable`) |
| **C# Prompts** | ✅ | `coding_agent_csharp` in prompts.json |
| **Flutter Prompts** | ✅ | `coding_agent_flutter` in prompts.json |
| **Dart Parser** | ✅ | `DartParser.cs` exists |
| **Flutter Pattern Detector** | ✅ | `FlutterPatternDetector.cs` exists |
| **Dart Pattern Validator** | ✅ | `DartPatternValidator.cs` exists |
| **CodingAgent** | ✅ | Generates code with Deepseek/Claude |
| **ValidationAgent** | ✅ | Validates with Phi4 |
| **MemoryAgent** | ✅ | Context, patterns, learning |
| **TaskOrchestrator** | ✅ | Basic orchestration with 10 retries |
| **ExecutionService** | ✅ | Docker-based execution |
| **MCP Handler** | ✅ | `orchestrate_task`, `get_task_status` |

### ❌ **MISSING (Need to Build)**

| Component | Priority | Effort | Blocks |
|-----------|----------|--------|--------|
| **1. Project Templates** | P0 | 2-3 days | Everything |
| **2. Phi4 Thinking Integration** | P0 | 2-3 days | Quality |
| **3. Stub Generator** | P1 | 1 day | Resilience |
| **4. Failure Report Generator** | P1 | 1 day | Debugging |
| **5. Root Cause Engine** | P2 | 2-3 days | Learning |
| **6. Progressive Escalation** | P2 | 2 days | 10+ attempts |
| **7. Test Generation** | P3 | 2 days | Test coverage |
| **8. Design Agent Integration** | P3 | 1 day | UI consistency |

---

## 🔥 **CRITICAL PATH: What's Needed to Generate Any App**

### **Week 1: Core Templates (P0)**

#### **Task 1.1: C# Project Templates**

```csharp
// File: CodingOrchestrator.Server/Templates/CSharpTemplates.cs

public static class CSharpTemplates
{
    public static Dictionary<string, ProjectTemplate> Templates = new()
    {
        ["console"] = new ProjectTemplate
        {
            ProjectType = "ConsoleApp",
            Files = new Dictionary<string, string>
            {
                ["Program.cs"] = @"namespace {{ProjectName}};

public class Program
{
    public static void Main(string[] args)
    {
        // TODO: Implement
    }
}",
                ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>"
            },
            FolderStructure = new[] { "Services", "Models", "Utils" }
        },
        
        ["webapi"] = new ProjectTemplate
        {
            ProjectType = "WebAPI",
            Files = new Dictionary<string, string>
            {
                ["Program.cs"] = @"var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();",
                ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>
</Project>"
            },
            FolderStructure = new[] { "Controllers", "Services", "Models", "DTOs" }
        },
        
        ["blazor-wasm"] = new ProjectTemplate
        {
            ProjectType = "BlazorWebAssembly",
            // ... Blazor template files
        },
        
        ["blazor-server"] = new ProjectTemplate
        {
            ProjectType = "BlazorServer",
            // ... Blazor Server template files
        },
        
        ["library"] = new ProjectTemplate
        {
            ProjectType = "ClassLibrary",
            // ... Library template files
        }
    };
}
```

**Deliverable:** 5 C# templates (Console, WebAPI, Blazor WASM, Blazor Server, Library)

#### **Task 1.2: Flutter Project Templates**

```csharp
// File: CodingOrchestrator.Server/Templates/FlutterTemplates.cs

public static class FlutterTemplates
{
    public static Dictionary<string, ProjectTemplate> Templates = new()
    {
        ["ios"] = new ProjectTemplate
        {
            ProjectType = "FlutterIOS",
            Files = new Dictionary<string, string>
            {
                ["pubspec.yaml"] = @"name: {{project_name}}
description: A new Flutter project.
version: 1.0.0+1

environment:
  sdk: '>=3.0.0 <4.0.0'

dependencies:
  flutter:
    sdk: flutter
  cupertino_icons: ^1.0.6
  provider: ^6.1.1
  dio: ^5.4.0
  flutter_secure_storage: ^9.0.0
  get_it: ^7.6.4

dev_dependencies:
  flutter_test:
    sdk: flutter
  flutter_lints: ^3.0.0

flutter:
  uses-material-design: true",

                ["lib/main.dart"] = @"import 'package:flutter/cupertino.dart';
import 'package:provider/provider.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return const CupertinoApp(
      title: '{{ProjectName}}',
      home: HomeScreen(),
    );
  }
}

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CupertinoPageScaffold(
      navigationBar: const CupertinoNavigationBar(
        middle: Text('{{ProjectName}}'),
      ),
      child: const Center(
        child: Text('Hello, iOS!'),
      ),
    );
  }
}",
                ["analysis_options.yaml"] = @"include: package:flutter_lints/flutter.yaml

linter:
  rules:
    prefer_const_constructors: true
    prefer_const_declarations: true
    avoid_print: true"
            },
            FolderStructure = new[] 
            { 
                "lib/models", 
                "lib/providers", 
                "lib/screens", 
                "lib/widgets", 
                "lib/services",
                "test"
            }
        },
        
        ["android"] = new ProjectTemplate
        {
            ProjectType = "FlutterAndroid",
            // Uses Material design instead of Cupertino
        },
        
        ["web"] = new ProjectTemplate
        {
            ProjectType = "FlutterWeb",
            // Responsive design patterns
        }
    };
}
```

**Deliverable:** 3 Flutter templates (iOS, Android, Web)

---

### **Week 2: Intelligence Layer (P0)**

#### **Task 2.1: Phi4 Thinking Integration**

Currently TaskOrchestrator uses Phi4 for validation. Need to add:

```csharp
// Add to TaskOrchestrator.cs

private async Task<ThinkingResult> ThinkAboutStepAsync(
    PlanStep step,
    Dictionary<string, FileChange> existingFiles,
    List<GenerationAttempt> previousAttempts,
    CancellationToken ct)
{
    var prompt = $@"
You are thinking about how to implement the next step.

STEP: {step.Description}
FILE: {step.FilePath}

EXISTING FILES:
{string.Join("\n", existingFiles.Select(f => $"- {f.Key}"))}

PREVIOUS ATTEMPTS: {previousAttempts.Count}
{(previousAttempts.Any() ? $"LAST SCORE: {previousAttempts.Last().Score}/10" : "")}
{(previousAttempts.Any() ? $"LAST ISSUES: {string.Join(", ", previousAttempts.Last().Issues.Take(3))}" : "")}

Think about:
1. What does this file need to do?
2. What dependencies does it need?
3. What could go wrong?
4. What patterns should be used?

Output your thinking as JSON:
{{
    ""approach"": ""Brief description of approach"",
    ""dependencies"": [""list of files this needs""],
    ""patterns"": [""patterns to use""],
    ""risks"": [""potential issues""],
    ""suggestions"": ""Specific suggestions for the generator""
}}
";

    var response = await _ollamaClient.GenerateAsync(
        "phi4:latest",
        prompt,
        ct);
    
    return JsonSerializer.Deserialize<ThinkingResult>(response);
}
```

**Deliverable:** Phi4 thinks before EVERY generation step

#### **Task 2.2: Enhanced Model Selection**

```csharp
// Update model selection based on complexity and history

private string SelectBestModel(
    PlanStep step,
    List<GenerationAttempt> previousAttempts,
    ProjectType projectType)
{
    // Attempt 1-3: Deepseek (free)
    if (previousAttempts.Count < 3)
        return "deepseek-coder-v2:16b";
    
    // Attempt 4-6: Check if C# or Flutter specific
    if (previousAttempts.Count < 6)
    {
        // Use Claude for complex patterns
        if (step.Complexity >= 7)
            return "claude-sonnet-4";
        
        // Try different Deepseek variant
        return "qwen2.5-coder:14b";
    }
    
    // Attempt 7-10: Premium Claude
    return "claude-sonnet-4";
}
```

---

### **Week 3: Resilience (P1)**

#### **Task 3.1: Stub Generator**

```csharp
// File: CodingOrchestrator.Server/Services/StubGenerator.cs

public class StubGenerator : IStubGenerator
{
    public GeneratedStub GenerateStub(
        PlanStep step,
        string language,
        RootCauseAnalysis? rootCause)
    {
        if (language == "csharp")
            return GenerateCSharpStub(step, rootCause);
        else if (language == "flutter" || language == "dart")
            return GenerateFlutterStub(step, rootCause);
        
        throw new NotSupportedException($"Language {language} not supported");
    }
    
    private GeneratedStub GenerateCSharpStub(PlanStep step, RootCauseAnalysis? rootCause)
    {
        var className = Path.GetFileNameWithoutExtension(step.FilePath);
        var stub = $@"namespace {{{{ProjectName}}}};

/// <summary>
/// TODO: NEEDS HUMAN REVIEW
/// This file failed generation after maximum attempts.
/// 
/// Root Cause: {rootCause?.RootCause ?? "Unknown"}
/// 
/// Suggested approach:
/// {string.Join("\n/// ", rootCause?.RecommendedActions ?? new[] { "Review and implement manually" })}
/// </summary>
public class {className}
{{
    public {className}()
    {{
    }}
    
    // TODO: Implement required methods
    // See failure report: {className}_failure_report.md
}}
";
        return new GeneratedStub
        {
            FilePath = step.FilePath,
            Content = stub,
            Status = "NEEDS_HUMAN_REVIEW"
        };
    }
}
```

---

## 📋 **MINIMUM VIABLE PRODUCT (MVP)**

To generate **any Flutter or C# app**, we need at minimum:

### **MVP Checklist (2-3 weeks)**

| Task | Days | Status |
|------|------|--------|
| 1. C# Templates (Console, WebAPI, Blazor) | 2 | ❌ |
| 2. Flutter Templates (iOS, Android) | 2 | ❌ |
| 3. Template Selection Logic | 1 | ❌ |
| 4. Phi4 Thinking Integration | 2 | ❌ |
| 5. Stub Generator | 1 | ❌ |
| 6. Failure Report Generator | 1 | ❌ |
| 7. Project Type Detection | 1 | ⚠️ Partial |
| 8. End-to-End Testing | 2 | ❌ |
| **TOTAL** | **12 days** | |

---

## 🚀 **Action Plan: What to Build First**

### **Phase 1: Templates (Days 1-4)**

```bash
# Create template files
CodingOrchestrator.Server/
├── Templates/
│   ├── CSharpTemplates.cs     # 5 C# project templates
│   ├── FlutterTemplates.cs    # 3 Flutter project templates
│   ├── IProjectTemplate.cs    # Interface
│   └── TemplateService.cs     # Template selection & hydration
```

### **Phase 2: Intelligence (Days 5-8)**

```bash
# Add thinking + model selection
CodingOrchestrator.Server/
├── Services/
│   ├── Phi4ThinkingService.cs   # Thinks before each step
│   ├── ModelSelector.cs         # Picks best model
│   └── TaskOrchestrator.cs      # Update to use thinking
```

### **Phase 3: Resilience (Days 9-12)**

```bash
# Add failure handling
CodingOrchestrator.Server/
├── Services/
│   ├── StubGenerator.cs         # Compilable stubs
│   ├── FailureReportGenerator.cs # Markdown reports
│   └── RootCauseAnalyzer.cs     # Basic analysis
```

---

## 📊 **Summary: What's Needed**

| Category | What | When | Days |
|----------|------|------|------|
| **Critical** | Templates (C# + Flutter) | Week 1 | 4 |
| **Critical** | Phi4 Thinking | Week 2 | 2 |
| **Important** | Stub Generator | Week 2 | 1 |
| **Important** | Failure Reports | Week 2 | 1 |
| **Important** | End-to-End Tests | Week 3 | 2 |
| **Nice to Have** | Root Cause Engine | Week 3+ | 3 |
| **Nice to Have** | Test Generation | Week 4+ | 2 |
| **TOTAL MVP** | | **3 weeks** | **~12 days** |

---

## ✅ **Start Here**

**To code ANY Flutter or C# app, start with:**

1. **Day 1-2:** Create `CSharpTemplates.cs` with Console + WebAPI templates
2. **Day 3-4:** Create `FlutterTemplates.cs` with iOS + Android templates
3. **Day 5-6:** Add Phi4 thinking before each generation step
4. **Day 7-8:** Add stub generator for graceful failure
5. **Day 9-10:** End-to-end testing with real apps
6. **Day 11-12:** Fix bugs, polish, ship!

**After 12 days of focused work, the agent can generate:**
- ✅ C# Console apps
- ✅ C# WebAPI apps
- ✅ C# Blazor apps
- ✅ Flutter iOS apps
- ✅ Flutter Android apps

🚀 **Ready to start building!**




