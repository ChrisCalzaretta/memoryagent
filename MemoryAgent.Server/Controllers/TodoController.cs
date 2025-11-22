using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemoryAgent.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private readonly ITodoService _todoService;
    private readonly IPlanService _planService;
    private readonly ILogger<TodoController> _logger;

    public TodoController(
        ITodoService todoService,
        IPlanService planService,
        ILogger<TodoController> logger)
    {
        _todoService = todoService;
        _planService = planService;
        _logger = logger;
    }

    // TODO Endpoints
    [HttpPost("add")]
    public async Task<ActionResult<TodoItem>> AddTodo(
        [FromBody] AddTodoRequest request,
        CancellationToken cancellationToken)
    {
        var todo = await _todoService.AddTodoAsync(request, cancellationToken);
        return Ok(todo);
    }

    [HttpDelete("remove/{todoId}")]
    public async Task<ActionResult> RemoveTodo(string todoId, CancellationToken cancellationToken)
    {
        var result = await _todoService.RemoveTodoAsync(todoId, cancellationToken);
        if (!result)
        {
            return NotFound($"TODO not found: {todoId}");
        }
        return Ok(new { message = "TODO removed successfully" });
    }

    [HttpGet("{todoId}")]
    public async Task<ActionResult<TodoItem>> GetTodo(string todoId, CancellationToken cancellationToken)
    {
        var todo = await _todoService.GetTodoAsync(todoId, cancellationToken);
        if (todo == null)
        {
            return NotFound($"TODO not found: {todoId}");
        }
        return Ok(todo);
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<TodoItem>>> GetTodos(
        [FromQuery] string? context = null,
        [FromQuery] TodoStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var todos = await _todoService.GetTodosAsync(context, status, cancellationToken);
        return Ok(todos);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<TodoItem>>> SearchTodos(
        [FromQuery] string? context = null,
        [FromQuery] TodoStatus? status = null,
        [FromQuery] TodoPriority? priority = null,
        [FromQuery] string? assignedTo = null,
        CancellationToken cancellationToken = default)
    {
        // For now, use the same service method
        // In the future, we can add more sophisticated search
        var todos = await _todoService.GetTodosAsync(context, status, cancellationToken);
        
        // Filter by priority if specified
        if (priority.HasValue)
        {
            todos = todos.Where(t => t.Priority == priority.Value).ToList();
        }
        
        // Filter by assignedTo if specified
        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            todos = todos.Where(t => t.AssignedTo.Contains(assignedTo, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        return Ok(todos);
    }

    [HttpPut("{todoId}/status")]
    public async Task<ActionResult<TodoItem>> UpdateTodoStatus(
        string todoId,
        [FromBody] TodoStatus status,
        CancellationToken cancellationToken)
    {
        var todo = await _todoService.UpdateTodoAsync(todoId, status, cancellationToken);
        return Ok(todo);
    }

}

