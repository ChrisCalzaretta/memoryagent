# 🌐 WEB-AUGMENTED MULTI-MODEL THINKING

## **OVERVIEW**

The CodingAgent now augments LLM thinking with **real-time web research** when models struggle with unfamiliar tasks. Instead of hallucinating patterns, models can access:

1. **Official Documentation** (provider-specific)
2. **Stack Overflow** (community solutions)
3. **GitHub** (real-world examples)
4. **Dev Blogs** (best practices)

---

## **HOW IT WORKS**

### **Flow:**

```
Attempt 1: Models debate (no web search yet)
  ↓
Score: 5/10 (low!)
  ↓
Attempt 2: TRIGGER WEB RESEARCH (score < 6)
  ↓
Web Search:
  - C# Microsoft Docs → "Blazor @rendermode InteractiveServer"
  - Stack Overflow → "Chess move validation bitboard algorithm"
  - GitHub → "dotnet/aspnetcore/Blazor examples"
  ↓
Inject research into thinking prompts
  ↓
Models debate WITH real-world context:
  - Phi4: "Based on Microsoft Docs 2024..."
  - DeepSeek: "GitHub shows this production pattern..."
  - Gemma3: "Stack Overflow consensus is..."
  ↓
Score: 9/10 ✅
```

---

## **TRIGGER CONDITIONS**

Web research is triggered when:

| Condition | Threshold |
|-----------|-----------|
| Low score (2+ attempts) | Score < 6 |
| Very low score (3+ attempts) | Score < 7 |
| Build errors (3+ attempts) | Has compilation errors |
| Critical failures (5+ attempts) | Score < 5 |

**NOT triggered on:**
- First attempt (give models a chance)
- Already has cached research

---

## **PROVIDER-SPECIFIC SEARCHES**

The system searches **official documentation** based on language:

| Language | Provider | URL |
|----------|----------|-----|
| **C#/.NET** | Microsoft Docs | `learn.microsoft.com` |
| **Python** | Python.org | `docs.python.org` |
| **JavaScript** | MDN Web Docs | `developer.mozilla.org` |
| **TypeScript** | TypeScript Docs | `typescriptlang.org` |
| **Java** | Oracle Docs | `docs.oracle.com` |
| **Go** | Go.dev | `pkg.go.dev` |
| **Rust** | Rust Docs | `doc.rust-lang.org` |
| **Flutter/Dart** | Flutter Docs | `api.flutter.dev` |
| **Blazor** | Blazor Docs | `learn.microsoft.com/blazor` |
| **React** | React Docs | `react.dev` |
| **Vue** | Vue Docs | `vuejs.org` |
| **Angular** | Angular Docs | `angular.io` |

---

## **SEARCH STRATEGY**

For each task, the system performs:

### **1. Official Docs Search (3 results)**
```csharp
Query: "chess game logic C#"
→ Searches: learn.microsoft.com for C# patterns
→ Result: Authoritative API docs, best practices
```

### **2. General Web Search (5-7 results)**
```csharp
Query: "C# chess game logic best practices example code"
→ Searches: Stack Overflow, GitHub, dev blogs
→ Result: Real-world examples, community solutions
```

### **3. Results Ranked by Relevance**
- Official docs: Relevance 95/100
- GitHub: Relevance 85/100
- Stack Overflow: Relevance 90/100

---

## **HOW RESEARCH IS USED**

### **Injected Into Thinking Prompts:**

```
You are Phi4, an expert code architect.

Task: Create a Blazor chess game
Language: C#

════════════════════════════════════════════════════════════════════════════════
📚 REAL-WORLD RESEARCH (Use these authoritative sources!):
════════════════════════════════════════════════════════════════════════════════

[Microsoft Docs] Blazor @rendermode for Interactive Components
URL: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes
Use @rendermode InteractiveServer for real-time bidirectional updates.
Enables SignalR WebSocket connection for instant UI updates without page refresh.
Tags: official-docs, csharp, blazor

[Stack Overflow] Chess Move Validation Algorithm
URL: https://stackoverflow.com/questions/12345/chess-move-validation
Use bitboards for efficient move validation. Store board state as 64-bit integers.
Example: ulong whitePawns = 0x000000000000FF00UL;
Tags: chess, algorithm, performance

[GitHub] dotnet/aspnetcore - Blazor Chess Example
URL: https://github.com/dotnet/aspnetcore/tree/main/examples/blazor-chess
Production-quality Blazor chess implementation with drag-and-drop via JSInterop.
Shows proper component structure and state management.
Tags: github, real-world, blazor

════════════════════════════════════════════════════════════════════════════════
💡 Use these examples and best practices to inform your approach!
════════════════════════════════════════════════════════════════════════════════

Provide a strategic approach with:
1. APPROACH: Clear implementation strategy
2. DEPENDENCIES: Required packages/libraries
3. PATTERNS: Design patterns to use
4. RISKS: Potential issues to avoid
5. SUGGESTIONS: Specific improvements
```

---

## **CACHING**

Research results are **cached for 24 hours** per task/language combination:

```csharp
Cache Key: "csharp:Create a chess game"
Expiry: 24 hours
```

**Benefits:**
- ⚡ Faster retries (no redundant searches)
- 💰 Reduces API costs
- 🔄 Consistent results across attempts

---

## **MULTI-MODEL STRATEGIES (ENHANCED)**

