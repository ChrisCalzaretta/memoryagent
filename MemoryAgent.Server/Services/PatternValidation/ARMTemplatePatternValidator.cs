using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Azure Resource Manager (ARM) Template patterns
/// Covers template structure, parameters, resources, functions, and anti-patterns
/// </summary>
public class ARMTemplatePatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.ARMTemplate };

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
            // Anti-Pattern: Hardcoded Secret
            if (pattern.Name.Contains("HardcodedSecret"))
            {
                result.Score = 1;
                result.SecurityScore = 0;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "CRITICAL: Hardcoded secret detected",
                    FixGuidance = "Use Azure Key Vault reference: \"reference\": { \"keyVault\": { \"id\": \"keyVaultResourceId\" }, \"secretName\": \"secretName\" }"
                });
                result.Recommendations.Add("// Use Key Vault reference\n\"password\": {\n  \"reference\": {\n    \"keyVault\": {\n      \"id\": \"[parameters('keyVaultId')]\"\n    },\n    \"secretName\": \"dbPassword\"\n  }\n}");
            }
            // Anti-Pattern: Missing Parameterization
            else if (pattern.Name.Contains("MissingParameterization"))
            {
                result.Score = 4;
                result.SecurityScore = 6;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = "Template not parameterized",
                    FixGuidance = "Add parameters section with environment-specific values"
                });
                result.Recommendations.Add("\"parameters\": {\n  \"environment\": {\n    \"type\": \"string\",\n    \"allowedValues\": [\"dev\", \"staging\", \"prod\"],\n    \"metadata\": { \"description\": \"Environment name\" }\n  },\n  \"location\": {\n    \"type\": \"string\",\n    \"defaultValue\": \"[resourceGroup().location]\"\n  }\n}");
            }
        }
        else
        {
            // Positive Patterns
            switch (pattern.Name)
            {
                case "ARM_Schema":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "Valid ARM template schema";
                    var isManaged = metadata.GetValueOrDefault("is_managed_app_template")?.ToString() == "True";
                    if (isManaged)
                    {
                        result.Summary += " (Managed Application)";
                    }
                    break;

                case "ARM_Parameter":
                    var hasValidation = metadata.GetValueOrDefault("has_validation")?.ToString() == "True";
                    var hasMetadata = metadata.GetValueOrDefault("has_metadata")?.ToString() == "True";
                    var hasDefaultValue = metadata.GetValueOrDefault("has_default_value")?.ToString() == "True";

                    result.Score = 7;
                    result.SecurityScore = 7;

                    if (hasValidation) result.Score += 1;
                    if (hasMetadata) result.Score += 1;
                    if (hasDefaultValue) result.Score += 1;

                    result.Summary = $"Parameter with {(hasValidation ? "validation" : "no validation")}";

                    if (!hasMetadata)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.BestPractice,
                            Message = "Missing metadata",
                            FixGuidance = "Add: \"metadata\": { \"description\": \"Parameter description\" }"
                        });
                    }

                    if (!hasValidation)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Low,
                            Category = IssueCategory.Correctness,
                            Message = "Parameter without validation",
                            FixGuidance = "Add validation constraints to prevent invalid values"
                        });
                    }
                    break;

                case "ARM_Variables":
                    result.Score = 8;
                    result.SecurityScore = 8;
                    result.Summary = "ARM template variables for DRY principle";
                    break;

                case "ARM_Resource":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "ARM template resource definition";
                    break;

                case "ARM_Output":
                    result.Score = 8;
                    result.SecurityScore = 8;
                    result.Summary = "ARM template output";
                    break;

                case "ARM_UserDefinedFunctions":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "User-defined functions for reusable logic";
                    break;

                case "ARM_ParameterFile":
                    result.Score = 10;
                    result.SecurityScore = 9;
                    result.Summary = "Excellent: Separate parameter file for environment-specific values";
                    break;

                case "ARM_LinkedTemplate":
                    result.Score = 10;
                    result.SecurityScore = 9;
                    result.Summary = "Excellent: Linked templates for modular deployments";
                    break;

                case "ARM_CopyLoop":
                    result.Score = 9;
                    result.SecurityScore = 8;
                    result.Summary = "Copy loops for resource iteration";
                    break;

                case "ARM_DependsOn":
                    result.Score = 9;
                    result.SecurityScore = 9;
                    result.Summary = "Explicit dependencies to prevent deployment race conditions";
                    break;

                default:
                    if (pattern.Name.StartsWith("ARM_Function_"))
                    {
                        result.Score = 8;
                        result.SecurityScore = 8;
                        result.Summary = "ARM template function usage";
                    }
                    else
                    {
                        result.Score = 7;
                        result.SecurityScore = 7;
                        result.Summary = "ARM template pattern detected";
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

