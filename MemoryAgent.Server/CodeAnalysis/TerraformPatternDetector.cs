using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Terraform/HCL (HashiCorp Configuration Language) patterns for Infrastructure as Code (IaC)
/// Covers: Resources, Data Sources, Variables, Outputs, Modules, Providers, State Management, Security
/// </summary>
public class TerraformPatternDetector
{
    private readonly ILogger<TerraformPatternDetector>? _logger;

    public TerraformPatternDetector(ILogger<TerraformPatternDetector>? logger = null)
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
            // Core Terraform Constructs
            patterns.AddRange(DetectResourceBlocks(sourceCode, filePath, context));
            patterns.AddRange(DetectDataSources(sourceCode, filePath, context));
            patterns.AddRange(DetectVariables(sourceCode, filePath, context));
            patterns.AddRange(DetectOutputs(sourceCode, filePath, context));
            patterns.AddRange(DetectModules(sourceCode, filePath, context));
            patterns.AddRange(DetectProviders(sourceCode, filePath, context));
            patterns.AddRange(DetectLocals(sourceCode, filePath, context));
            patterns.AddRange(DetectTerraformBlock(sourceCode, filePath, context));
            
            // State Management Patterns
            patterns.AddRange(DetectRemoteBackend(sourceCode, filePath, context));
            patterns.AddRange(DetectStateLocking(sourceCode, filePath, context));
            patterns.AddRange(DetectWorkspaces(sourceCode, filePath, context));
            
            // Best Practice Patterns
            patterns.AddRange(DetectVersionPinning(sourceCode, filePath, context));
            patterns.AddRange(DetectResourceTagging(sourceCode, filePath, context));
            patterns.AddRange(DetectLifecycleRules(sourceCode, filePath, context));
            patterns.AddRange(DetectDynamicBlocks(sourceCode, filePath, context));
            patterns.AddRange(DetectForExpressions(sourceCode, filePath, context));
            patterns.AddRange(DetectConditionalExpressions(sourceCode, filePath, context));
            patterns.AddRange(DetectFunctions(sourceCode, filePath, context));
            
            // Security Patterns
            patterns.AddRange(DetectSensitiveVariables(sourceCode, filePath, context));
            patterns.AddRange(DetectEncryption(sourceCode, filePath, context));
            
            // Anti-Patterns (Security & Reliability Issues)
            patterns.AddRange(DetectHardcodedSecrets(sourceCode, filePath, context));
            patterns.AddRange(DetectMissingRemoteState(sourceCode, filePath, context));
            patterns.AddRange(DetectUnversionedProviders(sourceCode, filePath, context));
            patterns.AddRange(DetectMissingTags(sourceCode, filePath, context));

            _logger?.LogInformation("Detected {Count} Terraform patterns in {FilePath}", patterns.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting Terraform patterns in {FilePath}", filePath);
        }

