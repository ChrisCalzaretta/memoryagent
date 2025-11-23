# Pattern Validation & Recommendation System 🔍

## Overview

The Memory Agent now includes a **comprehensive pattern validation and recommendation system** that provides:

1. **Quality Scoring** (1-10 grades A-F) for detected patterns
2. **Security Auditing** with vulnerability detection
3. **Anti-Pattern Detection** for poorly implemented code
4. **Migration Guidance** for legacy frameworks
5. **Architecture Recommendations** based on missing best practices
6. **Auto-Fix Code Generation** for common issues

---

## 🎯 5 New MCP Tools for Pattern Validation

### 1. `validate_pattern_quality`
**Deep validation of a specific pattern's implementation quality**

**Input:**
- `pattern_id`: Pattern ID to validate
- `context`: Project context (optional)
- `include_auto_fix`: Include auto-fix code (default: true)
- `min_severity`: Minimum severity to report (low|medium|high|critical)

**Output:**
- Quality Score (0-10)
- Grade (A-F)
- Security Score (0-10)
- List of issues with severity and fix guidance
- Auto-fix code (if available)
- Recommendations

**Example Use:**
```json
{
  "pattern_id": "CacheAside_GetUserById",
  "context": "MyProject",
  "include_auto_fix": true,
  "min_severity": "low"
}
```

**Sample Output:**
```
🔍 Pattern Quality Validation

Pattern: CacheAside_TryGetValue
Quality Score: 7/10 (Grade: C)
Security Score: 8/10

❌ Issues Found:

🚨 CRITICAL: No cache expiration policy set - risk of stale data and memory leaks
   💡 Fix: Add AbsoluteExpirationRelativeToNow or SlidingExpiration to cache options

⚠️ MEDIUM: No concurrency protection - race condition possible with multiple threads
   💡 Fix: Use lock, SemaphoreSlim, or distributed lock for thread safety

🔧 Auto-Fix Code:

_cache.Set(key, value, new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    SlidingExpiration = TimeSpan.FromMinutes(1)
});
```

---

### 2. `find_anti_patterns`
**Find all anti-patterns and poorly implemented patterns in a project**

**Input:**
- `context`: Project context to search
- `min_severity`: Minimum severity (low|medium|high|critical, default: medium)
- `include_legacy`: Include legacy/deprecated patterns (default: true)

**Output:**
- Total anti-patterns found
- Critical issues count
- Overall security score
- List of anti-patterns with details

**What It Detects:**
- Patterns with quality score < 5
- Patterns with critical security issues
- Legacy/deprecated patterns (AutoGen, old Semantic Kernel Planners)
- Missing essential features (expiration, null checks, logging)
- Performance anti-patterns
- Security vulnerabilities

**Example:**
```json
{
  "context": "MyProject",
  "min_severity": "medium",
  "include_legacy": true
}
```

**Sample Output:**
```
🚨 Anti-Pattern Analysis for MyProject

Total Anti-Patterns Found: 3
Critical Issues: 1
Overall Security Score: 6/10

📋 Anti-Patterns Detected:

• AutoGen_CodeExecution (Score: 2/10)
  File: Services/AgentService.cs
  🚨 CRITICAL: Code execution without sandboxing - CRITICAL security risk

• CacheAside_LoadUsers (Score: 4/10)
  File: Repositories/UserRepository.cs
  ❗ HIGH: No cache expiration policy set

• Retry_ExternalApi (Score: 5/10)
  File: Services/ApiClient.cs
  ⚠️ MEDIUM: Retry policy without exponential backoff
```

---

### 3. `validate_security`
**Security audit of detected patterns**

**Input:**
- `context`: Project context to validate
- `pattern_types`: Specific pattern types to check (optional, defaults to high-risk types)

**Output:**
- Overall security score (0-10)
- Security grade (A-F)
- List of vulnerabilities with severity
- CWE/CVE references
- Remediation steps

**High-Risk Patterns Checked:**
- Security patterns
- AutoGen patterns (code execution risks)
- API design patterns
- Validation patterns

**Example:**
```json
{
  "context": "MyProject"
}
```

