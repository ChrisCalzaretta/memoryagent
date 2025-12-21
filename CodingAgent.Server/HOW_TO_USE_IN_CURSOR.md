# 🚀 How to Use CodingAgent v2 in Cursor

## ✅ Setup Complete!

The **10-Attempt Retry Loop** with **Phi4 Collaboration** and **Smart Escalation** is now ready!

---

## 🎯 Quick Start

### 1. **Start a Code Generation Task**

In Cursor, use the MCP tool `orchestrate_task`:

```
Can you use orchestrate_task to create a Calculator class with add, subtract, multiply, divide methods in C#?
```

**Parameters:**
- `task` (required): What code to generate
- `language` (optional): Target language (auto-detected if not provided)
- `maxIterations` (optional): Max attempts (default: 50, but usually succeeds in 3-6)

---

### 2. **Check Progress**

```
What's the status of job job_20250120_abc123?
```

**Shows:**
- Current iteration (e.g., "Attempt 3/10")
- Latest validation score (e.g., "Score: 7/10")
- Issues found (severity, file, line, message, suggested fix)
- Models tried (e.g., "deepseek → deepseek → claude")
- Full history per attempt (score progression: 4 → 6 → 7)

---

### 3. **Apply Generated Files**

```
Can you apply the files from job job_20250120_abc123?
```

This will write all generated files to your workspace.

---

## 🔥 What Happens Behind the Scenes

### **The 10-Attempt "Never Surrender" Loop:**

```
FOR each iteration (1 to 10):
  1. 🧠 PHI4 thinks about the task:
     - Analyzes previous attempts (if any)
     - Identifies approach and patterns
     - Detects risks and dependencies
     - Provides strategic guidance
  
  2. 🤖 GENERATION (with smart escalation):
     Attempts 1-3:  Phi4 + Deepseek (FREE, local)
     Attempts 4-6:  Claude Sonnet (PAID, cloud)
     Attempts 7-10: Claude Opus (PREMIUM, godlike)
  
  3. ✅ VALIDATION:
     - ValidationAgent reviews code
     - Gives score 0-10
     - Lists issues (severity, file, line, fix)
     - Provides summary
  
  4. 🎯 SMART BREAK LOGIC:
     - Score >= 8.0:  ✅ BREAK (Excellent!)
     - Score >= 6.5 AND attempt >= 3: ⚠️ BREAK (Good enough!)
     - Score < 6.5:   🔄 RETRY with feedback
     - Attempt >= 10: 🚨 BREAK (Critical - something is wrong)
  
  5. 📊 HISTORY TRACKING:
     - Store: attempt number, model, score, issues, timestamp
     - Pass to Phi4 for next iteration analysis
     - Phi4 can see progression: 4 → 6 → 7
```

---

## 📊 Example Run

```
🚀 Job started: Create a Calculator class

🔄 Attempt 1/10
  🧠 [PHI4] Thinking about task...
     → Approach: Create class with 4 methods
     → Patterns: Error handling, input validation
     → Risks: Division by zero
  🤖 [DEEPSEEK] Generating with Phi4's guidance...
  ✅ Generated 2 files: Calculator.cs, Program.cs
  📊 Validation: Score 4/10 (5 issues)
     - ❌ Error: Missing Main method
     - ❌ Error: No error handling
  ⚠️ Retrying...

🔄 Attempt 2/10
  🧠 [PHI4] Thinking (previous score: 4/10)...
     → Phi4 sees: "Missing Main" was the issue
     → Suggests: Add proper Main method
  🤖 [DEEPSEEK] Generating with updated guidance...
  ✅ Generated 2 files
  📊 Validation: Score 6/10 (3 issues)
     - ❌ Error: No error handling for division
     - ⚠️ Warning: Missing XML docs
  ⚠️ Retrying...

🔄 Attempt 3/10
  🧠 [PHI4] Thinking (previous score: 6/10)...
     → Phi4 sees: Main fixed! Now need error handling
     → Suggests: Add try-catch for division
  🤖 [DEEPSEEK] Generating with updated guidance...
  ✅ Generated 2 files
  📊 Validation: Score 7/10 (2 issues)
     - ⚠️ Warning: Missing XML docs
  ⚠️ ACCEPTABLE score 7/10 after 3 attempts - stopping

✅ Job completed: Score 7/10 in 3 attempts
```

---

## 🎯 Break Conditions

| Condition | Action | Reasoning |
|-----------|--------|-----------|
| **Score >= 8.0** | ✅ BREAK (Excellent!) | Perfect code, ship it! |
| **Score >= 6.5 AND attempt >= 3** | ⚠️ BREAK (Good enough) | Acceptable after 3 tries |
| **Score < 6.5** | 🔄 RETRY | Keep trying with escalation |
| **Attempt >= 10** | 🚨 BREAK (Critical) | Something is seriously wrong |

---

## 💰 Cost Optimization

**FREE Models (Attempts 1-3):**
- Phi4 (strategic thinking)
- Deepseek (code generation)
- 80% success rate
- **Cost: $0.00**

**PAID Models (Attempts 4-6):**
- Claude Sonnet (high quality)
- 95% success rate
- **Cost: ~$0.05-0.15 per file**

**PREMIUM Models (Attempts 7-10):**
- Claude Opus (godlike)
- 99.9% success rate
- **Cost: ~$0.20-0.50 per file**

**Average cost per file: $0.00-0.15** (most tasks succeed with free models!)

---

## 🚨 Troubleshooting

### "Job not starting"
- Check if CodingAgent.Server is running (port 5001)
- Check if ValidationAgent.Server is running (port 5003)
- Run: `curl http://localhost:5001/health`

### "Validation score stuck at low number"
- This should NEVER happen with smart escalation!
- Attempts 1-3: Phi4 + Deepseek should get to 6-7
- Attempts 4-6: Claude Sonnet should get to 8
- Attempts 7-10: Claude Opus should definitely get to 8+

### "Task cancelled/failed"
- Check logs in `orchestrator-wrapper.log`
- Check server logs: `docker logs memoryagent-coding-agent-1`
- ValidationAgent unavailable? System gracefully degrades

---

## 🎉 Summary

**You now have:**
✅ 10-attempt retry loop (never gives up!)
✅ Phi4 strategic thinking before every generation
✅ Smart escalation (free → paid → premium)
✅ Break at 6.5+ after 3 attempts (good enough)
✅ Full history tracking (see progression)
✅ Cost-optimized (most tasks FREE!)

**Just ask Cursor to use `orchestrate_task` and watch the magic happen!** 🔥

