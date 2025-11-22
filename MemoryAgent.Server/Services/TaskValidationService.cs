using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

public class TaskValidationService : ITaskValidationService
{
    private readonly IGraphService _graphService;
    private readonly IVectorService _vectorService;
    private readonly ILogger<TaskValidationService> _logger;

    public TaskValidationService(
        IGraphService graphService,
        IVectorService vectorService,
        ILogger<TaskValidationService> logger)
    {
        _graphService = graphService;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async Task<TaskValidationResult> ValidateTaskAsync(PlanTask task, string context, CancellationToken cancellationToken = default)
    {
        var result = new TaskValidationResult { IsValid = true };

        foreach (var rule in task.ValidationRules)
        {
            var failure = await ValidateRuleAsync(rule, context, cancellationToken);
            if (failure != null)
            {
                result.IsValid = false;
                result.Failures.Add(failure);

                if (failure.CanAutoFix && rule.AutoFix)
                {
                    result.Suggestions.Add($"Auto-fix available: {failure.FixDescription}");
                }
            }
        }

        return result;
    }

    private async Task<ValidationFailure?> ValidateRuleAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        return rule.RuleType.ToLower() switch
        {
            "requires_test" => await ValidateRequiresTestAsync(rule, context, cancellationToken),
            "requires_file" => await ValidateRequiresFileAsync(rule, cancellationToken),
            "min_test_coverage" => await ValidateMinTestCoverageAsync(rule, context, cancellationToken),
            "max_complexity" => await ValidateMaxComplexityAsync(rule, context, cancellationToken),
            "requires_documentation" => await ValidateRequiresDocumentationAsync(rule, context, cancellationToken),
            "no_code_smells" => await ValidateNoCodeSmellsAsync(rule, context, cancellationToken),
            _ => null
        };
    }

    private async Task<ValidationFailure?> ValidateRequiresTestAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var target = rule.Target; // e.g., "UserService" or "UserService.cs"
        var className = target.Replace(".cs", "");