**Sample Output:**
```
🔒 Security Validation for MyProject

Security Score: 7/10 (C (Fair))
Vulnerabilities Found: 2

🚨 Security Vulnerabilities:

🚨 CRITICAL - AutoGen_CodeExecution
  Description: Code execution without sandboxing - CRITICAL security risk
  File: Services/AgentService.cs
  Reference: CWE-94: Improper Control of Generation of Code
  🔧 Remediation: Implement Docker/container sandboxing or migrate to Agent Framework with MCP

❗ HIGH - ApiEndpoint_Upload
  Description: No input validation - risk of injection attacks or crashes
  File: Controllers/UploadController.cs
  Reference: CWE-20: Improper Input Validation
  🔧 Remediation: Validate all user inputs before processing

📋 Priority Remediation Steps:
• AutoGen_CodeExecution: Implement Docker/container sandboxing or migrate to Agent Framework with MCP
• ApiEndpoint_Upload: Validate all user inputs before processing
```

---

### 4. `get_migration_path`
**Step-by-step migration guidance for legacy/deprecated patterns**

**Input:**
- `pattern_id`: Pattern ID to get migration path for
- `include_code_example`: Include before/after code example (default: true)

**Output:**
- Current pattern name
- Target pattern name
- Migration status (Recommended|Deprecated|Critical|Optional)
- Effort estimate
- Complexity (Low|Medium|High)
- Step-by-step migration instructions
- Before/after code examples
- Benefits of migrating
- Risks of NOT migrating

**Supported Migrations:**
- AutoGen → Agent Framework
- Semantic Kernel Planners → Agent Framework Workflows
- (More coming soon)

**Example:**
```json
{
  "pattern_id": "AutoGen_ConversableAgent_ChatBot",
  "include_code_example": true
}
```

**Sample Output:**
```
🔄 Migration Path

Current Pattern: AutoGen ConversableAgent
Target Pattern: Agent Framework Workflow
Status: Critical
Effort Estimate: 2-4 hours
Complexity: Medium

📋 Migration Steps:

1. Create Workflow Class
   Create new class inheriting from Workflow<TInput, TOutput>
   Files: Workflows/MyWorkflow.cs

2. Define Input/Output Types
   Create strongly-typed input and output records

3. Implement ExecuteAsync
   Move AutoGen logic to workflow ExecuteAsync method

4. Register in DI
   Add services.AddSingleton<MyWorkflow>() to Program.cs
   Files: Program.cs

5. Update Calling Code
   Replace AutoGen calls with workflow.ExecuteAsync(input)

6. Test & Remove
   Test thoroughly, then remove AutoGen references

💡 Code Example:

Before:
// AutoGen (Legacy)
var agent = new ConversableAgent("assistant");
var response = await agent.GenerateReplyAsync(messages);

After:
// Agent Framework
public class MyWorkflow : Workflow<MyInput, MyOutput>
{
    protected override async Task<MyOutput> ExecuteAsync(
        MyInput input, CancellationToken cancellationToken)
    {
        var agent = new ChatCompletionAgent(...);
        var response = await agent.InvokeAsync(input.Message);
        return new MyOutput { Response = response };
    }
}

✅ Benefits:
• Type-safe execution (no runtime errors)
• Deterministic workflows (easier debugging)
• Better observability (built-in telemetry)
• Enterprise features (checkpointing, state management)
• Active support and updates

⚠️ Risks of NOT Migrating:
• AutoGen is deprecated and will not receive updates
• Non-deterministic execution makes debugging hard
• No type safety leads to runtime errors
• Limited enterprise features
```

---

### 5. `validate_project`
**Comprehensive project validation - All patterns, all checks**

**Input:**
- `context`: Project context to validate

**Output:**
- Overall quality score (0-10)
- Overall security score (0-10)
- Total patterns detected
- Patterns by grade (A-F breakdown)
- Critical issues list
- Security vulnerabilities list
- Legacy patterns needing migration
- Top recommendations

**Example:**
```json
{
  "context": "MyProject"
}
```

**Sample Output:**
```
📊 Project Validation Report - MyProject

Overall Quality Score: 7/10
Security Score: 6/10
Total Patterns: 42

📈 Patterns by Grade:
  Grade A: 12 patterns
  Grade B: 15 patterns
  Grade C: 10 patterns
  Grade D: 3 patterns
  Grade F: 2 patterns

🚨 Critical Issues (3):
  • No cache expiration policy in CacheAside_LoadUsers
  • Code execution without sandboxing in AutoGen_CodeExecution
  • Missing null check in Retry_ApiCall

🔒 Security Vulnerabilities (2):
  CRITICAL: Code execution without sandboxing
  HIGH: No input validation before processing

⚠️ Legacy Patterns Needing Migration (1):
  • AutoGen ConversableAgent → Agent Framework Workflow (2-4 hours)

📋 Top Recommendations:
  🚨 Fix 3 critical issues immediately
  🔒 Address 2 security vulnerabilities
  ⚠️ Migrate 1 legacy pattern to modern frameworks
  📉 Improve 2 patterns with quality score below 5

Summary: Project Score: 7/10, Security: 6/10, 42 patterns (3 critical issues)
Generated: 2025-11-23 10:45:00 UTC
```

