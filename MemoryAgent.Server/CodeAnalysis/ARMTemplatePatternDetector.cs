using System.Text.Json;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Azure ARM Template (JSON) patterns for Infrastructure as Code
/// Covers: Parameters, Variables, Resources, Outputs, Functions, Best Practices
/// Reference: https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/syntax
/// </summary>
public class ARMTemplatePatternDetector
{
    private readonly ILogger<ARMTemplatePatternDetector>? _logger;

    public ARMTemplatePatternDetector(ILogger<ARMTemplatePatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            // Parse JSON
            var doc = JsonDocument.Parse(sourceCode);
            var root = doc.RootElement;

            // Detect schema and version
            patterns.AddRange(DetectSchema(root, filePath, context));
            
            // Core ARM Template sections
            patterns.AddRange(DetectParameters(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVariables(root, filePath, context, sourceCode));
            patterns.AddRange(DetectResources(root, filePath, context, sourceCode));
            patterns.AddRange(DetectOutputs(root, filePath, context, sourceCode));
            patterns.AddRange(DetectFunctions(root, filePath, context, sourceCode));
            
            // Best Practices
            patterns.AddRange(DetectParameterFiles(root, filePath, context));
            patterns.AddRange(DetectLinkedTemplates(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCopyLoops(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDependsOn(root, filePath, context, sourceCode));
            
            // Template Functions Usage
            patterns.AddRange(DetectTemplateFunctions(sourceCode, filePath, context));
            
            // Anti-Patterns
            patterns.AddRange(DetectHardcodedSecrets(sourceCode, filePath, context));
            patterns.AddRange(DetectMissingParameterization(root, filePath, context, sourceCode));

            _logger?.LogInformation("Detected {Count} ARM template patterns in {FilePath}", patterns.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting ARM template patterns in {FilePath}", filePath);
        }

        return await Task.FromResult(patterns);
    }

    #region Core ARM Template Sections

    private List<CodePattern> DetectSchema(JsonElement root, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("$schema", out var schema))
        {
            var schemaUrl = schema.GetString() ?? "";
            var isDeploymentTemplate = schemaUrl.Contains("deploymentTemplate.json");
            var isSubscriptionTemplate = schemaUrl.Contains("subscriptionDeploymentTemplate.json");
            var isManagedAppTemplate = schemaUrl.Contains("managedapplication");

            patterns.Add(new CodePattern
            {
                Name = "ARM_Schema",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"ARM Template Schema: {(isDeploymentTemplate ? "Resource Group" : isSubscriptionTemplate ? "Subscription" : "Other")}",
                FilePath = filePath,
                LineNumber = 1,
                Content = schemaUrl,
                BestPractice = "ARM template schema: defines template type and validation rules",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/syntax",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["schema_url"] = schemaUrl,
                    ["is_deployment_template"] = isDeploymentTemplate,
                    ["is_subscription_template"] = isSubscriptionTemplate,
                    ["is_managed_app_template"] = isManagedAppTemplate,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectParameters(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("parameters", out var parameters))
        {
            foreach (var param in parameters.EnumerateObject())
            {
                var paramName = param.Name;
                var paramValue = param.Value;

                var hasType = paramValue.TryGetProperty("type", out _);
                var hasDefaultValue = paramValue.TryGetProperty("defaultValue", out _);
                var hasAllowedValues = paramValue.TryGetProperty("allowedValues", out _);
                var hasMetadata = paramValue.TryGetProperty("metadata", out _);
                var hasMinValue = paramValue.TryGetProperty("minValue", out _);
                var hasMaxValue = paramValue.TryGetProperty("maxValue", out _);
                var hasMinLength = paramValue.TryGetProperty("minLength", out _);
                var hasMaxLength = paramValue.TryGetProperty("maxLength", out _);

                patterns.Add(new CodePattern
                {
                    Name = "ARM_Parameter",
                    Type = PatternType.ARMTemplate,
                    Category = PatternCategory.InfrastructureAsCode,
                    Implementation = $"Parameter: {paramName}",
                    FilePath = filePath,
                    LineNumber = 1,
                    Content = paramName,
                    BestPractice = "ARM template parameters: define input values with type constraints and validation",
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/parameters",
                    Context = context,
                    Confidence = 0.95f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["parameter_name"] = paramName,
                        ["has_type"] = hasType,
                        ["has_default_value"] = hasDefaultValue,
                        ["has_allowed_values"] = hasAllowedValues,
                        ["has_metadata"] = hasMetadata,
                        ["has_validation"] = hasMinValue || hasMaxValue || hasMinLength || hasMaxLength || hasAllowedValues,
                        ["is_positive_pattern"] = true
                    }
                });
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectVariables(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("variables", out var variables))
        {
            var variableCount = variables.EnumerateObject().Count();

            if (variableCount > 0)
            {
                patterns.Add(new CodePattern
                {
                    Name = "ARM_Variables",
                    Type = PatternType.ARMTemplate,
                    Category = PatternCategory.InfrastructureAsCode,
                    Implementation = $"{variableCount} variables defined",
                    FilePath = filePath,
                    LineNumber = 1,
                    Content = "variables",
                    BestPractice = "ARM template variables: compute and reuse values to reduce duplication (DRY)",
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/variables",
                    Context = context,
                    Confidence = 0.90f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["variable_count"] = variableCount,
                        ["is_positive_pattern"] = true
                    }
                });
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectResources(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("resources", out var resources))
        {
            foreach (var resource in resources.EnumerateArray())
            {
                if (resource.TryGetProperty("type", out var type) && 
                    resource.TryGetProperty("apiVersion", out var apiVersion))
                {
                    var resourceType = type.GetString() ?? "unknown";
                    var apiVer = apiVersion.GetString() ?? "unknown";
                    var name = resource.TryGetProperty("name", out var n) ? n.GetString() : "unnamed";

                    patterns.Add(new CodePattern
                    {
                        Name = "ARM_Resource",
                        Type = PatternType.ARMTemplate,
                        Category = PatternCategory.InfrastructureAsCode,
                        Implementation = $"Resource: {resourceType}",
                        FilePath = filePath,
                        LineNumber = 1,
                        Content = resourceType,
                        BestPractice = "ARM template resource: define Azure infrastructure declaratively",
                        AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/resource-declaration",
                        Context = context,
                        Confidence = 0.95f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["resource_type"] = resourceType,
                            ["resource_name"] = name ?? "unnamed",
                            ["api_version"] = apiVer,
                            ["is_positive_pattern"] = true
                        }
                    });
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOutputs(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("outputs", out var outputs))
        {
            foreach (var output in outputs.EnumerateObject())
            {
                var outputName = output.Name;
                var outputValue = output.Value;
                var hasType = outputValue.TryGetProperty("type", out _);

                patterns.Add(new CodePattern
                {
                    Name = "ARM_Output",
                    Type = PatternType.ARMTemplate,
                    Category = PatternCategory.InfrastructureAsCode,
                    Implementation = $"Output: {outputName}",
                    FilePath = filePath,
                    LineNumber = 1,
                    Content = outputName,
                    BestPractice = "ARM template outputs: export values for use by other deployments or applications",
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/outputs",
                    Context = context,
                    Confidence = 0.95f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["output_name"] = outputName,
                        ["has_type"] = hasType,
                        ["is_positive_pattern"] = true
                    }
                });
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectFunctions(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (root.TryGetProperty("functions", out var functions))
        {
            patterns.Add(new CodePattern
            {
                Name = "ARM_UserDefinedFunctions",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "User-defined functions",
                FilePath = filePath,
                LineNumber = 1,
                Content = "functions",
                BestPractice = "User-defined functions: create custom reusable functions within templates",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/user-defined-functions",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Best Practices

    private List<CodePattern> DetectParameterFiles(JsonElement root, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Check if this is a parameters file (has parameters but no resources)
        var hasParameters = root.TryGetProperty("parameters", out _);
        var hasResources = root.TryGetProperty("resources", out _);

        if (hasParameters && !hasResources)
        {
            patterns.Add(new CodePattern
            {
                Name = "ARM_ParameterFile",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Parameter file pattern",
                FilePath = filePath,
                LineNumber = 1,
                Content = "parameters file",
                BestPractice = "Parameter files: separate environment-specific values from template logic",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/parameter-files",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectLinkedTemplates(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check for linked/nested templates
        if (sourceCode.Contains("Microsoft.Resources/deployments"))
        {
            patterns.Add(new CodePattern
            {
                Name = "ARM_LinkedTemplate",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Linked/nested deployment",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Microsoft.Resources/deployments",
                BestPractice = "Linked templates: break complex deployments into smaller, reusable templates",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/linked-templates",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectCopyLoops(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (sourceCode.Contains("\"copy\""))
        {
            var count = Regex.Matches(sourceCode, "\"copy\"").Count;

            patterns.Add(new CodePattern
            {
                Name = "ARM_CopyLoop",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Copy loops - {count} occurrences",
                FilePath = filePath,
                LineNumber = 1,
                Content = "copy",
                BestPractice = "Copy loops: create multiple instances of resources, properties, variables, or outputs",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/copy-resources",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["copy_count"] = count,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectDependsOn(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        if (sourceCode.Contains("\"dependsOn\""))
        {
            var count = Regex.Matches(sourceCode, "\"dependsOn\"").Count;

            patterns.Add(new CodePattern
            {
                Name = "ARM_DependsOn",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Explicit dependencies - {count} occurrences",
                FilePath = filePath,
                LineNumber = 1,
                Content = "dependsOn",
                BestPractice = "dependsOn: explicitly define resource deployment order to prevent race conditions",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/resource-dependency",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["dependency_count"] = count,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Template Functions

    private List<CodePattern> DetectTemplateFunctions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        var functionCategories = new Dictionary<string, (string[] functions, string description)>
        {
            ["Resource"] = (new[] { "resourceId(", "subscription(", "resourceGroup(", "reference(", "list" }, 
                "Resource functions: get resource IDs, properties, and list secrets"),
            ["String"] = (new[] { "concat(", "format(", "guid(", "uniqueString(", "toLower(", "toUpper(", "substring(" }, 
                "String functions: string manipulation and generation"),
            ["Array"] = (new[] { "concat(", "contains(", "empty(", "first(", "last(", "length(", "union(" }, 
                "Array functions: array operations and transformations"),
            ["Deployment"] = (new[] { "deployment(", "environment(", "parameters(", "variables(" }, 
                "Deployment functions: access template context and inputs"),
            ["Logical"] = (new[] { "if(", "and(", "or(", "not(" }, 
                "Logical functions: conditional logic"),
            ["Numeric"] = (new[] { "add(", "sub(", "mul(", "div(", "int(", "min(", "max(" }, 
                "Numeric functions: mathematical operations")
        };

        foreach (var (category, (functions, description)) in functionCategories)
        {
            foreach (var func in functions)
            {
                if (sourceCode.Contains(func))
                {
                    patterns.Add(new CodePattern
                    {
                        Name = $"ARM_Function_{category}",
                        Type = PatternType.ARMTemplate,
                        Category = PatternCategory.InfrastructureAsCode,
                        Implementation = $"{category} function: {func.TrimEnd('(')}",
                        FilePath = filePath,
                        LineNumber = 1,
                        Content = func,
                        BestPractice = description,
                        AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/template-functions",
                        Context = context,
                        Confidence = 0.85f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["function_name"] = func.TrimEnd('('),
                            ["function_category"] = category,
                            ["is_positive_pattern"] = true
                        }
                    });
                    break; // Only report category once
                }
            }
        }

        return patterns;
    }

    #endregion

    #region Anti-Patterns

    private List<CodePattern> DetectHardcodedSecrets(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Check for hardcoded passwords, keys, connection strings
        var secretPatterns = new[]
        {
            new Regex(@"""password""\s*:\s*""[^""]{8,}""", RegexOptions.IgnoreCase),
            new Regex(@"""apiKey""\s*:\s*""[^""]{10,}""", RegexOptions.IgnoreCase),
            new Regex(@"""connectionString""\s*:\s*""[^""]{20,}""", RegexOptions.IgnoreCase)
        };

        foreach (var regex in secretPatterns)
        {
            if (regex.IsMatch(sourceCode))
            {
                patterns.Add(new CodePattern
                {
                    Name = "ARM_HardcodedSecret_AntiPattern",
                    Type = PatternType.ARMTemplate,
                    Category = PatternCategory.SecurityAntiPattern,
                    Implementation = "Hardcoded secret detected",
                    FilePath = filePath,
                    LineNumber = 1,
                    Content = "Hardcoded secret",
                    BestPractice = "ANTI-PATTERN: Never hardcode secrets. Use Azure Key Vault references or secure parameters",
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/best-practices#security",
                    Context = context,
                    Confidence = 0.70f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["is_positive_pattern"] = false,
                        ["severity"] = "Critical"
                    }
                });
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMissingParameterization(JsonElement root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check if template has resources but no parameters
        var hasResources = root.TryGetProperty("resources", out var resources) && resources.GetArrayLength() > 0;
        var hasParameters = root.TryGetProperty("parameters", out var parameters) && parameters.EnumerateObject().Any();

        if (hasResources && !hasParameters)
        {
            patterns.Add(new CodePattern
            {
                Name = "ARM_MissingParameterization_AntiPattern",
                Type = PatternType.ARMTemplate,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Template with resources but no parameters",
                FilePath = filePath,
                LineNumber = 1,
                Content = "No parameters",
                BestPractice = "ANTI-PATTERN: Templates without parameters are not reusable. Parameterize environment-specific values",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/best-practices#parameters",
                Context = context,
                Confidence = 0.75f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Medium"
                }
            });
        }

        return patterns;
    }

    #endregion
}

