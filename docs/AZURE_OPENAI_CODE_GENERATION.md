# Azure OpenAI Code Generation for Memory Agent

## 🎯 **Overview**

High-quality code generation service using Azure OpenAI GPT-4o for the Memory Agent MCP server.

---

## 🤖 **Model Selection**

### **Primary: GPT-4o**
- **Use for:** Complex refactoring, migrations, architectural changes
- **Quality:** Best available (10/10)
- **Speed:** 2-5 seconds
- **Cost:** $2.50 per million input tokens, $10 per million output tokens

### **Secondary: GPT-4o-mini**
- **Use for:** Auto-fixes, simple pattern implementation
- **Quality:** Excellent for straightforward tasks (8/10)
- **Speed:** 1-3 seconds
- **Cost:** $0.15 per million input tokens, $0.60 per million output tokens (10x cheaper)

### **Decision Logic**
```csharp
if (task.Complexity == "simple" && task.LinesOfCode < 50)
    → Use GPT-4o-mini (cheap, fast enough)
else if (task.RequiresDeepReasoning || task.LinesOfCode > 100)
    → Use GPT-4o (best quality)
```

---

## 💰 **Cost Estimation**

### **Monthly Usage Scenarios**

| Scenario | Model | Monthly Cost |
|----------|-------|--------------|
| **100 code generations/month** (avg 500 lines) | GPT-4o | $5-10 |
| **100 code generations/month** (avg 500 lines) | GPT-4o-mini | $0.50-1 |
| **500 code generations/month** | GPT-4o | $25-50 |
| **500 code generations/month** | GPT-4o-mini | $2.50-5 |
| **Auto-fix on every file save** (aggressive) | GPT-4o-mini | $50-100 |

**Recommendation:** Use GPT-4o-mini for 80% of tasks, GPT-4o for 20% complex tasks.

---

## 🏗️ **Architecture**

### **Services to Build**

1. **`AzureOpenAIService.cs`**
   - Core LLM communication
   - Handles API calls, retries, error handling
   - Temperature/token management

2. **`CodeGenerationService.cs`**
   - High-level orchestrator
   - Builds prompts with codebase context
   - Validates generated code
   - Model selection logic

3. **`CodeGenerationContextBuilder.cs`**
   - Gathers context from codebase
   - Finds Grade A pattern examples
   - Extracts validation issues
   - Discovers dependencies

---

## 🎯 **Features to Implement**

### **Priority 1: Must Have**

#### **1. Auto-Fix Validation Issues**
```json
{
  "tool": "auto_fix_validation",
  "input": {
    "filePath": "/workspace/Services/UserService.cs",
    "methodName": "GetUserAsync",
    "issues": [
      { "severity": "High", "message": "Missing cache expiration" }
    ]
  },
  "model": "gpt-4o-mini"
}
```

**Use Case:** After validation detects issues, automatically generate fix code.

---

#### **2. Pattern Implementation**
```json
{
  "tool": "implement_pattern",
  "input": {
    "filePath": "/workspace/Services/UserService.cs",
    "methodName": "GetUserAsync",
    "pattern": "Caching",
    "requirements": [
      "Add IMemoryCache",
      "5-minute absolute expiration",
      "Include null check",
      "Add logging"
    ]
  },
  "model": "gpt-4o"
}
```

**Use Case:** "Add caching to this method", "Add retry logic to HTTP call"

---

#### **3. Integration Test Generation**
```json
{
  "tool": "generate_test",
  "input": {
    "filePath": "/workspace/Services/UserService.cs",
    "methodName": "GetUserAsync",
    "testType": "integration",
    "scenarios": ["happy_path", "null_handling", "cache_hit", "cache_miss"]
  },
  "model": "gpt-4o"
}
```

**Use Case:** Auto-generate integration tests for every new method.

---

### **Priority 2: Nice to Have**

#### **4. Refactoring**
- Extract method
- Reduce complexity
- Simplify nested logic
- Split large files (>800 LOC)

#### **5. Pattern Migration**
- AutoGen → Agent Framework
- Old Semantic Kernel Planners → Agent Framework Workflows
- Legacy patterns → Modern patterns

