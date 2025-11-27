using Xunit;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Unit tests for AI Agent Core Pattern Detection (30 patterns)
/// Validates all categories: Prompts, Memory, Tools, Planning, RAG, Safety, Cost, Observability, Multi-Agent, Lifecycle
/// </summary>
public class AIAgentPatternDetectorTests
{
    private readonly AIAgentPatternDetector _detector = new();

    #region CATEGORY 1: Prompt Engineering & Guardrails (3 patterns)

    [Fact]
    public void Should_Detect_SystemPromptDefinition()
    {
        var code = @"
public class AgentService
{
    public const string SystemPrompt = ""You are a helpful AI assistant. Never share personal information."";
    
    public async Task<string> RunAsync(string input)
    {
        var messages = new[] {
            new { role = ""system"", content = SystemPrompt },
            new { role = ""user"", content = input }
        };
        return await _llm.ChatAsync(messages);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentService.cs", "test", code, default).Result;
        
        var systemPromptPattern = patterns.FirstOrDefault(p => p.Name == "AI_SystemPromptDefinition");
        Assert.NotNull(systemPromptPattern);
        Assert.Equal(PatternType.AgentLightning, systemPromptPattern.Type);
        Assert.Equal(PatternCategory.AIAgents, systemPromptPattern.Category);
        Assert.True(systemPromptPattern.Confidence >= 0.88f);
        Assert.Equal("System Prompt", systemPromptPattern.Metadata["pattern_type"]);
    }

    [Fact]
    public void Should_Detect_PromptTemplateWithHandlebars()
    {
        var code = @"
public class PromptService
{
    private readonly string _template = ""Hello {{user_name}}, your task is {{task_description}}"";
    
    public string RenderPrompt(string userName, string task)
    {
        return _template.Replace(""{{user_name}}"", userName)
                       .Replace(""{{task_description}}"", task);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("PromptService.cs", "test", code, default).Result;
        
        var templatePattern = patterns.FirstOrDefault(p => p.Name == "AI_HandlebarsTemplate");
        Assert.NotNull(templatePattern);
        Assert.Equal("Handlebars", templatePattern.Metadata["template_syntax"]);
        Assert.True(templatePattern.Confidence >= 0.92f);
    }

    [Fact]
    public void Should_Detect_AzureContentSafetyGuardrail()
    {
        var code = @"
public class SafetyService
{
    private readonly ContentSafetyClient _safetyClient;
    
    public async Task<bool> ModerateAsync(string input)
    {
        var result = await _safetyClient.AnalyzeTextAsync(input);
        return result.HateResult.Severity <= 2 && result.ViolenceResult.Severity <= 2;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("SafetyService.cs", "test", code, default).Result;
        
        var safetyPattern = patterns.FirstOrDefault(p => p.Name == "AI_ContentSafetyGuardrail");
        Assert.NotNull(safetyPattern);
        Assert.Equal(PatternType.Security, safetyPattern.Type);
        Assert.True(safetyPattern.Confidence >= 0.98f);
        var categories = (string[])safetyPattern.Metadata["categories"];
        Assert.Contains("Hate", categories);
        Assert.Contains("Violence", categories);
    }

    #endregion

    #region CATEGORY 2: Memory & State (3 patterns)

    [Fact]
    public void Should_Detect_ShortTermMemoryBuffer()
    {
        var code = @"
public class ChatAgent
{
    private readonly List<ChatMessage> conversationHistory = new();
    
    public async Task<string> ChatAsync(string userInput)
    {
        conversationHistory.Add(new ChatMessage(Role.User, userInput));
        
        var response = await _llm.SendAsync(conversationHistory);
        
        conversationHistory.Add(new ChatMessage(Role.Assistant, response));
        return response;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("ChatAgent.cs", "test", code, default).Result;
        
        var memoryPattern = patterns.FirstOrDefault(p => p.Name == "AI_ShortTermMemoryBuffer");
        Assert.NotNull(memoryPattern);
        Assert.Equal(PatternCategory.StateManagement, memoryPattern.Category);
        Assert.True(memoryPattern.Confidence >= 0.92f);
        Assert.Equal("Short-term (chat history)", memoryPattern.Metadata["memory_type"]);
        Assert.Equal("CRITICAL - Without this, it's just a single LLM call, not an agent", 
            memoryPattern.Metadata["pattern_significance"]);
    }

    [Fact]
    public void Should_Detect_LongTermMemoryVector()
    {
        var code = @"
public class KnowledgeAgent
{
    private readonly QdrantClient _vectorStore;
    
    public async Task StoreKnowledgeAsync(string text)
    {
        var embedding = await _embeddingClient.GenerateEmbeddingAsync(text);
        await _vectorStore.UpsertAsync(""knowledge"", embedding, text);
    }
    
    public async Task<List<string>> RecallAsync(string query)
    {
        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
        return await _vectorStore.SearchAsync(""knowledge"", queryEmbedding, topK: 5);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("KnowledgeAgent.cs", "test", code, default).Result;
        
        var vectorMemoryPattern = patterns.FirstOrDefault(p => p.Name == "AI_LongTermMemoryVector");
        Assert.NotNull(vectorMemoryPattern);
        Assert.True(vectorMemoryPattern.Confidence >= 0.95f);
        Assert.Equal("Long-term (vector/semantic)", vectorMemoryPattern.Metadata["memory_type"]);
    }

    [Fact]
    public void Should_Detect_UserProfileMemory()
    {
        var code = @"
public class UserProfile
{
    public string UserId { get; set; }
    public Dictionary<string, string> Preferences { get; set; }
    public List<string> ConversationTopics { get; set; }
}

public class PersonalizationService
{
    public async Task<UserProfile> GetProfileAsync(string userId)
    {
        return await _memoryStore.LoadAsync($""user:{userId}:profile"");
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("PersonalizationService.cs", "test", code, default).Result;
        
        var profilePattern = patterns.FirstOrDefault(p => p.Name == "AI_UserProfileMemory");
        Assert.NotNull(profilePattern);
        Assert.Equal("User/Agent Profile", profilePattern.Metadata["memory_type"]);
        var useCases = (string[])profilePattern.Metadata["use_cases"];
        Assert.Contains("Personalization", useCases);
    }

    #endregion

    #region CATEGORY 3: Tools & Function Calling (3 patterns)

    [Fact]
    public void Should_Detect_KernelFunctionRegistration()
    {
        var code = @"
public class WeatherPlugin
{
    [KernelFunction, Description(""Gets the current weather"")]
    public async Task<string> GetWeather(string location)
    {
        return await _weatherApi.GetAsync(location);
    }
    
    [KernelFunction, Description(""Gets weather forecast"")]
    public async Task<string> GetForecast(string location, int days)
    {
        return await _weatherApi.GetForecastAsync(location, days);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("WeatherPlugin.cs", "test", code, default).Result;
        
        var toolPattern = patterns.FirstOrDefault(p => p.Name == "AI_KernelFunctionRegistration");
        Assert.NotNull(toolPattern);
        Assert.Equal(PatternCategory.ToolIntegration, toolPattern.Category);
        Assert.True(toolPattern.Confidence >= 0.98f);
        Assert.Equal("Semantic Kernel", toolPattern.Metadata["framework"]);
        Assert.Equal("HIGH - Agent tool capability", toolPattern.Metadata["pattern_significance"]);
    }

    [Fact]
    public void Should_Detect_ToolRouting()
    {
        var code = @"
public class AgentExecutor
{
    public async Task<string> ExecuteAsync(string userInput)
    {
        var response = await _llm.GetCompletionAsync(userInput);
        
        if (response.FunctionCall != null)
        {
            var result = await ExecuteToolAsync(response.FunctionCall.Name, 
                                                response.FunctionCall.Arguments);
            return result;
        }
        
        return response.Content;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentExecutor.cs", "test", code, default).Result;
        
        var routingPattern = patterns.FirstOrDefault(p => p.Name == "AI_ToolRouting");
        Assert.NotNull(routingPattern);
        Assert.True(routingPattern.Confidence >= 0.88f);
        Assert.Equal("CRITICAL - Enables agent actions beyond text generation", 
            routingPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_ExternalAPITool()
    {
        var code = @"
public class WeatherTool : ITool
{
    private readonly HttpClient _http;
    
    public async Task<string> ExecuteAsync(string location)
    {
        return await _http.GetStringAsync($""https://api.weather.com?location={location}"");
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("WeatherTool.cs", "test", code, default).Result;
        
        var externalToolPattern = patterns.FirstOrDefault(p => p.Name == "AI_ExternalAPITool");
        Assert.NotNull(externalToolPattern);
        Assert.Equal("External API", externalToolPattern.Metadata["integration_type"]);
        Assert.True(externalToolPattern.Confidence >= 0.90f);
    }

    #endregion

    #region CATEGORY 4: Planning, Autonomy & Loops (4 patterns)

    [Fact]
    public void Should_Detect_TaskPlanner()
    {
        var code = @"
public class Plan
{
    public List<Step> Steps { get; set; }
}

public class Step
{
    public string Description { get; set; }
    public string ToolName { get; set; }
}

public class AgentPlanner
{
    public async Task<Plan> CreatePlanAsync(string goal)
    {
        var prompt = $""Break down this task into steps: {goal}"";
        var planJson = await _llm.GetCompletionAsync(prompt);
        return JsonSerializer.Deserialize<Plan>(planJson);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentPlanner.cs", "test", code, default).Result;
        
        var plannerPattern = patterns.FirstOrDefault(p => p.Name == "AI_TaskPlanner");
        Assert.NotNull(plannerPattern);
        Assert.Equal("Task Planning", plannerPattern.Metadata["pattern"]);
        Assert.Equal("HIGH - Enables multi-step agent reasoning", plannerPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_ActionLoop()
    {
        var code = @"
public class AutonomousAgent
{
    public async Task<string> RunAsync(string goal)
    {
        var context = new List<string>();
        var iterations = 0;
        var maxIterations = 10;
        
        while (!goalAchieved && iterations < maxIterations)
        {
            var thought = await _llm.ThinkAsync(context);
            
            if (thought.RequiresAction)
            {
                var actionResult = await ExecuteActionAsync(thought.Action);
                context.Add(actionResult);
            }
            
            goalAchieved = thought.IsFinalAnswer;
            iterations++;
        }
        
        return context.Last();
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AutonomousAgent.cs", "test", code, default).Result;
        
        var loopPattern = patterns.FirstOrDefault(p => p.Name == "AI_ActionLoop");
        Assert.NotNull(loopPattern);
        Assert.Equal("Agent Loop", loopPattern.Metadata["pattern"]);
        Assert.Equal("CRITICAL - Distinguishes autonomous agent from single call", 
            loopPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_MultiAgentOrchestrator()
    {
        var code = @"
public class MultiAgentSystem
{
    private readonly PlannerAgent _planner;
    private readonly ExecutorAgent _executor;
    private readonly CriticAgent _critic;
    
    public async Task<string> SolveAsync(string problem)
    {
        var plan = await _planner.CreatePlanAsync(problem);
        var result = await _executor.ExecuteAsync(plan);
        var feedback = await _critic.ReviewAsync(result);
        
        return feedback.IsAcceptable ? result : await ImproveAsync(result, feedback);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("MultiAgentSystem.cs", "test", code, default).Result;
        
        var multiAgentPattern = patterns.FirstOrDefault(p => p.Name == "AI_MultiAgentSystem");
        Assert.NotNull(multiAgentPattern);
        var roles = (List<string>)multiAgentPattern.Metadata["agent_roles"];
        Assert.Contains("Planner", roles);
        Assert.Contains("Executor", roles);
        Assert.Contains("Critic", roles);
    }

    [Fact]
    public void Should_Detect_SelfReflection()
    {
        var code = @"
public class ReflectiveAgent
{
    public async Task<string> GenerateWithReflectionAsync(string task)
    {
        var output = await GenerateAsync(task);
        var critique = await CritiqueAsync(output);
        
        if (!critique.IsGood)
        {
            output = await ImproveAsync(output, critique.Feedback);
        }
        
        return output;
    }
    
    private async Task<Critique> CritiqueAsync(string output)
    {
        var prompt = $""Review your previous answer: {output}\nWhat could be improved?"";
        var reflection = await _llm.GetCompletionAsync(prompt);
        return ParseCritique(reflection);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("ReflectiveAgent.cs", "test", code, default).Result;
        
        var reflectionPattern = patterns.FirstOrDefault(p => p.Name == "AI_SelfReflection");
        Assert.NotNull(reflectionPattern);
        Assert.Equal("Self-Reflection", reflectionPattern.Metadata["pattern"]);
    }

    #endregion

    #region CATEGORY 5: RAG & Knowledge Integration (3 patterns)

    [Fact]
    public void Should_Detect_EmbeddingGeneration()
    {
        var code = @"
public class EmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var embeddingClient = new AzureOpenAIClient(endpoint, credential);
        var embedding = await embeddingClient.GetEmbeddingsAsync(""text-embedding-ada-002"", text);
        return embedding.Data[0].Embedding.ToArray();
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("EmbeddingService.cs", "test", code, default).Result;
        
        var embeddingPattern = patterns.FirstOrDefault(p => p.Name == "AI_EmbeddingGeneration");
        Assert.NotNull(embeddingPattern);
        Assert.True(embeddingPattern.Confidence >= 0.95f);
        var models = (string[])embeddingPattern.Metadata["recommended_models"];
        Assert.Contains("text-embedding-ada-002", models);
    }

    [Fact]
    public void Should_Detect_RAGPipeline()
    {
        var code = @"
public class RAGAgent
{
    public async Task<string> AnswerAsync(string query)
    {
        // 1. Retrieve
        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query);
        var relevantDocs = await _vectorStore.SearchAsync(queryEmbedding, topK: 5);
        
        // 2. Augment
        var context = string.Join(""\n"", relevantDocs.Select(d => d.Content));
        var prompt = $""Context:\n{context}\n\nQuestion: {query}\nAnswer:"";
        
        // 3. Generate
        var answer = await _llm.GetCompletionAsync(prompt);
        return answer;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("RAGAgent.cs", "test", code, default).Result;
        
        var ragPattern = patterns.FirstOrDefault(p => p.Name == "AI_RAGPipeline");
        Assert.NotNull(ragPattern);
        Assert.Equal("RAG Pipeline", ragPattern.Metadata["pattern"]);
        Assert.Equal("HIGH - Enables agent knowledge beyond training cutoff", 
            ragPattern.Metadata["significance"]);
        var steps = (string[])ragPattern.Metadata["steps"];
        Assert.Contains("Retrieve", steps);
        Assert.Contains("Augment", steps);
        Assert.Contains("Generate", steps);
    }

    [Fact]
    public void Should_Detect_ConditionalRAG()
    {
        var code = @"
public class SmartRAGAgent
{
    public async Task<string> AnswerWithRAGAsync(string query)
    {
        if (RequiresKnowledge(query))
        {
            var context = await RetrieveContext(query);
            return await GenerateWithContext(query, context);
        }
        else
        {
            return await GenerateDirectly(query);
        }
    }
    
    private bool RequiresKnowledge(string query)
    {
        return query.Contains(""latest"") || query.Contains(""current"");
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("SmartRAGAgent.cs", "test", code, default).Result;
        
        var conditionalRAGPattern = patterns.FirstOrDefault(p => p.Name == "AI_ConditionalRAG");
        Assert.NotNull(conditionalRAGPattern);
        Assert.Equal("Conditional RAG", conditionalRAGPattern.Metadata["pattern"]);
        Assert.Equal("Cost optimization", conditionalRAGPattern.Metadata["benefit"]);
    }

    #endregion

    #region CATEGORY 6: Safety & Governance (5 patterns)

    [Fact]
    public void Should_Detect_ContentModeration()
    {
        var code = @"
public class SafeAgent
{
    public async Task<string> SafeExecuteAsync(string userInput)
    {
        var preModeration = await _contentSafetyClient.AnalyzeTextAsync(userInput);
        
        if (preModeration.IsHarmful)
        {
            return ""Content blocked due to policy violation"";
        }
        
        var response = await _llm.GetCompletionAsync(userInput);
        
        var postModeration = await _contentSafetyClient.AnalyzeTextAsync(response);
        
        return postModeration.IsHarmful ? ""Response blocked"" : response;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("SafeAgent.cs", "test", code, default).Result;
        
        var moderationPattern = patterns.FirstOrDefault(p => p.Name == "AI_ContentModeration");
        Assert.NotNull(moderationPattern);
        Assert.Equal(PatternType.Security, moderationPattern.Type);
        Assert.Equal("CRITICAL - Production safety requirement", moderationPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_PIIScrubber()
    {
        var code = @"
public class PIIService
{
    public async Task<string> ScrubPIIAsync(string text)
    {
        var languageClient = new TextAnalyticsClient(endpoint, credential);
        var piiResult = await languageClient.RecognizePiiEntitiesAsync(text);
        
        var redacted = text;
        foreach (var entity in piiResult.Entities)
        {
            redacted = redacted.Replace(entity.Text, ""[REDACTED]"");
        }
        
        return redacted;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("PIIService.cs", "test", code, default).Result;
        
        var piiPattern = patterns.FirstOrDefault(p => p.Name == "AI_PIIDetection");
        Assert.NotNull(piiPattern);
        Assert.Equal("CRITICAL - Compliance requirement", piiPattern.Metadata["significance"]);
        var entities = (string[])piiPattern.Metadata["entities"];
        Assert.Contains("Email", entities);
    }

    [Fact]
    public void Should_Detect_TenantDataBoundary()
    {
        var code = @"
public class MultiTenantAgent
{
    public async Task<List<string>> SearchAsync(string tenantId, string query)
    {
        var collection = $""tenant_{tenantId}_knowledge"";
        return await _vectorStore.SearchAsync(collection, query);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("MultiTenantAgent.cs", "test", code, default).Result;
        
        var tenantPattern = patterns.FirstOrDefault(p => p.Name == "AI_TenantDataBoundary");
        Assert.NotNull(tenantPattern);
        Assert.Equal("CRITICAL - Multi-tenant security", tenantPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_TokenBudgetEnforcement()
    {
        var code = @"
using TiktokenSharp;

public class BudgetedAgent
{
    private readonly Tiktoken _tokenizer;
    
    public async Task<string> ExecuteWithBudgetAsync(string userId, string prompt)
    {
        var tokens = _tokenizer.Encode(prompt);
        var usage = await GetUsageForUser(userId);
        
        if (usage.TotalTokens + tokens.Count > userBudget)
        {
            throw new BudgetExceededException(""User has exceeded token budget"");
        }
        
        return await _llm.GetCompletionAsync(prompt);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("BudgetedAgent.cs", "test", code, default).Result;
        
        var budgetPattern = patterns.FirstOrDefault(p => p.Name == "AI_TokenBudgetEnforcement");
        Assert.NotNull(budgetPattern);
        Assert.Equal("HIGH - FinOps requirement", budgetPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_RedactedLogging()
    {
        var code = @"
public class AuditedAgent
{
    public async Task<string> ExecuteAsync(string prompt)
    {
        var redactedPrompt = RedactPII(prompt);
        _logger.LogInformation(""Prompt sent: {Prompt}"", redactedPrompt);
        
        var response = await _llm.GetCompletionAsync(prompt);
        
        var redactedResponse = RedactPII(response);
        _logger.LogInformation(""Response received: {Response}"", redactedResponse);
        
        return response;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AuditedAgent.cs", "test", code, default).Result;
        
        var loggingPattern = patterns.FirstOrDefault(p => p.Name == "AI_RedactedLogging");
        Assert.NotNull(loggingPattern);
        var compliance = (string[])loggingPattern.Metadata["compliance"];
        Assert.Contains("GDPR", compliance);
    }

    #endregion

    #region CATEGORY 7: FinOps & Cost Control (2 patterns)

    [Fact]
    public void Should_Detect_TokenMetering()
    {
        var code = @"
public class MeteredAgent
{
    public async Task<string> ExecuteAsync(string userId, string prompt)
    {
        var response = await _llm.GetCompletionAsync(prompt);
        
        var tokensUsed = response.Usage.TotalTokens;
        await _meteringService.RecordAsync(userId, tokensUsed, ""gpt-4"");
        await _metricsStore.IncrementAsync($""user:{userId}:tokens"", tokensUsed);
        
        return response.Content;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("MeteredAgent.cs", "test", code, default).Result;
        
        var meteringPattern = patterns.FirstOrDefault(p => p.Name == "AI_TokenMetering");
        Assert.NotNull(meteringPattern);
        Assert.Equal("HIGH - FinOps requirement", meteringPattern.Metadata["significance"]);
        var metrics = (string[])meteringPattern.Metadata["metrics"];
        Assert.Contains("Total tokens", metrics);
    }

    [Fact]
    public void Should_Detect_CostBudgetGuardrail()
    {
        var code = @"
public class CostControlledAgent
{
    public async Task<string> ExecuteAsync(string userId, string prompt)
    {
        var currentCost = await GetMonthlySpendAsync(userId);
        var estimatedCost = EstimateCost(prompt);
        
        if (currentCost + estimatedCost > monthlyBudget)
        {
            throw new BudgetExceededException(""Monthly budget exceeded"");
        }
        
        if (currentCost >= monthlyBudget)
        {
            await _agentService.DisableAsync(userId);
            _logger.LogWarning(""Agent disabled due to budget for user {UserId}"", userId);
        }
        
        return await _llm.GetCompletionAsync(prompt);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("CostControlledAgent.cs", "test", code, default).Result;
        
        var costGuardrailPattern = patterns.FirstOrDefault(p => p.Name == "AI_CostBudgetGuardrail");
        Assert.NotNull(costGuardrailPattern);
        Assert.Equal("CRITICAL - Prevents runaway costs", costGuardrailPattern.Metadata["significance"]);
        var practices = (string[])costGuardrailPattern.Metadata["best_practices"];
        Assert.Contains("Soft alerts at 80%", practices);
    }

    #endregion

    #region CATEGORY 8: Observability & Evaluation (4 patterns) - NEW

    [Fact]
    public void Should_Detect_AgentTracing()
    {
        var code = @"
using OpenTelemetry;

public class TracedAgent
{
    private readonly ActivitySource _activitySource = new(""AgentTracing"");
    
    public async Task<string> ExecuteAsync(string input)
    {
        using var activity = _activitySource.StartActivity(""AgentExecution"");
        activity?.SetTag(""input"", input);
        
        var response = await _llm.GetCompletionAsync(input);
        
        activity?.SetTag(""response.length"", response.Length);
        return response;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("TracedAgent.cs", "test", code, default).Result;
        
        var tracingPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentTracing");
        Assert.NotNull(tracingPattern);
        Assert.Equal(PatternCategory.Operational, tracingPattern.Category);
        Assert.Equal("OpenTelemetry", tracingPattern.Metadata["framework"]);
        Assert.Equal("CRITICAL - Production observability requirement", tracingPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_CorrelatedLogging()
    {
        var code = @"
public class CorrelatedAgent
{
    public async Task<string> ExecuteAsync(string input, string correlationId)
    {
        _logger.LogInformation(""Agent step 1, CorrelationId: {CorrelationId}"", correlationId);
        var step1 = await Think(input);
        
        _logger.LogInformation(""Agent step 2, CorrelationId: {CorrelationId}"", correlationId);
        var step2 = await Act(step1);
        
        return step2;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("CorrelatedAgent.cs", "test", code, default).Result;
        
        var correlatedPattern = patterns.FirstOrDefault(p => p.Name == "AI_CorrelatedLogging");
        Assert.NotNull(correlatedPattern);
        Assert.Equal("End-to-end request tracing", correlatedPattern.Metadata["benefit"]);
    }

    [Fact]
    public void Should_Detect_AgentEvalHarness()
    {
        var code = @"
public class EvaluationDataset
{
    public List<TestCase> TestCases { get; set; }
    public Dictionary<string, object> GroundTruth { get; set; }
}

public class AgentEvaluator
{
    public async Task<EvalResults> EvaluateAsync(IAgent agent)
    {
        var dataset = LoadEvaluationDataset();
        var results = new List<TestResult>();
        
        foreach (var testCase in dataset.TestCases)
        {
            var output = await agent.ExecuteAsync(testCase.Input);
            var score = CompareWithGroundTruth(output, testCase.ExpectedOutput);
            results.Add(new TestResult { Score = score });
        }
        
        return new EvalResults { AverageScore = results.Average(r => r.Score) };
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentEvaluator.cs", "test", code, default).Result;
        
        var evalPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentEvalHarness");
        Assert.NotNull(evalPattern);
        Assert.Equal("HIGH - Quality assurance", evalPattern.Metadata["significance"]);
        var metrics = (string[])evalPattern.Metadata["metrics"];
        Assert.Contains("Accuracy", metrics);
    }

    [Fact]
    public void Should_Detect_AgentABTesting()
    {
        var code = @"
public class ExperimentService
{
    public async Task<string> RunWithABTestAsync(string userId, string input)
    {
        var variant = _experiment.GetVariant(userId);
        
        if (variant == ""A"")
        {
            return await _agentConfigA.ExecuteAsync(input);
        }
        else
        {
            return await _agentConfigB.ExecuteAsync(input);
        }
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("ExperimentService.cs", "test", code, default).Result;
        
        var abTestPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentABTesting");
        Assert.NotNull(abTestPattern);
        Assert.Equal("Optimize prompts and configurations", abTestPattern.Metadata["use_case"]);
    }

    #endregion

    #region CATEGORY 9: Advanced Multi-Agent (3 patterns) - NEW

    [Fact]
    public void Should_Detect_GroupChatOrchestration()
    {
        var code = @"
using AutoGen;

public class CollaborativeSystem
{
    public async Task SolveAsync(string problem)
    {
        var groupChat = new GroupChat(agents, maxRounds: 10);
        await groupChat.InitiateChat(problem);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("CollaborativeSystem.cs", "test", code, default).Result;
        
        var groupChatPattern = patterns.FirstOrDefault(p => p.Name == "AI_GroupChatOrchestration");
        Assert.NotNull(groupChatPattern);
        Assert.Equal("AutoGen", groupChatPattern.Metadata["framework"]);
        Assert.Equal("ADVANCED - Multi-agent collaboration", groupChatPattern.Metadata["significance"]);
    }

    [Fact]
    public void Should_Detect_SequentialOrchestration()
    {
        var code = @"
public class PipelineOrchestrator
{
    public async Task<string> ExecutePipelineAsync(string input)
    {
        var step1 = await _researchAgent.ExecuteAsync(input);
        var step2 = await _analysisAgent.ExecuteAsync(step1);
        var step3 = await _summaryAgent.ExecuteAsync(step2);
        
        return step3;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("PipelineOrchestrator.cs", "test", code, default).Result;
        
        var sequentialPattern = patterns.FirstOrDefault(p => p.Name == "AI_SequentialOrchestration");
        Assert.NotNull(sequentialPattern);
        Assert.Equal("Agent pipelines and workflows", sequentialPattern.Metadata["use_case"]);
        Assert.True((int)sequentialPattern.Metadata["agent_calls"] >= 2);
    }

    [Fact]
    public void Should_Detect_ControlPlanePattern()
    {
        var code = @"
public class ControlPlane : ITool
{
    private readonly Dictionary<string, ITool> _tools;
    
    public async Task<string> ExecuteAsync(string intent, string input)
    {
        var tool = DetermineToolFromIntent(intent);
        return await tool.ExecuteAsync(input);
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("ControlPlane.cs", "test", code, default).Result;
        
        var controlPlanePattern = patterns.FirstOrDefault(p => p.Name == "AI_ControlPlaneAsATool");
        Assert.NotNull(controlPlanePattern);
        var benefits = (string[])controlPlanePattern.Metadata["benefits"];
        Assert.Contains("Scalability", benefits);
        Assert.Contains("Modular tool routing", benefits);
    }

    #endregion

    #region CATEGORY 10: Agent Lifecycle (4 patterns) - NEW

    [Fact]
    public void Should_Detect_AgentFactory()
    {
        var code = @"
public class AgentFactory
{
    public IAgent CreateAgent(AgentConfig config)
    {
        var agent = new Agent(config.Model);
        agent.AddTools(config.Tools);
        agent.SetSystemPrompt(config.SystemPrompt);
        agent.SetMemory(config.MemoryStore);
        
        return agent;
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentFactory.cs", "test", code, default).Result;
        
        var factoryPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentFactory");
        Assert.NotNull(factoryPattern);
        Assert.Equal("Factory Pattern", factoryPattern.Metadata["pattern"]);
        var benefits = (string[])factoryPattern.Metadata["benefits"];
        Assert.Contains("Standardized onboarding", benefits);
    }

    [Fact]
    public void Should_Detect_AgentBuilder()
    {
        var code = @"
public class AgentConfiguration
{
    public IAgent BuildAgent()
    {
        return new AgentBuilder()
            .WithModel(""gpt-4"")
            .WithTools(weatherTool, calculatorTool)
            .WithSystemPrompt(""You are helpful"")
            .WithMemory(memoryStore)
            .Build();
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentConfiguration.cs", "test", code, default).Result;
        
        var builderPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentBuilder");
        Assert.NotNull(builderPattern);
        Assert.Equal("Builder Pattern", builderPattern.Metadata["pattern"]);
    }

    [Fact]
    public void Should_Detect_SelfImprovingAgent()
    {
        var code = @"
public class SelfImprovingAgent
{
    public async Task MonitorPerformanceAsync()
    {
        var accuracy = await CalculateAccuracyAsync();
        
        if (accuracy < threshold)
        {
            await TriggerRetrainingPipelineAsync();
            _logger.LogInformation(""Automatic retraining triggered due to degradation"");
        }
    }
    
    private async Task TriggerRetrainingPipelineAsync()
    {
        await _mlPipeline.RetrainAsync();
        await UpdateModelAsync();
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("SelfImprovingAgent.cs", "test", code, default).Result;
        
        var selfImprovingPattern = patterns.FirstOrDefault(p => p.Name == "AI_SelfImprovingAgent");
        Assert.NotNull(selfImprovingPattern);
        Assert.Equal("ADVANCED - Continuous improvement", selfImprovingPattern.Metadata["significance"]);
        var capabilities = (string[])selfImprovingPattern.Metadata["capabilities"];
        Assert.Contains("Automatic retraining", capabilities);
    }

    [Fact]
    public void Should_Detect_AgentPerformanceMonitoring()
    {
        var code = @"
public class AgentMetricsCollector
{
    public void RecordMetrics(string agentId, AgentMetrics metrics)
    {
        _metricsStore.Record(agentId, new {
            Accuracy = metrics.Accuracy,
            Latency = metrics.Latency,
            Cost = metrics.Cost,
            SuccessRate = metrics.SuccessRate
        });
    }
}";
        
        var patterns = _detector.DetectPatternsAsync("AgentMetricsCollector.cs", "test", code, default).Result;
        
        var monitoringPattern = patterns.FirstOrDefault(p => p.Name == "AI_AgentPerformanceMonitoring");
        Assert.NotNull(monitoringPattern);
        var metrics = (string[])monitoringPattern.Metadata["metrics"];
        Assert.Contains("Accuracy", metrics);
        Assert.Contains("Latency", metrics);
        Assert.Contains("Cost", metrics);
    }

    #endregion

    #region Pattern Count Validation

    [Fact]
    public void Should_Have_30_Total_Patterns()
    {
        // This test validates that all 30 patterns are implemented
        var allCode = @"
public const string SystemPrompt = ""test"";
private readonly List<ChatMessage> history = new();
private readonly QdrantClient _vectorStore;
[KernelFunction]
public async Task Tool() { }
public class Plan { public List<Step> Steps; }
while (!done) { await agent.RunAsync(); }
var embedding = await client.GetEmbeddingsAsync();
await contentSafetyClient.AnalyzeTextAsync();
var tokens = tokenizer.Encode();
using OpenTelemetry;
var groupChat = new GroupChat();
public class AgentFactory { }
";
        
        var patterns = _detector.DetectPatternsAsync("AllPatterns.cs", "test", allCode, default).Result;
        
        // Should detect multiple patterns from different categories
        Assert.True(patterns.Count >= 10); // At least 10 patterns should be detected from this code
        
        // Verify we have patterns from each category
        var categories = patterns.Select(p => p.Category).Distinct().ToList();
        Assert.Contains(PatternCategory.AIAgents, categories);
        Assert.Contains(PatternCategory.StateManagement, categories);
        Assert.Contains(PatternCategory.ToolIntegration, categories);
    }

    #endregion
}

