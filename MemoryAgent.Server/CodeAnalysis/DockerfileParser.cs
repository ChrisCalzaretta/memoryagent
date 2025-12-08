using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Dockerfile and docker-compose files
/// </summary>
public class DockerfileParser
{
    public static ParseResult ParseDockerfile(string filePath, string? context = null)
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

            if (fileName.Contains("docker-compose"))
            {
                return ParseDockerCompose(filePath, context);
            }
            else
            {
                return ParseDockerFile(filePath, context);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Docker file: {ex.Message}");
        }
        
        return result;
    }
    
    private static ParseResult ParseDockerFile(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        
        // Create file-level node
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
                ["file_type"] = "Dockerfile",
                ["is_dockerfile"] = true,
                ["language"] = "dockerfile",
                ["infrastructure"] = "docker"
            }
        };
        
        result.CodeElements.Add(fileNode);
        
        // Extract base images (FROM instructions)
        var fromPattern = @"FROM\s+([^\s]+)(?:\s+AS\s+([^\s]+))?";
        var fromMatches = Regex.Matches(content, fromPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in fromMatches)
        {
            var baseImage = match.Groups[1].Value;
            var stageName = match.Groups[2].Success ? match.Groups[2].Value : null;
            var lineNumber = GetLineNumber(content, match.Index);
            
            var imageNode = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = stageName != null ? $"Stage: {stageName}" : $"BaseImage: {baseImage}",
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "docker_stage",
                    ["base_image"] = baseImage,
                    ["stage_name"] = stageName ?? "default"
                }
            };
            
            result.CodeElements.Add(imageNode);
            
            // Create dependency relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fileName,
                ToName = baseImage,
                Type = RelationshipType.DependsOn,
                Context = context ?? "default",
                Properties = new Dictionary<string, object>
                {
                    ["dependency_type"] = "docker_image",
                    ["stage"] = stageName ?? "default"
                }
            });
        }
        
        // Extract exposed ports
        var exposePattern = @"EXPOSE\s+(\d+(?:/(?:tcp|udp))?)";
        var exposeMatches = Regex.Matches(content, exposePattern, RegexOptions.IgnoreCase);
        
        var exposedPorts = new List<string>();
        foreach (Match match in exposeMatches)
        {
            exposedPorts.Add(match.Groups[1].Value);
        }
        
        if (exposedPorts.Any())
        {
            fileNode.Metadata["exposed_ports"] = string.Join(", ", exposedPorts);
        }
        
        // Extract environment variables
        var envPattern = @"ENV\s+(\w+)(?:=|\s+)([^\n]+)";
        var envMatches = Regex.Matches(content, envPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in envMatches)
        {
            var envName = match.Groups[1].Value;
            var envValue = match.Groups[2].Value.Trim();
            fileNode.Metadata[$"env_{envName}"] = envValue;
        }
        
        // Extract WORKDIR
        var workdirPattern = @"WORKDIR\s+([^\n]+)";
        var workdirMatch = Regex.Match(content, workdirPattern, RegexOptions.IgnoreCase);
        if (workdirMatch.Success)
        {
            fileNode.Metadata["workdir"] = workdirMatch.Groups[1].Value.Trim();
        }
        
        // Extract ENTRYPOINT and CMD
        var entrypointPattern = @"ENTRYPOINT\s+(\[.+\]|[^\n]+)";
        var entrypointMatch = Regex.Match(content, entrypointPattern, RegexOptions.IgnoreCase);
        if (entrypointMatch.Success)
        {
            fileNode.Metadata["entrypoint"] = entrypointMatch.Groups[1].Value.Trim();
        }
        
        var cmdPattern = @"CMD\s+(\[.+\]|[^\n]+)";
        var cmdMatch = Regex.Match(content, cmdPattern, RegexOptions.IgnoreCase);
        if (cmdMatch.Success)
        {
            fileNode.Metadata["cmd"] = cmdMatch.Groups[1].Value.Trim();
        }
        
        return result;
    }
    
    private static ParseResult ParseDockerCompose(string filePath, string? context)
    {
        var result = new ParseResult();
        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileName(filePath);
        
        // Create file-level node
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
                ["file_type"] = "docker-compose",
                ["is_docker_compose"] = true,
                ["language"] = "yaml",
                ["infrastructure"] = "docker"
            }
        };
        
        result.CodeElements.Add(fileNode);
        
        // Extract services (basic YAML parsing)
        // Format: servicename:\n  image: imagename
        var servicePattern = @"^\s*([a-zA-Z0-9_-]+):\s*$\s*image:\s*([^\s]+)";
        var serviceMatches = Regex.Matches(content, servicePattern, RegexOptions.Multiline);
        
        foreach (Match match in serviceMatches)
        {
            var serviceName = match.Groups[1].Value;
            var image = match.Groups[2].Value;
            var lineNumber = GetLineNumber(content, match.Index);
            
            var serviceNode = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Service: {serviceName}",
                Content = $"{serviceName}:\n  image: {image}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "docker_service",
                    ["service_name"] = serviceName,
                    ["image"] = image
                }
            };
            
            result.CodeElements.Add(serviceNode);
            
            // Create relationship
            result.Relationships.Add(new CodeRelationship
            {
                FromName = serviceName,
                ToName = image,
                Type = RelationshipType.DependsOn,
                Context = context ?? "default",
                Properties = new Dictionary<string, object>
                {
                    ["dependency_type"] = "docker_image"
                }
            });
        }
        
        return result;
    }
    
    private static int GetLineNumber(string content, int index)
    {
        return content.Substring(0, Math.Min(index, content.Length)).Count(c => c == '\n') + 1;
    }
}
















