# AI Development Rules for DataPrepPlatform

## 🚨 CRITICAL WORKFLOW - ALWAYS FOLLOW

When working with AI assistants on this codebase:

### Before ANY Code Changes:

1. **🔍 Search First** - Run `smartsearch` to understand existing implementation
2. **📊 Analyze Impact** - Run `dependency_chain`, `impact_analysis`, `find_circular_dependencies`
3. **💬 Discuss Plan** - Present findings and proposed solution to team lead
4. **✅ Get Approval** - Wait for explicit approval before implementing

### During Implementation:

- **Integration Tests ONLY** - No mocks allowed
- **Test Coverage Required** - Every method must have tests for:
  - Happy path
  - Error cases
  - Edge cases
  - Timeouts
  - Concurrency
- **File Size Limit** - Maximum 800 lines per file
- **Build After Changes** - Compile after every file modification

### After Code Changes:

1. **Index Files** - Run `index_file` on all modified files
2. **Run Tests** - All integration tests must pass
3. **Analyze Complexity** - Run `analyze_code_complexity`
4. **Validate Patterns** - Run `validate_pattern_quality` (score must be >= 6)
5. **Security Check** - Run `validate_security` (score must be >= 8)

## 🚫 BLOCKING CONDITIONS - STOP Work If:

| Condition | Threshold | Action Required |
|-----------|-----------|-----------------|
| Security Score | < 8/10 | Fix CRITICAL/HIGH severity issues immediately |
| Pattern Quality | < 6 | Fix before continuing |
| Breaking Changes | > 10 files | Get approval before proceeding |
| Code Complexity | Grade F | Refactor immediately |

## 🏗️ Architecture Guidelines

### Plugin System
- **Always check** `ProjectPluginActivations` - plugins must be ACTIVATED before use
- **Use PluginManager** - Call `GetActivePluginAsync(projectId, pluginId)` for initialized instances
- **Config Changes** - Follow: Save → `InvalidatePluginCacheAsync` → `RefreshPluginAsync` → Verify
- **Plugin Types** - `LLM`, `Embedding`, `VectorSearch`, `DataSource`

### Database & Export
- **Dynamic Schema Generation** - Query `INFORMATION_SCHEMA`, NEVER hardcode schemas
- **Graceful Degradation** - Handle missing tables/columns (log warnings, don't error)
- **100% Query Success** - Table queries must work in all scenarios

### Prompt Templates
- **Use Service** - `IPromptTemplateService.RenderPromptAsync(promptKey, variables, companyId)`
- **Never Hardcode** - All prompts go through the template system
- **Naming Convention** - `"category.purpose"` (e.g., `"embedding.field_description"`)
- **3-Tier Hierarchy** - Project Override > Company Override > System Default

## 📊 Quality Gates

### Pattern Validation Thresholds:
- **9-10 (A)**: ✅ Excellent - Ship it
- **8 (B)**: ✅ Good - Acceptable
- **7 (C)**: ⚠️ Fair - Address before release
- **6 (D)**: ❌ Poor - FIX BEFORE CONTINUING
- **0-5 (F)**: 🚨 CRITICAL - FIX IMMEDIATELY

### Security Score Requirements:
- **10/10**: 🔒 Perfect - Maintain vigilance
- **8-9/10**: ✅ Acceptable for development
- **< 8/10**: 🚨 STOP - Fix high/critical vulnerabilities

## 🔧 MCP Tool Workflow

### Analysis Order:
```
smartsearch 
  → search_patterns 
  → dependency_chain 
  → impact_analysis 
  → validate_pattern_quality 
  → get_recommendations
```

### After Changes:
```
Build ✅ 
  → index_file 
  → validate_security 
  → find_anti_patterns 
  → get_recommendations
```

## 🧪 Testing Standards

### Integration Tests Only
- ❌ **NO MOCKS** - All tests must use real implementations
- ✅ **Real Database** - Use test database instances
- ✅ **Real Services** - Test actual service integrations
- ✅ **Full Stack** - End-to-end integration testing

### Test Requirements:
```csharp
[Fact]
public async Task MethodName_Should_HappyPath() { }

[Fact]
public async Task MethodName_Should_ThrowException_WhenInvalidInput() { }

[Fact]
public async Task MethodName_Should_HandleTimeout() { }

[Fact]
public async Task MethodName_Should_HandleConcurrency() { }

[Fact]
public async Task MethodName_Should_HandleEdgeCase() { }
```

## ⚡ Anti-Pattern Actions (Fix Immediately)

| Anti-Pattern | Required Fix |
|--------------|--------------|
| Legacy patterns (AutoGen, old SK) | Get migration path and migrate |
| Caching without expiration | Add expiration policy |
| Retry without backoff | Add exponential backoff |
| SQL injection risk | Add parameterized queries |
| Code execution without sandbox | Add validation and sandboxing |

## 📝 Commands Available

Use these commands to enforce rules:

- **FollowRulesReminder** - Review rules before starting
- **PreFlightCheck** - Pre-implementation checklist
- **ValidateWork** - Post-implementation validation
- **FixBug** - Structured bug fix workflow
- **RulesViolation** - When rules are broken

Run commands with: `Ctrl+Shift+P` → Search command name

## 🎯 Success Metrics

Track improvements over time:
- ✅ Pattern quality scores trending up
- ✅ Security score consistently > 8/10
- ✅ Critical issues → 0
- ✅ Test coverage increasing
- ✅ Legacy patterns decreasing

## 🚀 Goal

**Code that is:**
- ✅ Works correctly
- ✅ Is secure
- ✅ Is maintainable
- ✅ Follows best practices
- ✅ Is consistent

---

**Remember:** When in doubt, DISCUSS first. No surprises, no uncommitted changes without approval.

