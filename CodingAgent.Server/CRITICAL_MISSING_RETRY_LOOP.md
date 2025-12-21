# 🚨 CRITICAL: RETRY LOOP IS MISSING!

## THE PROBLEM:

**The OLD TaskOrchestrator (archived) HAD a retry loop:**
```csharp
for (int iteration = 1; iteration <= effectiveMaxIterations; iteration++)
{
    // Generate code
    var codingResult = await _codingAgent.FixAsync(...);
    
    // Validate
    var validation = await _validationAgent.ValidateAsync(...);
    
    // Check if good enough
    if (validation.Score >= 8) break;
    
    // Retry with feedback
    feedback = validation.ToFeedback();
}
```

**The NEW architecture (CodingAgent v2) DOES NOT:**
```csharp
// JobManager.StartJobAsync()
public async Task<string> StartJobAsync(string task, string? language, int maxIterations, CancellationToken ct)
{
    // ...
    var result = await _orchestrator.GenerateProjectAsync(task, language, workspacePath: null, context: "memoryagent", cts.Token);
    // ❌ NO RETRY LOOP! Just calls ONCE!
}

// ProjectOrchestrator.GenerateProjectAsync()
public async Task<GenerateCodeResponse> GenerateProjectAsync(...)
{
    // ...
    return await _codeGeneration.GenerateAsync(new GenerateCodeRequest {...}, ct);
    // ❌ NO RETRY LOOP! Just calls ONCE!
}

// CodeGenerationService.GenerateAsync()
public async Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
{
    // ... generates code ONCE ...
    return new GenerateCodeResponse { ... };
    // ❌ NO RETRY LOOP! Just generates ONCE!
}
```

---

## WHY THIS IS CRITICAL:

**The V2 plan EXPLICITLY requires:**
> "10-Attempt Persistence Loop: Never gives up! If first generation fails validation, retry with different models/strategies"

**Current behavior:**
1. User calls `orchestrate_task`
2. JobManager calls ProjectOrchestrator ONCE
3. ProjectOrchestrator calls CodeGenerationService ONCE
4. Code is generated ONCE
5. **NO VALIDATION**
6. **NO RETRY**
7. Done.

**Expected behavior:**
1. User calls `orchestrate_task`
2. For iteration 1 to 10:
   - Generate code
   - **VALIDATE code**
   - If score >= 8: DONE!
   - If score < 8: Retry with feedback
3. If all 10 attempts fail: Generate stub + failure report
4. Done.

---

## WHERE THE RETRY LOOP SHOULD BE:

### **Option 1: In JobManager (RECOMMENDED)**

```csharp
public async Task<string> StartJobAsync(string task, string? language, int maxIterations, CancellationToken ct)
{
    // ... setup ...
    
    _ = Task.Run(async () =>
    {
        try
        {
            ValidationFeedback? feedback = null;
            GenerateCodeResponse? lastResult = null;
            
            // 🔄 RETRY LOOP - Keep trying until we get a good score!
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                jobState.Progress = (iteration * 90) / maxIterations;
                await PersistJobAsync(jobState);
                
                // Generate/fix code
                var request = new GenerateCodeRequest
                {
                    Task = task,
                    Language = language,
                    WorkspacePath = "/data/workspace",
                    PreviousFeedback = feedback  // Pass previous validation feedback
                };
                
                lastResult = feedback == null
                    ? await _codeGeneration.GenerateAsync(request, cts.Token)
                    : await _codeGeneration.FixAsync(request, cts.Token);
                
                // ✅ VALIDATE the generated code (with ADAPTIVE ENSEMBLE!)
                var validation = await _validation.ValidateAsync(new ValidateCodeRequest
                {
                    Files = lastResult.FileChanges.Select(f => new FileToValidate
                    {
                        Path = f.Path,
                        Content = f.Content
                    }).ToList(),
                    Language = language ?? "csharp",
                    WorkspacePath = "/data/workspace",
                    EnsembleStrategy = "adaptive",  // 🎯 Smart ensemble validation!
                    IterationNumber = iteration,
                    MaxIterations = maxIterations
                }, cts.Token);
                
                // Check if good enough (with confidence check!)
                if (validation.Score >= 8 && validation.Confidence >= 0.7)
                {
                    _logger.LogInformation(
                        "✅ Validation passed: score={Score}/10, confidence={Confidence:P0}, models={Models} on iteration {Iteration}", 
                        validation.Score, validation.Confidence, 
                        string.Join(", ", validation.ModelsUsed), iteration);
                    break;
                }
                
                // Prepare feedback for next iteration
                feedback = validation.ToFeedback();
                feedback.TriedModels.Add(lastResult.ModelUsed);
                
                _logger.LogInformation("⚠️ Validation score {Score}/10 on iteration {Iteration}, retrying...", 
                    validation.Score, iteration);
            }
            
            jobState.Status = lastResult.Success ? "completed" : "failed";
            jobState.Result = lastResult;
            // ...
        }
        catch (Exception ex)
        {
            // ...
        }
    }, cts.Token);
}
```

