# 🎯 FILE-BY-FILE GENERATION PLAN

## Current Status
✅ **DONE - Strategic Level:**
- Phi4 generates intelligent file-by-file plan
- Multi-model debates the plan (strategic collaboration)
- Plan stored as todos
- Todos displayed on status page

❌ **TODO - Tactical Level:**
- Replace retry loop with file-by-file generation loop
- Follow the plan in order
- Multi-model collaboration per file

---

## The Complete Flow (What User Wants)

```
JOB START
  ↓
📋 STEP 1: STRATEGIC PLANNING
  ├─ 🧠 Phi4.PlanProjectAsync()
  │   └─ Generates: Calculator.cs, ICalculator.cs, CalculatorTests.cs, Program.cs, README.md
  │
  ├─ 🤝 Multi-model strategic debate
  │   ├─ Phi4: "Interface first for testability"
  │   ├─ DeepSeek: "Add factory pattern"
  │   └─ Gemma: "Separate validation class"
  │   └─ Consensus → Refined plan
  │
  └─ 💾 Store as todos (5 files = 5 todos)

  ↓
💻 STEP 2: TACTICAL GENERATION (FOR EACH FILE)
  │
  ├─ TODO 1: ICalculator.cs
  │   ├─ Mark "in_progress"
  │   ├─ 🤝 Multi-model tactical debate
  │   │   └─ "How to implement THIS interface?"
  │   ├─ 🤖 Generate ICalculator.cs
  │   ├─ ✅ Validate
  │   └─ Mark "completed"
  │
  ├─ TODO 2: Calculator.cs (depends on ICalculator.cs)
  │   ├─ Mark "in_progress"
  │   ├─ 🤝 Multi-model: "Implement interface"
  │   ├─ 🤖 Generate Calculator.cs
  │   ├─ ✅ Validate
  │   └─ Mark "completed"
  │
  ├─ TODO 3: CalculatorTests.cs
  │   ├─ Mark "in_progress"
  │   ├─ 🤝 Multi-model: "Test all methods"
  │   ├─ 🤖 Generate tests
  │   ├─ ✅ Run tests
  │   └─ Mark "completed"
  │
  └─ ... (repeat for each file)

  ↓
🎉 ALL TODOS COMPLETED → JOB DONE!
```

---

## Code Changes Needed

### Replace This (Line 589-1051 in JobManager.cs):
```csharp
// ❌ CURRENT: Retry loop generates ALL files at once
for (int iteration = 1; iteration <= maxIterations; iteration++) {
    // Generate ALL files
    var codeResult = await _agenticCoding.GenerateWithToolsAsync(
        task: task,  // Full task
        ...
    );
    
    // Validate ALL files
    // If score < 8, retry ALL files
}
```

### With This:
```csharp
// ✅ NEW: File-by-file loop following plan
var allGeneratedFiles = new List<CodeFile>();

foreach (var todo in jobState.ProjectPlan.OrderBy(t => t.Priority)) {
    
    // Mark current file in-progress
    todo.Status = "in_progress";
    await PersistJobAsync(jobState);
    
    // 🤝 TACTICAL: Multi-model debate for THIS specific file
    var fileThinking = await _multiThinking.ThinkSmartAsync(new ThinkingContext {
        TaskDescription = $"Generate {todo.Description}",
        FilePath = ExtractFileName(todo.Description),
        Language = language,
        ExistingFiles = allGeneratedFiles.ToDictionary(f => f.Path, f => f.Content),
        ...
    }, attempt, cts.Token);
    
    // 🤖 Generate THIS specific file
    var fileResult = await _agenticCoding.GenerateWithToolsAsync(
        task: $"Generate ONLY this file: {todo.Description}",
        language: language,
        workspacePath: actualWorkspacePath,
        jobWorkspacePath: jobWorkspacePath,
        codebaseContext: codebaseContext,
        previousFeedback: null,
        cancellationToken: cts.Token
    );
    
    if (fileResult.Success && fileResult.GeneratedFiles.Count > 0) {
        var generatedFile = fileResult.GeneratedFiles[0];
        allGeneratedFiles.Add(generatedFile);
        
        // ✅ Validate THIS file
        var validation = await _validation.ValidateAsync(new ValidateCodeRequest {
            Files = new List<CodeFile> { generatedFile },
            ...
        }, cts.Token);
        
        if (validation.Score >= 6) {
            // Good! Mark completed
            todo.Status = "completed";
            _logger.LogInformation("✅ {File} completed (score: {Score}/10)", 
                generatedFile.Path, validation.Score);
        } else {
            // Retry THIS file (up to 3 attempts)
            for (int retry = 1; retry <= 3; retry++) {
                // Retry logic for just this file...
            }
        }
    }
    
    await PersistJobAsync(jobState);
}

// 🎉 All files generated!
```

---

## Benefits

| Current (All-at-once) | New (File-by-file) |
|-----------------------|-------------------|
| Generate 10 files in one shot | Generate 1 file at a time |
| 1 context window = all code | 1 context window = 1 file (focused!) |
| If fails, retry ALL 10 files | If fails, retry THAT 1 file |
| Hard to track: "30% done" | Precise: "3/10 files done" |
| No dependency order | Phi4 orders by dependencies |
| Generic multi-model debate | Two-level debate (strategic + tactical) |

---

## Implementation Status

✅ **Completed:**
1. Phi4 planning integration
2. Strategic multi-model debate on plan
3. Todo storage and display
4. Helper methods (DetectTemplateType, DetermineStepType)

❌ **NOT YET DONE:**
1. Replace retry loop (lines 589-1051)
2. Implement file-by-file generation loop
3. Tactical multi-model debate per file
4. Per-file validation and retry logic

---

## Next Steps

**Option 1: Full Implementation (Big)**
- Replace entire retry loop
- ~500 lines of code changes
- Test thoroughly

**Option 2: Hybrid Approach (Safer)**
- Keep retry loop as fallback
- Add optional file-by-file mode
- Toggle with config flag

**Recommendation:** Option 2 (safer for production)

```csharp
if (useFileByFileMode && jobState.ProjectPlan != null) {
    // NEW: File-by-file generation
} else {
    // EXISTING: All-at-once retry loop (current code)
}
```

This way we don't break existing functionality while testing the new approach.

