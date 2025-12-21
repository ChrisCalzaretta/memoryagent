using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace UserServiceDemo
{
    /// <summary>
    /// In-memory implementation of user repository for demonstration purposes
    /// </summary>
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly ConcurrentDictionary<Guid, User> _users = new();

        /// <summary>
        /// Creates a new user in memory
        /// </summary>
        /// <param name="user">The user to create</param>
        /// <returns>The created user</returns>
        public Task<User> CreateAsync(User user)
        {
            _users.TryAdd(user.Id, user);
            return Task.FromResult(user);
        }

        /// <summary>
        /// Retrieves a user by ID from memory
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>The user if found, null otherwise</returns>
        public Task<User> GetByIdAsync(Guid id)
        {
            _users.TryGetValue(id, out var user);
            return Task.FromResult(user);
        }

        /// <summary>
        /// Updates a user in memory
        /// </summary>
        /// <param name="user">The user to update</param>
        /// <returns>The updated user</returns>
        public Task<User> UpdateAsync(User user)
        {
            _users.TryUpdate(user.Id, user, _users[user.Id]);
            return Task.FromResult(user);
        }

        /// <summary>
        /// Deletes a user from memory
        /// </summary>
        /// <param name="id">The user ID to delete</param>
        /// <returns>A task representing the operation</returns>
        public Task DeleteAsync(Guid id)
        {
            _users.TryRemove(id, out _);
            return Task.CompletedTask;
        }
    }
}
