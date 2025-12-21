using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UserServiceDemo
{
    /// <summary>
    /// Service class for managing user operations with proper error handling and logging
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;

        /// <summary>
        /// Initializes a new instance of the UserService class
        /// </summary>
        /// <param name="userRepository">Repository for user data operations</param>
        /// <param name="logger">Logger for recording service operations</param>
        public UserService(IUserRepository userRepository, ILogger<UserService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new user asynchronously with validation and error handling
        /// </summary>
        /// <param name="user">The user to create</param>
        /// <returns>The created user with assigned ID and timestamps</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when user data is invalid</exception>
        public async Task<User> CreateUserAsync(User user)
        {
            if (user == null)
            {
                _logger.LogError("Attempted to create a null user");
                throw new ArgumentNullException(nameof(user));
            }

            try
            {
                _logger.LogInformation("Creating user with name: {UserName}", user.Name);

                // Validate user data
                ValidateUser(user);

                // Set system properties
                user.Id = Guid.NewGuid();
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;

                var createdUser = await _userRepository.CreateAsync(user);
                
                _logger.LogInformation("Successfully created user with ID: {UserId}", createdUser.Id);
                return createdUser;
            }
            catch (Exception ex) when (!(ex is ArgumentNullException || ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Unexpected error occurred while creating user");
                throw new InvalidOperationException("Failed to create user due to an unexpected error", ex);
            }
        }

        /// <summary>
        /// Retrieves a user by their unique identifier asynchronously
        /// </summary>
        /// <param name="userId">The unique identifier of the user</param>
        /// <returns>The user if found</returns>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        public async Task<User> GetUserByIdAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving user with ID: {UserId}", userId);

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    throw new UserNotFoundException($"User with ID {userId} was not found");
                }

                _logger.LogInformation("Successfully retrieved user: {UserName}", user.Name);
                return user;
            }
            catch (UserNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while retrieving user with ID: {UserId}", userId);
                throw new InvalidOperationException($"Failed to retrieve user with ID {userId}", ex);
            }
        }

        /// <summary>
        /// Updates an existing user asynchronously with validation
        /// </summary>
        /// <param name="user">The user with updated information</param>
        /// <returns>The updated user</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null</exception>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        public async Task<User> UpdateUserAsync(User user)
        {
            if (user == null)
            {
                _logger.LogError("Attempted to update a null user");
                throw new ArgumentNullException(nameof(user));
            }

            try
            {
                _logger.LogInformation("Updating user with ID: {UserId}", user.Id);

                // Validate user data
                ValidateUser(user);

                // Check if user exists
                var existingUser = await _userRepository.GetByIdAsync(user.Id);
                if (existingUser == null)
                {
                    _logger.LogWarning("Attempted to update non-existent user with ID: {UserId}", user.Id);
                    throw new UserNotFoundException($"User with ID {user.Id} was not found");
                }

                // Update timestamp
                user.UpdatedAt = DateTime.UtcNow;
                user.CreatedAt = existingUser.CreatedAt; // Preserve original creation date

                var updatedUser = await _userRepository.UpdateAsync(user);
                
                _logger.LogInformation("Successfully updated user with ID: {UserId}", updatedUser.Id);
                return updatedUser;
            }
            catch (ArgumentNullException || UserNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while updating user with ID: {UserId}", user.Id);
                throw new InvalidOperationException($"Failed to update user with ID {user.Id}", ex);
            }
        }

        /// <summary>
        /// Deletes a user by their unique identifier asynchronously
        /// </summary>
        /// <param name="userId">The unique identifier of the user to delete</param>
        /// <exception cref="UserNotFoundException">Thrown when user is not found</exception>
        public async Task DeleteUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Deleting user with ID: {UserId}", userId);

                // Check if user exists
                var existingUser = await _userRepository.GetByIdAsync(userId);
                if (existingUser == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent user with ID: {UserId}", userId);
                    throw new UserNotFoundException($"User with ID {userId} was not found");
                }

                await _userRepository.DeleteAsync(userId);
                
                _logger.LogInformation("Successfully deleted user with ID: {UserId}", userId);
            }
            catch (UserNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while deleting user with ID: {UserId}", userId);
                throw new InvalidOperationException($"Failed to delete user with ID {userId}", ex);
            }
        }

        /// <summary>
        /// Validates user data for business rules
        /// </summary>
        /// <param name="user">The user to validate</param>
        /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
        private void ValidateUser(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Name))
            {
                throw new InvalidOperationException("User name cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new InvalidOperationException("User email cannot be empty");
            }

            if (!user.Email.Contains("@"))
            {
                throw new InvalidOperationException("User email must be a valid email address");
            }

            if (user.Age < 0 || user.Age > 150)
            {
                throw new InvalidOperationException("User age must be between 0 and 150");
            }
        }
    }
}
