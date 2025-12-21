using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using UserManagement.Interfaces;
using UserManagement.Models;

namespace UserManagement.Services
{
    /// <summary>
    /// Service for managing user operations with comprehensive error handling
    /// </summary>
    public class UserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Initializes a new instance of the UserService
        /// </summary>
        public UserService(IUserRepository userRepository, ILogger<UserService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new user asynchronously with validation and error handling
        /// </summary>
        /// <param name="name">The user's name</param>
        /// <param name="email">The user's email</param>
        /// <returns>The created user</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        /// <exception cref="InvalidOperationException">Thrown when email already exists</exception>
        public async Task<User> CreateUserAsync(string name, string email)
        {
            try
            {
                _logger.LogInformation("Creating user with email: {Email}", email);

                // Validate input parameters
                ValidateUserInput(name, email);

                var user = new User
                {
                    Name = name.Trim(),
                    Email = email.Trim().ToLowerInvariant()
                };

                var createdUser = await _userRepository.CreateUserAsync(user);
                
                _logger.LogInformation("Successfully created user with ID: {UserId}", createdUser.Id);
                return createdUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user with email: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Gets a user by their ID asynchronously
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>The user if found</returns>
        /// <exception cref="ArgumentException">Thrown when ID is invalid</exception>
        /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
        public async Task<User> GetUserByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("User ID must be greater than zero.", nameof(id));
                }

                _logger.LogInformation("Retrieving user with ID: {UserId}", id);

                var user = await _userRepository.GetUserByIdAsync(id);
                
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    throw new KeyNotFoundException($"User with ID {id} not found.");
                }

                _logger.LogInformation("Successfully retrieved user with ID: {UserId}", id);
                return user;
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException || ex is ArgumentException))
            {
                _logger.LogError(ex, "Failed to retrieve user with ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing user asynchronously with validation
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <param name="name">The updated name</param>
        /// <param name="email">The updated email</param>
        /// <returns>The updated user</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
        /// <exception cref="InvalidOperationException">Thrown when email already exists</exception>
        public async Task<User> UpdateUserAsync(int id, string name, string email)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("User ID must be greater than zero.", nameof(id));
                }

                _logger.LogInformation("Updating user with ID: {UserId}", id);

                // Validate input parameters
                ValidateUserInput(name, email);

                // Get existing user to ensure it exists
                var existingUser = await _userRepository.GetUserByIdAsync(id);
                if (existingUser == null)
                {
                    throw new KeyNotFoundException($"User with ID {id} not found.");
                }

                var updatedUser = new User
                {
                    Id = id,
                    Name = name.Trim(),
                    Email = email.Trim().ToLowerInvariant(),
                    CreatedAt = existingUser.CreatedAt
                };

                var result = await _userRepository.UpdateUserAsync(updatedUser);
                
                _logger.LogInformation("Successfully updated user with ID: {UserId}", id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user with ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a user by their ID asynchronously
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>True if deleted successfully</returns>
        /// <exception cref="ArgumentException">Thrown when ID is invalid</exception>
        /// <exception cref="KeyNotFoundException">Thrown when user is not found</exception>
        public async Task<bool> DeleteUserAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("User ID must be greater than zero.", nameof(id));
                }

                _logger.LogInformation("Deleting user with ID: {UserId}", id);

                // Verify user exists before attempting deletion
                var existingUser = await _userRepository.GetUserByIdAsync(id);
                if (existingUser == null)
                {
                    throw new KeyNotFoundException($"User with ID {id} not found.");
                }

                var result = await _userRepository.DeleteUserAsync(id);
                
                if (result)
                {
                    _logger.LogInformation("Successfully deleted user with ID: {UserId}", id);
                }
                else
                {
                    _logger.LogWarning("Failed to delete user with ID: {UserId}", id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user with ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Validates user input parameters with improved email validation
        /// </summary>
        private void ValidateUserInput(string name, string email)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            }

            if (name.Length > 100)
            {
                throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email cannot be null or empty.", nameof(email));
            }

            if (email.Length > 255)
            {
                throw new ArgumentException("Email cannot exceed 255 characters.", nameof(email));
            }

            if (!IsValidEmail(email))
            {
                throw new ArgumentException("Invalid email format.", nameof(email));
            }
        }

        /// <summary>
        /// Validates email format using regex for improved robustness
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                return EmailRegex.IsMatch(email.Trim());
            }
            catch
            {
                return false;
            }
        }
    }
}
