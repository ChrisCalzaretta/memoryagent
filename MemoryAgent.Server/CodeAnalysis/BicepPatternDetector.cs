using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Azure Bicep patterns for Infrastructure as Code (IaC)
/// Covers: Resources, Modules, Parameters, Variables, Outputs, Decorators, Functions, Best Practices
/// Reference: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/
/// </summary>
public class BicepPatternDetector
{
    private readonly ILogger<BicepPatternDetector>? _logger;

    public BicepPatternDetector(ILogger<BicepPatternDetector>? logger = null)
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
            // Core Bicep Constructs
            patterns.AddRange(DetectResources(sourceCode, filePath, context));
            patterns.AddRange(DetectModules(sourceCode, filePath, context));
            patterns.AddRange(DetectParameters(sourceCode, filePath, context));
            patterns.AddRange(DetectVariables(sourceCode, filePath, context));
            patterns.AddRange(DetectOutputs(sourceCode, filePath, context));
            patterns.AddRange(DetectExistingResources(sourceCode, filePath, context));
            patterns.AddRange(DetectTargetScope(sourceCode, filePath, context));
            
            // Decorators (Best Practices)
            patterns.AddRange(DetectDecorators(sourceCode, filePath, context));
            
            // Control Flow
            patterns.AddRange(DetectConditionals(sourceCode, filePath, context));
            patterns.AddRange(DetectLoops(sourceCode, filePath, context));
            
            // Functions
            patterns.AddRange(DetectBicepFunctions(sourceCode, filePath, context));
            
            // Best Practices
            patterns.AddRange(DetectUniqueString(sourceCode, filePath, context));
            patterns.AddRange(DetectDependsOn(sourceCode, filePath, context));
            patterns.AddRange(DetectLocationParameter(sourceCode, filePath, context));
            
            // Anti-Patterns
            patterns.AddRange(DetectHardcodedValues(sourceCode, filePath, context));
            patterns.AddRange(DetectMissingValidation(sourceCode, filePath, context));
            patterns.AddRange(DetectOldApiVersions(sourceCode, filePath, context));

            _logger?.LogInformation("Detected {Count} Bicep patterns in {FilePath}", patterns.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting Bicep patterns in {FilePath}", filePath);
        }

