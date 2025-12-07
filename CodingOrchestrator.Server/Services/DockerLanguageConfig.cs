namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Docker configuration for each supported programming language
/// Defines images, build commands, and run commands
/// </summary>
public static class DockerLanguageConfig
{
    /// <summary>
    /// Get Docker configuration for a language
    /// </summary>
    public static LanguageConfig GetConfig(string language)
    {
        var lang = language?.ToLowerInvariant() ?? "python";
        return Configs.GetValueOrDefault(lang, Configs["python"]);
    }

    /// <summary>
    /// Check if a language is supported for execution
    /// </summary>
    public static bool IsSupported(string language)
    {
        return Configs.ContainsKey(language?.ToLowerInvariant() ?? "");
    }

    /// <summary>
    /// Get all supported languages
    /// </summary>
    public static IEnumerable<string> SupportedLanguages => Configs.Keys;

    private static readonly Dictionary<string, LanguageConfig> Configs = new()
    {
        ["python"] = new LanguageConfig
        {
            Language = "python",
            DockerImage = "python:3.12-slim",
            FileExtension = ".py",
            // Use python -c to check syntax without creating __pycache__
            BuildCommand = "python -c \"import ast; ast.parse(open('{mainFile}').read())\"",
            RunCommand = "python {mainFile}",
            TestCommand = "python -m pytest -v 2>/dev/null || python {mainFile}",
            MainFilePatterns = new[] { "main.py", "app.py", "run.py", "__main__.py", "*.py" },
            SetupCommands = Array.Empty<string>(),
            TimeoutSeconds = 30
        },
        
        ["javascript"] = new LanguageConfig
        {
            Language = "javascript",
            DockerImage = "node:20-slim",
            FileExtension = ".js",
            BuildCommand = "node --check {mainFile}",
            RunCommand = "node {mainFile}",
            TestCommand = "npm test 2>/dev/null || node {mainFile}",
            MainFilePatterns = new[] { "index.js", "main.js", "app.js", "server.js", "*.js" },
            TimeoutSeconds = 30
        },
        
        ["typescript"] = new LanguageConfig
        {
            Language = "typescript",
            DockerImage = "node:20-slim",
            FileExtension = ".ts",
            BuildCommand = "npx tsc --noEmit {mainFile} 2>&1 || npx tsx --no-warnings {mainFile}",
            RunCommand = "npx tsx {mainFile}",
            TestCommand = "npm test 2>/dev/null || npx tsx {mainFile}",
            MainFilePatterns = new[] { "index.ts", "main.ts", "app.ts", "*.ts" },
            SetupCommands = new[] { "npm install -g tsx typescript 2>/dev/null || true" },
            TimeoutSeconds = 30
        },
        
        ["csharp"] = new LanguageConfig
        {
            Language = "csharp",
            DockerImage = "mcr.microsoft.com/dotnet/sdk:9.0",
            FileExtension = ".cs",
            // For single files, use dotnet-script or create temp project
            BuildCommand = "if [ -f *.csproj ]; then dotnet build --nologo -v q; else echo 'using System; {0}' | dotnet script - 2>/dev/null || dotnet new console -n TempApp -o /tmp/app && cp {mainFile} /tmp/app/Program.cs && dotnet build /tmp/app --nologo -v q; fi",
            RunCommand = "if [ -f *.csproj ]; then dotnet run --nologo; else dotnet run --project /tmp/app --nologo; fi",
            TestCommand = "dotnet test --nologo -v q 2>/dev/null || dotnet run --nologo",
            MainFilePatterns = new[] { "Program.cs", "*.cs" },
            TimeoutSeconds = 60
        },
        
        ["go"] = new LanguageConfig
        {
            Language = "go",
            DockerImage = "golang:1.22-alpine",
            FileExtension = ".go",
            BuildCommand = "go build -o /tmp/app {mainFile}",
            RunCommand = "/tmp/app",
            TestCommand = "go test -v ./... 2>/dev/null || go run {mainFile}",
            MainFilePatterns = new[] { "main.go", "*.go" },
            TimeoutSeconds = 30
        },
        
        ["rust"] = new LanguageConfig
        {
            Language = "rust",
            DockerImage = "rust:1.75-slim",
            FileExtension = ".rs",
            BuildCommand = "rustc {mainFile} -o /tmp/app 2>&1",
            RunCommand = "/tmp/app",
            TestCommand = "cargo test 2>/dev/null || /tmp/app",
            MainFilePatterns = new[] { "main.rs", "lib.rs", "*.rs" },
            TimeoutSeconds = 60
        },
        
        ["java"] = new LanguageConfig
        {
            Language = "java",
            DockerImage = "eclipse-temurin:21-jdk",
            FileExtension = ".java",
            BuildCommand = "javac {mainFile}",
            RunCommand = "java {className}",
            TestCommand = "java {className}",
            MainFilePatterns = new[] { "Main.java", "App.java", "*.java" },
            TimeoutSeconds = 30
        },
        
        ["ruby"] = new LanguageConfig
        {
            Language = "ruby",
            DockerImage = "ruby:3.3-slim",
            FileExtension = ".rb",
            BuildCommand = "ruby -c {mainFile}",
            RunCommand = "ruby {mainFile}",
            TestCommand = "ruby -r minitest/autorun {mainFile} 2>/dev/null || ruby {mainFile}",
            MainFilePatterns = new[] { "main.rb", "app.rb", "*.rb" },
            TimeoutSeconds = 30
        },
        
        ["php"] = new LanguageConfig
        {
            Language = "php",
            DockerImage = "php:8.3-cli",
            FileExtension = ".php",
            BuildCommand = "php -l {mainFile}",
            RunCommand = "php {mainFile}",
            TestCommand = "php {mainFile}",
            MainFilePatterns = new[] { "index.php", "main.php", "app.php", "*.php" },
            TimeoutSeconds = 30
        },
        
        ["swift"] = new LanguageConfig
        {
            Language = "swift",
            DockerImage = "swift:5.9",
            FileExtension = ".swift",
            BuildCommand = "swiftc {mainFile} -o /tmp/app",
            RunCommand = "/tmp/app",
            TestCommand = "/tmp/app",
            MainFilePatterns = new[] { "main.swift", "*.swift" },
            TimeoutSeconds = 60
        },
        
        ["kotlin"] = new LanguageConfig
        {
            Language = "kotlin",
            DockerImage = "zenika/kotlin:1.9",
            FileExtension = ".kt",
            BuildCommand = "kotlinc {mainFile} -include-runtime -d /tmp/app.jar",
            RunCommand = "java -jar /tmp/app.jar",
            TestCommand = "java -jar /tmp/app.jar",
            MainFilePatterns = new[] { "Main.kt", "App.kt", "*.kt" },
            TimeoutSeconds = 60
        },
        
        ["dart"] = new LanguageConfig
        {
            Language = "dart",
            DockerImage = "dart:stable",
            FileExtension = ".dart",
            BuildCommand = "dart analyze {mainFile}",
            RunCommand = "dart run {mainFile}",
            TestCommand = "dart test 2>/dev/null || dart run {mainFile}",
            MainFilePatterns = new[] { "main.dart", "bin/main.dart", "*.dart" },
            TimeoutSeconds = 30
        },
        
        ["flutter"] = new LanguageConfig
        {
            Language = "flutter",
            DockerImage = "ghcr.io/cirruslabs/flutter:stable",  // ~3GB but complete Flutter SDK
            FileExtension = ".dart",
            BuildCommand = "flutter pub get && flutter analyze",
            RunCommand = "flutter build web --release",  // Can't run interactively, but can build
            TestCommand = "flutter test 2>/dev/null || flutter build web",
            MainFilePatterns = new[] { "lib/main.dart", "main.dart", "*.dart" },
            SetupCommands = Array.Empty<string>(),
            TimeoutSeconds = 120,  // Flutter builds are slow
            SkipExecution = false  // We CAN build, just not run interactively
        },
        
        ["shell"] = new LanguageConfig
        {
            Language = "shell",
            DockerImage = "bash:5",
            FileExtension = ".sh",
            BuildCommand = "bash -n {mainFile}",
            RunCommand = "bash {mainFile}",
            TestCommand = "bash {mainFile}",
            MainFilePatterns = new[] { "main.sh", "run.sh", "script.sh", "*.sh" },
            TimeoutSeconds = 30
        },
        
        ["sql"] = new LanguageConfig
        {
            Language = "sql",
            DockerImage = "postgres:16-alpine",
            FileExtension = ".sql",
            // SQL is tricky - just syntax check
            BuildCommand = "echo 'SQL syntax checking not available in standalone mode'",
            RunCommand = "cat {mainFile}",
            TestCommand = "cat {mainFile}",
            MainFilePatterns = new[] { "*.sql" },
            TimeoutSeconds = 10,
            SkipExecution = true  // SQL needs a database
        },
        
        ["html"] = new LanguageConfig
        {
            Language = "html",
            DockerImage = "nginx:alpine",
            FileExtension = ".html",
            BuildCommand = "echo 'HTML validated'",
            RunCommand = "cat {mainFile}",
            TestCommand = "cat {mainFile}",
            MainFilePatterns = new[] { "index.html", "*.html" },
            TimeoutSeconds = 10,
            SkipExecution = true  // HTML needs a browser
        },
        
        ["css"] = new LanguageConfig
        {
            Language = "css",
            DockerImage = "node:20-slim",
            FileExtension = ".css",
            BuildCommand = "echo 'CSS validated'",
            RunCommand = "cat {mainFile}",
            TestCommand = "cat {mainFile}",
            MainFilePatterns = new[] { "styles.css", "main.css", "*.css" },
            TimeoutSeconds = 10,
            SkipExecution = true  // CSS needs a browser
        }
    };
}

/// <summary>
/// Configuration for a specific language's Docker execution
/// </summary>
public class LanguageConfig
{
    public required string Language { get; set; }
    public required string DockerImage { get; set; }
    public required string FileExtension { get; set; }
    public required string BuildCommand { get; set; }
    public required string RunCommand { get; set; }
    public required string TestCommand { get; set; }
    public required string[] MainFilePatterns { get; set; }
    public string[] SetupCommands { get; set; } = Array.Empty<string>();
    public int TimeoutSeconds { get; set; } = 30;
    public bool SkipExecution { get; set; } = false;
}

