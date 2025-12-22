# 🎯 Ensemble Validation - Model Collaboration for Higher Quality

## Overview

The **Ensemble Validation System** uses multiple AI models to validate code, providing:
- **Higher Confidence** through consensus voting
- **Reduced False Positives/Negatives** by leveraging multiple perspectives
- **Model Specialization** for security, patterns, and architecture
- **Adaptive Strategies** that balance speed vs. thoroughness

---

## 🚀 Quick Start

### Enable Ensemble Validation

```csharp
var request = new ValidateCodeRequest
{
    Files = files,
    Context = "myproject",
    EnsembleStrategy = "adaptive",  // 👈 Enable ensemble
    IterationNumber = 5,
    MaxIterations = 10
};

var response = await validationClient.ValidateAsync(request);

// Check confidence
Console.WriteLine($"Score: {response.Score}/10");
Console.WriteLine($"Confidence: {response.Confidence:P0}");
Console.WriteLine($"Models used: {string.Join(", ", response.ModelsUsed)}");
```

---

## 📊 Ensemble Strategies

### 1. **Single Model** (Default - Fastest)
```csharp
EnsembleStrategy = "single"
```
- Uses smart model selection to pick best single model
- **Speed:** ⚡⚡⚡ Very Fast (1-3 seconds)
- **Confidence:** ✓ Good (single perspective)
- **Cost:** $ Low
- **Use When:** Early iterations (1-7), exploration, rapid feedback

---

### 2. **Sequential** (Recommended - Cost-Effective) ⭐
```csharp
EnsembleStrategy = "sequential"
```
- Starts with fast model (phi4)
- Adds second opinion only if borderline (score 4-8)
- Adds tiebreaker if models disagree (>2 points)

**Flow:**
```
Stage 1: phi4 validates
  ├─ Score >= 9 → ✅ PASS (confident, done)
  ├─ Score <= 3 → ❌ FAIL (confident, done)
  └─ Score 4-8 → Stage 2
  
Stage 2: deepseek-coder validates
  ├─ Agreement (diff <= 2) → Average scores, done
  └─ Disagreement (diff > 2) → Stage 3
  
Stage 3: qwen2.5-coder validates (tiebreaker)
  └─ Vote on final score
```

- **Speed:** ⚡⚡ Adaptive (3-15 seconds)
- **Confidence:** ✓✓ Very Good (2-3 models)
- **Cost:** $$ Adaptive
- **Use When:** Late iterations (8-9), borderline cases, most production use

---

### 3. **Parallel Voting** (Highest Quality)
```csharp
EnsembleStrategy = "parallel"
```
- Runs 3 models simultaneously
- Averages scores
- Includes issues that ≥2 models agree on

**Models:**
- phi4:latest (fast, patterns)
- deepseek-coder:1.5b (security)
- qwen2.5-coder:1.5b (architecture)

- **Speed:** ⚡ Medium (5-10 seconds parallel)
- **Confidence:** ✓✓✓ Excellent (3 models voting)
- **Cost:** $$$ High
- **Use When:** Final iteration (10), critical code, maximum confidence needed

---

### 4. **Specialized** (Domain-Specific)
```csharp
EnsembleStrategy = "specialized"
```
- Uses different models for different aspects
- Security specialist: deepseek-coder
- Architecture specialist: qwen2.5-coder:3b
- General quality: phi4

- **Speed:** ⚡⚡ Fast (parallel execution)
- **Confidence:** ✓✓ Very Good (expert models)
- **Cost:** $$ Medium
- **Use When:** Security-critical code, architecture reviews, `/review` endpoint

---

### 5. **Adaptive** (Smart - Changes with Iteration) ⭐
```csharp
EnsembleStrategy = "adaptive"
IterationNumber = 5
MaxIterations = 10
```
- **Iterations 1-7:** Single model (fast iteration)
- **Iterations 8-9:** Sequential ensemble (thorough)
- **Iteration 10:** Full parallel voting (maximum confidence)

- **Speed:** ⚡⚡⚡ → ⚡⚡ → ⚡ (adaptive)
- **Confidence:** ✓ → ✓✓ → ✓✓✓ (increases over time)
- **Cost:** $ → $$ → $$$ (adaptive)
- **Use When:** **RECOMMENDED FOR RETRY LOOPS** - optimal balance

---

### 6. **Pessimistic** (Safest)
```csharp
EnsembleStrategy = "pessimistic"
```
- Runs 2 models
- Takes the **LOWEST** score (safest approach)
- Prevents false positives

