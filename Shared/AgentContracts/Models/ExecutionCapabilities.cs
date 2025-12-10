namespace AgentContracts.Models;

/// <summary>
/// Describes what the ExecutionService can execute
/// Send this to CodingAgent so it knows what languages/runtimes are available
/// </summary>
public class ExecutionCapabilities
{
    /// <summary>
    /// List of supported execution environments
    /// </summary>
    public List<ExecutionEnvironment> Environments { get; set; } = new();

    /// <summary>
    /// Generate a prompt-friendly description of capabilities
    /// </summary>
    public string ToPromptString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== EXECUTION CAPABILITIES ===");
        sb.AppendLine("Your generated code WILL BE EXECUTED in Docker. Choose from these supported languages:");
        sb.AppendLine();
        
        foreach (var env in Environments.Where(e => !e.SkipExecution))
        {
            sb.AppendLine($"**{env.Language.ToUpperInvariant()}**");
            sb.AppendLine($"  - Docker Image: {env.DockerImage}");
            sb.AppendLine($"  - File Extension: {env.FileExtension}");
            sb.AppendLine($"  - Build: {env.BuildCommand}");
            sb.AppendLine($"  - Run: {env.RunCommand}");
            sb.AppendLine();
        }
        
        sb.AppendLine("⚠️ CRITICAL REQUIREMENTS:");
        sb.AppendLine("1. Pick ONE primary language that can be executed");
        sb.AppendLine("2. For web apps, prefer backend languages (Python/Flask, Node/Express, C#/ASP.NET)");
        sb.AppendLine("3. Include execution instructions in your response");
        sb.AppendLine("4. Your code WILL be run - make sure it actually works!");
        sb.AppendLine("5. ❌ DO NOT use interactive input (input(), readline(), Console.ReadLine())");
        sb.AppendLine("   - Code runs in Docker WITHOUT user interaction");
        sb.AppendLine("   - Use hardcoded test values or command-line arguments instead");
        sb.AppendLine("   - Example: Instead of 'x = input()', use 'x = 5' or 'x = sys.argv[1]'");
        
        return sb.ToString();
    }

    /// <summary>
    /// Create default capabilities from DockerLanguageConfig
    /// </summary>
    public static ExecutionCapabilities CreateDefault()
    {
        return new ExecutionCapabilities
        {
            Environments = new List<ExecutionEnvironment>
            {
                new() { Language = "python", DockerImage = "python:3.12-slim", FileExtension = ".py", 
                        BuildCommand = "python -c \"import ast; ast.parse(open('{mainFile}').read())\"",
                        RunCommand = "python {mainFile}", MainFilePatterns = new[] { "main.py", "app.py", "*.py" } },
                
                new() { Language = "javascript", DockerImage = "node:20-slim", FileExtension = ".js",
                        BuildCommand = "node --check {mainFile}", RunCommand = "node {mainFile}",
                        MainFilePatterns = new[] { "index.js", "main.js", "app.js", "*.js" } },
                
                new() { Language = "typescript", DockerImage = "node:20-slim", FileExtension = ".ts",
                        BuildCommand = "npx tsc --noEmit {mainFile}", RunCommand = "npx tsx {mainFile}",
                        MainFilePatterns = new[] { "index.ts", "main.ts", "*.ts" } },
                
                new() { Language = "csharp", DockerImage = "mcr.microsoft.com/dotnet/sdk:9.0", FileExtension = ".cs",
                        BuildCommand = "dotnet build", RunCommand = "dotnet run",
                        MainFilePatterns = new[] { "Program.cs", "*.cs" } },
                
                new() { Language = "go", DockerImage = "golang:1.22-alpine", FileExtension = ".go",
                        BuildCommand = "go build -o /tmp/app {mainFile}", RunCommand = "/tmp/app",
                        MainFilePatterns = new[] { "main.go", "*.go" } },
                
                new() { Language = "rust", DockerImage = "rust:1.75-slim", FileExtension = ".rs",
                        BuildCommand = "rustc {mainFile} -o /tmp/app", RunCommand = "/tmp/app",
                        MainFilePatterns = new[] { "main.rs", "*.rs" } },
                
                new() { Language = "java", DockerImage = "eclipse-temurin:21-jdk", FileExtension = ".java",
                        BuildCommand = "javac {mainFile}", RunCommand = "java {className}",
                        MainFilePatterns = new[] { "Main.java", "App.java", "*.java" } },
                
                new() { Language = "ruby", DockerImage = "ruby:3.3-slim", FileExtension = ".rb",
                        BuildCommand = "ruby -c {mainFile}", RunCommand = "ruby {mainFile}",
                        MainFilePatterns = new[] { "main.rb", "app.rb", "*.rb" } },
                
                new() { Language = "php", DockerImage = "php:8.3-cli", FileExtension = ".php",
                        BuildCommand = "php -l {mainFile}", RunCommand = "php {mainFile}",
                        MainFilePatterns = new[] { "index.php", "main.php", "*.php" } },
                
                new() { Language = "shell", DockerImage = "bash:5", FileExtension = ".sh",
                        BuildCommand = "bash -n {mainFile}", RunCommand = "bash {mainFile}",
                        MainFilePatterns = new[] { "main.sh", "run.sh", "*.sh" } },
                
                // Skip execution for these (need browser/database)
                new() { Language = "html", DockerImage = "nginx:alpine", FileExtension = ".html",
                        SkipExecution = true, Note = "HTML requires a browser to test" },
                new() { Language = "css", DockerImage = "node:20-slim", FileExtension = ".css",
                        SkipExecution = true, Note = "CSS requires a browser to test" },
                new() { Language = "sql", DockerImage = "postgres:16-alpine", FileExtension = ".sql",
                        SkipExecution = true, Note = "SQL requires a database to test" }
            }
        };
    }
}

/// <summary>
/// A single execution environment (language + Docker config)
/// </summary>
public class ExecutionEnvironment
{
    public required string Language { get; set; }
    public required string DockerImage { get; set; }
    public required string FileExtension { get; set; }
    public string BuildCommand { get; set; } = "";
    public string RunCommand { get; set; } = "";
    public string[] MainFilePatterns { get; set; } = Array.Empty<string>();
    public bool SkipExecution { get; set; } = false;
    public string? Note { get; set; }
}

/// <summary>
/// Execution instructions returned by CodingAgent
/// Tells ExecutionService exactly how to run the generated code
/// </summary>
public class ExecutionInstructions
{
    /// <summary>
    /// Primary language of the generated code
    /// </summary>
    public string Language { get; set; } = "unknown";
    
    /// <summary>
    /// The main file to execute (auto-detected if not provided)
    /// </summary>
    public string MainFile { get; set; } = "";
    
    /// <summary>
    /// Command to build/compile (optional)
    /// </summary>
    public string? BuildCommand { get; set; }
    
    /// <summary>
    /// Command to run the code (auto-generated if not provided)
    /// </summary>
    public string RunCommand { get; set; } = "";
    
    /// <summary>
    /// Expected output (for validation)
    /// </summary>
    public string? ExpectedOutput { get; set; }
    
    /// <summary>
    /// Any setup commands needed (npm install, pip install, etc.)
    /// </summary>
    public List<string> SetupCommands { get; set; } = new();
    
    /// <summary>
    /// Working directory (relative to temp dir)
    /// </summary>
    public string WorkingDirectory { get; set; } = ".";
}

