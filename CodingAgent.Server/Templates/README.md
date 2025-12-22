# CodingAgent.Server Templates

## Overview

This module provides project templates for C# and Flutter code generation. Templates define the initial structure, files, and configuration for different project types.

## Architecture

```
Templates/
├── IProjectTemplate.cs          # Base interface & abstract class
├── TemplateService.cs           # Template selection & generation
├── CSharp/
│   ├── ConsoleAppTemplate.cs    # C# Console Application
│   ├── WebApiTemplate.cs        # C# Web API (ASP.NET Core)
│   ├── BlazorWasmTemplate.cs    # Blazor WebAssembly SPA
│   └── ClassLibraryTemplate.cs  # C# Class Library (NuGet-ready)
└── Flutter/
    ├── FlutterIosTemplate.cs    # Flutter iOS (Cupertino design)
    ├── FlutterAndroidTemplate.cs # Flutter Android (Material design)
    └── FlutterWebTemplate.cs    # Flutter Web (Responsive)
```

## Usage

### Auto-Detect Template

```csharp
var templateService = serviceProvider.GetRequiredService<ITemplateService>();

// Auto-detect based on task description
var match = await templateService.DetectTemplateAsync(
    "Create a REST API for user management",
    preferredLanguage: "csharp");

Console.WriteLine($"Template: {match.Template.TemplateId}");
Console.WriteLine($"Confidence: {match.Confidence:P0}");
```

### Generate Project Files

```csharp
var files = templateService.GenerateProjectFiles(
    "csharp-webapi",
    new ProjectContext
    {
        ProjectName = "UserApi",
        Description = "User management REST API",
        Namespace = "MyCompany.UserApi",
        TargetFramework = "net9.0"
    });

foreach (var (path, content) in files)
{
    Console.WriteLine($"Generated: {path}");
}
```

## Available Templates

### C# Templates

| Template ID | Project Type | Description |
|-------------|--------------|-------------|
| `csharp-console` | ConsoleApp | Command-line application |
| `csharp-webapi` | WebAPI | REST API with Swagger |
| `csharp-blazor-wasm` | BlazorWasm | Blazor WebAssembly SPA |
| `csharp-library` | ClassLibrary | Reusable class library |

### Flutter Templates

| Template ID | Project Type | Description |
|-------------|--------------|-------------|
| `flutter-ios` | FlutterIOS | iOS app with Cupertino design |
| `flutter-android` | FlutterAndroid | Android app with Material design |
| `flutter-web` | FlutterWeb | Responsive web app |

## Template Placeholders

Templates support these placeholders:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{{ProjectName}}` | PascalCase name | `MyApp` |
| `{{projectName}}` | camelCase name | `myApp` |
| `{{project_name}}` | snake_case name | `my_app` |
| `{{Namespace}}` | C# namespace | `MyCompany.MyApp` |
| `{{Description}}` | Project description | `A cool app` |
| `{{TargetFramework}}` | .NET framework | `net9.0` |

## Extending

To add a new template:

1. Create a class extending `ProjectTemplateBase`
2. Implement required properties
3. Define the `Files` dictionary with template content
4. Register in `TemplateService.RegisterTemplates()`

```csharp
public class MyCustomTemplate : ProjectTemplateBase
{
    public override string TemplateId => "csharp-my-custom";
    public override string DisplayName => "My Custom Template";
    public override string Language => "csharp";
    public override string ProjectType => "Custom";
    // ... etc
}
```

## Services

### ITemplateService

- `GetAllTemplates()` - List all available templates
- `GetTemplatesForLanguage(language)` - Filter by language
- `GetTemplateById(templateId)` - Get specific template
- `DetectTemplateAsync(task, language?)` - Auto-detect best template
- `GenerateProjectFiles(templateId, context)` - Generate files

### IPhi4ThinkingService

- `ThinkAboutStepAsync(context)` - Plan before generating a file
- `AnalyzeFailuresAsync(context)` - Root cause analysis
- `ShouldBuildNowAsync(context)` - Decide when to compile

### IStubGenerator

- `GenerateStub(context)` - Create compilable stub for failed files

### IFailureReportGenerator

- `GenerateReport(context)` - Create markdown failure report



