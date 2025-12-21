using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UserServiceDemo
{
    /// <summary>
    /// Main program entry point demonstrating the UserService functionality
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Setup dependency injection and logging
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging();
                    services.AddSingleton<IUserRepository, InMemoryUserRepository>();
                    services.AddScoped<IUserService, UserService>();
                })
                .Build();

            var userService = host.Services.GetRequiredService<IUserService>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting UserService demonstration");

            try
            {
                // Demonstrate UserService functionality
                await DemonstrateUserService(userService, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during demonstration");
            }

            logger.LogInformation("UserService demonstration completed");
        }

        private static async Task DemonstrateUserService(IUserService userService, ILogger logger)
        {
            // Create a new user
            var newUser = new User
            {
                Name = "John Doe",
                Email = "john.doe@example.com",
                Age = 30
            };

            logger.LogInformation("Creating new user: {UserName}", newUser.Name);
            var createdUser = await userService.CreateUserAsync(newUser);
            logger.LogInformation("Created user with ID: {UserId}", createdUser.Id);

            // Get user by ID
            logger.LogInformation("Retrieving user by ID: {UserId}", createdUser.Id);
            var retrievedUser = await userService.GetUserByIdAsync(createdUser.Id);
            logger.LogInformation("Retrieved user: {UserName} ({UserEmail})", 
                retrievedUser.Name, retrievedUser.Email);

            // Update user
            retrievedUser.Age = 31;
            retrievedUser.Email = "john.doe.updated@example.com";
            logger.LogInformation("Updating user with ID: {UserId}", retrievedUser.Id);
            var updatedUser = await userService.UpdateUserAsync(retrievedUser);
            logger.LogInformation("Updated user email to: {UserEmail}", updatedUser.Email);

            // Try to get non-existent user
            logger.LogInformation("Attempting to retrieve non-existent user");
            try
            {
                await userService.GetUserByIdAsync(Guid.NewGuid());
            }
            catch (UserNotFoundException ex)
            {
                logger.LogInformation("Expected exception caught: {ExceptionMessage}", ex.Message);
            }

            // Delete user
            logger.LogInformation("Deleting user with ID: {UserId}", updatedUser.Id);
            await userService.DeleteUserAsync(updatedUser.Id);
            logger.LogInformation("User deleted successfully");
        }
    }
}
