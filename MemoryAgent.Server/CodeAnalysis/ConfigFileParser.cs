using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for configuration files (package.json, appsettings.json, tsconfig.json, etc.)
/// </summary>
public class ConfigFileParser
{
    public static ParseResult ParseConfigFile(string filePath, string? context = null)
    {
        var result = new ParseResult();
        var fileName = Path.GetFileName(filePath).ToLower();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            return fileName switch
            {
                "package.json" => ParsePackageJson(filePath, context),
                "package-lock.json" => ParsePackageLockJson(filePath, context),
                var name when name.StartsWith("appsettings") && name.EndsWith(".json") => ParseAppSettings(filePath, context),
                "tsconfig.json" => ParseTsConfig(filePath, context),
                "web.config" => ParseWebConfig(filePath, context),
                _ => ParseGenericJson(filePath, context)
            };
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing config file: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParsePackageJson(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        
        try
        {
            var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            
            var projectName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "package";
            var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : "unknown";
            
            // Create project node
            var projectNode = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = projectName ?? "package",
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = "package.json",
                    ["is_package_json"] = true,
                    ["project_name"] = projectName ?? "package",
                    ["version"] = version ?? "unknown",
                    ["language"] = "json",
                    ["framework"] = "nodejs"
                }
            };
            
            result.CodeElements.Add(projectNode);
            
            // Extract dependencies
            if (root.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dep in dependencies.EnumerateObject())
                {
                    var packageName = dep.Name;
                    var packageVersion = dep.Value.GetString() ?? "latest";
                    
                    var depNode = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = $"Package: {packageName}",
                        Content = $"\"{packageName}\": \"{packageVersion}\"",
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["chunk_type"] = "npm_package",
                            ["package_name"] = packageName,
                            ["version"] = packageVersion,
                            ["dependency_type"] = "npm",
                            ["is_dev_dependency"] = false
                        }
                    };
                    
                    result.CodeElements.Add(depNode);
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = projectName ?? "package",
                        ToName = packageName,
                        Type = RelationshipType.DependsOn,
                        Context = context ?? "default",
                        Properties = new Dictionary<string, object>
                        {
                            ["version"] = packageVersion,
                            ["dependency_type"] = "npm"
                        }
                    });
                }
            }
            
            // Extract devDependencies
            if (root.TryGetProperty("devDependencies", out var devDependencies))
            {
                foreach (var dep in devDependencies.EnumerateObject())
                {
                    var packageName = dep.Name;
                    var packageVersion = dep.Value.GetString() ?? "latest";
                    
                    result.Relationships.Add(new CodeRelationship
                    {
                        FromName = projectName ?? "package",
                        ToName = packageName,
                        Type = RelationshipType.DependsOn,
                        Context = context ?? "default",
                        Properties = new Dictionary<string, object>
                        {
                            ["version"] = packageVersion,
                            ["dependency_type"] = "npm-dev"
                        }
                    });
                }
            }
            
            // Extract scripts
            if (root.TryGetProperty("scripts", out var scripts))
            {
                var scriptNames = new List<string>();
                foreach (var script in scripts.EnumerateObject())
                {
                    scriptNames.Add(script.Name);
                }
                projectNode.Metadata["scripts"] = string.Join(", ", scriptNames);
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON in package.json: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParsePackageLockJson(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        
        // Create simple file node - package-lock.json is huge, just track it exists
        var fileNode = new CodeMemory
        {
            Type = CodeMemoryType.File,
            Name = "package-lock.json",
            Content = "package-lock.json (lock file)",
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = 1,
            Metadata = new Dictionary<string, object>
            {
                ["file_type"] = "package-lock.json",
                ["is_lock_file"] = true,
                ["language"] = "json",
                ["framework"] = "nodejs"
            }
        };
        
        result.CodeElements.Add(fileNode);
        return result;
    }
    
    private static ParseResult ParseAppSettings(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        
        try
        {
            var json = JsonDocument.Parse(content);
            
            var fileNode = new CodeMemory
            {
                Type = CodeMemoryType.File,
                Name = fileName,
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = fileName,
                    ["is_app_settings"] = true,
                    ["language"] = "json",
                    ["framework"] = "aspnet-core",
                    ["config_file"] = true
                }
            };
            
            // Extract connection strings
            if (json.RootElement.TryGetProperty("ConnectionStrings", out var connStrings))
            {
                var connections = new List<string>();
                foreach (var conn in connStrings.EnumerateObject())
                {
                    connections.Add(conn.Name);
                }
                fileNode.Metadata["connection_strings"] = string.Join(", ", connections);
            }
            
            // Extract logging configuration
            if (json.RootElement.TryGetProperty("Logging", out var logging))
            {
                fileNode.Metadata["has_logging_config"] = true;
            }
            
            result.CodeElements.Add(fileNode);
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON in {fileName}: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParseTsConfig(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        
        var fileNode = new CodeMemory
        {
            Type = CodeMemoryType.File,
            Name = "tsconfig.json",
            Content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = 1,
            Metadata = new Dictionary<string, object>
            {
                ["file_type"] = "tsconfig.json",
                ["is_tsconfig"] = true,
                ["language"] = "json",
                ["framework"] = "typescript",
                ["config_file"] = true
            }
        };
        
        result.CodeElements.Add(fileNode);
        return result;
    }
    
    private static ParseResult ParseWebConfig(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        
        var fileNode = new CodeMemory
        {
            Type = CodeMemoryType.File,
            Name = "web.config",
            Content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = 1,
            Metadata = new Dictionary<string, object>
            {
                ["file_type"] = "web.config",
                ["is_web_config"] = true,
                ["language"] = "xml",
                ["framework"] = "aspnet",
                ["config_file"] = true
            }
        };
        
        result.CodeElements.Add(fileNode);
        return result;
    }
    
    private static ParseResult ParseGenericJson(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        
        var fileNode = new CodeMemory
        {
            Type = CodeMemoryType.File,
            Name = fileName,
            Content = content.Length > 1000 ? content.Substring(0, 1000) + "..." : content,
            FilePath = filePath,
            Context = context ?? "default",
            LineNumber = 1,
            Metadata = new Dictionary<string, object>
            {
                ["file_type"] = fileName,
                ["is_json_file"] = true,
                ["language"] = "json",
                ["config_file"] = true
            }
        };
        
        result.CodeElements.Add(fileNode);
        return result;
    }
}






