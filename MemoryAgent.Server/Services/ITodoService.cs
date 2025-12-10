using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

public interface ITodoService
{
    Task<TodoItem> AddTodoAsync(AddTodoRequest request, CancellationToken cancellationToken = default);
    Task<bool> RemoveTodoAsync(string todoId, CancellationToken cancellationToken = default);
    Task<TodoItem?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default);
    Task<List<TodoItem>> GetTodosAsync(string? context = null, TodoStatus? status = null, CancellationToken cancellationToken = default);
    Task<TodoItem> UpdateTodoAsync(string todoId, TodoStatus status, CancellationToken cancellationToken = default);
}

