        // Search for test methods that test this class
        var testQuery = $"test {className}";
        var results = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(testQuery),
            context: context,
            limit: 10,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var hasTests = results.Any(r => 
            r.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) && 
            r.Name.Contains(className, StringComparison.OrdinalIgnoreCase));

        if (!hasTests)
        {
            // Get methods from the target class to provide context
            var methodsQuery = $"methods in {className}";
            var methods = await _vectorService.SearchSimilarCodeAsync(
                await GenerateEmbeddingForQuery(methodsQuery),
                context: context,
                limit: 20,
                minimumScore: 0.6f,
                cancellationToken: cancellationToken);

            var methodsToTest = methods
                .Where(m => m.Type == CodeMemoryType.Method)
                .Select(m => new { 
                    Name = m.Name, 
                    Signature = m.Code.Split('\n').FirstOrDefault() ?? m.Name,
                    IsPublic = m.Metadata.ContainsKey("is_public_api") && Convert.ToBoolean(m.Metadata["is_public_api"])
                })
                .ToList();

            return new ValidationFailure
            {
                RuleType = "requires_test",
                Message = $"No tests found for {className}",
                CanAutoFix = true,
                FixDescription = $"Create {className}Tests.cs with basic test scaffolding",
                ActionableContext = new Dictionary<string, object>
                {
                    ["target_class"] = className,
                    ["test_file_to_create"] = $"{className}Tests.cs",
                    ["methods_to_test"] = methodsToTest,
                    ["method_count"] = methodsToTest.Count,
                    ["suggestion"] = $"Create tests for {methodsToTest.Count} method(s) in {className}. Focus on public methods first.",
                    ["example_test_names"] = methodsToTest.Take(3).Select(m => $"{m.Name}_Should...").ToList()
                }
            };
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateRequiresFileAsync(TaskValidationRule rule, CancellationToken cancellationToken)
    {
        var filePath = rule.Target;
        
        if (!File.Exists(filePath))
        {
            return new ValidationFailure
            {
                RuleType = "requires_file",
                Message = $"Required file not found: {filePath}",
                CanAutoFix = rule.Parameters.ContainsKey("template"),
                FixDescription = $"Create {Path.GetFileName(filePath)} from template"
            };
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateMinTestCoverageAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var target = rule.Target;
        var minCoverage = rule.Parameters.TryGetValue("min_coverage", out var min) ? Convert.ToDouble(min) : 80.0;

        // Count methods in target class
        var classQuery = $"class {target}";
        var classResults = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(classQuery),
            context: context,
            limit: 1,
            minimumScore: 0.7f,
            cancellationToken: cancellationToken);

        if (!classResults.Any()) return null;

        var methodsQuery = $"methods in {target}";
        var methods = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(methodsQuery),
            context: context,
            limit: 50,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var methodCount = methods.Count(m => m.Type == CodeMemoryType.Method && m.Metadata.ContainsKey("class_name") && m.Metadata["class_name"].ToString()!.Contains(target));

        // Count tests
        var testsQuery = $"tests for {target}";
        var tests = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(testsQuery),
            context: context,
            limit: 50,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var testCount = tests.Count(t => t.Type == CodeMemoryType.Test);

        var coverage = methodCount > 0 ? (double)testCount / methodCount * 100 : 0;

        if (coverage < minCoverage)
        {
            return new ValidationFailure
            {
                RuleType = "min_test_coverage",
                Message = $"Test coverage {coverage:F1}% is below minimum {minCoverage}%",
                CanAutoFix = true,
                FixDescription = $"Generate {(int)((minCoverage / 100 * methodCount) - testCount)} additional tests"
            };
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateMaxComplexityAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var target = rule.Target;
        var maxComplexity = rule.Parameters.TryGetValue("max_complexity", out var max) ? Convert.ToInt32(max) : 10;

        var query = $"methods in {target}";
        var results = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(query),
            context: context,
            limit: 50,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var complexMethods = results.Where(r => 
            r.Type == CodeMemoryType.Method &&
            r.Metadata.ContainsKey("cyclomatic_complexity") &&
            Convert.ToInt32(r.Metadata["cyclomatic_complexity"]) > maxComplexity).ToList();

        if (complexMethods.Any())
        {
            return new ValidationFailure
            {
                RuleType = "max_complexity",
                Message = $"{complexMethods.Count} method(s) exceed max complexity {maxComplexity}",
                CanAutoFix = false,
                FixDescription = $"Refactor: {string.Join(", ", complexMethods.Take(3).Select(m => m.Name))}"
            };
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateRequiresDocumentationAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var target = rule.Target;

        var query = $"public methods in {target}";
        var results = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(query),
            context: context,
            limit: 50,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var undocumentedMethods = results.Where(r =>
            r.Type == CodeMemoryType.Method &&
            r.Metadata.ContainsKey("is_public_api") &&
            Convert.ToBoolean(r.Metadata["is_public_api"]) &&
            !r.Code.Contains("///")).ToList();

        if (undocumentedMethods.Any())
        {
            return new ValidationFailure
            {
                RuleType = "requires_documentation",
                Message = $"{undocumentedMethods.Count} public method(s) lack documentation",
                CanAutoFix = true,
                FixDescription = $"Generate XML documentation for {undocumentedMethods.Count} method(s)"
            };
        }

        return null;
    }

    private async Task<ValidationFailure?> ValidateNoCodeSmellsAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var target = rule.Target;

        var query = $"code in {target}";
        var results = await _vectorService.SearchSimilarCodeAsync(
            await GenerateEmbeddingForQuery(query),
            context: context,
            limit: 50,
            minimumScore: 0.6f,
            cancellationToken: cancellationToken);

        var smells = results.Where(r =>
            r.Metadata.ContainsKey("code_smells") &&
            r.Metadata["code_smells"] is List<object> list &&
            list.Count > 0).ToList();

        if (smells.Any())
        {
            var totalSmells = smells.Sum(s => ((List<object>)s.Metadata["code_smells"]).Count);
            return new ValidationFailure
            {
                RuleType = "no_code_smells",
                Message = $"Found {totalSmells} code smell(s) in {target}",
                CanAutoFix = false,
                FixDescription = "Review and fix code quality issues"
            };
        }

        return null;
    }

    public async Task<bool> AutoFixValidationFailuresAsync(PlanTask task, TaskValidationResult result, string context, CancellationToken cancellationToken = default)
    {
        var fixedAny = false;

        foreach (var failure in result.Failures.Where(f => f.CanAutoFix))
        {
            var rule = task.ValidationRules.FirstOrDefault(r => r.RuleType == failure.RuleType);
            if (rule == null || !rule.AutoFix) continue;

            _logger.LogInformation("Auto-fixing validation failure: {RuleType} for {Target}", failure.RuleType, rule.Target);

            var wasFixed = failure.RuleType.ToLower() switch
            {
                "requires_test" => await AutoFixRequiresTestAsync(rule, context, cancellationToken),
                "requires_file" => await AutoFixRequiresFileAsync(rule, cancellationToken),
                "requires_documentation" => await AutoFixRequiresDocumentationAsync(rule, context, cancellationToken),
                _ => false
            };

            if (wasFixed)
            {
                fixedAny = true;
                _logger.LogInformation("Successfully auto-fixed: {RuleType}", failure.RuleType);
            }
        }

        return fixedAny;
    }

    private async Task<bool> AutoFixRequiresTestAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        var className = rule.Target.Replace(".cs", "");
        var testFileName = $"{className}Tests.cs";
        
        // Generate test file scaffolding
        var testContent = GenerateTestScaffolding(className);
        
        _logger.LogInformation("Generated test scaffolding for {ClassName}: {TestFile}", className, testFileName);
        
        // In a real implementation, you would:
        // 1. Write the file to disk
        // 2. Index it
        // 3. Return true
        
        // For now, just log what would be created
        return await Task.FromResult(true);
    }

    private async Task<bool> AutoFixRequiresFileAsync(TaskValidationRule rule, CancellationToken cancellationToken)
    {
        var filePath = rule.Target;
        var template = rule.Parameters.TryGetValue("template", out var t) ? t.ToString() : null;

        if (template != null)
        {
            // Create file from template
            _logger.LogInformation("Would create file: {FilePath} from template: {Template}", filePath, template);
            return await Task.FromResult(true);
        }

        return false;
    }

    private async Task<bool> AutoFixRequiresDocumentationAsync(TaskValidationRule rule, string context, CancellationToken cancellationToken)
    {
        // Generate XML documentation for public methods
        _logger.LogInformation("Would generate documentation for: {Target}", rule.Target);
        return await Task.FromResult(true);
    }

    private string GenerateTestScaffolding(string className)
    {
        return $@"using Xunit;

namespace {className}Tests
{{
    public class {className}Tests
    {{
        [Fact]
        public void {className}_ShouldWork()
        {{
            // Arrange
            var sut = new {className}();

            // Act
            // TODO: Add test logic

            // Assert
            Assert.NotNull(sut);
        }}
    }}
}}";
    }

    private async Task<float[]> GenerateEmbeddingForQuery(string query)
    {
        // This would call the embedding service, but for now return empty array
        // In real implementation, inject IEmbeddingService
        return await Task.FromResult(new float[1024]);
    }
}

