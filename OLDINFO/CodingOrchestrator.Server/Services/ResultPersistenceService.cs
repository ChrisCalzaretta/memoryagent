using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingOrchestrator.Server.Clients;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Service for persisting successful results to Lightning memory
/// and recording prompt feedback for learning
/// </summary>
public interface IResultPersistenceService
{
    /// <summary>
    /// Store a successful coding result in Lightning memory
    /// </summary>
    Task StoreSuccessAsync(
        OrchestrateTaskRequest request,
        GenerateCodeResponse generatedCode,
        int validationScore,
        CancellationToken cancellationToken);

    /// <summary>
    /// Record failure feedback for learning
    /// </summary>
    Task RecordFailureAsync(int validationScore, CancellationToken cancellationToken);

    /// <summary>
    /// Write generated files to workspace (when autoWriteFiles is enabled)
    /// </summary>
    Task<List<string>> WriteFilesToWorkspaceAsync(
        List<GeneratedFile> files,
        string workspacePath,
        CancellationToken cancellationToken);
}

public class ResultPersistenceService : IResultPersistenceService
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<ResultPersistenceService> _logger;

    public ResultPersistenceService(
        IMemoryAgentClient memoryAgent,
        ILogger<ResultPersistenceService> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
    }

    public async Task StoreSuccessAsync(
        OrchestrateTaskRequest request,
        GenerateCodeResponse generatedCode,
        int validationScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var answer = generatedCode.Explanation + "\n\n" +
                string.Join("\n\n", generatedCode.FileChanges.Select(f => 
                    $"// File: {f.Path}\n{f.Content}"));

            await _memoryAgent.StoreQaAsync(
                request.Task,
                answer,
                generatedCode.FileChanges.Select(f => f.Path).ToList(),
                request.Context,
                cancellationToken);

            await _memoryAgent.RecordPromptFeedbackAsync(
                "coding_agent_system",
                wasSuccessful: true,
                rating: validationScore,
                cancellationToken);

            _logger.LogInformation("✅ Stored successful result in Lightning memory");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to store result in Lightning memory (non-critical)");
        }
    }

    public async Task RecordFailureAsync(int validationScore, CancellationToken cancellationToken)
    {
        try
        {
            await _memoryAgent.RecordPromptFeedbackAsync(
                "coding_agent_system",
                wasSuccessful: false,
                rating: validationScore,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to record feedback in Lightning (non-critical)");
        }
    }

    public async Task<List<string>> WriteFilesToWorkspaceAsync(
        List<GeneratedFile> files,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var writtenFiles = new List<string>();

        foreach (var file in files)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Build full path
                var fullPath = Path.IsPathRooted(file.Path)
                    ? file.Path
                    : Path.Combine(workspacePath, file.Path);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogDebug("Created directory: {Directory}", directory);
                }

                // Check if file exists and create backup if needed
                if (File.Exists(fullPath) && file.ChangeType == FileChangeType.Created)
                {
                    var backupPath = fullPath + ".backup";
                    File.Copy(fullPath, backupPath, overwrite: true);
                    _logger.LogInformation("Created backup: {BackupPath}", backupPath);
                }

                // Write the file
                await File.WriteAllTextAsync(fullPath, file.Content, cancellationToken);
                writtenFiles.Add(file.Path);

                _logger.LogInformation("📝 Wrote file: {FilePath} ({ChangeType})", file.Path, file.ChangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write file: {FilePath}", file.Path);
            }
        }

        return writtenFiles;
    }
}

