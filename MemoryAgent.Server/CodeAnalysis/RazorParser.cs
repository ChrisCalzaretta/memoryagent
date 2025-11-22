using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Razor/CSHTML files with smart chunking
/// </summary>
public class RazorParser
{
    public static ParseResult ParseRazorFile(string filePath, string? context = null, ILoggerFactory? loggerFactory = null)
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
            
            // Extract @model directive
            var modelMatch = Regex.Match(content, @"@model\s+([^\r\n]+)");
            string? modelType = modelMatch.Success ? modelMatch.Groups[1].Value.Trim() : null;
            
            // Extract @page directive (Razor Pages)
            var pageMatch = Regex.Match(content, @"@page\s+""([^""]+)""");
            string? pageRoute = pageMatch.Success ? pageMatch.Groups[1].Value : null;
            
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
                    ["file_type"] = Path.GetExtension(filePath),
                    ["is_razor"] = true,
                    ["language"] = "razor",
                    ["framework"] = "aspnet-core",
                    ["layer"] = "UI"
                }
            };
            
            if (modelType != null)
                fileNode.Metadata["model_type"] = modelType;
            if (pageRoute != null)
                fileNode.Metadata["page_route"] = pageRoute;
            
            result.CodeElements.Add(fileNode);
            
            // Extract @section blocks
            ExtractSections(content, filePath, context, result);
            
            // Extract @code blocks (Razor components)
            ExtractCodeBlocks(content, filePath, context, result);
            
            // Extract @functions blocks
            ExtractFunctionsBlocks(content, filePath, context, result);
            
            // Extract major HTML sections (by heading tags or components)
            ExtractHtmlSections(content, filePath, context, result);
            
            // Extract semantic HTML elements (forms, tables, nav, etc.)
            ExtractSemanticHtmlElements(content, filePath, context, result);
            
            // Extract Razor components (e.g., <Component>)
            ExtractComponentUsages(content, filePath, context, result);
            
            // Extract <style> tags
            ExtractStyleTags(content, filePath, context, result);
            
            // Extract significant inline styles
            ExtractInlineStyles(content, filePath, context, result);
            
            // Create relationships
            CreateRazorRelationships(fileName, modelType, result);

            // ========================================
            // SEMANTIC ANALYSIS (NEW!)
            // ========================================
            if (loggerFactory != null)
            {
                var semanticAnalyzer = new RazorSemanticAnalyzer(loggerFactory.CreateLogger<RazorSemanticAnalyzer>());
                semanticAnalyzer.AnalyzeRazorFile(content, filePath, context, result);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Razor file: {ex.Message}");
        }
        
        return result;
    }
    
    private static void ExtractSections(string content, string filePath, string? context, ParseResult result)
    {
        // Match @section Name { ... }
        var sectionRegex = new Regex(@"@section\s+(\w+)\s*\{", RegexOptions.Multiline);
        var matches = sectionRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            var sectionName = match.Groups[1].Value;
            var startIndex = match.Index;
            var braceCount = 1;
            var endIndex = startIndex + match.Length;
            
            // Find matching closing brace
            for (int i = endIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}') braceCount--;
                if (braceCount == 0) endIndex = i;
            }
            
            var sectionContent = content.Substring(startIndex, endIndex - startIndex + 1);
            var lineNumber = content.Substring(0, startIndex).Count(c => c == '\n') + 1;
            
            var section = new CodeMemory
            {
                Type = CodeMemoryType.Method, // Treat sections as methods for searching
                Name = $"Section_{sectionName}",
                Content = sectionContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["element_type"] = "razor_section",
                    ["section_name"] = sectionName
                }
            };
            
            result.CodeElements.Add(section);
        }
    }
    
    private static void ExtractCodeBlocks(string content, string filePath, string? context, ParseResult result)
    {
        // Match @code { ... }
        var codeRegex = new Regex(@"@code\s*\{", RegexOptions.Multiline);
        var matches = codeRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            var startIndex = match.Index;
            var braceCount = 1;
            var endIndex = startIndex + match.Length;
            
            // Find matching closing brace
            for (int i = endIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}') braceCount--;
                if (braceCount == 0) endIndex = i;
            }
            
            var codeContent = content.Substring(startIndex, endIndex - startIndex + 1);
            var lineNumber = content.Substring(0, startIndex).Count(c => c == '\n') + 1;
            
            var codeBlock = new CodeMemory
            {
                Type = CodeMemoryType.Class, // Treat @code blocks as classes
                Name = $"{Path.GetFileNameWithoutExtension(filePath)}_CodeBlock",
                Content = codeContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["element_type"] = "razor_code_block"
                }
            };
            
            result.CodeElements.Add(codeBlock);
        }
    }
    
    private static void ExtractFunctionsBlocks(string content, string filePath, string? context, ParseResult result)
    {
        // Match @functions { ... }
        var functionsRegex = new Regex(@"@functions\s*\{", RegexOptions.Multiline);
        var matches = functionsRegex.Matches(content);
        
        foreach (Match match in matches)
        {
            var startIndex = match.Index;
            var braceCount = 1;
            var endIndex = startIndex + match.Length;
            
            for (int i = endIndex; i < content.Length && braceCount > 0; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}') braceCount--;
                if (braceCount == 0) endIndex = i;
            }
            
            var functionsContent = content.Substring(startIndex, endIndex - startIndex + 1);
            var lineNumber = content.Substring(0, startIndex).Count(c => c == '\n') + 1;
            
            var functionsBlock = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = $"{Path.GetFileNameWithoutExtension(filePath)}_Functions",
                Content = functionsContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["element_type"] = "razor_functions_block"
                }
            };
            
            result.CodeElements.Add(functionsBlock);
        }
    }
    
    private static void ExtractHtmlSections(string content, string filePath, string? context, ParseResult result)
    {
        // Extract major sections by headings or divs with IDs
        var sectionRegex = new Regex(@"<(h[1-3]|div[^>]*id\s*=\s*""([^""]+)"")", RegexOptions.Multiline);
        var matches = sectionRegex.Matches(content);
        
        var sections = new List<(int start, int end, string name)>();
        
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var sectionName = match.Groups[2].Success ? match.Groups[2].Value : $"Section_{i}";
            var startIndex = match.Index;
            
            // Find end (next section or end of file)
            var endIndex = i < matches.Count - 1 ? matches[i + 1].Index : content.Length;
            
            // Skip very small sections
            if (endIndex - startIndex < 100) continue;
            
            var sectionContent = content.Substring(startIndex, Math.Min(1500, endIndex - startIndex));
            var lineNumber = content.Substring(0, startIndex).Count(c => c == '\n') + 1;
            
            var section = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = $"HtmlSection_{sectionName}",
                Content = sectionContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["element_type"] = "html_section",
                    ["section_id"] = sectionName
                }
            };
            
            result.CodeElements.Add(section);
        }
    }
    
    private static void ExtractSemanticHtmlElements(string content, string filePath, string? context, ParseResult result)
    {
        // Extract important semantic HTML elements
        var semanticElements = new[]
        {
            "form",
            "table",
            "nav",
            "header",
            "footer",
            "aside",
            "main",
            "section",
            "article"
        };
        
        foreach (var elementType in semanticElements)
        {
            // Match opening tag with optional attributes
            var pattern = $@"<{elementType}([^>]*)>(.*?)</{elementType}>";
            var matches = Regex.Matches(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            int elementIndex = 1;
            foreach (Match match in matches)
            {
                var attributes = match.Groups[1].Value;
                var elementContent = match.Groups[2].Value.Trim();
                var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
                
                // Extract ID or class for naming
                var idMatch = Regex.Match(attributes, @"id\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                var classMatch = Regex.Match(attributes, @"class\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                
                var identifier = idMatch.Success 
                    ? idMatch.Groups[1].Value 
                    : (classMatch.Success 
                        ? classMatch.Groups[1].Value.Split(' ')[0] 
                        : $"{elementType}{elementIndex}");
                
                // Truncate content if too long
                var displayContent = elementContent.Length > 1000 
                    ? elementContent.Substring(0, 1000) + "..." 
                    : elementContent;
                
                var element = new CodeMemory
                {
                    Type = CodeMemoryType.Pattern,
                    Name = $"Html_{elementType}_{identifier}",
                    Content = $"<{elementType}{attributes}>\n{displayContent}\n</{elementType}>",
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = lineNumber,
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunk_type"] = "html_element",
                        ["element_type"] = elementType,
                        ["element_id"] = idMatch.Success ? idMatch.Groups[1].Value : "",
                        ["element_class"] = classMatch.Success ? classMatch.Groups[1].Value : "",
                        ["has_id"] = idMatch.Success,
                        ["has_class"] = classMatch.Success
                    }
                };
                
                result.CodeElements.Add(element);
                
                // Create relationship: File DEFINES Element
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = Path.GetFileName(filePath),
                    ToName = element.Name,
                    Type = RelationshipType.Defines,
                    Context = context ?? "default"
                });
                
                elementIndex++;
            }
        }
        
        // Extract forms with action attributes (important for understanding endpoints)
        var formPattern = @"<form[^>]*action\s*=\s*[""']([^""']+)[""'][^>]*>";
        var formMatches = Regex.Matches(content, formPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in formMatches)
        {
            var action = match.Groups[1].Value;
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            // Create a pattern for form submission
            var formSubmit = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"FormSubmit: {action}",
                Content = match.Value,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "form_submit",
                    ["action"] = action
                }
            };
            
            result.CodeElements.Add(formSubmit);
            
            // Create relationship showing the form posts to an action
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = action,
                Type = RelationshipType.Calls,
                Properties = new Dictionary<string, object>
                {
                    ["relationship_type"] = "form_submission"
                }
            });
        }
    }
    
    private static void ExtractComponentUsages(string content, string filePath, string? context, ParseResult result)
    {
        // Find Razor component tags (start with uppercase)
        var componentRegex = new Regex(@"<([A-Z][a-zA-Z0-9]*)[^>]*>", RegexOptions.Multiline);
        var matches = componentRegex.Matches(content);
        
        var usedComponents = new HashSet<string>();
        foreach (Match match in matches)
        {
            var componentName = match.Groups[1].Value;
            usedComponents.Add(componentName);
        }
        
        // Create relationships for component usage
        foreach (var componentName in usedComponents)
        {
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileNameWithoutExtension(filePath),
                ToName = componentName,
                Type = RelationshipType.Uses,
                Properties = new Dictionary<string, object>
                {
                    ["relationship_type"] = "uses_component"
                }
            });
        }
    }
    
    private static void ExtractStyleTags(string content, string filePath, string? context, ParseResult result)
    {
        // Match <style> tags and their content
        var styleRegex = new Regex(@"<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = styleRegex.Matches(content);
        
        int styleIndex = 1;
        foreach (Match match in matches)
        {
            var styleContent = match.Groups[1].Value.Trim();
            
            // Skip empty style tags
            if (string.IsNullOrWhiteSpace(styleContent))
                continue;
            
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            // Parse the CSS content using CssParser logic
            var styleElement = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"InlineStyles_{styleIndex}",
                Content = styleContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "style_tag",
                    ["embedded_in"] = "razor",
                    ["style_index"] = styleIndex
                }
            };
            
            result.CodeElements.Add(styleElement);
            
            // Create relationship: File DEFINES StyleTag
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = styleElement.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
            
            styleIndex++;
        }
    }
    
    private static void ExtractInlineStyles(string content, string filePath, string? context, ParseResult result)
    {
        // Match inline style attributes that are significant (longer than 50 chars)
        var inlineStyleRegex = new Regex(@"style\s*=\s*""([^""]{50,})""", RegexOptions.IgnoreCase);
        var matches = inlineStyleRegex.Matches(content);
        
        var inlineStyles = new Dictionary<string, (string content, int lineNumber)>();
        
        foreach (Match match in matches)
        {
            var styleContent = match.Groups[1].Value.Trim();
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            // Try to find the element this style belongs to
            var elementMatch = Regex.Match(
                content.Substring(Math.Max(0, match.Index - 100), Math.Min(150, content.Length - match.Index + 100)),
                @"<(\w+)[^>]*style\s*=",
                RegexOptions.IgnoreCase);
            
            var elementName = elementMatch.Success ? elementMatch.Groups[1].Value : "unknown";
            var key = $"{elementName}_{lineNumber}";
            
            if (!inlineStyles.ContainsKey(key))
            {
                inlineStyles[key] = (styleContent, lineNumber);
            }
        }
        
        // Create elements for significant inline styles
        foreach (var kvp in inlineStyles)
        {
            var (styleContent, lineNumber) = kvp.Value;
            var elementName = kvp.Key.Split('_')[0];
            
            var inlineStyle = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"InlineStyle_{elementName}_L{lineNumber}",
                Content = styleContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "inline_style",
                    ["element"] = elementName,
                    ["embedded_in"] = "razor"
                }
            };
            
            result.CodeElements.Add(inlineStyle);
            
            // Create relationship: File DEFINES InlineStyle
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = inlineStyle.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }
    
    private static void CreateRazorRelationships(string fileName, string? modelType, ParseResult result)
    {
        var fileBaseName = Path.GetFileNameWithoutExtension(fileName);
        
        // Create relationship to model type
        if (modelType != null)
        {
            result.Relationships.Add(new CodeRelationship
            {
                FromName = fileBaseName,
                ToName = modelType,
                Type = RelationshipType.Uses,
                Properties = new Dictionary<string, object>
                {
                    ["relationship_type"] = "razor_model"
                }
            });
        }
    }
}

