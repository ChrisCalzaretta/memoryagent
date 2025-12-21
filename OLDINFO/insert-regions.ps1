param([string]$FilePath, [hashtable]$RegionMap)

$content = Get-Content $FilePath
$output = @()
$regionsAdded = @{}

for ($i = 0; $i -lt $content.Count; $i++) {
    $line = $content[$i]
    
    # Check if this line starts a detection method
    foreach ($methodPattern in $RegionMap.Keys) {
        if ($line -match "private.*List<CodePattern>.*Detect$methodPattern\(") {
            $regionName = $RegionMap[$methodPattern]
            
            # Only add region if we haven't added it yet
            if (-not $regionsAdded.ContainsKey($regionName)) {
                # Close previous region if exists
                if ($regionsAdded.Count -gt 0) {
                    $output += ""
                    $output += "    #endregion"
                    $output += ""
                }
                
                # Add new region
                $output += "    #region $regionName"
                $output += ""
                $regionsAdded[$regionName] = $true
            }
            break
        }
    }
    
    $output += $line
}

# Close final region
if ($regionsAdded.Count -gt 0) {
    $output += ""
    $output += "    #endregion"
}

Set-Content $FilePath -Value $output
Write-Host "Added $($regionsAdded.Count) regions to $FilePath"

