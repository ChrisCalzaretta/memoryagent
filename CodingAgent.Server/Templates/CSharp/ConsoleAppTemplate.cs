namespace CodingAgent.Server.Templates.CSharp;

/// <summary>
/// Template for C# Console Application
/// </summary>
public class ConsoleAppTemplate : ProjectTemplateBase
{
    public override string TemplateId => "csharp-console";
    public override string DisplayName => "C# Console Application";
    public override string Language => "csharp";
    public override string ProjectType => "ConsoleApp";
    public override string Description => "A command-line application using .NET";
    public override int Complexity => 3;
    
    public override string[] Keywords => new[]
    {
        "console", "cli", "command line", "terminal", "tool", 
        "utility", "script", "batch", "automation"
    };
    
    public override string[] FolderStructure => new[]
    {
        "Services",
        "Models",
        "Utils"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.DependencyInjection"
    };
    
    public override Dictionary<string, string> Files => new()
    {
        ["Program.cs"] = @"namespace {{Namespace}};

/// <summary>
/// Main entry point for {{ProjectName}}
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine(""{{ProjectName}} starting..."");
            
            // TODO: Implement your application logic here
            await RunAsync(args);
            
            Console.WriteLine(""{{ProjectName}} completed successfully."");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($""Error: {ex.Message}"");
            return 1;
        }
    }
    
    private static async Task RunAsync(string[] args)
    {
        // TODO: Add your implementation here
        await Task.CompletedTask;
    }
}
",
        ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{{TargetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>{{Namespace}}</RootNamespace>
  </PropertyGroup>

</Project>
",
        [".gitignore"] = @"bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
"
    };
}