---

## 💡 Recommendation System (Already Existing!)

### `get_recommendations`
**Analyzes a project and provides prioritized recommendations for missing or weak patterns**

**Input:**
- `context`: Project context to analyze
- `categories`: Focus on specific categories (optional)
- `include_low_priority`: Include low-priority recommendations (default: false)
- `max_recommendations`: Maximum recommendations to return (default: 10)

**Output:**
- Overall health score
- Total patterns detected
- Prioritized recommendations (Critical → High → Medium → Low)
- Each recommendation includes:
  - Issue description
  - Category (Security, Performance, Reliability, etc.)
  - Specific recommendation
  - Impact assessment
  - Azure best practice URL
  - **CODE EXAMPLE** showing how to implement it

**What It Analyzes:**
- Missing caching patterns → Performance issues
- Missing retry/circuit breaker → Reliability issues
- Missing validation → Security issues
- Missing authentication/authorization → Security issues
- Missing health checks → Operational issues
- Missing logging → Maintainability issues
- And 20+ more Azure best practices!

**Example Output:**
```
🎯 Architecture Recommendations for 'MyProject'

Overall Health: 65 %
Patterns Detected: 12
Recommendations: 5

🚨 CRITICAL PRIORITY:

• No input validation detected
  Category: Security (Validation)
  Recommendation: Add DataAnnotations or FluentValidation to validate user inputs
  Impact: Missing validation can lead to security vulnerabilities
  📚 Learn more: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation

  CODE EXAMPLE:
  // Add input validation
  public class CreateUserRequest
  {
      [Required]
      [StringLength(100, MinimumLength = 2)]
      public string Name { get; set; }

      [Required]
      [EmailAddress]
      public string Email { get; set; }
  }

⚠️ HIGH PRIORITY:

• No retry logic detected in external service calls
  Category: Reliability (Resilience)
  Recommendation: Add Polly retry policies for transient fault handling
  Impact: Without retry logic, transient failures will cause user-facing errors
  📚 Learn more: https://learn.microsoft.com/en-us/azure/architecture/patterns/retry

  CODE EXAMPLE:
  // Add Polly retry policy
  services.AddHttpClient<IMyService, MyService>()
      .AddTransientHttpErrorPolicy(policy =>
          policy.WaitAndRetryAsync(3, retryAttempt =>
              TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
```

---

## 📊 Pattern Quality Scoring System

### How Patterns Are Scored (0-10 scale)

Each pattern starts with a perfect score of **10** and loses points for issues:

| Issue Severity | Points Deducted | Example |
|---------------|-----------------|---------|
| **Critical** | -3 to -5 | No input validation, code execution without sandboxing |
| **High** | -2 | Missing null checks, no retry policy |
| **Medium** | -1 to -2 | No concurrency protection, missing logging |
| **Low** | -1 | Cache keys not prefixed, generic catch blocks |

### Grade Mapping

| Score | Grade | Meaning |
|-------|-------|---------|
| 9-10 | A | Excellent - Best practice implementation |
| 8 | B | Good - Minor improvements needed |
| 7 | C | Fair - Several issues to address |
| 6 | D | Poor - Major improvements required |
| 0-5 | F | Failing - Critical issues present |

---

## 🔍 Pattern-Specific Validation Rules

### Caching Patterns
✅ **Checks for:**
- Cache expiration policy (AbsoluteExpiration or SlidingExpiration)
- Null checks after data fetch
- Concurrency protection (lock, SemaphoreSlim)
- Cache key prefixing

⚠️ **Common Issues:**
- Missing expiration → Risk of stale data and memory leaks
- No null check → Can cache null values
- No concurrency protection → Race conditions
- No key prefix → Key collisions

### Resilience Patterns (Retry, Circuit Breaker)
✅ **Checks for:**
- Exponential backoff in retry policies
- Circuit breaker for fail-fast
- Logging of retry attempts

