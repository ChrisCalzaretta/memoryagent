namespace AgentContracts.Services;

/// <summary>
/// Client for Ollama LLM API
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Generate text using a model
    /// </summary>
    Task<OllamaResponse> GenerateAsync(
        string model, 
        string prompt, 
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate text using a vision model with images (for models like LLaVA)
    /// </summary>
    Task<OllamaResponse> GenerateWithVisionAsync(
        string model, 
        string prompt, 
        List<string> images,
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Chat with a model
    /// </summary>
    Task<OllamaResponse> ChatAsync(
        string model,
        List<ChatMessage> messages,
        int? port = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a model is loaded on a specific port
    /// </summary>
    Task<bool> IsModelLoadedAsync(string model, int port, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get list of available models
    /// </summary>
    Task<List<string>> GetModelsAsync(int? port = null, CancellationToken cancellationToken = default);
}

public class OllamaResponse
{
    public required string Response { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalDurationMs { get; set; }
    public int PromptTokens { get; set; }
    public int ResponseTokens { get; set; }
}

public class ChatMessage
{
    public required string Role { get; set; } // "system", "user", "assistant"
    public required string Content { get; set; }
}