#### **6. Documentation Generation**
- XML documentation comments
- README sections
- API reference

---

## 📝 **Prompt Engineering Strategy**

### **Prompt Template**

```csharp
public class CodeGenerationPrompt
{
    // System prompt (sets behavior)
    public string SystemPrompt => @"
You are an expert C# developer specializing in Azure best practices.
Generate production-ready code that follows these rules:
- Include error handling (try/catch)
- Add logging where appropriate
- Follow naming conventions (PascalCase for methods, camelCase for variables)
- Add XML documentation comments
- Use async/await for I/O operations
- Include timeout/cancellation token support
Output ONLY the code, no explanations or markdown.
";

    // User prompt (specific task)
    public string BuildUserPrompt(CodeGenerationRequest request)
    {
        return $@"
Task: {request.Task}

Current Code:
```csharp
{request.ExistingCode}
```

Pattern to Apply: {request.PatternName}
Best Practice: {request.BestPractice}

Context from Codebase (Grade A Examples):
{request.RelevantExamples}

Validation Issues to Fix:
{string.Join("\n", request.Issues)}

Requirements:
{string.Join("\n", request.Requirements)}

Generate the complete updated method/class.
";
    }
}
```

---

## 🔍 **Context Gathering**

### **What Information to Provide to LLM**

1. **Existing Code** (the method/class being modified)
2. **Pattern Details** (from pattern detection)
3. **Validation Issues** (what needs fixing)
4. **Similar Examples** (Grade A patterns from codebase)
5. **File Context** (imports, dependencies, using statements)
6. **Dependencies** (from Neo4j graph)

### **How to Gather Context**

```csharp
public async Task<CodeGenerationContext> BuildContextAsync(
    string filePath,
    string methodName,
    string patternToApply)
{
    // 1. Get existing code from file
    var parseResult = await _codeParser.ParseFileAsync(filePath);
    var method = parseResult.CodeElements
        .FirstOrDefault(e => e.Name == methodName);
    
    // 2. Find validation issues
    var validation = await _validation.ValidatePatternQualityAsync(
        patternId: $"{filePath}_{methodName}",
        includeAutoFix: false
    );
    
    // 3. Search for similar high-quality examples (Grade A only)
    var examples = await _search.SmartSearchAsync(
        query: $"show me examples of {patternToApply} pattern with Grade A quality",
        limit: 3
    );
    
    var highQualityExamples = examples.Results
        .Where(r => r.Metadata.ContainsKey("quality_grade") 
                 && r.Metadata["quality_grade"].ToString() == "A")
        .ToList();
    
    // 4. Get dependency information from Neo4j
    var dependencies = await _graph.GetDependenciesAsync(
        className: method.ClassName,
        maxDepth: 1
    );
    
    return new CodeGenerationContext
    {
        ExistingCode = method.Content,
        Issues = validation.Issues,
        Examples = highQualityExamples.Select(e => e.Content).ToList(),
        Dependencies = dependencies,
        PatternType = patternToApply,
        BestPractice = GetBestPracticeDescription(patternToApply)
    };
}
```

---

## 🛠️ **Implementation Steps**

### **Step 1: Azure OpenAI Setup**

**Required:**
- Azure OpenAI resource
- GPT-4o deployment
- API key

