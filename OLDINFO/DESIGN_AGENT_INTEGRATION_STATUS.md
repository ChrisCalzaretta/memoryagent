# 🎨 Design Agent Integration Status

## Current Status: ❌ **NOT INTEGRATED**

The **DesignAgent.Server** exists as a separate service but is **NOT** currently integrated with `CodingAgent.Server`.

---

## 🏗️ Architecture

### Services:

1. **MemoryAgent.Server** (port 5000)
   - Lightning Q&A, prompts, patterns
   - Qdrant (semantic search)
   - Neo4j (graph database)

2. **CodingAgent.Server** (port 5001) - **NEW**
   - Multi-agent code generation
   - Uses Deepseek + Phi4 (local)
   - Escalates to Claude when needed

3. **DesignAgent.Server** (port 5004) - **SEPARATE SERVICE**
   - Brand management (`/api/design/brand/*`)
   - Design validation (`/api/design/validate`)
   - UI component specs
   - Accessibility validation
   - Design Intelligence (auto-learning from web)

---

## ❌ What's Missing

### 1. **MCP Wrapper Routes to Wrong Service**
**Current:**
```javascript
case 'design_get_brand': {
  const result = await sendToOrchestrator('/api/mcp/call', ...);  // ❌ WRONG - goes to port 5001
  return result.content?.[0]?.text || 'Error getting brand';
}
```

**Should Be:**
```javascript
case 'design_get_brand': {
  const result = await sendToDesignAgent('/api/design/brand/' + args.context);  // ✅ Correct - port 5004
  return JSON.stringify(result);
}
```

### 2. **No DesignAgent Client in Coding Agent**
`CodingAgent.Server` needs an `IDesignAgentClient` to communicate with `DesignAgent.Server`.

### 3. **PromptBuilder Doesn't Use Design Guidelines**
When generating UI code (Blazor, Flutter, React), the prompt should include:
- Brand colors
- Typography
- Component guidelines
- Accessibility requirements

### 4. **No Design Validation Loop**
After generating UI code, it should be validated against brand guidelines and fixed if issues are found.

---

## ✅ What Needs to Happen

### Phase 1: Fix MCP Routing ⚡ **HIGH PRIORITY**
Update `orchestrator-mcp-wrapper.js`:

```javascript
const DESIGN_AGENT_HOST = process.env.DESIGN_AGENT_HOST || 'localhost';
const DESIGN_AGENT_PORT = process.env.DESIGN_AGENT_PORT || 5004;

// NEW: Send HTTP request to DesignAgent
function sendToDesignAgent(endpoint, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: DESIGN_AGENT_HOST,
      port: DESIGN_AGENT_PORT,
      path: endpoint,
      method: method,
      headers: { 'Content-Type': 'application/json' }
    };
    // ... (same as sendToOrchestrator)
  });
}

// UPDATE: Design tool handlers
case 'design_get_brand': {
  const result = await sendToDesignAgent(`/api/design/brand/${args.context}`);
  return JSON.stringify(result);
}

case 'design_validate': {
  const result = await sendToDesignAgent('/api/design/validate', 'POST', {
    context: args.context,
    code: args.code
  });
  return JSON.stringify(result);
}

case 'design_create_brand': {
  const result = await sendToDesignAgent('/api/design/brand/create', 'POST', args);
  return JSON.stringify(result);
}
```

### Phase 2: Create DesignAgent Client 🔧
Add to `CodingAgent.Server`:

