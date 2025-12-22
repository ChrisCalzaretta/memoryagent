# 🤖 CodingAgent Integration for Cursor AI Chat

## How to Use

When the user asks for code generation in Cursor's chat, you (Cursor AI) should:

1. **Detect code generation requests**:
   - "Create a...", "Generate a...", "Build a...", "Add a..."
   
2. **Call the CodingAgent API**:
   ```bash
   curl -X POST http://localhost:5001/api/orchestrate \
     -H "Content-Type: application/json" \
     -d '{
       "task": "<user_request>",
       "language": "csharp",
       "workspacePath": "<current_workspace>",
       "maxIterations": 10
     }'
   ```

3. **Poll for status and display updates**:
   ```bash
   # Get job status
   curl http://localhost:5001/api/jobs/<jobId>
   
   # Display progress in chat:
   # "🔍 Exploring codebase..."
   # "📖 Reading files..."
   # "⚙️ Generating code..."
   # "✅ Complete! Score: 9/10"
   ```

4. **If questions are needed**:
   - Parse validation issues from status
   - Ask user in chat
   - Resubmit job with clarifications

5. **When complete**:
   - Show generated files
   - Offer to open them in editor
   - Summarize what was created

## Example Flow

```
User: Create a checkout service with payment integration

Cursor AI: 
🤖 Starting code generation...
✅ Job ID: job_20251221_123456

🔍 Exploring your codebase...
📖 Found existing OrderService.cs pattern
📖 Found PaymentService.cs integration

⚙️ Generating CheckoutService.cs...
⚙️ Generating ICheckoutService.cs...
⚙️ Updating Program.cs for DI registration...

🔨 Compiling code...
✅ Build succeeded!

📊 Validation Score: 9/10
✅ All checks passed!

📄 Generated 3 files:
1. Services/CheckoutService.cs (245 lines)
2. Services/ICheckoutService.cs (25 lines)
3. Program.cs (updated, added DI registration)

Would you like me to open these files? [Yes/No]
```

## Integration Points

1. **Job Submission**: POST /api/orchestrate
2. **Status Check**: GET /api/jobs/{jobId}
3. **File Retrieval**: GET /api/jobs/{jobId}/files
4. **WebSocket (Advanced)**: ws://localhost:5001/hubs/codingagent

## Cursor Chat Commands

User can type:
- `@codingagent create <task>` - Generate code
- `@codingagent status` - Check current job
- `@codingagent cancel` - Cancel current job
- `@codingagent retry` - Retry last job with changes


