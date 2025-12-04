using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Flutter widget and UI patterns
/// Covers widgets, lifecycle, performance, state management, and anti-patterns
/// </summary>
public class FlutterPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Flutter };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Check for anti-patterns in metadata
        if (pattern.Metadata.TryGetValue("is_anti_pattern", out var isAntiPattern) && (bool)isAntiPattern)
        {
            var severity = pattern.Metadata.TryGetValue("severity", out var sev) ? sev.ToString() : "medium";
            var severityLevel = severity switch
            {
                "critical" => IssueSeverity.Critical,
                "high" => IssueSeverity.High,
                "low" => IssueSeverity.Low,
                _ => IssueSeverity.Medium
            };

            result.Issues.Add(new ValidationIssue
            {
                Severity = severityLevel,
                Category = IssueCategory.BestPractice,
                Message = $"Flutter Anti-Pattern: {pattern.Implementation}",
                ScoreImpact = severity == "critical" ? 5 : severity == "high" ? 3 : 2,
                FixGuidance = pattern.BestPractice
            });
            result.Score -= severity == "critical" ? 5 : severity == "high" ? 3 : 2;
        }

        // Widget-specific validations
        switch (pattern.Name)
        {
            case "Flutter_StatelessWidget":
                if (pattern.Metadata.TryGetValue("has_const_constructor", out var hasConst) && !(bool)hasConst)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Performance,
                        Message = "StatelessWidget without const constructor - missed optimization opportunity",
                        ScoreImpact = 1,
                        FixGuidance = "Add const constructor: const MyWidget({super.key});"
                    });
                    result.Score -= 1;
                }
                break;

            case "Flutter_StatefulWidget":
                if (pattern.Metadata.TryGetValue("has_state_class", out var hasState) && !(bool)hasState)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Correctness,
                        Message = "StatefulWidget without corresponding State class",
                        ScoreImpact = 5,
                        FixGuidance = "Create _MyWidgetState class that extends State<MyWidget>"
                    });
                    result.Score -= 5;
                }
                break;

            case "Flutter_MissingDispose_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Reliability,
                    Message = "Memory leak: Controller found without dispose() method",
                    ScoreImpact = 4,
                    FixGuidance = "Override dispose() and call controller.dispose() before super.dispose()"
                });
                result.Score -= 4;
                break;

            case "Flutter_ChangeNotifier":
                if (pattern.Metadata.TryGetValue("has_dispose", out var hasDispose) && !(bool)hasDispose)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Reliability,
                        Message = "ChangeNotifier without dispose - potential memory leak",
                        ScoreImpact = 3,
                        FixGuidance = "Override dispose() to clean up resources and remove listeners"
                    });
                    result.Score -= 3;
                }
                break;

            case "Flutter_AnimationController":
                if (pattern.Metadata.TryGetValue("disposes_controller", out var disposesCtrl) && !(bool)disposesCtrl)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Reliability,
                        Message = "AnimationController without dispose - will cause memory leak",
                        ScoreImpact = 4,
                        FixGuidance = "Call _controller.dispose() in the dispose() method"
                    });
                    result.Score -= 4;
                }
                break;

            case "Flutter_InitState":
            case "Flutter_Dispose":
                if (pattern.Metadata.TryGetValue("calls_super", out var callsSuper) && !(bool)callsSuper)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Correctness,
                        Message = pattern.Name == "Flutter_InitState" 
                            ? "initState doesn't call super.initState()" 
                            : "dispose doesn't call super.dispose()",
                        ScoreImpact = 3,
                        FixGuidance = pattern.Name == "Flutter_InitState"
                            ? "Call super.initState() as the first line in initState()"
                            : "Call super.dispose() as the last line in dispose()"
                    });
                    result.Score -= 3;
                }
                break;

            case "Flutter_FutureBuilder":
            case "Flutter_StreamBuilder":
                if (pattern.Metadata.TryGetValue("handles_error", out var handlesError) && !(bool)handlesError)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.BestPractice,
                        Message = "FutureBuilder/StreamBuilder without error handling",
                        ScoreImpact = 2,
                        FixGuidance = "Handle snapshot.hasError and snapshot.connectionState in builder"
                    });
                    result.Score -= 2;
                }
                break;

            case "Flutter_FormHandling":
                if (pattern.Metadata.TryGetValue("has_validation", out var hasValidation) && !(bool)hasValidation)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Security,
                        Message = "Form without input validation - security risk",
                        ScoreImpact = 3,
                        FixGuidance = "Add validator property to TextFormField widgets"
                    });
                    result.Score -= 3;
                    result.SecurityScore -= 2;
                }
                break;

            case "Flutter_HeroAnimation":
                if (pattern.Metadata.TryGetValue("has_tag", out var hasTag) && !(bool)hasTag)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Correctness,
                        Message = "Hero widget without tag property",
                        ScoreImpact = 2,
                        FixGuidance = "Add unique tag property to Hero widget for transition matching"
                    });
                    result.Score -= 2;
                }
                break;

            // Performance anti-patterns
            case "Flutter_SetStateInBuild_AntiPattern":
            case "Flutter_AsyncInBuild_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Performance,
                    Message = pattern.Name == "Flutter_SetStateInBuild_AntiPattern"
                        ? "setState called in build() - causes infinite rebuild loop"
                        : "async/await in build() - build must be synchronous",
                    ScoreImpact = 5,
                    FixGuidance = pattern.Name == "Flutter_SetStateInBuild_AntiPattern"
                        ? "Move setState calls to event handlers or lifecycle methods"
                        : "Use FutureBuilder/StreamBuilder or fetch data in initState"
                });
                result.Score -= 5;
                break;

            case "Flutter_ListViewChildren_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Performance,
                    Message = "ListView with children instead of builder - not lazy loaded",
                    ScoreImpact = 2,
                    FixGuidance = "Use ListView.builder for lists with many items"
                });
                result.Score -= 2;
                break;

            case "Flutter_UncachedNetworkImage_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.Performance,
                    Message = "Image.network without caching - images redownloaded on rebuild",
                    ScoreImpact = 1,
                    FixGuidance = "Use CachedNetworkImage from cached_network_image package"
                });
                result.Score -= 1;
                break;

            case "Flutter_ExcessiveSetState_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = $"Too many setState calls ({pattern.Metadata.GetValueOrDefault("setState_count", 0)})",
                    ScoreImpact = 2,
                    FixGuidance = "Consider Provider, Riverpod, or BLoC for complex state management"
                });
                result.Score -= 2;
                break;
        }

        // Add positive recommendations for good patterns
        if (pattern.Name == "Flutter_LazyListBuilder")
            result.Recommendations.Add("Excellent! ListView.builder ensures lazy loading for performance");
        if (pattern.Name == "Flutter_RepaintBoundary")
            result.Recommendations.Add("Good performance practice - RepaintBoundary isolates repaints");
        if (pattern.Name == "Flutter_ComputeIsolate")
            result.Recommendations.Add("Excellent! Using compute/Isolate keeps UI thread responsive");
        if (pattern.Name == "Flutter_ConstWidget")
            result.Recommendations.Add("Great! const widgets enable compile-time optimization");

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Flutter Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

