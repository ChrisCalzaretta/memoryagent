# 🧠 LLM-Powered Intent Classification System

## Overview

The **Intent Classification System** uses DeepSeek Coder (via Ollama) to understand natural language requests and automatically suggest relevant architecture patterns, categories, and best practices.

---

## 🎯 What Problem Does This Solve?

### ❌ Before (Manual)
```
User: "get_recommendations --context my_app"
System: "Which categories do you want? (Performance, Security, AIAgents, etc.)"
User: "Uhh... not sure. Let me think..."
```

### ✅ After (AI-Powered)
```
User: "get_recommendations --context my_app --user_goal 'make my Flutter app more secure'"

🧠 AI Analysis:
  - Project Type: MobileApp
  - Primary Goal: Security
  - Technologies: [Flutter, Dart]
  - Auto-Suggested Categories: [Security, Validation, UserExperience]
  
🎯 Here are 8 critical security recommendations...
```

---

## 🚀 Features

### 1. **Automatic Intent Detection**
- Classifies project type (MobileApp, WebAPI, AIAgent, etc.)
- Determines primary goal (Security, Performance, Migration, etc.)
- Identifies technologies (Flutter, Dart, C#, AI, Blazor, etc.)
- Estimates complexity (Simple, Medium, Complex, Enterprise)

### 2. **Smart Category Suggestions**
- Maps user goals to relevant pattern categories
- Considers project type and technologies
- Provides confidence scores (0.0 to 1.0)

### 3. **Best Practice Recommendations**
- Technology-specific practices (e.g., Flutter null safety, Microsoft.Extensions.AI patterns)
- Goal-specific practices (e.g., input validation for security)
- Domain-specific practices (e.g., e-commerce, healthcare)

---

## 📋 Supported Enums

### ProjectType
```csharp
- Unknown
- MobileApp          // Flutter, React Native, Xamarin
- WebAPI             // ASP.NET Core API, FastAPI, Express
- AIAgent            // Agent-based systems
- WebApp             // Blazor, React, Angular
- DesktopApp         // WPF, WinForms, Electron
- BackendService     // Microservices, workers
- Library            // Reusable packages
- DataPipeline       // ETL, data processing
- MicroService       // Containerized service
```

### UserGoal
```csharp
- Unknown
- Performance        // Optimize speed, reduce latency
- Security           // Add auth, prevent attacks
- Refactoring        // Clean up code, improve architecture
- NewFeature         // Build new functionality
- BugFix             // Fix existing issues
- Migration          // Upgrade framework/library
- Testing            // Add test coverage
- Observability      // Add logging, monitoring
- Scalability        // Handle more load
- CostOptimization   // Reduce cloud costs
```

### ComplexityLevel
```csharp
- Simple      // 1-2 files, < 1 hour
- Medium      // 3-10 files, 1-4 hours
- Complex     // 10-50 files, 1-3 days
- Enterprise  // 50+ files, 1+ weeks
```

---

## 🛠️ Integrated Tools

### 1. `create_plan` (Enhanced)

**New Parameters:**
- `include_recommendations` (boolean): Auto-generate tasks from architecture analysis
- `max_recommendations` (number): Max recommended tasks to add (default: 10)
- `recommendation_categories` (array): Filter by category (auto-suggested if not provided)

**Example 1: AI-Driven Plan**
```bash
mcp_code-memory_create_plan \
  --context "my_flutter_app" \
  --name "Q1 2025 Security Hardening" \
  --include_recommendations true \
  --max_recommendations 10
```

**Output:**
```
🧠 Intent classified: MobileApp / Security / Tech: [Flutter, Dart] / Confidence: 95%
🤖 Using AI-suggested categories: Security, Validation, UserExperience

✅ Development Plan created!

Tasks: 10 total (all AI-recommended)
1. [Security] Replace hardcoded API keys with flutter_secure_storage
2. [Security] Add HTTPS cert pinning for API calls
3. [Validation] Use flutter_form validation for user inputs
4. [Security] Implement biometric authentication
... 6 more tasks
```

**Example 2: Hybrid Plan (Manual + AI)**
```bash
mcp_code-memory_create_plan \
  --context "order_service" \
  --name "Performance Optimization Sprint" \
  --description "Reduce API latency from 500ms to <100ms" \
  --tasks '[{"title": "Profile slow endpoints", "description": "Use dotnet-trace"}]' \
  --include_recommendations true \
  --max_recommendations 5
```

**Output:**
```
🧠 Intent classified: WebAPI / Performance / Tech: [CSharp] / Confidence: 92%
🤖 Using AI-suggested categories: Performance, ResiliencyPatterns

✅ Development Plan created!

Tasks: 6 total
- 1 manual task (Profile slow endpoints)
- 5 AI-recommended tasks:
  1. [Performance] Add distributed caching with Redis
  2. [Performance] Implement response caching for GET endpoints
  3. [ResiliencyPatterns] Add connection pooling for database
  4. [Performance] Use async/await for all I/O operations
  5. [Performance] Add lazy loading for related entities
```

---

### 2. `get_recommendations` (Enhanced)

**New Parameter:**
- `user_goal` (string): Natural language description of what you want to achieve

**Example 1: Natural Language Goal**
```bash
mcp_code-memory_get_recommendations \
  --context "my_dotnet_app" \
  --user_goal "migrate from Semantic Kernel to Microsoft.Extensions.AI"
```

**Output:**
```
🧠 Classifying user intent from goal: 'migrate from Semantic Kernel to Microsoft.Extensions.AI'
🎯 Detected: AIAgent / Migration / Tech: [CSharp, AI, Microsoft.Extensions.AI] / Confidence: 95%
🤖 AI-suggested 3 categories: AIAgents, ToolIntegration, DependencyInjection

🎯 Architecture Recommendations for 'my_dotnet_app'

Overall Health: 72%
Patterns Detected: 45
Recommendations: 7

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🚨 CRITICAL PRIORITY:

• Migrate Kernel Planners to IChatClient
  📝 AutoGen.Core and SemanticKernel Planners are deprecated. Use Microsoft.Extensions.AI IChatClient with function calling.
  🎯 Impact: Avoid using unsupported frameworks
  🔗 Docs: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
  
... more recommendations

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🧠 AI Intent Classification:

  Project Type: AIAgent
  Primary Goal: Migration
  Technologies: CSharp, AI, Microsoft.Extensions.AI
  Complexity: Medium
  Confidence: 95%
  
  🎯 AI-Suggested Categories (3): AIAgents, ToolIntegration, DependencyInjection
```

**Example 2: Simple Goal**
```bash
mcp_code-memory_get_recommendations \
  --context "ecommerce_api" \
  --user_goal "make it faster and handle more traffic"
```

**Output:**
```
🧠 Detected: WebAPI / Performance + Scalability / Tech: [CSharp] / Confidence: 88%
🤖 AI-suggested 4 categories: Performance, ResiliencyPatterns, DistributedSystems

Recommendations:
1. [CRITICAL] Add distributed caching (Redis/Memcached)
2. [HIGH] Implement circuit breakers for external services
3. [HIGH] Add database connection pooling
4. [HIGH] Use async/await for all I/O operations
5. [MEDIUM] Implement rate limiting to prevent abuse
```

---

## 🏗️ Architecture

### Components

```
┌─────────────────────────────────────────────────────────┐
│                     MCP Tools                            │
│  (create_plan, get_recommendations, smartsearch, etc.)  │
└──────────────────┬──────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────┐
│          IntentClassificationService                     │
│                                                          │
│  • ClassifyIntentAsync()                                │
│  • SuggestPatternCategoriesAsync()                      │
│  • SuggestBestPracticesAsync()                          │
└──────────────────┬──────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────┐
│                  LLMService                              │
│        (DeepSeek Coder via Ollama)                      │
└─────────────────────────────────────────────────────────┘
```

### Flow Diagram

```
1. User Request
   "create_plan --name 'Build secure Flutter app' --include_recommendations true"
   
2. IntentClassificationService.ClassifyIntentAsync()
   → Calls LLMService with structured prompt
   → Parses JSON response
   → Returns UserIntent object
   
3. IntentClassificationService.SuggestPatternCategoriesAsync()
   → Maps UserIntent to PatternCategory[]
   → Returns: [Security, StateManagement, UserExperience]
   
4. RecommendationService.AnalyzeAndRecommendAsync()
   → Queries graph database for detected patterns
   → Finds missing/weak patterns in suggested categories
   → Returns prioritized recommendations
   
5. PlanService.AddPlanAsync()
   → Converts recommendations to plan tasks
   → Stores in database
   → Returns created plan
```

---

## 🧪 Testing

### Manual Test Cases

**Test 1: Flutter Security**
```bash
# Input
mcp_code-memory_get_recommendations \
  --context "my_flutter_app" \
  --user_goal "add security features to my Flutter e-commerce app"

# Expected Classification
Project Type: MobileApp
Primary Goal: Security
Technologies: [Flutter, Dart]
Domain: ecommerce
Categories: [Security, Validation, UserExperience]

# Expected Recommendations
- Use flutter_secure_storage for tokens
- Add HTTPS cert pinning
- Implement input validation for forms
- Use encrypted SharedPreferences
```

**Test 2: API Performance**
```bash
# Input
mcp_code-memory_get_recommendations \
  --context "order_api" \
  --user_goal "my API is too slow, need to make it faster"

# Expected Classification
Project Type: WebAPI
Primary Goal: Performance
Technologies: [CSharp] (or inferred from context)
Categories: [Performance, ResiliencyPatterns]

# Expected Recommendations
- Add distributed caching
- Implement response caching
- Use async/await
- Add database connection pooling
```

**Test 3: AI Agent Migration**
```bash
# Input
mcp_code-memory_create_plan \
  --context "my_ai_agent" \
  --name "Migrate to Microsoft.Extensions.AI" \
  --include_recommendations true

# Expected Classification
Project Type: AIAgent
Primary Goal: Migration
Technologies: [CSharp, AI, Microsoft.Extensions.AI]
Categories: [AIAgents, ToolIntegration, DependencyInjection]

# Expected Plan Tasks
1. Replace Kernel Planners with IChatClient
2. Migrate ChatCompletionAgent to ChatClientBuilder
3. Update dependency injection configuration
4. Add OpenTelemetry middleware
5. Implement function calling with [Description] attributes
```

---

## 📊 Confidence Scoring

### High Confidence (>= 0.8)
- Specific technology mentions ("Flutter", "Microsoft.Extensions.AI")
- Clear goal keywords ("secure", "performance", "migrate")
- Explicit domain ("e-commerce", "healthcare")

### Medium Confidence (0.5 - 0.8)
- Generic terms ("app", "API")
- Vague goals ("improve", "better")
- No domain specified

### Low Confidence (< 0.5)
- Very short requests
- Ambiguous wording
- LLM parse failures → Falls back to keyword-based classification

---

## 🔧 Fallback Mechanism

If LLM classification fails (timeout, parse error, etc.), the system falls back to **keyword-based classification**:

```csharp
// Keyword matching
if (request.contains("flutter")) → ProjectType.MobileApp
if (request.contains("secure")) → UserGoal.Security
if (request.contains("api")) → ProjectType.WebAPI
...
```

**Fallback Confidence:** Always 0.5 (50%)

---

## 🎓 Prompt Engineering

### Classification Prompt Template

```
You are an expert software architect analyzing user intent.

USER REQUEST: "{userRequest}"
PROJECT CONTEXT: {context}

Analyze this request and classify the user's intent. Return ONLY valid JSON.

JSON Schema:
{
  "projectType": "MobileApp | WebAPI | AIAgent | ...",
  "primaryGoal": "Performance | Security | Migration | ...",
  "technologies": ["Flutter", "Dart", "CSharp", ...],
  "relevantCategories": ["Performance", "Security", ...],
  "domain": "ecommerce | healthcare | fintech | general",
  "complexity": "Simple | Medium | Complex | Enterprise",
  "confidence": 0.0-1.0,
  "reasoning": "Brief explanation"
}

EXAMPLES: (provided in prompt)

Now classify this request:
USER REQUEST: "{userRequest}"
Return ONLY the JSON object:
```

### Key Prompt Features
1. **Strict JSON output** (no markdown, no explanation)
2. **Enum constraints** (forces valid values)
3. **Few-shot examples** (teaches LLM the pattern)
4. **Confidence scoring** (self-assessment)
5. **Reasoning field** (for debugging)

---

## 🚀 Performance

### Benchmarks (Ollama with DeepSeek Coder)

| Operation | Average Time | Notes |
|-----------|--------------|-------|
| Intent Classification | ~2-4 seconds | LLM inference |
| Category Suggestion | ~50ms | Rule-based mapping |
| Fallback Classification | ~10ms | Keyword matching |

### Optimization Tips
1. **Cache common patterns** (e.g., "secure Flutter app")
2. **Use smaller LLM** for simple requests
3. **Timeout fallback** (3 seconds → keyword-based)

---

## 🎯 Future Enhancements

### Planned
- [ ] **Learning from user feedback** (track accepted/rejected suggestions)
- [ ] **Context-aware history** (remember past projects)
- [ ] **Multi-turn conversations** (refine intent iteratively)
- [ ] **Specialized domain models** (e-commerce vs healthcare patterns)
- [ ] **Confidence thresholds** (ask user for clarification if < 0.6)

### Experimental
- [ ] **Embedding-based similarity** (match to existing successful projects)
- [ ] **Code analysis integration** (infer intent from existing codebase)
- [ ] **A/B testing** (compare LLM vs keyword accuracy)

---

## 📚 Related Documentation
- [MCP Tools](./MCP_TOOLS.md)
- [Pattern Detection](./PATTERN_DETECTION.md)
- [Best Practices](./BEST_PRACTICES.md)
- [Recommendation System](./RECOMMENDATIONS.md)

---

## 🤝 Contributing

To add new intent patterns:

1. Update `UserGoal` or `ProjectType` enums in `UserIntent.cs`
2. Add mapping logic in `IntentClassificationService.cs`
3. Update prompt examples in `BuildIntentClassificationPrompt()`
4. Add test cases in `IntentClassificationTests.cs`
5. Update this documentation

---

**Last Updated:** December 4, 2025  
**Version:** 1.0.0

