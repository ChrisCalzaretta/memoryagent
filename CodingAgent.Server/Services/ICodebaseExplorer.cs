namespace CodingAgent.Server.Services;

/// <summary>
/// Explores workspace to provide context for LLM code generation
/// Makes local LLMs work like Claude - exploring BEFORE generating
/// </summary>
public interface ICodebaseExplorer
{
    /// <summary>
    /// Analyze workspace and build rich context for code generation
    /// </summary>
    Task<CodebaseContext> ExploreAsync(string workspacePath, CancellationToken cancellationToken);
}

public class CodebaseContext
{
    // Project structure
    public string RootPath { get; set; } = "";
    public List<string> Directories { get; set; } = new();
    public List<string> Files { get; set; } = new();
    
    // Key file contents (for pattern matching)
    public Dictionary<string, string> KeyFileContents { get; set; } = new(); // filename -> content
    
    // Extracted patterns
    public List<string> Namespaces { get; set; } = new();
    public List<string> ServiceRegistrations { get; set; } = new();
    public List<string> CommonImports { get; set; } = new();
    public List<string> Dependencies { get; set; } = new(); // NuGet packages
    
    // Project metadata
    public string? ProjectType { get; set; } // "blazor", "webapi", "console", etc.
    public string? TargetFramework { get; set; } // "net9.0"
    public bool IsEmpty { get; set; }
    
    /// <summary>
    /// Generate human-readable summary for LLM prompt
    /// </summary>
    public string ToLLMSummary()
    {
        var sb = new System.Text.StringBuilder();
        
        if (IsEmpty)
        {
            sb.AppendLine("📁 WORKSPACE: Empty (new project)");
            return sb.ToString();
        }
        
        sb.AppendLine("📁 WORKSPACE CONTEXT:");
        sb.AppendLine();
        
        // Project info
        if (!string.IsNullOrEmpty(ProjectType))
        {
            sb.AppendLine($"🎯 Project Type: {ProjectType}");
            if (!string.IsNullOrEmpty(TargetFramework))
            {
                sb.AppendLine($"🎯 Target Framework: {TargetFramework}");
            }
            sb.AppendLine();
        }
        
        // Directory structure
        if (Directories.Any())
        {
            sb.AppendLine("📂 DIRECTORY STRUCTURE:");
            foreach (var dir in Directories.Take(20))
            {
                sb.AppendLine($"  - {dir}");
            }
            if (Directories.Count > 20)
            {
                sb.AppendLine($"  ... and {Directories.Count - 20} more directories");
            }
            sb.AppendLine();
        }
        
        // Existing files
        if (Files.Any())
        {
            sb.AppendLine($"📄 EXISTING FILES ({Files.Count} files):");
            foreach (var file in Files.Take(30))
            {
                sb.AppendLine($"  - {file}");
            }
            if (Files.Count > 30)
            {
                sb.AppendLine($"  ... and {Files.Count - 30} more files");
            }
            sb.AppendLine();
        }
        
        // Key file contents
        if (KeyFileContents.Any())
        {
            sb.AppendLine("📝 KEY FILE CONTENTS (for pattern matching):");
            sb.AppendLine();
            
            foreach (var kvp in KeyFileContents)
            {
                sb.AppendLine($"--- {kvp.Key} ---");
                // Truncate to first 80 lines
                var lines = kvp.Value.Split('\n');
                sb.AppendLine(string.Join('\n', lines.Take(80)));
                if (lines.Length > 80)
                {
                    sb.AppendLine($"... ({lines.Length - 80} more lines)");
                }
                sb.AppendLine($"--- END {kvp.Key} ---");
                sb.AppendLine();
            }
        }
        
        // Namespaces
        if (Namespaces.Any())
        {
            sb.AppendLine("📦 EXISTING NAMESPACES:");
            foreach (var ns in Namespaces.Distinct().Take(15))
            {
                sb.AppendLine($"  - {ns}");
            }
            sb.AppendLine();
        }
        
        // Service patterns
        if (ServiceRegistrations.Any())
        {
            sb.AppendLine("🔧 SERVICE REGISTRATION PATTERNS:");
            foreach (var svc in ServiceRegistrations.Take(10))
            {
                sb.AppendLine($"  {svc}");
            }
            sb.AppendLine();
        }
        
        // Common imports
        if (CommonImports.Any())
        {
            sb.AppendLine("📥 COMMON IMPORTS:");
            foreach (var import in CommonImports.Distinct().Take(15))
            {
                sb.AppendLine($"  - {import}");
            }
            sb.AppendLine();
        }
        
        // Dependencies
        if (Dependencies.Any())
        {
            sb.AppendLine("📦 NUGET PACKAGES:");
            foreach (var dep in Dependencies.Take(15))
            {
                sb.AppendLine($"  - {dep}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("🎯 INSTRUCTIONS:");
        sb.AppendLine("✅ Follow existing patterns and naming conventions");
        sb.AppendLine("✅ Use existing namespaces where appropriate");
        sb.AppendLine("✅ Register services following existing patterns");
        sb.AppendLine("✅ Use imports that match existing code");
        sb.AppendLine("✅ Make your code fit seamlessly into this project");
        
        return sb.ToString();
    }
}


