# Build Error Feedback Fix

## Problem

When Docker execution failed with build/compilation errors, those errors were NOT being properly sent to the LLM for the next iteration. The system had infrastructure for "focused build error prompts" that put errors at the TOP of the prompt, but this wasn't being triggered.

### Symptoms

- LLM would see generic validation feedback instead of actual compiler errors
- The `BuildBuildErrorFixPromptAsync` method existed but was never called
- Repeated build failures because LLM couldn't see what was actually wrong
- The same error would repeat 3+ times (stagnation) because the LLM never got the real error message

## Root Cause

The `ValidationFeedback.BuildErrors` property was **never being set** when execution failed. The flow was:

1. Docker execution fails with build errors in `executionResult.Errors`
2. `TaskOrchestrator` creates a `ValidateCodeResponse` with errors in `Issues[].Suggestion`
3. `ValidateCodeResponse.ToFeedback()` converts to `ValidationFeedback`
4. **BUT** the `BuildErrors` property was not copied!
5. `CodeGenerationService` checks `HasBuildErrors` to decide which prompt to use
6. Since `BuildErrors` was null, it used the regular fix prompt instead of the focused one
7. Errors ended up buried in the middle of a huge prompt instead of at the top

## Solution

### Changes Made

#### 1. Added `BuildErrors` property to `ValidateCodeResponse`

**File:** `Shared/AgentContracts/Responses/ValidateCodeResponse.cs`

```csharp
/// <summary>
/// Raw build/execution errors from Docker (if any)
/// When set, indicates this is a build/execution failure
/// </summary>
public string? BuildErrors { get; set; }
```

#### 2. Updated `ToFeedback()` to copy `BuildErrors`

**File:** `Shared/AgentContracts/Responses/ValidateCodeResponse.cs`

```csharp
public ValidationFeedback ToFeedback() => new()
{
    Score = Score,
    Issues = Issues,
    Summary = Summary,
    BuildErrors = BuildErrors  // 🔧 CRITICAL: Copy build errors so focused prompt is used!
};
```

#### 3. Set `BuildErrors` when execution fails (3 places)

**File:** `CodingOrchestrator.Server/Services/TaskOrchestrator.cs`

**Location 1:** Regular execution failure (line ~751)
```csharp
lastValidation = new ValidateCodeResponse
{
    Score = 0,
    Passed = false,
    BuildErrors = executionResult.Errors, // 🔧 CRITICAL: Set this so focused build error prompt is used!
    Issues = new List<ValidationIssue>
    {
        new ValidationIssue
        {
            Severity = "critical",
            Message = executionResult.BuildPassed 
                ? "Code compiled but failed to execute" 
                : "Code failed to compile/build",
            File = executionFiles.FirstOrDefault()?.Path ?? "unknown",
            Line = 1,
            Suggestion = $"Fix the following errors:\n\n{executionResult.Errors}"
        }
    },
    // ... rest omitted
};
```

**Location 2:** Stagnation detection (line ~721)
```csharp
lastValidation = new ValidateCodeResponse
{
    Score = 0,
    Passed = false,
    BuildErrors = executionResult.Errors, // 🔧 Set build errors for stagnation too!
    // ... rest omitted
};
```

**Location 3:** Scaffolding mode build check (line ~1791)
```csharp
lastValidation = new ValidateCodeResponse
{
    Score = 2,
    Passed = false,
    BuildErrors = buildCheckResult.Errors, // 🔧 CRITICAL: Set build errors for focused prompt!
    // ... rest omitted
};
```

#### 4. Removed hacky detection logic

**File:** `CodingOrchestrator.Server/Services/TaskOrchestrator.cs` (line ~385)

**Before:**
```csharp
if (feedback != null)
{
    feedback.TriedModels = triedModels;
    
    // 🔧 If this was a BUILD failure (not just validation), set BuildErrors
    // so CodingAgent uses the focused fix prompt
    if (lastValidation?.Summary?.Contains("Build:") == true && 
        lastValidation?.Summary?.Contains("❌") == true)
    {
        // Extract build errors from the summary/issues
        feedback.BuildErrors = lastValidation?.Issues
            .FirstOrDefault(i => !string.IsNullOrEmpty(i.Suggestion))?.Suggestion 
            ?? lastValidation?.Summary;
        _logger.LogDebug("[BUILD-ERROR] Detected build failure, setting BuildErrors for focused fix");
    }
}
```

**After:**
```csharp
if (feedback != null)
{
    feedback.TriedModels = triedModels;
    
    // ✅ BuildErrors is now properly set in ValidateCodeResponse.ToFeedback()
    // No need for hacky extraction logic here!
}
```

## How It Works Now

When Docker execution fails:

1. `TaskOrchestrator` creates `ValidateCodeResponse` with `BuildErrors = executionResult.Errors`
2. `ValidateCodeResponse.ToFeedback()` copies `BuildErrors` to `ValidationFeedback`
3. `TaskOrchestrator` passes `ValidationFeedback` to `CodingAgent` via `GenerateCodeRequest.PreviousFeedback`
4. `CodeGenerationService.FixAsync()` checks `request.PreviousFeedback?.HasBuildErrors`
5. **If true:** Uses `PromptBuilder.BuildBuildErrorFixPromptAsync()` which puts errors AT THE TOP
6. **If false:** Uses regular `PromptBuilder.BuildFixPromptAsync()` for validation issues

### The Focused Build Error Prompt

The focused prompt (from `PromptBuilder.BuildBuildErrorFixPromptAsync`):

- Puts the **actual compiler errors at the very top** of the prompt
- Shows the **broken code** right after the errors
- Adds minimal language guidance
- Is **much smaller** than the regular fix prompt (~5KB vs ~48KB)
- Results in the LLM seeing the error message FIRST instead of buried in the middle

## Testing

All builds pass successfully:
- ✅ `AgentContracts` builds (contains the contract changes)
- ✅ `CodingOrchestrator.Server` builds (contains the orchestrator logic)
- ✅ `CodingAgent.Server` builds (contains the prompt builder)

## Impact

This fix ensures:
- ✅ Build errors are **immediately visible** to the LLM
- ✅ Focused build error prompts are **actually used** when they should be
- ✅ Fewer stagnation scenarios (same error 3+ times)
- ✅ Faster iteration cycles (LLM sees real errors, not generic messages)
- ✅ Better success rates on complex code generation tasks

## Files Modified

1. `Shared/AgentContracts/Responses/ValidateCodeResponse.cs` - Added `BuildErrors` property and updated `ToFeedback()`
2. `CodingOrchestrator.Server/Services/TaskOrchestrator.cs` - Set `BuildErrors` in 3 places, removed hacky detection

## Related Code (Already Existed)

- `ValidationFeedback.BuildErrors` property (in `GenerateCodeRequest.cs`) - already existed
- `ValidationFeedback.HasBuildErrors` property - already existed
- `PromptBuilder.BuildBuildErrorFixPromptAsync()` - already existed
- `CodeGenerationService` check for `HasBuildErrors` - already existed

The infrastructure was all there, it just wasn't being wired up properly!