All strategies now support web research:

| Strategy | Models | Research Trigger |
|----------|--------|------------------|
| **Duo Debate (DeepSeek)** | Phi4 + DeepSeek | Attempt 2, score < 6 |
| **Trio Consensus** | Phi4 + Gemma3 + Qwen | Attempt 3, score < 7 |
| **Quad Debate** | + DeepSeek | Attempt 4, score < 6 |
| **Full Ensemble** | + Llama3, Codestral | Attempt 5, score < 5 |
| **Multi-Round Ensemble** | 3 rounds, all models | Attempt 8, critical |

---

## **EXAMPLE SCENARIO**

### **Task:** "Create a Blazor chess game with drag-and-drop"

#### **Without Web Search (Old):**
```
Attempt 1:
  Phi4: "Use @onclick for moves" (outdated Blazor 3.0)
  Gemma3: "Use ComponentBase" (generic, no specifics)
  DeepSeek: "Create Chess class" (hallucinated structure)
  → Score: 5/10

Attempt 2:
  Same generic advice, slightly different wording
  → Score: 6/10

Attempt 3:
  Still guessing, hallucinating patterns
  → Score: 6/10 (stuck!)
```

#### **With Web Search (New):**
```
Attempt 1:
  Phi4: "Use @onclick for moves" (outdated)
  → Score: 5/10

Attempt 2: 🔍 RESEARCH TRIGGERED
  Web Search:
    - Microsoft Docs → "@rendermode InteractiveServer"
    - Stack Overflow → "Bitboard chess validation"
    - GitHub → "dotnet/aspnetcore chess example"
  
  Models now have context:
    Phi4: "Use @rendermode InteractiveServer (from MS Docs 2024)"
    DeepSeek: "Bitboard validation (Stack Overflow consensus)"
    Gemma3: "JSInterop for drag-and-drop (GitHub example)"
  
  → Score: 9/10 ✅
```

---

## **CONFIGURATION**

### **Environment Variables:**
```bash
# Optional: Configure search API (future)
SEARCH_API_KEY=your_api_key_here
SEARCH_API_PROVIDER=brave  # brave, google, bing
```

### **Code Configuration:**
```csharp
// In MultiModelThinkingService.cs
private bool ShouldResearchWeb(context, attemptNumber, score)
{
    if (attemptNumber >= 2 && score < 6) return true;
    if (attemptNumber >= 3 && score < 7) return true;
    // ... more conditions
}
```

---

## **BENEFITS**

1. **🔥 Up-to-Date Knowledge:** Access to 2024/2025 patterns (models trained on 2023 data)
2. **📚 Real Examples:** Actual working code from GitHub/Stack Overflow
3. **🐛 Avoid Pitfalls:** Learn from community mistakes
4. **🎯 Framework-Specific:** Exact API syntax for new libraries
5. **🌍 Community Wisdom:** Collective developer knowledge
6. **⚡ Faster Convergence:** Higher scores in fewer attempts

---

## **METRICS TO TRACK**

In AI Lightning, track:
- `research_triggered_count`: How often web search is used
- `research_improved_score`: Score delta before/after research
- `research_sources`: Which sources (MS Docs, SO, GitHub) were most helpful
- `convergence_speed`: Attempts to reach score ≥ 8 (with vs without research)

---

## **FUTURE ENHANCEMENTS**

1. **Real Search API Integration:**
   - Brave Search API (free tier)
   - Google Custom Search
   - Bing Search API

2. **Semantic Filtering:**
   - Rank results by relevance to task
   - Extract code snippets automatically
   - Filter outdated content (< 2 years old)

3. **Learning System:**
   - Track which sources lead to better code
   - Prioritize high-value sources per language
   - Learn when NOT to search (waste of time)

4. **User Override:**
   - Allow users to force research on first attempt
   - Disable research for specific tasks
   - Provide custom documentation URLs

---

## **TESTING**

Test web-augmented thinking:

```bash
# Start a job
curl -X POST http://localhost:5001/api/orchestrator/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "task": "Create a Blazor chess game with drag-and-drop",
    "language": "csharp",
    "minValidationScore": 8
  }'

# Watch logs for research trigger
docker logs memory-coding-agent -f | grep "researching web"

# Expected output:
# 🔍 Low score detected - researching web...
# 📖 Searching Microsoft Docs for: chess game blazor
# 🌐 Web search: C# chess game best practices example code
# ✅ Augmented context with 8 research results
```

---

## **IMPLEMENTATION STATUS**

✅ **COMPLETED:**
- IWebSearchService interface
- WebSearchService with 18 provider mappings
- Integration into MultiModelThinkingService
- Research caching (24h TTL)
- Prompt augmentation with research results
- Trigger logic (score-based, attempt-based)
- Registered in DI container

⚠️ **PLACEHOLDER (Real API Integration Needed):**
- Currently returns structured placeholders indicating where results would come from
- Production deployment requires:
  - Search API key (Brave/Google/Bing)
  - Real HTTP calls to search endpoints
  - HTML parsing for code snippet extraction

---

## **SEE ALSO**

- `MultiModelThinkingService.cs` - Core thinking service with research integration
- `WebSearchService.cs` - Web search implementation
- `IWebSearchService.cs` - Service interface
- `Phi4ThinkingService.cs` - ThinkingContext definition (includes WebResearch field)

