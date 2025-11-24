# Test script to list all available MCP tools and verify new pattern detection tools are registered

$ErrorActionPreference = "Stop"
$baseUrl = "http://localhost:5098"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Listing All MCP Tools" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" `
        -Method POST `
        -ContentType "application/json" `
        -Body (@{
            jsonrpc = "2.0"
            id = 1
            method = "tools/list"
        } | ConvertTo-Json)
    
    $tools = $response.result.tools
    Write-Host "Total Tools: $($tools.Count)" -ForegroundColor Green
    Write-Host ""
    
    # Check for new pattern detection tools
    $patternTools = @("search_patterns", "validate_best_practices", "get_recommendations", "get_available_best_practices")
    
    Write-Host "Checking for new Pattern Detection tools..." -ForegroundColor Yellow
    foreach ($toolName in $patternTools) {
        $tool = $tools | Where-Object { $_.name -eq $toolName }
        if ($tool) {
            Write-Host "✅ $toolName" -ForegroundColor Green
            Write-Host "   Description: $($tool.description)" -ForegroundColor Gray
        } else {
            Write-Host "❌ $toolName NOT FOUND!" -ForegroundColor Red
        }
    }
    Write-Host ""
    
    # List all tools by category
    Write-Host "All Available Tools:" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor Cyan
    
    $toolGroups = @{
        "Indexing" = @("index_file", "index_directory", "reindex")
        "Search" = @("query", "search", "smartsearch")
        "Graph Analysis" = @("impact_analysis", "dependency_chain", "find_circular_dependencies")
        "Pattern Detection" = @("search_patterns", "validate_best_practices", "get_recommendations", "get_available_best_practices")
        "TODO Management" = @("add_todo", "search_todos", "update_todo_status")
        "Plan Management" = @("create_plan", "get_plan_status", "update_task_status", "complete_plan", "search_plans", "validate_task")
    }
    
    foreach ($category in $toolGroups.Keys | Sort-Object) {
        Write-Host ""
        Write-Host "${category}:" -ForegroundColor Yellow
        foreach ($toolName in $toolGroups[$category]) {
            $tool = $tools | Where-Object { $_.name -eq $toolName }
            if ($tool) {
                Write-Host "  ✅ $toolName" -ForegroundColor Green
            } else {
                Write-Host "  ❌ $toolName (NOT FOUND)" -ForegroundColor Red
            }
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Tool List Check Complete!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
    exit 1
}

