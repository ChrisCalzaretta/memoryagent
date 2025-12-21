using System;
using System.Threading.Tasks;

namespace UserServiceDemo
{
    /// <summary>
    /// Interface defining user service operations
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Creates a new user asynchronously
        /// </summary>
        /// <param name="user">The user to create</param>
        /// <returns>The created user with assigned ID</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when user data is invalid</exception>
        Task<User> CreateUserAsync(User user);

        /// <summary>
        /// Retrieves a user by their unique identifier asynchronously
        /// </summary>
        /// <param name="userId">The unique identifier of the user</param>
        /// <returns>The user if found</returns>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        Task<User> GetUserByIdAsync(Guid userId);

        /// <summary>
        /// Updates an existing user asynchronously
        /// </summary>
        /// <param name="user">The user with updated information</param>
        /// <returns>The updated user</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null</exception>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        Task<User> UpdateUserAsync(User user);

        /// <summary>
        /// Deletes a user by their unique identifier asynchronously
        /// </summary>
        /// <param name="userId">The unique identifier of the user to delete</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        Task DeleteUserAsync(Guid userId);
    }
}