⚠️ **Common Issues:**
- No exponential backoff → Can overwhelm failing service
- No circuit breaker → Won't fail fast during outages
- No logging → Hard to diagnose issues

### Agent Framework Patterns
✅ **Checks for:**
- Timeout configuration or CancellationToken
- Retry policy for resilience
- Input validation for security
- Telemetry/logging

⚠️ **Common Issues:**
- No timeout → Agent calls can hang indefinitely
- No input validation → Injection attack risks
- No logging → Hard to monitor performance

### AutoGen Patterns (Legacy)
🚨 **All AutoGen patterns are flagged as legacy/deprecated**

**Special Checks:**
- Code execution patterns get **CRITICAL** security flag
- Migration path is automatically provided
- Score starts at 2/10 (low due to legacy status)

### Security Patterns
✅ **Additional security checks:**
- Authentication/authorization presence
- Data encryption
- Input sanitization
- Secure coding practices

---

## 🎯 How to Use the System

### 1. **After Indexing a Project**
```bash
# Index your project
.\start-project.ps1 -ProjectPath "E:\GitHub\MyProject" -AutoIndex

# Wait for indexing to complete
```

### 2. **Get Overall Project Health**
```json
// MCP Call: validate_project
{
  "context": "MyProject"
}
```

### 3. **Get Architecture Recommendations**
```json
// MCP Call: get_recommendations
{
  "context": "MyProject",
  "include_low_priority": false,
  "max_recommendations": 10
}
```

### 4. **Find Critical Security Issues**
```json
// MCP Call: validate_security
{
  "context": "MyProject"
}
```

### 5. **Find Anti-Patterns**
```json
// MCP Call: find_anti_patterns
{
  "context": "MyProject",
  "min_severity": "high"
}
```

### 6. **Validate Specific Pattern**
```json
// MCP Call: validate_pattern_quality
{
  "pattern_id": "CacheAside_GetUserById",
  "context": "MyProject",
  "include_auto_fix": true
}
```

### 7. **Get Migration Guidance**
```json
// MCP Call: get_migration_path
{
  "pattern_id": "AutoGen_ChatAgent",
  "include_code_example": true
}
```

---

## 🚀 Integration with Cursor

All tools are available in Cursor via the MCP protocol. Just ask:

- "Validate the quality of all caching patterns in my project"
- "Find any anti-patterns or security issues"
- "Show me how to migrate from AutoGen to Agent Framework"
- "What architecture improvements should I make?"
- "Give me recommendations for improving code quality"

---

## 📈 Future Enhancements (From Roadmap)

See `PATTERN_VALIDATION_ROADMAP.md` for detailed plans:

1. **Pattern Relationship Validation**
   - Check if complementary patterns are used together
   - Example: RL training should have reward signals

2. **Configuration Validation**
   - Validate configuration values (not just presence)
   - Check for best practice configs (e.g., retry count, timeout values)

3. **Performance Validation**
   - Detect performance anti-patterns
   - Example: Buffering before streaming

4. **Advanced Auto-Fix**
   - Generate full code snippets for complex fixes
   - Multi-file refactoring suggestions

5. **Migration Complexity Analysis**
   - Estimate migration effort based on codebase analysis
   - Identify blockers and dependencies

---

## 📊 Summary

The Pattern Validation & Recommendation System provides:

| Feature | Status | Details |
|---------|--------|---------|
| **Quality Scoring** | ✅ Implemented | 1-10 scores with A-F grades |
| **Security Auditing** | ✅ Implemented | CWE references, remediation steps |
| **Anti-Pattern Detection** | ✅ Implemented | 10+ pattern types |
| **Migration Paths** | ✅ Implemented | AutoGen, Semantic Kernel |
| **Auto-Fix Code** | ✅ Implemented | For common caching/retry issues |
| **Architecture Recommendations** | ✅ Implemented | 21 Azure best practices |
| **MCP Tools** | ✅ Implemented | 5 new validation tools |
| **Code Examples** | ✅ Implemented | In all recommendations |

**Total Patterns Detected:** 93 (60 AI Agent + 33 Azure Best Practices)

**Validation Coverage:**
- ✅ Caching patterns (6 validation rules)
- ✅ Resilience patterns (4 validation rules)
- ✅ Agent Framework patterns (5 validation rules)
- ✅ Security patterns
- ✅ AutoGen patterns (with migration paths)
- ✅ Error handling patterns
- ✅ And more...

This system transforms the Memory Agent from a **code index** into an **intelligent architecture advisor**! 🚀

