param([string]$FilePath)

$content = Get-Content $FilePath -Raw
$lines = Get-Content $FilePath

# Extract header
$usingStatements = @()
$namespace = ""
$classDeclaration = ""
$classStartLine = 0

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match "^using ") { $usingStatements += $line }
    if ($line -match "^namespace ") { $namespace = $line }
    if ($line -match "^public class ") {
        $classDeclaration = $line -replace "public class ", "public partial class "
        $classStartLine = $i
        break
    }
}

# Find all regions
$regionPattern = '(?ms)    #region\s+(?<name>[^\r\n]+)\s*\r?\n(?<content>.*?)    #endregion'
$matches = [regex]::Matches($content, $regionPattern)

Write-Host "Found $($matches.Count) regions in $FilePath"

# Process each region
foreach ($match in $matches) {
    $regionName = $match.Groups["name"].Value.Trim()
    $regionContent = $match.Groups["content"].Value
    
    $fileName = $regionName -replace '\s+', '' -replace '[^\w]', ''
    $outputFile = $FilePath -replace '\.cs$', ".$fileName.cs"
    
    Write-Host "Creating $outputFile for region: $regionName"
    
    $partialContent = @"
$($usingStatements -join "`n")

$namespace

/// <summary>
/// $regionName
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region $regionName

$regionContent
    #endregion
}
"@
    
    Set-Content -Path $outputFile -Value $partialContent
}

Write-Host "Region extraction complete!"