- **Speed:** ⚡⚡ Fast (2 models)
- **Confidence:** ✓✓ High (conservative)
- **Cost:** $$ Medium
- **Use When:** Critical production code, zero tolerance for bugs

---

### 7. **Optimistic** (Fastest Iteration)
```csharp
EnsembleStrategy = "optimistic"
```
- Runs 2 models
- Takes the **HIGHEST** score (fastest iteration)
- Prevents false negatives

- **Speed:** ⚡⚡ Fast (2 models)
- **Confidence:** ✓ Good (progressive)
- **Cost:** $$ Medium
- **Use When:** Rapid iteration, exploratory development

---

## 📈 Confidence Scoring

The `Confidence` field (0.0-1.0) indicates model agreement:

```
Confidence = 1.0 - (score_std_dev / 5.0)
```

**Examples:**
- All models give 8/10 → Confidence = 1.0 (perfect agreement)
- Models give 7, 8, 9 → Confidence = 0.89 (good agreement)
- Models give 4, 8, 10 → Confidence = 0.52 (low agreement, needs human review)

**Decision Rules:**
```csharp
if (response.Confidence >= 0.9)
    // Very confident - trust the score
else if (response.Confidence >= 0.7)
    // Good confidence - proceed with caution
else
    // Low confidence - consider human review
```

---

## 🔄 Integration with Retry Loop

### Recommended Pattern

```csharp
public async Task<GenerateCodeResponse> GenerateWithValidationAsync(
    string task,
    int maxIterations = 10,
    CancellationToken ct)
{
    for (int iteration = 1; iteration <= maxIterations; iteration++)
    {
        // Generate code
        var code = await _codingAgent.GenerateAsync(task, ct);
        
        // Validate with adaptive ensemble
        var validation = await _validationAgent.ValidateAsync(new ValidateCodeRequest
        {
            Files = code.Files,
            Context = "myproject",
            EnsembleStrategy = "adaptive",  // 👈 Smart strategy
            IterationNumber = iteration,
            MaxIterations = maxIterations
        }, ct);
        
        // Check if good enough
        if (validation.Score >= 8 && validation.Confidence >= 0.7)
        {
            _logger.LogInformation(
                "✅ Validation passed: score={Score}, confidence={Confidence:P0}, models={Models}",
                validation.Score, validation.Confidence, 
                string.Join(", ", validation.ModelsUsed));
            return code;
        }
        
        // Log ensemble details
        if (validation.EnsembleResults != null)
        {
            foreach (var result in validation.EnsembleResults)
            {
                _logger.LogInformation(
                    "  Model {Model}: score={Score}, issues={Issues}, duration={Duration}ms, warm={Warm}",
                    result.Model, result.Score, result.IssueCount, 
                    result.DurationMs, result.WasWarm);
            }
        }
        
        // Retry with feedback
        feedback = validation.ToFeedback();
    }
}
```

---

## 🎯 Strategy Selection Guide

| Scenario | Recommended Strategy | Reason |
|----------|---------------------|--------|
| **Retry Loop (iterations 1-10)** | `adaptive` | Balances speed early, quality late |
| **Early iterations (1-3)** | `single` | Fast feedback, rapid iteration |
| **Mid iterations (4-7)** | `single` or `sequential` | Good balance |
| **Late iterations (8-9)** | `sequential` | Thorough validation |
| **Final iteration (10)** | `parallel` | Maximum confidence |
| **Security-critical code** | `specialized` or `pessimistic` | Expert security model |
| **Architecture review** | `specialized` | Expert architecture model |
| **Production deployment** | `parallel` or `pessimistic` | Highest quality bar |
| **Rapid prototyping** | `single` or `optimistic` | Speed over perfection |
| **Code review endpoint** | `specialized` | Domain experts |

---

## 📊 Performance Benchmarks

**Single File (100 lines):**
- Single: 1-2 seconds
- Sequential: 2-5 seconds (if borderline)
- Parallel: 5-8 seconds
- Specialized: 4-7 seconds

**Multiple Files (5 files, 500 lines):**
- Single: 3-5 seconds
- Sequential: 5-15 seconds (if borderline)
- Parallel: 10-20 seconds
- Specialized: 8-15 seconds

**Warm vs Cold Models:**
- Warm model (already loaded): ~1-3 seconds
- Cold model (needs loading): +10-30 seconds first time

---

## 🧠 Model Specializations

### phi4:latest
- **Strengths:** Fast, good at patterns, general quality
- **Best For:** Quick validation, pattern detection
- **Size:** ~4B parameters

