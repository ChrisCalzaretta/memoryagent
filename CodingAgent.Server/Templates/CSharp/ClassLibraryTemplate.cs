namespace CodingAgent.Server.Templates.CSharp;

/// <summary>
/// Template for C# Class Library
/// </summary>
public class ClassLibraryTemplate : ProjectTemplateBase
{
    public override string TemplateId => "csharp-library";
    public override string DisplayName => "C# Class Library";
    public override string Language => "csharp";
    public override string ProjectType => "ClassLibrary";
    public override string Description => "A reusable .NET class library";
    public override int Complexity => 4;
    
    public override string[] Keywords => new[]
    {
        "library", "lib", "class library", "package", "nuget",
        "dll", "shared", "common", "sdk", "framework"
    };
    
    public override string[] FolderStructure => new[]
    {
        "Models",
        "Services",
        "Extensions",
        "Abstractions"
    };
    
    public override Dictionary<string, string> Files => new()
    {
        ["Class1.cs"] = @"namespace {{Namespace}};

/// <summary>
/// Main class for {{ProjectName}}
/// </summary>
public class Class1
{
    /// <summary>
    /// Example method
    /// </summary>
    /// <returns>A greeting message</returns>
    public string GetGreeting()
    {
        return ""Hello from {{ProjectName}}!"";
    }
}
",
        ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>{{TargetFramework}}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>{{Namespace}}</RootNamespace>
    
    <!-- NuGet Package Metadata -->
    <PackageId>{{ProjectName}}</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>{{Description}}</Description>
    <PackageTags>library</PackageTags>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

</Project>
",
        [".gitignore"] = @"bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
*.nupkg
"
    };
}

