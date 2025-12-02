namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for LLM text generation using Ollama
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Generate text using the configured LLM model
    /// </summary>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