        return await Task.FromResult(patterns);
    }

    #region Core Terraform Constructs

    private List<CodePattern> DetectResourceBlocks(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: resource "provider_type" "name" { ... }
        var regex = new Regex(@"resource\s+""([^""]+)""\s+""([^""]+)""\s*\{", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var resourceType = match.Groups[1].Value;
            var resourceName = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Resource",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Resource: {resourceType}.{resourceName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform resource definition: declarative infrastructure component",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/resources/syntax",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["resource_type"] = resourceType,
                    ["resource_name"] = resourceName,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectDataSources(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: data "provider_type" "name" { ... }
        var regex = new Regex(@"data\s+""([^""]+)""\s+""([^""]+)""\s*\{", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var dataType = match.Groups[1].Value;
            var dataName = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_DataSource",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Data Source: {dataType}.{dataName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform data source: query existing infrastructure or external data",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/data-sources",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["data_type"] = dataType,
                    ["data_name"] = dataName,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectVariables(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: variable "name" { ... }
        var regex = new Regex(@"variable\s+""([^""]+)""\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var varBody = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasType = varBody.Contains("type");
            var hasDescription = varBody.Contains("description");
            var hasDefault = varBody.Contains("default");
            var hasValidation = varBody.Contains("validation");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Variable",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Variable: {varName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform variable: parameterize configurations for reusability and flexibility",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/values/variables",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["variable_name"] = varName,
                    ["has_type"] = hasType,
                    ["has_description"] = hasDescription,
                    ["has_default"] = hasDefault,
                    ["has_validation"] = hasValidation,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectOutputs(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: output "name" { ... }
        var regex = new Regex(@"output\s+""([^""]+)""\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var outputName = match.Groups[1].Value;
            var outputBody = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasDescription = outputBody.Contains("description");
            var hasSensitive = outputBody.Contains("sensitive");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Output",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Output: {outputName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform output: export values for use by other configurations or users",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/values/outputs",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["output_name"] = outputName,
                    ["has_description"] = hasDescription,
                    ["is_sensitive"] = hasSensitive,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectModules(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: module "name" { source = "..." }
        var regex = new Regex(@"module\s+""([^""]+)""\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var moduleName = match.Groups[1].Value;
            var moduleBody = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var sourceMatch = Regex.Match(moduleBody, @"source\s*=\s*""([^""]+)""");
            var source = sourceMatch.Success ? sourceMatch.Groups[1].Value : "unknown";
            
            var versionMatch = Regex.Match(moduleBody, @"version\s*=\s*""([^""]+)""");
            var hasVersion = versionMatch.Success;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Module",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Module: {moduleName} (source: {source})",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform module: encapsulate and reuse infrastructure patterns",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/modules",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["module_name"] = moduleName,
                    ["module_source"] = source,
                    ["has_version_pinning"] = hasVersion,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectProviders(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: provider "name" { ... }
        var regex = new Regex(@"provider\s+""([^""]+)""\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var providerName = match.Groups[1].Value;
            var providerBody = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasAlias = providerBody.Contains("alias");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Provider",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Provider: {providerName}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform provider: configure cloud/service API interactions",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/providers/configuration",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["provider_name"] = providerName,
                    ["has_alias"] = hasAlias,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectLocals(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: locals { ... }
        var regex = new Regex(@"locals\s*\{", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_Locals",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Local values block",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Terraform locals: compute and reuse values within a module (DRY principle)",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/values/locals",
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

    private List<CodePattern> DetectTerraformBlock(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern: terraform { required_version = "..." required_providers { ... } }
        // Use better regex to handle nested braces
        var regex = new Regex(@"terraform\s*\{", RegexOptions.Singleline);
        var match = regex.Match(sourceCode);

        if (match.Success)
        {
            // Find matching closing brace by counting braces
            int startIndex = match.Index + match.Length;
            int braceCount = 1;
            int endIndex = startIndex;

            for (int i = startIndex; i < sourceCode.Length && braceCount > 0; i++)
            {
                if (sourceCode[i] == '{') braceCount++;
                else if (sourceCode[i] == '}') braceCount--;
                endIndex = i;
            }

            var tfBody = sourceCode.Substring(startIndex, endIndex - startIndex);
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasRequiredVersion = tfBody.Contains("required_version");
            var hasRequiredProviders = tfBody.Contains("required_providers");
            var hasBackend = tfBody.Contains("backend");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_ConfigBlock",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Terraform configuration block",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = sourceCode.Substring(match.Index, endIndex - match.Index + 1),
                BestPractice = "Terraform block: specify Terraform version, required providers, and backend configuration",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/settings",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["has_required_version"] = hasRequiredVersion,
                    ["has_required_providers"] = hasRequiredProviders,
                    ["has_backend"] = hasBackend,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    #endregion

    #region State Management Patterns

    private List<CodePattern> DetectRemoteBackend(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: backend "s3" { ... } or backend "azurerm" { ... }
        var regex = new Regex(@"backend\s+""([^""]+)""\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var backendType = match.Groups[1].Value;
            var backendBody = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasEncryption = backendBody.Contains("encrypt") || backendBody.Contains("encryption");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_RemoteBackend",
                Type = PatternType.Terraform,
                Category = PatternCategory.StateManagement,
                Implementation = $"Remote backend: {backendType}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Remote backend: store Terraform state in a shared, secure location (S3, Azure Storage, etc.)",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/settings/backends/configuration",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["backend_type"] = backendType,
                    ["has_encryption"] = hasEncryption,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectStateLocking(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: dynamodb_table (AWS) or lock = true (Azure)
        if (sourceCode.Contains("dynamodb_table") || (sourceCode.Contains("backend") && sourceCode.Contains("lock")))
        {
            var lineNumber = 1;
            
            patterns.Add(new CodePattern
            {
                Name = "Terraform_StateLocking",
                Type = PatternType.Terraform,
                Category = PatternCategory.StateManagement,
                Implementation = "State locking enabled",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = "State locking configuration detected",
                BestPractice = "State locking: prevent concurrent modifications to Terraform state",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/state/locking",
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

    private List<CodePattern> DetectWorkspaces(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: terraform.workspace reference
        if (sourceCode.Contains("terraform.workspace"))
        {
            var lineNumber = 1;
            
            patterns.Add(new CodePattern
            {
                Name = "Terraform_Workspaces",
                Type = PatternType.Terraform,
                Category = PatternCategory.StateManagement,
                Implementation = "Workspace reference detected",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = "terraform.workspace",
                BestPractice = "Workspaces: manage multiple environments (dev, staging, prod) with the same configuration",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/state/workspaces",
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

    #region Best Practice Patterns

    private List<CodePattern> DetectVersionPinning(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: version = "~> 1.0" or required_version = ">= 1.0"
        var regex = new Regex(@"(version|required_version)\s*=\s*""([^""]+)""", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var versionConstraint = match.Groups[2].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_VersionPinning",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Version constraint: {versionConstraint}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Version pinning: lock provider/module versions to ensure consistent, reproducible deployments",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/expressions/version-constraints",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["version_constraint"] = versionConstraint,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectResourceTagging(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: tags = { ... }
        var regex = new Regex(@"tags\s*=\s*\{", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_ResourceTagging",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Resource tagging",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Resource tagging: organize, track, and manage cloud resources (environment, cost center, etc.)",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/tutorials/aws-get-started/aws-variables",
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

    private List<CodePattern> DetectLifecycleRules(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: lifecycle { prevent_destroy = true / create_before_destroy = true / ignore_changes = [...] }
        var regex = new Regex(@"lifecycle\s*\{([^\}]*)\}", RegexOptions.Singleline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lifecycleBody = match.Groups[1].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            var hasPreventDestroy = lifecycleBody.Contains("prevent_destroy");
            var hasCreateBeforeDestroy = lifecycleBody.Contains("create_before_destroy");
            var hasIgnoreChanges = lifecycleBody.Contains("ignore_changes");

            patterns.Add(new CodePattern
            {
                Name = "Terraform_LifecycleRules",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Lifecycle meta-arguments",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Lifecycle rules: control resource creation, updates, and destruction behavior",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/meta-arguments/lifecycle",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["has_prevent_destroy"] = hasPreventDestroy,
                    ["has_create_before_destroy"] = hasCreateBeforeDestroy,
                    ["has_ignore_changes"] = hasIgnoreChanges,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectDynamicBlocks(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: dynamic "block_type" { for_each = ... content { ... } }
        var regex = new Regex(@"dynamic\s+""([^""]+)""\s*\{", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var blockType = match.Groups[1].Value;
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_DynamicBlock",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Dynamic block: {blockType}",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Dynamic blocks: generate nested blocks programmatically using for_each",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/expressions/dynamic-blocks",
                Context = context,
                Confidence = 0.95f,
                Metadata = new Dictionary<string, object>
                {
                    ["block_type"] = blockType,
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectForExpressions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: [for x in list : transform(x)] or { for k, v in map : k => v }
        var regex = new Regex(@"\[?\s*for\s+\w+\s+(in|,)", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_ForExpression",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "For expression",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "For expressions: transform collections (lists, maps) programmatically",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/expressions/for",
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

    private List<CodePattern> DetectConditionalExpressions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: condition ? true_val : false_val
        var regex = new Regex(@"\?\s*[^:]+\s*:\s*", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_ConditionalExpression",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = "Conditional expression",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Conditional expressions: select values based on conditions (ternary operator)",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/expressions/conditionals",
                Context = context,
                Confidence = 0.80f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = true
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectFunctions(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: Built-in functions (file, templatefile, jsonencode, yamldecode, etc.)
        var functionPatterns = new[]
        {
            "file(", "templatefile(", "jsonencode(", "jsondecode(", "yamlencode(", "yamldecode(",
            "base64encode(", "base64decode(", "md5(", "sha256(", "cidrsubnet(", "lookup(", "merge("
        };

        foreach (var func in functionPatterns)
        {
            if (sourceCode.Contains(func))
            {
                var lineNumber = 1;
                
                patterns.Add(new CodePattern
                {
                    Name = "Terraform_Function",
                    Type = PatternType.Terraform,
                    Category = PatternCategory.InfrastructureAsCode,
                    Implementation = $"Built-in function: {func.TrimEnd('(')}",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Content = func,
                    BestPractice = "Built-in functions: transform and manipulate data (file reading, encoding, hashing, etc.)",
                    AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/functions",
                    Context = context,
                    Confidence = 0.85f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["function_name"] = func.TrimEnd('('),
                        ["is_positive_pattern"] = true
                    }
                });
            }
        }

        return patterns;
    }

    #endregion

    #region Security Patterns

    private List<CodePattern> DetectSensitiveVariables(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: sensitive = true in variables or outputs
        var regex = new Regex(@"sensitive\s*=\s*true", RegexOptions.Multiline);
        var matches = regex.Matches(sourceCode);

        foreach (Match match in matches)
        {
            var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

            patterns.Add(new CodePattern
            {
                Name = "Terraform_SensitiveVariable",
                Type = PatternType.Terraform,
                Category = PatternCategory.Security,
                Implementation = "Sensitive variable/output",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = match.Value,
                BestPractice = "Sensitive variables: prevent secrets from being displayed in console output or logs",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/values/variables#suppressing-values-in-cli-output",
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

    private List<CodePattern> DetectEncryption(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: encryption settings (encrypt = true, encryption_configuration, kms_key_id, etc.)
        var encryptionKeywords = new[] { "encrypt", "encryption", "kms_key_id", "kms_master_key_id", "sse_", "server_side_encryption" };

        foreach (var keyword in encryptionKeywords)
        {
            if (sourceCode.Contains(keyword))
            {
                var lineNumber = 1;
                
                patterns.Add(new CodePattern
                {
                    Name = "Terraform_Encryption",
                    Type = PatternType.Terraform,
                    Category = PatternCategory.Security,
                    Implementation = $"Encryption configuration: {keyword}",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Content = keyword,
                    BestPractice = "Encryption at rest: protect sensitive data in cloud storage (S3, Azure Storage, etc.)",
                    AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/tutorials/security",
                    Context = context,
                    Confidence = 0.85f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["encryption_type"] = keyword,
                        ["is_positive_pattern"] = true
                    }
                });
                break; // Only report once per file
            }
        }

        return patterns;
    }

    #endregion

    #region Anti-Patterns

    private List<CodePattern> DetectHardcodedSecrets(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: Hardcoded secrets (password, api_key, secret_key, access_key)
        var secretPatterns = new[]
        {
            new Regex(@"password\s*=\s*""[^""]{8,}""", RegexOptions.IgnoreCase),
            new Regex(@"api_key\s*=\s*""[^""]{10,}""", RegexOptions.IgnoreCase),
            new Regex(@"secret_key\s*=\s*""[^""]{10,}""", RegexOptions.IgnoreCase),
            new Regex(@"access_key\s*=\s*""[A-Z0-9]{16,}""", RegexOptions.None)
        };

        foreach (var regex in secretPatterns)
        {
            var matches = regex.Matches(sourceCode);
            foreach (Match match in matches)
            {
                var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

                patterns.Add(new CodePattern
                {
                    Name = "Terraform_HardcodedSecret_AntiPattern",
                    Type = PatternType.Terraform,
                    Category = PatternCategory.SecurityAntiPattern,
                    Implementation = "Hardcoded secret detected",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Content = match.Value,
                    BestPractice = "ANTI-PATTERN: Hardcoded secrets expose credentials. Use variables, environment variables, or secrets management tools (AWS Secrets Manager, Azure Key Vault)",
                    AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/tutorials/security/secrets",
                    Context = context,
                    Confidence = 0.75f,
                    Metadata = new Dictionary<string, object>
                    {
                        ["is_positive_pattern"] = false,
                        ["severity"] = "Critical"
                    }
                });
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMissingRemoteState(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Check if file has terraform block but NO backend
        if (sourceCode.Contains("terraform {") && !sourceCode.Contains("backend"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Terraform_MissingRemoteState_AntiPattern",
                Type = PatternType.Terraform,
                Category = PatternCategory.StateManagement,
                Implementation = "No remote backend configured",
                FilePath = filePath,
                LineNumber = 1,
                Content = "terraform { } without backend",
                BestPractice = "ANTI-PATTERN: Local state is not recommended for production. Use remote backends (S3, Azure Storage) for team collaboration and state locking",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/settings/backends/configuration",
                Context = context,
                Confidence = 0.70f,
                Metadata = new Dictionary<string, object>
                {
                    ["is_positive_pattern"] = false,
                    ["severity"] = "High"
                }
            });
        }

        return patterns;
    }

    private List<CodePattern> DetectUnversionedProviders(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Pattern: provider without version constraint
        // Find required_providers block with brace counting
        var providerRegex = new Regex(@"required_providers\s*\{", RegexOptions.Singleline);
        var match = providerRegex.Match(sourceCode);

        if (match.Success)
        {
            // Find matching closing brace by counting braces
            int startIndex = match.Index + match.Length;
            int braceCount = 1;
            int endIndex = startIndex;

            for (int i = startIndex; i < sourceCode.Length && braceCount > 0; i++)
            {
                if (sourceCode[i] == '{') braceCount++;
                else if (sourceCode[i] == '}') braceCount--;
                endIndex = i;
            }

            var providersBody = sourceCode.Substring(startIndex, endIndex - startIndex);
            
            // Check if any provider is missing version
            var providerNames = new Regex(@"(\w+)\s*=\s*\{").Matches(providersBody);
            foreach (Match providerMatch in providerNames)
            {
                var providerName = providerMatch.Groups[1].Value;
                var providerBlockStart = providerMatch.Index;
                
                // Find the closing brace for this provider block
                int provBraceCount = 1;
                int provEndIndex = providerBlockStart + providerMatch.Length;
                for (int i = providerBlockStart + providerMatch.Length; i < providersBody.Length && provBraceCount > 0; i++)
                {
                    if (providersBody[i] == '{') provBraceCount++;
                    else if (providersBody[i] == '}') provBraceCount--;
                    provEndIndex = i;
                }
                
                var providerBlock = providersBody.Substring(providerBlockStart, provEndIndex - providerBlockStart + 1);

                if (!providerBlock.Contains("version"))
                {
                    var lineNumber = sourceCode.Substring(0, match.Index + providerBlockStart).Count(c => c == '\n') + 1;

                    patterns.Add(new CodePattern
                    {
                        Name = "Terraform_UnversionedProvider_AntiPattern",
                        Type = PatternType.Terraform,
                        Category = PatternCategory.InfrastructureAsCode,
                        Implementation = $"Provider without version: {providerName}",
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Content = providerBlock,
                        BestPractice = "ANTI-PATTERN: Unversioned providers can cause unexpected breaking changes. Always pin provider versions",
                        AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/language/providers/requirements",
                        Context = context,
                        Confidence = 0.85f,
                        Metadata = new Dictionary<string, object>
                        {
                            ["provider_name"] = providerName,
                            ["is_positive_pattern"] = false,
                            ["severity"] = "Medium"
                        }
                    });
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMissingTags(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        
        // Check if resources exist but very few tags
        var resourceCount = Regex.Matches(sourceCode, @"resource\s+""").Count;
        var tagsCount = Regex.Matches(sourceCode, @"tags\s*=\s*\{").Count;

        if (resourceCount > 0 && tagsCount == 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Terraform_MissingTags_AntiPattern",
                Type = PatternType.Terraform,
                Category = PatternCategory.InfrastructureAsCode,
                Implementation = $"Resources without tags: {resourceCount} resources, 0 tags",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Resources without tags",
                BestPractice = "ANTI-PATTERN: Missing tags make resource management difficult. Tag all resources for organization, cost tracking, and compliance",
                AzureBestPracticeUrl = "https://developer.hashicorp.com/terraform/tutorials/aws-get-started/aws-variables",
                Context = context,
                Confidence = 0.70f,
                Metadata = new Dictionary<string, object>
                {
                    ["resource_count"] = resourceCount,
                    ["tags_count"] = tagsCount,
                    ["is_positive_pattern"] = false,
                    ["severity"] = "Low"
                }
            });
        }

        return patterns;
    }

    #endregion
}

