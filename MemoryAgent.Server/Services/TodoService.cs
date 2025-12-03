using MemoryAgent.Server.Models;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services;

public class TodoService : ITodoService
{
    private readonly IGraphService _graphService;
    private readonly ILogger<TodoService> _logger;

    public TodoService(IGraphService graphService, ILogger<TodoService> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<TodoItem> AddTodoAsync(AddTodoRequest request, CancellationToken cancellationToken = default)
    {
        var todo = new TodoItem
        {
            Context = request.Context,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            FilePath = request.FilePath,
            LineNumber = request.LineNumber,
            AssignedTo = request.AssignedTo
        };

        await _graphService.StoreTodoAsync(todo, cancellationToken);
        
        _logger.LogInformation("Added TODO: {Title} in context {Context}", todo.Title, todo.Context);
        return todo;
    }

    public async Task<bool> RemoveTodoAsync(string todoId, CancellationToken cancellationToken = default)
    {
        var result = await _graphService.DeleteTodoAsync(todoId, cancellationToken);
        
        if (result)
        {
            _logger.LogInformation("Removed TODO: {TodoId}", todoId);
        }
        
        return result;
    }

    public async Task<TodoItem?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default)
    {
        return await _graphService.GetTodoAsync(todoId, cancellationToken);
    }

    public async Task<List<TodoItem>> GetTodosAsync(string? context = null, TodoStatus? status = null, CancellationToken cancellationToken = default)
    {
        return await _graphService.GetTodosAsync(context, status, cancellationToken);
    }

    public async Task<TodoItem> UpdateTodoAsync(string todoId, TodoStatus status, CancellationToken cancellationToken = default)
    {
        var todo = await _graphService.GetTodoAsync(todoId, cancellationToken);
        if (todo == null)
        {
            throw new InvalidOperationException($"TODO not found: {todoId}");
        }

        todo.Status = status;
        if (status == TodoStatus.Completed)
        {
            todo.CompletedAt = DateTime.UtcNow;
        }

        await _graphService.UpdateTodoAsync(todo, cancellationToken);
        
        _logger.LogInformation("Updated TODO {TodoId} to status {Status}", todoId, status);
        return todo;
    }
}









