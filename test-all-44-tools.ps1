# Test all 44 tools through MemoryRouter
# Records timing and determines optimal execution mode

$results = @()

function Test-Tool {
    param($name, $request, $timeoutSec = 20)
    
    $body = @{
        jsonrpc = '2.0'
        id = [guid]::NewGuid().ToString()
        method = 'tools/call'
        params = @{
            name = 'execute_task'
            arguments = @{
                request = $request
            }
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $r = Invoke-RestMethod -Uri 'http://localhost:5010/api/mcp' -Method POST -ContentType 'application/json' -Body $body -TimeoutSec $timeoutSec
        $sw.Stop()
        
        $isBackground = $r.result.content[0].text -match 'Workflow.*Background'
        
        return @{
            Tool = $name
            Time = $sw.ElapsedMilliseconds
            Status = "OK"
            Mode = if ($isBackground) { "BACKGROUND" } else { "SYNC" }
        }
    }
    catch {
        return @{
            Tool = $name
            Time = $timeoutSec * 1000
            Status = "TIMEOUT"
            Mode = "UNKNOWN"
        }
    }
}

Write-Host "Testing all 44 tools..." -ForegroundColor Green
Write-Host ""

# MemoryAgent Tools (33)
Write-Host "=== MemoryAgent Tools (33) ===" -ForegroundColor Cyan

$results += Test-Tool "analyze_complexity" "Analyze complexity of McpHandler.cs"
Write-Host "  analyze_complexity: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "dependency_chain" "Get dependency chain for RouterService"
Write-Host "  dependency_chain: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "explain_code" "Explain what McpHandler does"
Write-Host "  explain_code: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "feedback" "Record positive feedback on routing"
Write-Host "  feedback: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "find_examples" "Find examples of dependency injection"
Write-Host "  find_examples: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "find_similar_questions" "Find similar questions about routing"
Write-Host "  find_similar_questions: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "generate_task_plan" "Generate plan to add caching"
Write-Host "  generate_task_plan: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_coedited_files" "Get files edited with RouterService.cs"
Write-Host "  get_coedited_files: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_context" "Get context for authentication implementation"
Write-Host "  get_context: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_important_files" "Get most important files in the project"
Write-Host "  get_important_files: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_insights" "Get insights about patterns"
Write-Host "  get_insights: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_loaded_models" "Get loaded Ollama models"
Write-Host "  get_loaded_models: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_migration_path" "Get migration path for legacy code"
Write-Host "  get_migration_path: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_project_symbols" "Get all project symbols"
Write-Host "  get_project_symbols: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_recommendations" "Get architecture recommendations"
Write-Host "  get_recommendations: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "impact_analysis" "Analyze impact if RouterService changes"
Write-Host "  impact_analysis: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "index" "Index the docs folder" 5  # Short timeout - should be background
Write-Host "  index: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Mode -eq "BACKGROUND"){"Green"}else{"Yellow"})

$results += Test-Tool "manage_patterns" "List all patterns"
Write-Host "  manage_patterns: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "manage_plan" "Get plan status"
Write-Host "  manage_plan: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "manage_prompts" "List all prompts"
Write-Host "  manage_prompts: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "manage_todos" "Search for TODO items"
Write-Host "  manage_todos: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "query_similar_tasks" "Find similar tasks about performance"
Write-Host "  query_similar_tasks: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "query_task_lessons" "What lessons about error handling"
Write-Host "  query_task_lessons: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "record_file_discussed" "Record that McpHandler.cs was discussed"
Write-Host "  record_file_discussed: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "record_file_edited" "Record that RouterService.cs was edited"
Write-Host "  record_file_edited: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "smartsearch" "Find code that handles authentication"
Write-Host "  smartsearch: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "store_qa" "Store Q&A about routing"
Write-Host "  store_qa: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "store_successful_task" "Store successful routing fix task"
Write-Host "  store_successful_task: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "store_task_failure" "Store failed task about timeout"
Write-Host "  store_task_failure: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "transform" "Analyze CSS quality"
Write-Host "  transform: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "validate" "Validate code for best practices"
Write-Host "  validate: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "validate_imports" "Validate imports in RouterService.cs"
Write-Host "  validate_imports: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "workspace_status" "Show workspace status" 5  # Should be background
Write-Host "  workspace_status: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Mode -eq "BACKGROUND"){"Green"}else{"Yellow"})

Write-Host ""
Write-Host "=== CodingOrchestrator Tools (11) ===" -ForegroundColor Cyan

$results += Test-Tool "orchestrate_task" "Generate hello world function" 5
Write-Host "  orchestrate_task: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "get_task_status" "Get status of job_123"
Write-Host "  get_task_status: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "cancel_task" "Cancel job_123"
Write-Host "  cancel_task: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "list_tasks" "List all running tasks" 5  # Should be background
Write-Host "  list_tasks: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Mode -eq "BACKGROUND"){"Green"}else{"Yellow"})

$results += Test-Tool "get_generated_files" "Get generated files for job_123"
Write-Host "  get_generated_files: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_questionnaire" "Start design questionnaire"
Write-Host "  design_questionnaire: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_create_brand" "Create a brand called TestBrand"
Write-Host "  design_create_brand: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_get_brand" "Get brand TestBrand"
Write-Host "  design_get_brand: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_list_brands" "List all brands"
Write-Host "  design_list_brands: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_validate" "Validate design tokens"
Write-Host "  design_validate: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

$results += Test-Tool "design_update_brand" "Update TestBrand colors"
Write-Host "  design_update_brand: $($results[-1].Time)ms ($($results[-1].Mode))" -ForegroundColor $(if($results[-1].Status -eq "OK"){"Green"}else{"Red"})

# Summary
Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Green

$syncTools = $results | Where-Object { $_.Mode -eq "SYNC" }
$backgroundTools = $results | Where-Object { $_.Mode -eq "BACKGROUND" }
$timeouts = $results | Where-Object { $_.Status -eq "TIMEOUT" }
$slow = $results | Where-Object { $_.Time -gt 10000 -and $_.Status -eq "OK" }

Write-Host "SYNC (real-time): $($syncTools.Count) tools" -ForegroundColor White
Write-Host "BACKGROUND: $($backgroundTools.Count) tools" -ForegroundColor White
Write-Host "TIMEOUT: $($timeouts.Count) tools" -ForegroundColor Red
Write-Host "SLOW (>10s): $($slow.Count) tools" -ForegroundColor Yellow

Write-Host ""
Write-Host "=== TOOLS THAT TIMED OUT (need attention) ===" -ForegroundColor Red
$timeouts | ForEach-Object { Write-Host "  $($_.Tool)" -ForegroundColor Red }

Write-Host ""
Write-Host "=== SLOW TOOLS (>10s) ===" -ForegroundColor Yellow
$slow | Sort-Object Time -Descending | ForEach-Object { Write-Host "  $($_.Tool): $($_.Time)ms" -ForegroundColor Yellow }
