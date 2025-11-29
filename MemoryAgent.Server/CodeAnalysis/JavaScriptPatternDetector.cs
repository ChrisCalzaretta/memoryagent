using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects state management and best practice patterns in JavaScript and TypeScript code
/// Supports: React, Vue, Angular, Redux, MobX, and vanilla JavaScript
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    public string GetLanguage() => "JavaScript";

    public List<PatternType> GetSupportedPatternTypes() => Enum.GetValues<PatternType>().ToList();

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var lines = sourceCode.Split('\n');
            var isTypeScript = filePath.EndsWith(".ts") || filePath.EndsWith(".tsx");
            var language = isTypeScript ? "typescript" : "javascript";

            patterns.AddRange(DetectReactStatePatterns(sourceCode, lines, filePath, context, language));
            patterns.AddRange(DetectReduxPatterns(sourceCode, lines, filePath, context, language));
            patterns.AddRange(DetectVueStatePatterns(sourceCode, lines, filePath, context, language));
            patterns.AddRange(DetectBrowserStoragePatterns(sourceCode, lines, filePath, context, language));
            patterns.AddRange(DetectServerStatePatterns(sourceCode, lines, filePath, context, language));
            patterns.AddRange(DetectFormStatePatterns(sourceCode, lines, filePath, context, language));
            
            // TYPESCRIPT DECORATOR PATTERNS (30 comprehensive patterns)
            if (isTypeScript)
            {
                patterns.AddRange(DetectAngularDecorators(sourceCode, lines, filePath, context, language));
                patterns.AddRange(DetectNestJSDecorators(sourceCode, lines, filePath, context, language));
                patterns.AddRange(DetectTypeORMDecorators(sourceCode, lines, filePath, context, language));
                patterns.AddRange(DetectValidationDecorators(sourceCode, lines, filePath, context, language));
                patterns.AddRange(DetectMobXDecorators(sourceCode, lines, filePath, context, language));
            }
            
            // AZURE ARCHITECTURE PATTERNS (36 patterns for JavaScript/TypeScript)
            // TODO: Implement DetectAzurePatternsJavaScript
            // patterns.AddRange(DetectAzurePatternsJavaScript(sourceCode, lines, filePath, context, language));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }
    
}