### **Option 2: In ProjectOrchestrator**

Add retry loop to `GenerateProjectAsync()` method.

### **Option 3: External Orchestrator**

Create a NEW `TaskOrchestrator` service that wraps JobManager.

---

## WHAT'S MISSING FOR RETRY LOOP:

1. ❌ **ValidationAgent Client** - No way to call validation service
2. ❌ **Retry loop in JobManager** - Just calls once
3. ❌ **History tracking** - Contract exists, but no one populates it
4. ❌ **Stub generation on failure** - If all 10 attempts fail
5. ❌ **Failure report** - Document why it failed

---

## IMMEDIATE ACTION REQUIRED:

**We need to BUILD the retry loop NOW!**

**Where to add it:**
- **JobManager.StartJobAsync()** - Best place (centralized, tracks progress)

**What we need:**
1. ✅ ValidationAgent client (already exists?)
2. ✅ Retry loop (need to build)
3. ✅ History tracking (contract ready, need to populate)
4. ⚠️ Stub generator (partially implemented)
5. ⚠️ Failure report generator (partially implemented)

**Estimated time:**
- 30 minutes to add retry loop to JobManager
- Add ValidationAgent client reference
- Wire up validation calls
- Track history
- Test!

**DO YOU WANT ME TO BUILD THIS NOW?** 🔥

---

## 🎯 ENSEMBLE VALIDATION INTEGRATION

**The retry loop now supports ENSEMBLE VALIDATION for higher quality!**

### What is Ensemble Validation?

Instead of using a single model to validate code, ensemble validation uses **multiple models** to:
- **Increase confidence** through consensus voting
- **Reduce false positives/negatives** by leveraging different perspectives
- **Leverage model specialization** (security, patterns, architecture)
- **Adapt strategy** based on iteration number

### Adaptive Strategy (RECOMMENDED)

```csharp
EnsembleStrategy = "adaptive"
IterationNumber = iteration
MaxIterations = maxIterations
```

**How it works:**
- **Iterations 1-7:** Single model (fast iteration)
- **Iterations 8-9:** Sequential ensemble (2-3 models, thorough)
- **Iteration 10:** Full parallel voting (3 models, maximum confidence)

**Benefits:**
- ⚡ Fast early iterations (1-3 seconds)
- 🎯 Thorough late iterations (5-15 seconds)
- ✅ Maximum confidence on final attempt (10-20 seconds)

### Confidence Scoring

The validation response now includes a `Confidence` field (0.0-1.0):

```csharp
if (validation.Score >= 8 && validation.Confidence >= 0.7)
{
    // High confidence - models agree, trust the score
    break;
}
```

**Confidence levels:**
- **0.9-1.0:** Models strongly agree (very confident)
- **0.7-0.9:** Models mostly agree (good confidence)
- **< 0.7:** Models disagree (needs human review)

### Example Output

```
Iteration 1: Score=6/10, Confidence=100%, Models=phi4:latest (single model)
Iteration 2: Score=7/10, Confidence=100%, Models=phi4:latest (single model)
...
Iteration 8: Score=7/10, Confidence=95%, Models=phi4:latest, deepseek-coder:1.5b (sequential)
Iteration 9: Score=8/10, Confidence=89%, Models=phi4:latest, deepseek-coder:1.5b, qwen2.5-coder:3b (tiebreaker)
✅ Validation passed: score=8/10, confidence=89%, models=phi4, deepseek-coder, qwen2.5-coder
```

### See Also

- [ENSEMBLE_VALIDATION.md](../ValidationAgent.Server/ENSEMBLE_VALIDATION.md) - Full documentation
- [ENSEMBLE_EXAMPLE.cs](../ValidationAgent.Server/ENSEMBLE_EXAMPLE.cs) - Code examples

