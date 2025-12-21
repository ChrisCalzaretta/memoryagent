$file = "MemoryAgent.Server/CodeAnalysis/CSharpPatternDetector.cs"
$content = Get-Content $file -Raw

# Define regions for each pattern category
$regions = @(
    @{pattern='DetectCachingPatterns'; region='Caching Patterns'},
    @{pattern='DetectRetryPatterns'; region='Retry Patterns'},
    @{pattern='DetectValidationPatterns'; region='Validation Patterns'},
    @{pattern='DetectDependencyInjectionPatterns'; region='Dependency Injection Patterns'},
    @{pattern='DetectLoggingPatterns'; region='Logging Patterns'},
    @{pattern='DetectErrorHandlingPatterns'; region='Error Handling Patterns'},
    @{pattern='DetectSecurityPatterns'; region='Security Patterns'},
    @{pattern='DetectConfigurationPatterns'; region='Configuration Patterns'},
    @{pattern='DetectApiDesignPatterns'; region='API Design Patterns'}
)

foreach ($r in $regions) {
    $pattern = "(\s+)(private List<CodePattern> $($r.pattern)\()"
    $replacement = "`$1#region $($r.region)`r`n`r`n`$1`$2"
    $content = $content -replace $pattern, $replacement
}

# Close final region before closing brace
$content = $content -replace '(}\s*)$', "    #endregion`r`n`$1"

Set-Content $file -Value $content -NoNewline
Write-Host "Regions added to CSharpPatternDetector.cs"

