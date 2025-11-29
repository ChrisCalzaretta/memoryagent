$file = "MemoryAgent.Server/CodeAnalysis/PluginArchitecturePatternDetector.cs"
$content = Get-Content $file -Raw

# Define region mappings (method name patterns -> region name)
$regions = @{
    "AssemblyLoadContext|AssemblyDependencyResolver|EnableDynamicLoading|CollectibleLoadContext|PrivateFalseReference|NativeLibraryLoading" = "Plugin Loading & Isolation"
    "MEFDirectoryCatalog|MEFImportExport|MEFMetadata|LazyPluginLoading|PluginRegistry|TypeScanning|ConfigurationBasedDiscovery" = "Plugin Discovery & Composition"
    "IPluginInterface|StatelessPlugin|PluginHealthCheck|PluginStartStop|PluginDependencyInjection" = "Plugin Lifecycle Management"
    "EventBus|SharedServiceInterface|PluginPipeline|PluginContext" = "Plugin Communication"
    "GatekeeperPattern|Sandboxing|CircuitBreakerPlugin|BulkheadIsolation|PluginSignatureVerification" = "Plugin Security & Governance"
    "SemanticVersioning|CompatibilityMatrix|SideBySideVersioning" = "Plugin Versioning & Compatibility"
}

foreach ($pattern in $regions.Keys) {
    $regionName = $regions[$pattern]
    $methodPattern = $pattern -split '\|' | ForEach-Object { "Detect$_" }
    
    Write-Host "Adding region: $regionName"
    
    # Find first method in this region
    $firstMethod = $methodPattern[0]
    $regex = "(?ms)(    private List<CodePattern> $firstMethod\()"
    
    if ($content -match $regex) {
        $replacement = "    #region $regionName`r`n`r`n" + $matches[1]
        $content = $content -replace $regex, $replacement
    }
}

# Add #endregion before each new #region (except the first)
$lines = $content -split "`r?`n"
$output = @()
$inRegion = $false

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "^\s*#region") {
        if ($inRegion) {
            # Close previous region
            $output += "    #endregion"
            $output += ""
        }
        $inRegion = $true
    }
    $output += $lines[$i]
}

# Close last region before Helper Methods
for ($i = $output.Count - 1; $i -ge 0; $i--) {
    if ($output[$i] -match "^\s*#region Helper Methods") {
        $output = $output[0..($i-1)] + "    #endregion" + "" + $output[$i..($output.Count-1)]
        break
    }
}

Set-Content $file -Value ($output -join "`r`n")
Write-Host "Regions added to $file"

