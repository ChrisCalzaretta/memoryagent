using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Form State Management
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Form State Management

    private List<CodePattern> DetectFormStatePatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // React Hook Form
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useForm\(|register\(|handleSubmit"))
            {
                patterns.Add(CreatePattern(
                    name: "ReactHookForm_FormState",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React Hook Form",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use React Hook Form for performant form state with minimal re-renders",
                    azureUrl: "https://react-hook-form.com/",
                    context: context,
                    language: language
                ));
            }
        }

        // Formik
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useFormik\(|<Formik"))
            {
                patterns.Add(CreatePattern(
                    name: "Formik_FormState",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Formik",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Formik for form state management with built-in validation",
                    azureUrl: "https://formik.org/",
                    context: context,
                    language: language
                ));
            }
        }

        // Controlled Components
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"value=\{.*\}\s+onChange=\{") || 
                Regex.IsMatch(lines[i], @"value:\s*\w+,\s*onChange:"))
            {
                patterns.Add(CreatePattern(
                    name: "React_ControlledComponent",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Controlled Component",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use controlled components for form inputs with React state as single source of truth",
                    azureUrl: "https://react.dev/learn/sharing-state-between-components",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
