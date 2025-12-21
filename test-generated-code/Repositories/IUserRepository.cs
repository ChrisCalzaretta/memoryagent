using System;
using System.Threading.Tasks;

namespace UserServiceDemo
{
    /// <summary>
    /// Interface for user data repository operations
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Creates a new user in the data store
        /// </summary>
        /// <param name="user">The user to create</param>
        /// <returns>The created user</returns>
        Task<User> CreateAsync(User user);

        /// <summary>
        /// Retrieves a user by ID from the data store
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>The user if found, null otherwise</returns>
        Task<User> GetByIdAsync(Guid id);

        /// <summary>
        /// Updates a user in the data store
        /// </summary>
        /// <param name="user">The user to update</param>
        /// <returns>The updated user</returns>
        Task<User> UpdateAsync(User user);

        /// <summary>
        /// Deletes a user from the data store
        /// </summary>
        /// <param name="id">The user ID to delete</param>
        /// <returns>A task representing the operation</returns>
        Task DeleteAsync(Guid id);
    }
}
