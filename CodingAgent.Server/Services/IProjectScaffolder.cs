namespace CodingAgent.Server.Services;

/// <summary>
/// Auto-scaffolds project structures using official CLI tools
/// (dotnet new, flutter create, npm init, etc.)
/// </summary>
public interface IProjectScaffolder
{
    /// <summary>
    /// Detect project type from task and scaffold base project
    /// </summary>
    Task<ScaffoldResult> ScaffoldProjectAsync(string task, string? language, CancellationToken cancellationToken);
}

public class ScaffoldResult
{
    public bool Success { get; set; }
    public string ProjectType { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public List<ScaffoldedFile> Files { get; set; } = new();
    public string? Error { get; set; }
    public string Command { get; set; } = "";
}

public class ScaffoldedFile
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsGenerated { get; set; } // True if from LLM, false if scaffolded
}


