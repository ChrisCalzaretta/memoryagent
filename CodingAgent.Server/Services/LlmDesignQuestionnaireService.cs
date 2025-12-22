using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// 🤖🎨 LLM-BASED DESIGN QUESTIONNAIRE ANSWERER
/// Uses an LLM to automatically answer design questionnaire based on task description
/// Instead of asking user OR using generic defaults!
/// </summary>
public class LlmDesignQuestionnaireService
{
    private readonly IOllamaClient _ollama;
    private readonly ILogger<LlmDesignQuestionnaireService> _logger;
    
    public LlmDesignQuestionnaireService(
        IOllamaClient ollama,
        ILogger<LlmDesignQuestionnaireService> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }
    
    /// <summary>
    /// Analyze task and automatically answer design questionnaire using LLM
    /// </summary>
    public async Task<Dictionary<string, string>> AnswerQuestionnaireAsync(
        string taskDescription,
        string language,
        DesignQuestionnaire questionnaire,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🤖 Using LLM to answer design questionnaire for task");
        
        var prompt = BuildQuestionnairePrompt(taskDescription, language, questionnaire);
        
        // Use Phi4 or Qwen for design analysis (fast, good at understanding context)
        var model = "phi4:latest";
        
        var response = await _ollama.GenerateAsync(
            model,
            prompt,
            null, // no system prompt
            11434, // default port
            cancellationToken);
        
        var answers = ParseAnswers(response.Response, questionnaire);
        
        _logger.LogInformation("✅ LLM answered {Count} design questions", answers.Count);
        
        return answers;
    }
    
    private string BuildQuestionnairePrompt(
        string taskDescription,
        string language,
        DesignQuestionnaire questionnaire)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a design expert analyzing a software development task.");
        sb.AppendLine("Based on the task description, answer the following design questions.");
        sb.AppendLine();
        sb.AppendLine("TASK DESCRIPTION:");
        sb.AppendLine(taskDescription);
        sb.AppendLine();
        sb.AppendLine($"LANGUAGE/FRAMEWORK: {language}");
        sb.AppendLine();
        sb.AppendLine("DESIGN QUESTIONS:");
        
        foreach (var section in questionnaire.Sections)
        {
            sb.AppendLine($"\n## {section.Title}");
            foreach (var question in section.Questions)
            {
                sb.AppendLine($"\n{question.Id}: {question.Text}");
                
                if (question.Options != null && question.Options.Any())
                {
                    sb.AppendLine($"Options: {string.Join(", ", question.Options)}");
                }
                
                if (!string.IsNullOrEmpty(question.DefaultValue))
                {
                    sb.AppendLine($"Default: {question.DefaultValue}");
                }
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Analyze the task description carefully");
        sb.AppendLine("2. Extract design hints (colors, themes, styles mentioned)");
        sb.AppendLine("3. Infer appropriate answers based on context");
        sb.AppendLine("4. For colors: Extract from task OR suggest based on theme");
        sb.AppendLine("5. For tone: Match the task's nature (professional, playful, mystical, etc.)");
        sb.AppendLine("6. For accessibility: Always use WCAG_AA minimum");
        sb.AppendLine();
        sb.AppendLine("OUTPUT FORMAT:");
        sb.AppendLine("Provide answers in this exact format:");
        sb.AppendLine("ANSWER_KEY: value");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("brandName: Wizard Chess");
        sb.AppendLine("primaryColor: #8B5CF6");
        sb.AppendLine("tone: mystical");
        
        return sb.ToString();
    }
    
    private Dictionary<string, string> ParseAnswers(string llmResponse, DesignQuestionnaire questionnaire)
    {
        var answers = new Dictionary<string, string>();
        
        // Parse LLM response for key-value pairs
        var lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Match pattern: "key: value"
            var match = Regex.Match(line.Trim(), @"^(\w+):\s*(.+)$");
            
            if (match.Success)
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();
                
                // Check if this is a valid question key (search across all sections)
                var questionExists = questionnaire.Sections
                    .SelectMany(s => s.Questions)
                    .Any(q => q.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
                
                if (questionExists)
                {
                    answers[key] = value;
                    _logger.LogDebug("  ✅ {Key}: {Value}", key, value);
                }
            }
        }
        
        // Fill in missing answers with smart defaults
        foreach (var section in questionnaire.Sections)
        {
            foreach (var question in section.Questions)
            {
                if (!answers.ContainsKey(question.Id))
                {
                    var defaultValue = question.DefaultValue ?? GetSmartDefault(question);
                    answers[question.Id] = defaultValue;
                    _logger.LogDebug("  ⚙️ {Key}: {Value} (default)", question.Id, defaultValue);
                }
            }
        }
        
        return answers;
    }
    
    private string GetSmartDefault(QuestionnaireQuestion question)
    {
        // Smart defaults based on question type
        return question.Id.ToLowerInvariant() switch
        {
            "brandname" => "Modern App",
            "primarycolor" => "#3B82F6", // Blue
            "secondarycolor" => "#8B5CF6", // Purple
            "accentcolor" => "#F59E0B", // Amber
            "backgroundcolor" => "#FFFFFF",
            "textcolor" => "#1F2937",
            "headingfont" => "system-ui, sans-serif",
            "bodyfont" => "system-ui, sans-serif",
            "monofont" => "monospace",
            "tone" => "professional",
            "spacing" => "8px",
            "accessibility" => "WCAG_AA",
            _ => question.Options?.FirstOrDefault() ?? "default"
        };
    }
}