        return await Task.FromResult(patterns);
    }

    #region Core Bicep Constructs

    private List<CodePattern> DetectResources(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: resource <name> '<type>@<apiVersion>' = { ... }
        var regex = new Regex(@"resource\s+(\w+)\s+'([^']+)@(\d{4}-\d{2}-\d{2}[^']*)'", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var resourceName = match.Groups[1].Value;
            var resourceType = match.Groups[2].Value;
            var apiVersion = match.Groups[3].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Bicep_Resource",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Resource: {resourceName} ({resourceType})",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Bicep resource: declarative Azure resource definition with typed syntax",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/resource-declaration",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["resource_name"] = resourceName,
                    ["resource_type"] = resourceType,
                    ["api_version"] = apiVersion,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectModules(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: module <name> '<path>' = { ... }
        var regex = new Regex(@"module\s+(\w+)\s+'([^']+)'", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var moduleName = match.Groups[1].Value;
            var modulePath = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var isRemote = modulePath.StartsWith("br:") || modulePath.StartsWith("ts:");

            patterns.Add(new CodePattern
            {
                Name = "Bicep_Module",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Module: {moduleName} ({(isRemote ? "remote" : "local")})",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Bicep modules: encapsulate reusable infrastructure patterns for composition and DRY principle",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/modules",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["module_name"] = moduleName,
                    ["module_path"] = modulePath,
                    ["is_remote_module"] = isRemote,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectParameters(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: param <name> <type> = <defaultValue>
        // Also: @decorators\nparam <name> <type>
        var regex = new Regex(@"param\s+(\w+)\s+(string|int|bool|array|object|\w+)", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            var paramType = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            // Check for decorators before this parameter
            var hasDescription = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@description");
            var hasSecure = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@secure");
            var hasAllowed = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@allowed");
            var hasMinLength = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@minLength");
            var hasMaxLength = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@maxLength");
            var hasMinValue = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@minValue");
            var hasMaxValue = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@maxValue");

            patterns.Add(new CodePattern
            {
                Name = "Bicep_Parameter",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Parameter: {paramName} ({paramType})",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Bicep parameters: strongly-typed input values with validation decorators",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameters",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["parameter_name"] = paramName,
                    ["parameter_type"] = paramType,
                    ["has_description"] = hasDescription,
                    ["has_secure"] = hasSecure,
                    ["has_allowed"] = hasAllowed,
                    ["has_min_length"] = hasMinLength,
                    ["has_max_length"] = hasMaxLength,
                    ["has_min_value"] = hasMinValue,
                    ["has_max_value"] = hasMaxValue,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectVariables(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: var <name> = <value>
        var regex = new Regex(@"var\s+(\w+)\s*=", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Bicep_Variable",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Variable: {varName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Bicep variables: computed values for reuse and DRY principle",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/variables",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["variable_name"] = varName,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectOutputs(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: output <name> <type> = <value>
        var regex = new Regex(@"output\s+(\w+)\s+(string|int|bool|array|object|\w+)", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var outputName = match.Groups[1].Value;
            var outputType = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasDescription = CheckDecoratorBeforeLine(sourceCode, lineNumber, "@description");

            patterns.Add(new CodePattern
            {
                Name = "Bicep_Output",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Output: {outputName} ({outputType})",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Bicep outputs: export values for use by other deployments or applications",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/outputs",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["output_name"] = outputName,
                    ["output_type"] = outputType,
                    ["has_description"] = hasDescription,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectExistingResources(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: resource <name> '<type>@<apiVersion>' existing = { ... }
        var regex = new Regex(@"resource\s+(\w+)\s+'[^']+'\s+existing", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var resourceName = match.Groups[1].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Bicep_ExistingResource",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Existing resource reference: {resourceName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Existing resources: reference existing Azure resources without redeploying them",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/existing-resource",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["resource_name"] = resourceName,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectTargetScope(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: targetScope = 'subscription' | 'managementGroup' | 'tenant' | 'resourceGroup'
        var regex = new Regex(@"targetScope\s*=\s*'(\w+)'", RegexOptions.Multiline);
        var match = regex.Match(sourceCode);

        if (match.Success)
        {
            var scope = match.Groups[1].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Bicep_TargetScope",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Target scope: {scope}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Target scope: define deployment level (resourceGroup, subscription, managementGroup, tenant)",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-to-subscription",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["scope"] = scope,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Decorators (Validation & Documentation)

    private List<CodePattern> DetectDecorators(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        var decorators = new Dictionary<string, string>
        {
            ["@description"] = "Documentation decorator: describes parameters, outputs, and resources",
            ["@secure"] = "Security decorator: marks parameters/outputs as sensitive to prevent logging",
            ["@allowed"] = "Validation decorator: restricts parameter values to allowed list",
            ["@minLength"] = "Validation decorator: enforces minimum string/array length",
            ["@maxLength"] = "Validation decorator: enforces maximum string/array length",
            ["@minValue"] = "Validation decorator: enforces minimum numeric value",
            ["@maxValue"] = "Validation decorator: enforces maximum numeric value",
            ["@metadata"] = "Metadata decorator: attach additional information to parameters/resources"
        };

        foreach (var (decorator, description) in decorators)
        {
            if (sourceCode.Contains(decorator))
            {
                var count = Regex.Matches(sourceCode, Regex.Escape(decorator)).Count;
                var lineNumber = sourceCode.IndexOf(decorator);
                lineNumber = sourceCode.Substring(0, lineNumber).Count(c => c == '\n') + 1;

                patterns.Add(new CodePattern
                {
                    Name = $"Bicep_Decorator_{decorator.TrimStart('@')}",
                    Type = PatternType.Bicep,
                    Category = PatternCategory.InfrastructureAsCode,
                    Implementation = $"{decorator} decorator (used {count} times)",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Content = decorator,
                    BestPractice = description,
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameters",
                    Context = context,
                    Confidence = 0.90f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["decorator"] = decorator,
                        ["usage_count"] = count,
                        ["is_positive_pattern"] = true
                    }
                });
            }
        }

        return patterns;
    }

    #endregion

    #region Control Flow

    private List<CodePattern> DetectConditionals(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: resource ... if <condition>
        var regex = new Regex(@"resource\s+\w+[^=]+=\s*\{\s*\n[^\}]*\}\s*if\s+", RegexOptions.Multiline | RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        if (matches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_ConditionalDeployment",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Conditional deployment (if statement)",
                FilePath = filePath,
                LineNumber = 1,
                Content = "if condition",
                BestPractice = "Conditional deployment: deploy resources only when conditions are met",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/conditional-resource-deployment",
                Context = context,
                Confidence = 0.85f,
                Metadata = new Dictionary<string, object>
                {
                    ["conditional_count"] = matches.Count,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectLoops(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: [for <item> in <collection>: { ... }]
        var regex = new Regex(@"\[for\s+\w+\s+in\s+", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        if (matches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_Loop",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "For loop iteration",
                FilePath = filePath,
                LineNumber = 1,
                Content = "[for item in collection: {...}]",
                BestPractice = "Bicep loops: iterate over collections to create multiple resources dynamically",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/loops",
                Context = context,
                Confidence = 0.90f,
                Metadata = new Dictionary<string, object>
                {
                    ["loop_count"] = matches.Count,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Functions

    private List<CodePattern> DetectBicepFunctions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        var functionCategories = new Dictionary<string, (string[] functions, string description)>
        {
            ["Resource"] = (new[] { "resourceId(", "subscription(", "resourceGroup(", "tenant(", "managementGroup(" }, 
                "Resource functions: get resource IDs and deployment scope information"),
            ["String"] = (new[] { "base64(", "concat(", "format(", "guid(", "uniqueString(", "toLower(", "toUpper(", "substring(" }, 
                "String functions: manipulate and generate strings"),
            ["Array"] = (new[] { "concat(", "contains(", "empty(", "first(", "last(", "length(", "union(", "intersection(" }, 
                "Array functions: work with arrays and collections"),
            ["Deployment"] = (new[] { "deployment(", "environment(", "utcNow(" }, 
                "Deployment functions: access deployment metadata and environment"),
            ["Logical"] = (new[] { "bool(", "if(" }, 
                "Logical functions: boolean operations and conditionals"),
            ["Numeric"] = (new[] { "int(", "min(", "max(", "range(" }, 
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
                        Name = $"Bicep_Function_{category}",
                        Type = PatternType.Bicep,
                        Category = PatternCategory.InfrastructureAsCode,
                        Implementation = $"{category} function: {func.TrimEnd('(')}",
                        FilePath = filePath,
                        LineNumber = 1,
                        Content = func,
                        BestPractice = description,
                        AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-functions",
                        Context = context,
                        Confidence = 0.85f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["function_name"] = func.TrimEnd('('),
                            ["function_category"] = category,
                            ["is_positive_pattern"] = true
                        }
                    });
                    break; // Only report each category once
                }
            }
        }

        return patterns;
    }

    #endregion

    #region Best Practices

    private List<CodePattern> DetectUniqueString(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        if (sourceCode.Contains("uniqueString("))
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_UniqueString",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "uniqueString() for deterministic unique names",
                FilePath = filePath,
                LineNumber = 1,
                Content = "uniqueString()",
                BestPractice = "uniqueString(): generate deterministic unique names for globally-scoped resources",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/best-practices",
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

    private List<CodePattern> DetectDependsOn(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        if (sourceCode.Contains("dependsOn:"))
        {
            var count = Regex.Matches(sourceCode, "dependsOn:").Count;
            
            patterns.Add(new CodePattern
            {
                Name = "Bicep_DependsOn",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Explicit dependencies (dependsOn) - {count} occurrences",
                FilePath = filePath,
                LineNumber = 1,
                Content = "dependsOn:",
                BestPractice = "dependsOn: explicitly define resource dependencies when Bicep can't infer them automatically",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/resource-dependencies",
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

    private List<CodePattern> DetectLocationParameter(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Check for location parameterization (not hardcoded)
        if (sourceCode.Contains("param location") || sourceCode.Contains("resourceGroup().location"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_LocationParameter",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Parameterized location",
                FilePath = filePath,
                LineNumber = 1,
                Content = "param location / resourceGroup().location",
                BestPractice = "Location parameterization: use resourceGroup().location or parameter for flexibility across regions",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/best-practices",
                Context = context,
                Confidence = 0.85f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Anti-Patterns

    private List<CodePattern> DetectHardcodedValues(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Check for hardcoded location values
        var locationRegex = new Regex(@"location:\s*'(eastus|westus|northeurope|westeurope|southeastasia)'", RegexOptions.IgnoreCase);
        var locationMatches = locationRegex.Matches(sourceCode);

        if (locationMatches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_HardcodedLocation_AntiPattern",
                Type = PatternType.Bicep,
                Category = PatternCategory.SecurityAntiPattern,
                Implementation = "Hardcoded location value",
                FilePath = filePath,
                LineNumber = 1,
                Content = locationMatches[0].Value,
                BestPractice = "ANTI-PATTERN: Hardcoded locations reduce flexibility. Use resourceGroup().location or location parameter",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/best-practices",
                Context = context,
                Confidence = 0.75f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Low"
                }
            });
        }

        // Check for hardcoded resource names (should use parameters or uniqueString)
        var nameRegex = new Regex(@"name:\s*'[a-z0-9\-]{10,}'", RegexOptions.IgnoreCase);
        var nameMatches = nameRegex.Matches(sourceCode);

        if (nameMatches.Count > 3) // More than 3 suggests hardcoding pattern
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_HardcodedNames_AntiPattern",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Multiple hardcoded resource names",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Hardcoded names",
                BestPractice = "ANTI-PATTERN: Hardcoded names reduce reusability. Use parameters with uniqueString() for unique names",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/best-practices",
                Context = context,
                Confidence = 0.70f,
                Metadata = new Dictionary<string, object>
                {
                    ["hardcoded_count"] = nameMatches.Count,
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Medium"
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectMissingValidation(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Count parameters vs validation decorators
        var paramCount = Regex.Matches(sourceCode, @"param\s+\w+").Count;
        var validationCount = Regex.Matches(sourceCode, @"@(allowed|minLength|maxLength|minValue|maxValue)").Count;

        if (paramCount > 3 && validationCount == 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_MissingValidation_AntiPattern",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"{paramCount} parameters without validation",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Parameters without validation decorators",
                BestPractice = "ANTI-PATTERN: Parameters without validation can accept invalid values. Use @allowed, @minLength, @maxLength decorators",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/parameters",
                Context = context,
                Confidence = 0.70f,
                Metadata = new Dictionary<string, object>
                {
                    ["parameter_count"] = paramCount,
                    ["validation_count"] = validationCount,
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Medium"
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectOldApiVersions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Check for API versions older than 2020
        var regex = new Regex(@"@(201\d-\d{2}-\d{2})", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        if (matches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Bicep_OldApiVersion_AntiPattern",
                Type = PatternType.Bicep,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Old API versions detected ({matches.Count} resources)",
                FilePath = filePath,
                LineNumber = 1,
                Content = matches[0].Value,
                BestPractice = "ANTI-PATTERN: Old API versions may be deprecated. Use latest stable API versions for new features and security fixes",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/best-practices",
                Context = context,
                Confidence = 0.75f,
                Metadata = new Dictionary<string, object>
                {
                    ["old_api_count"] = matches.Count,
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Low"
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Helpers

    private bool CheckDecoratorBeforeLine(string sourceCode, int lineNumber, string decorator)
    {
        var lines = sourceCode.Split('\n');
        if (lineNumber > 1 && lineNumber <= lines.Length)
        {
            // Check up to 5 lines before for decorator
            for (int i = Math.Max(0, lineNumber - 6); i < lineNumber - 1; i++)
            {
                if (i < lines.Length && lines[i].Contains(decorator))
                {
                    return true;
                }
            }
        }
        return false;
    }

    #endregion
}