### deepseek-coder:1.5b
- **Strengths:** Excellent security detection, CWE knowledge
- **Best For:** Security audits, vulnerability scanning
- **Size:** ~1.5B parameters

### qwen2.5-coder:1.5b / 3b
- **Strengths:** Architecture analysis, big-picture thinking
- **Best For:** Design patterns, system architecture
- **Size:** ~1.5B / 3B parameters

### granite3-dense:2b
- **Strengths:** Alternative perspective, code quality
- **Best For:** Tiebreaking, diverse opinions
- **Size:** ~2B parameters

---

## 🔧 Configuration

### Enable/Disable Smart Model Selection

```json
// appsettings.json
{
  "Gpu": {
    "UseSmartModelSelection": true,  // Enable LLM-based model selection
    "ValidationModel": "phi4:latest" // Fallback model
  }
}
```

### Exploration Rate

```json
{
  "ModelSelection": {
    "ExplorationRate": 0.1  // 10% chance to try new models
  }
}
```

---

## 📝 Response Structure

```csharp
public class ValidateCodeResponse
{
    public int Score { get; set; }              // 0-10
    public bool Passed { get; set; }            // Score >= 8
    public double Confidence { get; set; }      // 0.0-1.0 (model agreement)
    public List<string> ModelsUsed { get; set; } // Models that participated
    public List<EnsembleMemberResult> EnsembleResults { get; set; } // Individual results
    public List<ValidationIssue> Issues { get; set; }
    public string Summary { get; set; }
}

public class EnsembleMemberResult
{
    public string Model { get; set; }
    public int Score { get; set; }
    public int IssueCount { get; set; }
    public long DurationMs { get; set; }
    public bool WasWarm { get; set; }
}
```

---

## 🎓 Best Practices

### 1. Use Adaptive Strategy for Retry Loops
```csharp
// ✅ GOOD - Balances speed and quality
EnsembleStrategy = "adaptive"
IterationNumber = iteration
MaxIterations = 10
```

### 2. Check Confidence Before Accepting
```csharp
// ✅ GOOD - Verify model agreement
if (response.Score >= 8 && response.Confidence >= 0.7)
    return code;
```

### 3. Log Ensemble Details for Debugging
```csharp
// ✅ GOOD - Understand model decisions
foreach (var result in response.EnsembleResults)
    _logger.LogInformation("Model {Model}: {Score}/10", result.Model, result.Score);
```

### 4. Use Specialized for Security-Critical Code
```csharp
// ✅ GOOD - Expert security validation
if (isSecurityCritical)
    request.EnsembleStrategy = "specialized";
```

### 5. Fallback to Single Model on Errors
```csharp
// ✅ GOOD - Graceful degradation
try {
    return await _ensembleService.ValidateWithEnsembleAsync(request, ct);
} catch {
    return await _validationService.ValidateAsync(request, ct);
}
```

---

## 🚨 Common Pitfalls

### ❌ DON'T use parallel voting on every iteration
```csharp
// ❌ BAD - Too slow, wastes GPU
for (int i = 1; i <= 10; i++)
    EnsembleStrategy = "parallel"; // Every iteration!
```

### ✅ DO use adaptive strategy
```csharp
// ✅ GOOD - Smart escalation
EnsembleStrategy = "adaptive"
```

### ❌ DON'T ignore confidence scores
```csharp
// ❌ BAD - Accepting low confidence
if (response.Score >= 8)
    return code; // What if confidence = 0.3?
```

### ✅ DO check confidence
```csharp
// ✅ GOOD - Verify agreement
if (response.Score >= 8 && response.Confidence >= 0.7)
    return code;
```

---

## 📚 Related Documentation

- [CRITICAL_MISSING_RETRY_LOOP.md](./CRITICAL_MISSING_RETRY_LOOP.md) - Retry loop implementation
- [ValidationService.cs](./Services/ValidationService.cs) - Single model validation
- [ValidationModelSelector.cs](./Services/ValidationModelSelector.cs) - Smart model selection

---

## 🎯 Summary

**Ensemble validation provides:**
- ✅ Higher confidence through consensus
- ✅ Reduced false positives/negatives
- ✅ Model specialization (security, patterns, architecture)
- ✅ Adaptive strategies (speed early, quality late)
- ✅ Detailed debugging (individual model results)

**Recommended for:**
- 🔄 Retry loops (use `adaptive`)
- 🔒 Security-critical code (use `specialized` or `pessimistic`)
- 🏗️ Architecture reviews (use `specialized`)
- 🚀 Final validation (use `parallel`)

**Start with:** `EnsembleStrategy = "adaptive"` for best results! 🎯



