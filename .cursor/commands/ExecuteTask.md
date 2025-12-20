---
description: 🧠 AI-powered task execution using FunctionGemma orchestration
alwaysApply: false
---

# Execute Task

**MAIN ENTRY POINT for ALL operations!**

Uses FunctionGemma AI to intelligently orchestrate 44+ tools from MemoryAgent and CodingOrchestrator.

## Usage

Just describe what you want in natural language - the AI figures out which tools to call!

## Common Examples

### 🔍 Code Search & Understanding
```
execute_task(request: "Find all authentication code")
execute_task(request: "Explain how the API works")
execute_task(request: "Show me error handling examples")
```

### 🏗️ Code Generation
```
execute_task(request: "Create a REST API for user management in Python")
execute_task(request: "Generate React components for user profiles")
execute_task(request: "Write unit tests for Calculator class")
```

### ✅ Analysis & Validation
```
execute_task(request: "Analyze security vulnerabilities")
execute_task(request: "Check code complexity and suggest refactoring")
execute_task(request: "Validate all imports")
```

### 🎨 Design & Branding
```
execute_task(request: "Create a brand system for my healthcare app")
execute_task(request: "Validate this UI code against brand guidelines")
```

### 📋 Planning & Management
```
execute_task(request: "Create implementation plan for user authentication")
execute_task(request: "Check status of all coding tasks")
```

## What Happens Internally

1. **FunctionGemma** analyzes your natural language request
2. Creates an **execution plan** with the right tools
3. Executes tools from **MemoryAgent & CodingOrchestrator**
4. Returns **complete results**

## You Don't Need To Know:
- Which tools exist
- Which service provides them
- What order to call them in
- How to pass parameters

**Just describe what you want!** The AI handles the rest.


