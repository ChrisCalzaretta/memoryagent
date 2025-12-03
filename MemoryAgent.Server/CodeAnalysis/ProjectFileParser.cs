using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for .NET project files (.csproj, .vbproj, .fsproj) and solution files (.sln)
/// </summary>
public class ProjectFileParser
{
    public static ParseResult ParseProjectFile(string filePath, string? context = null)
    {
        var result = new ParseResult();
        var extension = Path.GetExtension(filePath).ToLower();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            if (extension == ".sln")
            {
                return ParseSolutionFile(filePath, context);
            }
            else
            {
                return ParseCsProjFile(filePath, context);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing project file: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParseCsProjFile(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        var projectName = Path.GetFileNameWithoutExtension(filePath);
        
        try
        {
            var doc = XDocument.Parse(content);
            var root = doc.Root;
            
            if (root == null)
            {
                result.Errors.Add("Invalid project file format");
                return result;
            }
            
            // Extract target framework
            var targetFramework = root.Descendants("TargetFramework").FirstOrDefault()?.Value;
            var targetFrameworks = root.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
            var outputType = root.Descendants("OutputType").FirstOrDefault()?.Value ?? "Library";
            var nullable = root.Descendants("Nullable").FirstOrDefault()?.Value;
            
            // Create project node
            var projectNode = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = projectName,
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = Path.GetExtension(filePath),
                    ["is_project_file"] = true,
                    ["project_name"] = projectName,
                    ["output_type"] = outputType,
                    ["language"] = "msbuild",
                    ["framework"] = "dotnet"
                }
            };
            
            if (targetFramework != null)
                projectNode.Metadata["target_framework"] = targetFramework;
            if (targetFrameworks != null)
                projectNode.Metadata["target_frameworks"] = targetFrameworks;
            if (nullable != null)
                projectNode.Metadata["nullable"] = nullable;
                
            result.CodeElements.Add(projectNode);
            
            // Extract PackageReferences (NuGet packages)
            var packageReferences = root.Descendants("PackageReference").ToList();
            foreach (var package in packageReferences)
            {
                var packageName = package.Attribute("Include")?.Value;
                var version = package.Attribute("Version")?.Value ?? package.Element("Version")?.Value;
                
                if (packageName != null)
                {
                    var packageNode = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = $"Package: {packageName}",
                        Content = $"<PackageReference Include=\"{packageName}\" Version=\"{version}\" />",
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["chunk_type"] = "nuget_package",
                            ["package_name"] = packageName,
                            ["version"] = version ?? "latest",
                            ["dependency_type"] = "nuget"
                        }
                    };
                    
                    result.CodeElements.Add(packageNode);
                    
                    // Create DEPENDS_ON relationship
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = projectName,
                        ToName = packageName,
                        Type = RelationshipType.DependsOn,
                        Context = context ?? "default",
                        Properties = new Dictionary<string, object>
                        {
                            ["version"] = version ?? "latest",
                            ["dependency_type"] = "nuget"
                        }
                    });
                }
            }
            
            // Extract ProjectReferences (project-to-project references)
            var projectReferences = root.Descendants("ProjectReference").ToList();
            foreach (var projectRef in projectReferences)
            {
                var referencedProject = projectRef.Attribute("Include")?.Value;
                if (referencedProject != null)
                {
                    var referencedProjectName = Path.GetFileNameWithoutExtension(referencedProject);
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = projectName,
                        ToName = referencedProjectName,
                        Type = RelationshipType.References,
                        Context = context ?? "default",
                        Properties = new Dictionary<string, object>
                        {
                            ["reference_path"] = referencedProject,
                            ["dependency_type"] = "project"
                        }
                    });
                }
            }
            
            // Extract properties as patterns
            var propertyGroups = root.Descendants("PropertyGroup").ToList();
            foreach (var propGroup in propertyGroups)
            {
                foreach (var prop in propGroup.Elements())
                {
                    if (prop.Name.LocalName != "TargetFramework" && 
                        prop.Name.LocalName != "TargetFrameworks" &&
                        prop.Name.LocalName != "OutputType" &&
                        !string.IsNullOrWhiteSpace(prop.Value))
                    {
                        projectNode.Metadata[$"property_{prop.Name.LocalName}"] = prop.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing .csproj XML: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParseSolutionFile(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        var solutionName = Path.GetFileNameWithoutExtension(filePath);
        
        // Create solution node
        var solutionNode = new CodeMemory
        {
            Type = CodeMemoryType.Class,
            Name = solutionName,
            Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = 1,
            Metadata = new Dictionary<string, object>
            {
                ["file_type"] = ".sln",
                ["is_solution_file"] = true,
                ["solution_name"] = solutionName,
                ["language"] = "msbuild",
                ["framework"] = "dotnet"
            }
        };
        
        result.CodeElements.Add(solutionNode);
        
        // Extract projects from solution
        // Format: Project("{GUID}") = "ProjectName", "Path\To\Project.csproj", "{PROJECT-GUID}"
        var projectPattern = @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)""\s*,\s*""([^""]+)""\s*,\s*""\{([^}]+)\}""";
        var matches = Regex.Matches(content, projectPattern);
        
        foreach (Match match in matches)
        {
            var projectName = match.Groups[1].Value;
            var projectPath = match.Groups[2].Value;
            var projectGuid = match.Groups[3].Value;
            
            // Create relationship: Solution CONTAINS Project
            result.Relationships.Add(new CodeRelationship
            {
                FromName = solutionName,
                ToName = Path.GetFileNameWithoutExtension(projectPath),
                Type = RelationshipType.Contains,
                Context = context ?? "default",
                Properties = new Dictionary<string, object>
                {
                    ["project_path"] = projectPath,
                    ["project_guid"] = projectGuid
                }
            });
        }
        
        // Extract solution folders
        var folderPattern = @"Project\(""\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}""\)\s*=\s*""([^""]+)""";
        var folderMatches = Regex.Matches(content, folderPattern);
        
        foreach (Match match in folderMatches)
        {
            var folderName = match.Groups[1].Value;
            solutionNode.Metadata[$"folder_{folderName}"] = true;
        }
        
        return result;
    }
}









