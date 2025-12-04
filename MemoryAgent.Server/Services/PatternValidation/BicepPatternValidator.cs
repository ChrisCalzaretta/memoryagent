using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Azure Bicep Infrastructure as Code patterns
/// Covers resources, modules, parameters, decorators, and anti-patterns
/// </summary>
public class BicepPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Bicep };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern
        };

        var metadata = pattern.Metadata;
        var isAntiPattern = pattern.Name.Contains("AntiPattern");

        if (isAntiPattern)
        {
            // Anti-Pattern: Hardcoded Location
            if (pattern.Name.Contains("HardcodedLocation"))
            {
                result.Score = 3;
                result.SecurityScore = 5;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = "Hardcoded location reduces flexibility",
                    FixGuidance = "Replace hardcoded location with: param location string = resourceGroup().location"
                });
                result.Recommendations.Add("// Replace hardcoded location\nparam location string = resourceGroup().location\n\nresource myResource 'type@version' = {\n  location: location\n}");
            }
            // Anti-Pattern: Hardcoded Names
            else if (pattern.Name.Contains("HardcodedNames"))
            {
                result.Score = 4;
                result.SecurityScore = 6;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = "Hardcoded resource names reduce reusability",
                    FixGuidance = "Use: var uniqueName = '${baseName}-${uniqueString(resourceGroup().id)}'"
                });
                result.Recommendations.Add("param baseName string\nvar uniqueName = '${baseName}-${uniqueString(resourceGroup().id)}'\n\nresource myResource 'type@version' = {\n  name: uniqueName\n}");
            }
            // Anti-Pattern: Missing Validation
            else if (pattern.Name.Contains("MissingValidation"))
            {
                result.Score = 5;
                result.SecurityScore = 5;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Correctness,
                    Message = "Parameters without validation decorators",
                    FixGuidance = "Add validation: @allowed(['dev', 'prod'])\nparam environment string"
                });
                result.Recommendations.Add("@description('Environment name')\n@allowed(['dev', 'staging', 'prod'])\nparam environment string\n\n@description('Resource name')\n@minLength(3)\n@maxLength(24)\nparam resourceName string");
            }
            // Anti-Pattern: Old API Versions
            else if (pattern.Name.Contains("OldApiVersion"))
            {
                result.Score = 6;
                result.SecurityScore = 7;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.BestPractice,
                    Message = "Old API versions may be deprecated",
                    FixGuidance = "Check Azure documentation for latest API version and update"
                });
            }
        }
        else
        {
            // Positive Patterns
            switch (pattern.Name)
            {
                case "Bicep_Resource":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "Well-defined Bicep resource with typed syntax";
                    if (!metadata.ContainsKey("api_version") || metadata["api_version"]?.ToString()?.StartsWith("201") == true)
                    {
                        result.Score = 7;
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.BestPractice,
                            Message = "API version might be outdated",
                            FixGuidance = "Check Azure template reference for latest API version"
                        });
                    }
                    break;

                case "Bicep_Module":
                    result.Score = 10;
                    result.SecurityScore = 9;
                    result.Summary = "Excellent: Bicep modules for reusable infrastructure";
                    var isRemote = metadata.GetValueOrDefault("is_remote_module")?.ToString() == "True";
                    if (isRemote)
                    {
                        result.Summary += " (remote module from registry)";
                    }
                    break;

                case "Bicep_Parameter":
                    var hasValidation = metadata.GetValueOrDefault("has_allowed")?.ToString() == "True" ||
                                      metadata.GetValueOrDefault("has_min_length")?.ToString() == "True" ||
                                      metadata.GetValueOrDefault("has_max_length")?.ToString() == "True";
                    var hasSecure = metadata.GetValueOrDefault("has_secure")?.ToString() == "True";
                    var hasDescription = metadata.GetValueOrDefault("has_description")?.ToString() == "True";

                    result.Score = 7;
                    result.SecurityScore = hasSecure ? 9 : 6;

                    if (hasValidation) result.Score += 1;
                    if (hasSecure) result.Score += 1;
                    if (hasDescription) result.Score += 1;

                    result.Summary = $"Parameter with {(hasValidation ? "validation" : "no validation")}, {(hasSecure ? "secure" : "not secure")}";

                    if (!hasDescription)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.BestPractice,
                            Message = "Missing @description decorator",
                            FixGuidance = "Add: @description('Parameter description here')"
                        });
                    }

                    if (!hasValidation && metadata.GetValueOrDefault("parameter_type")?.ToString() == "string")
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.Correctness,
                            Message = "String parameter without validation",
                            FixGuidance = "Add validation: @minLength(3) @maxLength(24)"
                        });
                    }
                    break;

                case "Bicep_Output":
                    result.Score = 8;
                    result.SecurityScore = 8;
                    result.Summary = "Bicep output for exported values";
                    if (metadata.GetValueOrDefault("has_description")?.ToString() != "True")
                    {
                        result.Score = 7;
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.BestPractice,
                            Message = "Output missing @description",
                            FixGuidance = "Add: @description('Output description')"
                        });
                    }
                    break;

                case "Bicep_ExistingResource":
                    result.Score = 9;
                    result.SecurityScore = 9;
                    result.Summary = "Existing resource reference (best practice)";
                    break;

                case "Bicep_TargetScope":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "Explicit target scope definition";
                    break;

                case "Bicep_ConditionalDeployment":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "Conditional deployment logic";
                    break;

                case "Bicep_Loop":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "For loop for dynamic resource creation";
                    break;

                case "Bicep_UniqueString":
                    result.Score = 10;
                    result.SecurityScore = 9;
                    result.Summary = "Excellent: uniqueString() for deterministic unique names";
                    break;

                case "Bicep_DependsOn":
                    result.Score = 8;
                    result.SecurityScore = 9;
                    result.Summary = "Explicit resource dependencies (prevents race conditions)";
                    break;

                case "Bicep_LocationParameter":
                    result.Score = 10;
                    result.SecurityScore = 9;
                    result.Summary = "Excellent: Parameterized location for flexibility";
                    break;

                default:
                    if (pattern.Name.StartsWith("Bicep_Decorator_"))
                    {
                        result.Score = 9;
                        result.SecurityScore = 9;
                        result.Summary = "Excellent: Using Bicep decorators for validation/documentation";
                    }
                    else if (pattern.Name.StartsWith("Bicep_Function_"))
                    {
                        result.Score = 8;
                        result.SecurityScore = 8;
                        result.Summary = "Using Bicep functions for dynamic values";
                    }
                    else
                    {
                        result.Score = 7;
                        result.SecurityScore = 7;
                        result.Summary = "Bicep pattern detected";
                    }
                    break;
            }
        }

        // Grade is computed from Score automatically
        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

