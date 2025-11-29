param([string]$FilePath)

$content = Get-Content $FilePath -Raw
$lines = Get-Content $FilePath

# Extract header
$usingStatements = @()
$namespace = ""
$classDeclaration = ""

foreach ($line in $lines) {
    if ($line -match "^using ") { $usingStatements += $line }
    if ($line -match "^namespace ") { $namespace = $line }
    if ($line -match "^public class ") {
        $classDeclaration = $line -replace "public class ", "public partial class "
        break
    }
}

# Find all regions
$regionPattern = '(?ms)    #region\s+(?<name>[^\r\n]+)\s*\r?\n(?<content>.*?)    #endregion'
$matches = [regex]::Matches($content, $regionPattern)

Write-Host "Found $($matches.Count) regions"

# Process each region
foreach ($match in $matches) {
    $regionName = $match.Groups["name"].Value.Trim()
    $regionContent = $match.Groups["content"].Value
    
    $fileName = $regionName -replace '\s+', '' -replace '[^\w]', ''
    $outputFile = $FilePath -replace '\.cs$', ".$fileName.cs"
    
    Write-Host "Creating $outputFile"
    
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

Write-Host "Split complete!"

