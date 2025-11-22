using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for VB.NET files with smart chunking
/// </summary>
public class VBNetParser
{
    public static ParseResult ParseVBNetFile(string filePath, string? context = null)
    {
        var result = new ParseResult();
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

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
                    ["file_type"] = ".vb",
                    ["is_vb"] = true,
                    ["language"] = "vb.net",
                    ["framework"] = "dotnet"
                }
            };
            
            result.CodeElements.Add(fileNode);
            
            // Extract Imports
            ExtractImports(content, filePath, context, result);
            
            // Extract Namespace
            ExtractNamespace(content, filePath, context, result);
            
            // Extract Classes/Modules
            ExtractClasses(content, filePath, context, result);
            
            // Extract Functions/Subs
            ExtractMethods(content, filePath, context, result);
            
            // Extract Properties
            ExtractProperties(content, filePath, context, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing VB.NET file: {ex.Message}");
        }
        
        return result;
    }
    
    private static void ExtractImports(string content, string filePath, string? context, ParseResult result)
    {
        // Match: Imports System.Collections.Generic
        var importPattern = @"^\s*Imports\s+([\w.]+)";
        var matches = Regex.Matches(content, importPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var importedNamespace = match.Groups[1].Value;
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileNameWithoutExtension(filePath),
                ToName = importedNamespace,
                Type = RelationshipType.Imports,
                Context = context ?? "default"
            });
        }
    }
    
    private static void ExtractNamespace(string content, string filePath, string? context, ParseResult result)
    {
        // Match: Namespace MyNamespace
        var namespacePattern = @"^\s*Namespace\s+([\w.]+)";
        var match = Regex.Match(content, namespacePattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var namespaceName = match.Groups[1].Value;
            // Store namespace for future use
        }
    }
    
    private static void ExtractClasses(string content, string filePath, string? context, ParseResult result)
    {
        // Match: Public Class ClassName
        // Match: Public Module ModuleName
        var classPattern = @"^\s*(?:(Public|Private|Protected|Friend)\s+)?(Class|Module|Structure|Interface)\s+(\w+)(?:\s+(?:Inherits|Implements)\s+([\w.]+))?";
        var matches = Regex.Matches(content, classPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var accessModifier = match.Groups[1].Success ? match.Groups[1].Value : "Public";
            var classType = match.Groups[2].Value;
            var className = match.Groups[3].Value;
            var baseClass = match.Groups[4].Success ? match.Groups[4].Value : null;
            var lineNumber = GetLineNumber(content, match.Index);
            
            // Extract class body
            var classBody = ExtractVBBlock(content, match.Index, "End " + classType);
            
            var classNode = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = className,
                Content = match.Value + "\n" + TruncateContent(classBody, 1500) + $"\nEnd {classType}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = classType.ToLower(),
                    ["class_name"] = className,
                    ["access_modifier"] = accessModifier.ToLower(),
                    ["class_type"] = classType.ToLower(),
                    ["language"] = "vb.net"
                }
            };
            
            if (baseClass != null)
            {
                classNode.Metadata["base_class"] = baseClass;
                var relationshipType = match.Value.Contains("Inherits", StringComparison.OrdinalIgnoreCase)
                    ? RelationshipType.Inherits
                    : RelationshipType.Implements;
                    
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = className,
                    ToName = baseClass,
                    Type = relationshipType,
                    Context = context ?? "default"
                });
            }
            
            result.CodeElements.Add(classNode);
        }
    }
    
    private static void ExtractMethods(string content, string filePath, string? context, ParseResult result)
    {
        // Match: Public Function FunctionName(params) As ReturnType
        // Match: Public Sub SubName(params)
        var methodPattern = @"^\s*(?:(Public|Private|Protected|Friend|Shared)\s+)?(Function|Sub)\s+(\w+)\s*\(([^)]*)\)(?:\s+As\s+([\w.]+))?";
        var matches = Regex.Matches(content, methodPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var accessModifier = match.Groups[1].Success ? match.Groups[1].Value : "Public";
            var methodType = match.Groups[2].Value;
            var methodName = match.Groups[3].Value;
            var parameters = match.Groups[4].Value;
            var returnType = match.Groups[5].Success ? match.Groups[5].Value : "Void";
            var lineNumber = GetLineNumber(content, match.Index);
            
            var methodNode = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = methodName,
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "method",
                    ["method_name"] = methodName,
                    ["method_type"] = methodType.ToLower(),
                    ["return_type"] = returnType,
                    ["parameters"] = parameters,
                    ["access_modifier"] = accessModifier.ToLower(),
                    ["language"] = "vb.net"
                }
            };
            
            result.CodeElements.Add(methodNode);
        }
    }
    
    private static void ExtractProperties(string content, string filePath, string? context, ParseResult result)
    {
        // Match: Public Property PropertyName As Type
        var propertyPattern = @"^\s*(?:(Public|Private|Protected|Friend)\s+)?Property\s+(\w+)\s*(?:\(([^)]*)\))?\s+As\s+([\w.]+)";
        var matches = Regex.Matches(content, propertyPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            var accessModifier = match.Groups[1].Success ? match.Groups[1].Value : "Public";
            var propertyName = match.Groups[2].Value;
            var propertyType = match.Groups[4].Value;
            var lineNumber = GetLineNumber(content, match.Index);
            
            var propertyNode = new CodeMemory
            {
                Type = CodeMemoryType.Property,
                Name = propertyName,
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "property",
                    ["property_name"] = propertyName,
                    ["property_type"] = propertyType,
                    ["access_modifier"] = accessModifier.ToLower(),
                    ["language"] = "vb.net"
                }
            };
            
            result.CodeElements.Add(propertyNode);
        }
    }
    
    private static string ExtractVBBlock(string content, int startIndex, string endKeyword)
    {
        var endPattern = @"^\s*" + Regex.Escape(endKeyword);
        var lines = content.Substring(startIndex).Split('\n');
        var sb = new StringBuilder();
        
        for (int i = 1; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], endPattern, RegexOptions.IgnoreCase))
            {
                break;
            }
            sb.AppendLine(lines[i]);
        }
        
        return sb.ToString();
    }
    
    private static string TruncateContent(string content, int maxLength)
    {
        return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;
    }
    
    private static int GetLineNumber(string content, int index)
    {
        return content.Substring(0, Math.Min(index, content.Length)).Count(c => c == '\n') + 1;
    }
}