**Configuration (`appsettings.json`):**
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentNameGpt4o": "gpt-4o",
    "DeploymentNameGpt4oMini": "gpt-4o-mini",
    "MaxTokens": 4096,
    "Temperature": 0.2
  }
}
```

---

### **Step 2: Build Services**

1. Create `AzureOpenAIService.cs`
2. Create `CodeGenerationService.cs`
3. Create `CodeGenerationContextBuilder.cs`
4. Register services in `Program.cs`

---

### **Step 3: Add MCP Tools**

#### **Tool 1: `generate_code`**
```json
{
  "name": "generate_code",
  "description": "Generate high-quality code using Azure OpenAI",
  "parameters": {
    "filePath": "string",
    "methodName": "string (optional)",
    "task": "string (e.g., 'add caching', 'add retry logic')",
    "requirements": ["array of specific requirements"]
  }
}
```

#### **Tool 2: `auto_fix`**
```json
{
  "name": "auto_fix",
  "description": "Automatically fix validation issues",
  "parameters": {
    "patternId": "string",
    "issues": ["array of issues from validation"]
  }
}
```

#### **Tool 3: `refactor_code`**
```json
{
  "name": "refactor_code",
  "description": "Refactor code to improve quality",
  "parameters": {
    "filePath": "string",
    "methodName": "string",
    "refactoringType": "extract_method | reduce_complexity | simplify"
  }
}
```

#### **Tool 4: `migrate_pattern`**
```json
{
  "name": "migrate_pattern",
  "description": "Migrate from legacy pattern to modern pattern",
  "parameters": {
    "filePath": "string",
    "fromPattern": "AutoGen | SemanticKernelPlanner",
    "toPattern": "AgentFramework"
  }
}
```

---

## ✅ **Quality Assurance**

### **Validation Pipeline**

After generating code:

1. **Syntax Check** - Compile with Roslyn
2. **Pattern Validation** - Run `validate_pattern_quality`
3. **Security Check** - Run `validate_security`
4. **Score Threshold** - Require score >= 7 (Grade C or better)

```csharp
public async Task<CodeGenerationResult> GenerateAndValidateAsync(...)
{
    // Generate code
    var generatedCode = await _azureOpenAI.GenerateAsync(prompt);
    
    // Validate syntax
    var isValid = await ValidateSyntax(generatedCode);
    if (!isValid) return Error("Syntax error");
    
    // Write to temp file and validate pattern
    var tempFile = WriteToTempFile(generatedCode);
    await _indexingService.IndexFileAsync(tempFile);
    
    var validation = await _patternValidation.ValidatePatternQualityAsync(
        patternId: tempFile,
        includeAutoFix: false
    );
    
    // Require minimum score
    if (validation.Score < 7)
    {
        return Error($"Generated code score too low: {validation.Score}/10");
    }
    
    return Success(generatedCode, validation);
}
```

---

## 🚀 **Getting Started**

### **Prerequisites**

1. Azure OpenAI resource with GPT-4o deployment
2. API key from Azure portal

### **Quick Setup**

1. Add Azure OpenAI configuration to `appsettings.json`
2. Install NuGet package: `Azure.AI.OpenAI`
3. Implement `AzureOpenAIService.cs`
4. Add MCP tool endpoints
5. Test with simple code generation task

---

## 📊 **Success Metrics**

Track:
- **Generated code quality scores** - Target: avg 8+/10
- **Auto-fix success rate** - Target: 90%+
- **Pattern implementation accuracy** - Target: 95%+
- **Cost per generation** - Target: < $0.10 avg
- **Generation time** - Target: < 5 seconds

---

## 🎯 **Next Steps**

### **Immediate (Week 1)**
1. Set up Azure OpenAI resource
2. Implement `AzureOpenAIService.cs`
3. Build one MCP tool (`auto_fix`)
4. Test with validation issues

### **Short Term (Week 2-3)**
1. Add `generate_code` tool
2. Implement context builder
3. Add pattern implementation
4. Build validation pipeline

### **Medium Term (Month 1)**
1. Add test generation
2. Implement refactoring
3. Build migration tools
4. Optimize prompts based on results

---

## 💡 **Best Practices**

1. **Always include Grade A examples** from codebase in prompts
2. **Use low temperature** (0.1-0.3) for deterministic code
3. **Validate all generated code** before presenting to user
4. **Cache frequent patterns** to reduce API calls
5. **Log all generations** for prompt improvement
6. **Monitor costs** and adjust model selection

---

## 🔗 **References**

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure OpenAI Pricing](https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/)
- [GPT-4o Model Card](https://platform.openai.com/docs/models/gpt-4o)
- [Memory Agent Pattern Validation](./PATTERN_VALIDATION_AND_RECOMMENDATIONS.md)

---

**Status:** Planning Phase  
**Priority:** High  
**Estimated Effort:** 2-3 weeks  
**Owner:** TBD  
**Last Updated:** 2025-11-24


