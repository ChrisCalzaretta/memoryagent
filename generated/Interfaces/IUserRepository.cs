using UserManagement.Models;

namespace UserManagement.Interfaces
{
    /// <summary>
    /// Defines the contract for user data access operations
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Creates a new user asynchronously
        /// </summary>
        /// <param name="user">The user to create</param>
        /// <returns>The created user with assigned ID</returns>
        Task<User> CreateUserAsync(User user);

        /// <summary>
        /// Gets a user by their ID asynchronously
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>The user if found, null otherwise</returns>
        Task<User?> GetUserByIdAsync(int id);

        /// <summary>
        /// Updates an existing user asynchronously
        /// </summary>
        /// <param name="user">The user to update</param>
        /// <returns>The updated user</returns>
        Task<User> UpdateUserAsync(User user);

        /// <summary>
        /// Deletes a user by their ID asynchronously
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteUserAsync(int id);

        /// <summary>
        /// Checks if a user exists with the specified email asynchronously
        /// </summary>
        /// <param name="email">The email to check</param>
        /// <param name="excludeUserId">Optional user ID to exclude from the check</param>
        /// <returns>True if email exists, false otherwise</returns>
        Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);
    }
}
