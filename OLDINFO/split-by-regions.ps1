param(
    [string]$FilePath
)

$content = Get-Content $FilePath -Raw
$lines = Get-Content $FilePath

# Extract using statements and namespace
$usingStatements = @()
$namespace = ""
$classDeclaration = ""

foreach ($line in $lines) {
    if ($line -match "^using ") {
        $usingStatements += $line
    }
    if ($line -match "^namespace ") {
        $namespace = $line
    }
    if ($line -match "^public class ") {
        $classDeclaration = $line -replace "public class ", "public partial class "
        break
    }
}

# Find all regions
$regionPattern = '(?ms)#region\s+(?<name>[^\r\n]+)\s*\r?\n(?<content>.*?)#endregion'
$matches = [regex]::Matches($content, $regionPattern)

Write-Host "Found $($matches.Count) regions in $FilePath"

# Create main file header
$header = @"
$($usingStatements -join "`n")

$namespace

/// <summary>
/// Detects patterns specific to Microsoft Agent Framework, Semantic Kernel, and AutoGen
/// Based on: https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview
/// SPLIT INTO PARTIAL CLASSES
/// </summary>
$classDeclaration
{
"@

# Process each region
foreach ($match in $matches) {
    $regionName = $match.Groups["name"].Value.Trim()
    $regionContent = $match.Groups["content"].Value
    
    # Sanitize filename
    $fileName = $regionName -replace '\s+', '' -replace '[^\w]', ''
    $outputFile = $FilePath -replace '\.cs$', ".$fileName.cs"
    
    Write-Host "Creating $outputFile for region: $regionName"
    
    # Create partial file
    $partialContent = @"
$($usingStatements -join "`n")

$namespace

/// <summary>
/// $regionName
/// </summary>
$classDeclaration
{
    #region $regionName

$regionContent
    #endregion
}
"@
    
    Set-Content -Path $outputFile -Value $partialContent
}

Write-Host "Region extraction complete!"

