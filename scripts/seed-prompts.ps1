# Seed Design Intelligence Prompts to Neo4j

$promptsFile = "DesignAgent.Server/Data/design_prompts.json"
$neo4jContainer = "memoryagent-neo4j"
$neo4jUser = "neo4j"
$neo4jPass = "memoryagent123"

Write-Host "🌱 Seeding Design Intelligence Prompts to Neo4j..." -ForegroundColor Cyan

# Read prompts JSON
$prompts = Get-Content $promptsFile -Raw | ConvertFrom-Json

$count = 0
foreach ($prompt in $prompts) {
    $name = $prompt.name
    $version = $prompt.version
    $category = $prompt.category
    $description = $prompt.description
    $content = $prompt.content -replace "'", "\'"
    
    # Create Cypher query
    $cypher = @"
MERGE (p:Prompt {name: '$name'})
SET p.version = $version,
    p.category = '$category',
    p.description = '$description',
    p.content = '$content',
    p.updatedAt = datetime()
RETURN p.name as name
"@
    
    # Execute query
    $result = docker exec $neo4jContainer cypher-shell -u $neo4jUser -p $neo4jPass "$cypher" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Seeded prompt: $name" -ForegroundColor Green
        $count++
    } else {
        Write-Host "❌ Failed to seed prompt: $name" -ForegroundColor Red
        Write-Host $result
    }
}

Write-Host ""
Write-Host "✅ Seeded $count/$($prompts.Count) prompts successfully!" -ForegroundColor Green

