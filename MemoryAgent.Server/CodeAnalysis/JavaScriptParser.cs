using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for JavaScript and TypeScript files with smart chunking
/// </summary>
public class JavaScriptParser
{
    public static ParseResult ParseJavaScriptFile(string filePath, string? context = null)
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
            var extension = Path.GetExtension(filePath).ToLower();
            var isTypeScript = extension == ".ts" || extension == ".tsx";
            
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
                    ["file_type"] = extension,
                    ["is_javascript"] = !isTypeScript,
                    ["is_typescript"] = isTypeScript,
                    ["language"] = isTypeScript ? "typescript" : "javascript"
                }
            };
            
            result.CodeElements.Add(fileNode);
            
            // Extract imports
            ExtractImports(content, filePath, context, result);
            
            // Extract classes
            ExtractClasses(content, filePath, context, result);
            
            // Extract functions
            ExtractFunctions(content, filePath, context, result);
            
            // Extract React components (function and class components)
            ExtractReactComponents(content, filePath, context, result);
            
            // Extract TypeScript interfaces and types
            if (isTypeScript)
            {
                ExtractTypeScriptTypes(content, filePath, context, result);
            }
            
            // Extract exports
            ExtractExports(content, filePath, context, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing JavaScript/TypeScript file: {ex.Message}");
        }
        
        return result;
    }
    
    private static void ExtractImports(string content, string filePath, string? context, ParseResult result)
    {
        // Match: import { something } from 'module'
        // Match: import something from 'module'
        // Match: const something = require('module')
        var importPatterns = new[]
        {
            @"import\s+(?:\{[^}]+\}|[\w]+|\*\s+as\s+[\w]+)\s+from\s+['""]([^'""]+)['""]",
            @"import\s+['""]([^'""]+)['""]",
            @"(?:const|let|var)\s+[\w\s{},]+\s*=\s*require\s*\(\s*['""]([^'""]+)['""]"
        };
        
        foreach (var pattern in importPatterns)
        {
            var matches = Regex.Matches(content, pattern);
            foreach (Match match in matches)
            {
                var module = match.Groups[1].Value;
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = Path.GetFileNameWithoutExtension(filePath),
                    ToName = module,
                    Type = RelationshipType.Imports,
                    Context = context ?? "default"
                });
            }
        }
    }
    
    private static void ExtractClasses(string content, string filePath, string? context, ParseResult result)
    {
        // Match: class ClassName extends BaseClass {
        var classPattern = @"(?:export\s+)?class\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{";
        var matches = Regex.Matches(content, classPattern);
        
        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var baseClass = match.Groups[2].Success ? match.Groups[2].Value : null;
            var lineNumber = GetLineNumber(content, match.Index);
            
            // Extract the class body
            var classBody = ExtractBlock(content, match.Index + match.Length - 1);
            
            var classNode = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = className,
                Content = $"class {className}" + (baseClass != null ? $" extends {baseClass}" : "") + " {\n" + TruncateContent(classBody, 1500) + "\n}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "class",
                    ["class_name"] = className,
                    ["has_base_class"] = baseClass != null
                }
            };
            
            if (baseClass != null)
            {
                classNode.Metadata["base_class"] = baseClass;
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = className,
                    ToName = baseClass,
                    Type = RelationshipType.Inherits,
                    Context = context ?? "default"
                });
            }
            
            result.CodeElements.Add(classNode);
            
            // Extract methods from class
            ExtractClassMethods(classBody, className, filePath, context, result, lineNumber);
        }
    }
    
    private static void ExtractFunctions(string content, string filePath, string? context, ParseResult result)
    {
        // Match: function functionName(params) { }
        // Match: const functionName = (params) => { }
        // Match: async function functionName(params) { }
        var functionPatterns = new[]
        {
            @"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(([^)]*)\)",
            @"(?:export\s+)?const\s+(\w+)\s*=\s*(?:async\s+)?\(([^)]*)\)\s*=>",
            @"(?:export\s+)?const\s+(\w+)\s*=\s*(?:async\s+)?function\s*\(([^)]*)\)"
        };
        
        foreach (var pattern in functionPatterns)
        {
            var matches = Regex.Matches(content, pattern);
            foreach (Match match in matches)
            {
                var functionName = match.Groups[1].Value;
                var parameters = match.Groups[2].Value;
                var lineNumber = GetLineNumber(content, match.Index);
                
                var functionNode = new CodeMemory
                {
                    Type = CodeMemoryType.Method,
                    Name = functionName,
                    Content = match.Value,
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = lineNumber,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_type"] = "function",
                        ["function_name"] = functionName,
                        ["parameters"] = parameters,
                        ["is_async"] = match.Value.Contains("async")
                    }
                };
                
                result.CodeElements.Add(functionNode);
            }
        }
    }
    
    private static void ExtractClassMethods(string classBody, string className, string filePath, string? context, ParseResult result, int classLineNumber)
    {
        // Match class methods
        var methodPattern = @"(?:async\s+)?(\w+)\s*\(([^)]*)\)\s*\{";
        var matches = Regex.Matches(classBody, methodPattern);
        
        foreach (Match match in matches)
        {
            var methodName = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            
            var methodNode = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = $"{className}.{methodName}",
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = classLineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "method",
                    ["class_name"] = className,
                    ["method_name"] = methodName,
                    ["parameters"] = parameters,
                    ["is_async"] = match.Value.Contains("async")
                }
            };
            
            result.CodeElements.Add(methodNode);
        }
    }
    
    private static void ExtractReactComponents(string content, string filePath, string? context, ParseResult result)
    {
        // Match: const ComponentName = () => { return (<JSX>...</JSX>) }
        // Match: function ComponentName() { return (<JSX>...</JSX>) }
        var componentPattern = @"(?:export\s+)?(?:const|function)\s+([A-Z]\w+)\s*=?\s*(?:\([^)]*\))?\s*(?:=>)?\s*\{";
        var matches = Regex.Matches(content, componentPattern);
        
        foreach (Match match in matches)
        {
            var componentName = match.Groups[1].Value;
            var lineNumber = GetLineNumber(content, match.Index);
            
            var componentNode = new CodeMemory
            {
                Type = CodeMemoryType.Class,
                Name = componentName,
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "react_component",
                    ["component_name"] = componentName,
                    ["framework"] = "react",
                    ["layer"] = "UI"
                }
            };
            
            result.CodeElements.Add(componentNode);
        }
    }
    
    private static void ExtractTypeScriptTypes(string content, string filePath, string? context, ParseResult result)
    {
        // Match interfaces: interface InterfaceName { }
        var interfacePattern = @"(?:export\s+)?interface\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{";
        var interfaceMatches = Regex.Matches(content, interfacePattern);
        
        foreach (Match match in interfaceMatches)
        {
            var interfaceName = match.Groups[1].Value;
            var baseInterface = match.Groups[2].Success ? match.Groups[2].Value : null;
            var lineNumber = GetLineNumber(content, match.Index);
            var body = ExtractBlock(content, match.Index + match.Length - 1);
            
            var interfaceNode = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Interface: {interfaceName}",
                Content = $"interface {interfaceName} {{\n{TruncateContent(body, 1000)}\n}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "typescript_interface",
                    ["interface_name"] = interfaceName,
                    ["language"] = "typescript"
                }
            };
            
            result.CodeElements.Add(interfaceNode);
            
            if (baseInterface != null)
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = interfaceName,
                    ToName = baseInterface,
                    Type = RelationshipType.Inherits,
                    Context = context ?? "default"
                });
            }
        }
        
        // Match type aliases: type TypeName = ...
        var typePattern = @"(?:export\s+)?type\s+(\w+)\s*=";
        var typeMatches = Regex.Matches(content, typePattern);
        
        foreach (Match match in typeMatches)
        {
            var typeName = match.Groups[1].Value;
            var lineNumber = GetLineNumber(content, match.Index);
            
            var typeNode = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Type: {typeName}",
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "typescript_type",
                    ["type_name"] = typeName,
                    ["language"] = "typescript"
                }
            };
            
            result.CodeElements.Add(typeNode);
        }
    }
    
    private static void ExtractExports(string content, string filePath, string? context, ParseResult result)
    {
        // Match: export { something }
        // Match: export default something
        var exportPattern = @"export\s+(?:default\s+)?(?:\{([^}]+)\}|(\w+))";
        var matches = Regex.Matches(content, exportPattern);
        
        foreach (Match match in matches)
        {
            var exported = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            // Store export info in metadata for future relationship tracking
        }
    }
    
    private static string ExtractBlock(string content, int startIndex)
    {
        int braceCount = 1;
        int endIndex = startIndex + 1;
        
        while (endIndex < content.Length && braceCount > 0)
        {
            if (content[endIndex] == '{') braceCount++;
            else if (content[endIndex] == '}') braceCount--;
            endIndex++;
        }
        
        return content.Substring(startIndex + 1, endIndex - startIndex - 2);
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