```csharp
// CodingAgent.Server/Clients/IDesignAgentClient.cs
public interface IDesignAgentClient
{
    Task<BrandDefinition?> GetBrandAsync(string context, CancellationToken cancellationToken);
    Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken);
}

// CodingAgent.Server/Clients/DesignAgentClient.cs
public class DesignAgentClient : IDesignAgentClient
{
    private readonly HttpClient _httpClient;
    
    public async Task<BrandDefinition?> GetBrandAsync(string context, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/api/design/brand/{context}", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BrandDefinition>(cancellationToken);
    }
    
    public async Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken)
    {
        var request = new { context, code };
        var response = await _httpClient.PostAsJsonAsync("/api/design/validate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DesignValidationResult>(cancellationToken) ?? 
            new DesignValidationResult { Score = 0, Issues = new() };
    }
}

// CodingAgent.Server/Program.cs
builder.Services.AddHttpClient<IDesignAgentClient, DesignAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DesignAgent:BaseUrl"] ?? "http://localhost:5004");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

### Phase 3: Integrate into PromptBuilder 🎨
Add brand guidelines to prompts for UI code:

```csharp
public async Task<string> BuildGeneratePromptAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
{
    var sb = new StringBuilder();
    
    // ... existing prompt building ...
    
    // 🎨 DESIGN GUIDELINES: For UI code, include brand guidelines
    if (IsUICode(request))
    {
        _logger.LogInformation("🎨 [DESIGN] Fetching brand guidelines for UI code generation...");
        
        var brand = await _designAgent.GetBrandAsync(context, cancellationToken);
        if (brand != null)
        {
            sb.AppendLine("=== 🎨 BRAND GUIDELINES (MUST FOLLOW) ===");
            sb.AppendLine($"Brand: {brand.Name}");
            sb.AppendLine($"Primary Color: {brand.Colors.Primary}");
            sb.AppendLine($"Font: {brand.Typography.FontFamily}");
            sb.AppendLine($"Theme: {brand.Theme.Preference}");
            sb.AppendLine($"Visual Style: {brand.VisualStyle}");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: All UI components MUST use these brand guidelines!");
            sb.AppendLine();
            
            _logger.LogInformation("✅ [DESIGN] Included brand '{BrandName}' in prompt", brand.Name);
        }
        else
        {
            _logger.LogWarning("⚠️ [DESIGN] No brand found for context {Context} - generating without guidelines", context);
        }
    }
    
    return sb.ToString();
}

