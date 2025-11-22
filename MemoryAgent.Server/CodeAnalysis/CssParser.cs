using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for CSS, SCSS, and LESS files with smart chunking
/// </summary>
public class CssParser
{
    public static ParseResult ParseCssFile(string filePath, string? context = null)
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
                    ["is_stylesheet"] = true
                }
            };
            
            result.CodeElements.Add(fileNode);
            
            // Extract CSS rules (selectors and declarations)
            ExtractCssRules(content, filePath, context, result);
            
            // Extract CSS variables (--var-name or $variable for SCSS)
            ExtractCssVariables(content, filePath, context, result, extension);
            
            // Extract media queries
            ExtractMediaQueries(content, filePath, context, result);
            
            // Extract keyframe animations
            ExtractKeyframes(content, filePath, context, result);
            
            // Extract SCSS/LESS mixins and functions
            if (extension == ".scss" || extension == ".less")
            {
                ExtractMixins(content, filePath, context, result);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing CSS file {filePath}: {ex.Message}");
        }

        return result;
    }

    private static void ExtractCssRules(string content, string filePath, string? context, ParseResult result)
    {
        // Match CSS rules: selector { declarations }
        // More permissive regex to handle nested rules and complex selectors
        var rulePattern = @"([^{}@]+)\{([^{}]+(?:\{[^{}]*\}[^{}]*)*)\}";
        var matches = Regex.Matches(content, rulePattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var selector = match.Groups[1].Value.Trim();
            var declarations = match.Groups[2].Value.Trim();
            
            // Skip empty selectors or comments
            if (string.IsNullOrWhiteSpace(selector) || selector.StartsWith("/*") || selector.StartsWith("//"))
                continue;
            
            // Skip if selector looks like it's inside a media query or keyframe (will be handled separately)
            if (selector.Contains("@media") || selector.Contains("@keyframes"))
                continue;

            var lineNumber = GetLineNumber(content, match.Index);
            
            // Create a chunk for this CSS rule
            var rule = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Style: {TruncateSelector(selector)}",
                Content = $"{selector} {{\n{declarations}\n}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "css_rule",
                    ["selector"] = selector,
                    ["has_nested"] = declarations.Contains("{")
                }
            };
            
            result.CodeElements.Add(rule);
            
            // Create relationship: File DEFINES Rule
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = rule.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private static void ExtractCssVariables(string content, string filePath, string? context, ParseResult result, string extension)
    {
        List<(string name, string value, int lineNumber)> variables = new();
        
        // CSS custom properties: --variable-name: value;
        var cssVarPattern = @"--([\w-]+)\s*:\s*([^;]+);";
        foreach (Match match in Regex.Matches(content, cssVarPattern))
        {
            variables.Add((
                $"--{match.Groups[1].Value}",
                match.Groups[2].Value.Trim(),
                GetLineNumber(content, match.Index)
            ));
        }
        
        // SCSS/LESS variables: $variable-name: value; or @variable-name: value;
        if (extension == ".scss" || extension == ".less")
        {
            var scssVarPattern = extension == ".scss" 
                ? @"\$([\w-]+)\s*:\s*([^;]+);" 
                : @"@([\w-]+)\s*:\s*([^;]+);";
            
            foreach (Match match in Regex.Matches(content, scssVarPattern))
            {
                var prefix = extension == ".scss" ? "$" : "@";
                variables.Add((
                    $"{prefix}{match.Groups[1].Value}",
                    match.Groups[2].Value.Trim(),
                    GetLineNumber(content, match.Index)
                ));
            }
        }

        foreach (var (name, value, lineNumber) in variables)
        {
            var variable = new CodeMemory
            {
                Type = CodeMemoryType.Property,
                Name = $"Variable: {name}",
                Content = $"{name}: {value};",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "css_variable",
                    ["variable_name"] = name,
                    ["variable_value"] = value
                }
            };
            
            result.CodeElements.Add(variable);
            
            // Create relationship: File DEFINES Variable
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = variable.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private static void ExtractMediaQueries(string content, string filePath, string? context, ParseResult result)
    {
        // Match @media queries
        var mediaPattern = @"@media\s*([^{]+)\{((?:[^{}]|\{[^{}]*\})*)\}";
        var matches = Regex.Matches(content, mediaPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var condition = match.Groups[1].Value.Trim();
            var rules = match.Groups[2].Value.Trim();
            var lineNumber = GetLineNumber(content, match.Index);
            
            var mediaQuery = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"MediaQuery: {condition}",
                Content = $"@media {condition} {{\n{rules}\n}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "media_query",
                    ["condition"] = condition
                }
            };
            
            result.CodeElements.Add(mediaQuery);
            
            // Create relationship: File DEFINES MediaQuery
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = mediaQuery.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private static void ExtractKeyframes(string content, string filePath, string? context, ParseResult result)
    {
        // Match @keyframes animations
        var keyframePattern = @"@keyframes\s+([\w-]+)\s*\{((?:[^{}]|\{[^{}]*\})*)\}";
        var matches = Regex.Matches(content, keyframePattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var animationName = match.Groups[1].Value.Trim();
            var frames = match.Groups[2].Value.Trim();
            var lineNumber = GetLineNumber(content, match.Index);
            
            var keyframe = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Animation: {animationName}",
                Content = $"@keyframes {animationName} {{\n{frames}\n}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "keyframe_animation",
                    ["animation_name"] = animationName
                }
            };
            
            result.CodeElements.Add(keyframe);
            
            // Create relationship: File DEFINES Keyframe
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = keyframe.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private static void ExtractMixins(string content, string filePath, string? context, ParseResult result)
    {
        // Match SCSS mixins: @mixin name() { ... }
        var mixinPattern = @"@mixin\s+([\w-]+)\s*\(([^)]*)\)\s*\{((?:[^{}]|\{[^{}]*\})*)\}";
        var matches = Regex.Matches(content, mixinPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var mixinName = match.Groups[1].Value.Trim();
            var parameters = match.Groups[2].Value.Trim();
            var body = match.Groups[3].Value.Trim();
            var lineNumber = GetLineNumber(content, match.Index);
            
            var mixin = new CodeMemory
            {
                Type = CodeMemoryType.Method,
                Name = $"Mixin: {mixinName}",
                Content = $"@mixin {mixinName}({parameters}) {{\n{body}\n}}",
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = lineNumber,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "scss_mixin",
                    ["mixin_name"] = mixinName,
                    ["parameters"] = parameters
                }
            };
            
            result.CodeElements.Add(mixin);
            
            // Create relationship: File DEFINES Mixin
            result.Relationships.Add(new CodeRelationship
            {
                FromName = Path.GetFileName(filePath),
                ToName = mixin.Name,
                Type = RelationshipType.Defines,
                Context = context ?? "default"
            });
        }
    }

    private static string TruncateSelector(string selector)
    {
        // Truncate long selectors for readability
        if (selector.Length > 60)
        {
            return selector.Substring(0, 57) + "...";
        }
        return selector;
    }

    private static int GetLineNumber(string content, int index)
    {
        return content.Substring(0, Math.Min(index, content.Length)).Count(c => c == '\n') + 1;
    }
}