private bool IsUICode(GenerateCodeRequest request)
{
    var language = request.Language?.ToLowerInvariant() ?? "";
    var task = request.Task.ToLowerInvariant();
    
    return language is "flutter" or "blazor" or "react" or "vue" ||
           task.Contains("ui") || task.Contains("component") || task.Contains("screen") || 
           task.Contains("page") || task.Contains("view");
}
```

### Phase 4: Design Validation Loop 🔍
Add validation after UI code generation:

```csharp
// In ProjectOrchestrator or CodeGenerationService
private async Task<GenerateCodeResponse> ValidateDesignAsync(
    GenerateCodeResponse response, 
    string context,
    CancellationToken cancellationToken)
{
    // Only validate UI code
    if (!IsUICode(response)) return response;
    
    _logger.LogInformation("🎨 [DESIGN] Validating UI code against brand guidelines...");
    
    // Combine all generated files for validation
    var combinedCode = string.Join("\n\n", response.FileChanges.Select(f => f.Content));
    
    var validationResult = await _designAgent.ValidateAsync(context, combinedCode, cancellationToken);
    
    _logger.LogInformation("🎨 [DESIGN] Validation score: {Score}/10 ({Grade})", 
        validationResult.Score, validationResult.Grade);
    
    // If score < 8, add issues to feedback for fixing
    if (validationResult.Score < 8)
    {
        _logger.LogWarning("⚠️ [DESIGN] Code does not meet brand guidelines - adding issues for fix");
        
        // Convert design issues to validation feedback
        foreach (var issue in validationResult.Issues)
        {
            response.Feedback?.Issues.Add(new ValidationIssue
            {
                Severity = issue.Severity ?? "warning",
                Message = $"[DESIGN] {issue.Message}",
                Suggestion = issue.Suggestion,
                File = ""
            });
        }
        
        response.Feedback.Score = Math.Min(response.Feedback.Score, validationResult.Score);
    }
    else
    {
        _logger.LogInformation("✅ [DESIGN] Code meets brand guidelines!");
    }
    
    return response;
}
```

---

## 🔄 Complete Integration Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. USER REQUEST                                                     │
│    "Create a Flutter login screen"                                  │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. ProjectOrchestrator                                              │
│    - Detects UI code request                                        │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. PromptBuilder                                                    │
│    ├─→ MemoryAgent: search, patterns, similar solutions            │
│    └─→ DesignAgent: GET /api/design/brand/{context}  ⚡ NEW!       │
│        Returns: colors, fonts, theme, component guidelines          │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. ENHANCED PROMPT                                                  │
│    - Existing code context                                          │
│    - Similar solutions                                              │
│    - Best practices patterns                                        │
│    - 🎨 BRAND GUIDELINES (colors, fonts, theme)  ⚡ NEW!            │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 5. LLM GENERATION (Deepseek/Claude)                                 │
│    Generates UI code following brand guidelines                     │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 6. DESIGN VALIDATION  ⚡ NEW!                                       │
│    POST /api/design/validate                                        │
│    - Checks colors match brand                                      │
│    - Checks fonts match brand                                       │
│    - Checks spacing/layout                                          │
│    - Checks accessibility (WCAG)                                    │
│    Returns: score (0-10), issues, suggestions                       │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 7. IF SCORE < 8: FIX & RETRY                                        │
│    - Add design issues to validation feedback                       │
│    - Regenerate with specific fixes                                 │
│    - Validate again                                                 │
│    (Loop up to 10 times or until score >= 8)                        │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 8. FINAL UI CODE                                                    │
│    ✅ Follows brand guidelines                                      │
│    ✅ Matches colors, fonts, theme                                  │
│    ✅ Meets accessibility standards                                 │
│    ✅ Score >= 8/10                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 📋 Benefits of Integration

### Without Design Agent:
❌ UI code has random colors  
❌ Fonts don't match brand  
❌ Inconsistent component styling  
❌ Accessibility issues  
❌ Manual design review needed  

### With Design Agent:
✅ **Brand-consistent UI** - automatically follows guidelines  
✅ **Validated colors** - matches brand palette  
✅ **Correct typography** - uses brand fonts  
✅ **Accessible** - WCAG AA/AAA compliance  
✅ **Professional quality** - looks production-ready  
✅ **No manual review** - automated validation  

---

## 🚀 Implementation Priority

1. **HIGH**: Fix MCP wrapper routing (30 minutes)
2. **HIGH**: Add DesignAgentClient to CodingAgent.Server (1 hour)
3. **MEDIUM**: Integrate into PromptBuilder for UI code (1 hour)
4. **LOW**: Add design validation loop (2 hours)

**Total Time: ~4-5 hours for full integration**

---

## 📊 Design Agent Status

| Component | Status | Port | Notes |
|-----------|--------|------|-------|
| DesignAgent.Server | ✅ Running | 5004 | Separate service |
| Brand Management | ✅ Working | - | Create/get/update brands |
| Design Validation | ✅ Working | - | Validates code vs brand |
| Design Intelligence | ✅ Working | - | Auto-learns from web |
| MCP Integration | ❌ Broken | - | Routes to wrong port |
| CodingAgent Integration | ❌ Missing | - | No client exists |
| PromptBuilder Integration | ❌ Missing | - | Doesn't use guidelines |
| Validation Loop | ❌ Missing | - | No auto-fix for design issues |

---

## ✅ Agent Lightning Status

**YES! Agent Lightning (MemoryAgent) is fully integrated:**

1. ✅ **Prompts** - All code generation prompts come from Lightning
2. ✅ **Q&A Learning** - Finds similar past solutions
3. ✅ **Pattern Detection** - Applies best practices
4. ✅ **Smart Search** - Qdrant + Neo4j search before write
5. ✅ **Model Learning** - Tracks which models work best
6. ✅ **Feedback Loop** - Records prompt performance

The PromptBuilder already fetches prompts from Lightning via:
- `GetPromptAsync("coding_agent_system")` - Main system prompt
- `GetPromptAsync("coding_agent_csharp")` - Language-specific prompts
- `GetPromptAsync("coding_agent_flutter")` - Flutter prompts
- etc.

**Lightning is actively improving prompts based on:**
- Successful generations (records via feedback)
- Failed generations (analyzes errors)
- Model performance (which models work best)
- Pattern effectiveness (which patterns help most)

---

## 🎯 Next Steps

1. **Fix the MCP wrapper** to route design calls to port 5004
2. **Add DesignAgentClient** to CodingAgent.Server
3. **Integrate brand guidelines** into PromptBuilder for UI code
4. **Add design validation** to ensure brand compliance

Once complete, the CodingAgent will generate **brand-consistent, accessible UI code automatically!** 🎨✨

