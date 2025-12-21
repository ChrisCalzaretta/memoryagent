using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserManagement.Data;
using UserManagement.Interfaces;
using UserManagement.Services;

namespace UserManagement
{
    /// <summary>
    /// Main program entry point with proper service provider disposal
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            await using var serviceProvider = host.Services.CreateAsyncScope();
            var userService = serviceProvider.ServiceProvider.GetRequiredService<UserService>();
            var logger = serviceProvider.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                await RunUserServiceDemo(userService, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during execution");
            }
            
            // Properly dispose the host
            if (host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IUserRepository, InMemoryUserRepository>();
                    services.AddTransient<UserService>();
                    services.AddLogging(builder => builder.AddConsole());
                });

        private static async Task RunUserServiceDemo(UserService userService, ILogger<Program> logger)
        {
            logger.LogInformation("=== User Service Demo ===");

            try
            {
                // Test CreateUser
                logger.LogInformation("Creating users...");
                var user1 = await userService.CreateUserAsync("John Doe", "john.doe@example.com");
                var user2 = await userService.CreateUserAsync("Jane Smith", "jane.smith@example.com");
                
                logger.LogInformation("Created user: {Name} ({Email}) with ID {Id}", 
                    user1.Name, user1.Email, user1.Id);
                logger.LogInformation("Created user: {Name} ({Email}) with ID {Id}", 
                    user2.Name, user2.Email, user2.Id);

                // Test GetUserById
                logger.LogInformation("\nRetrieving users...");
                var retrievedUser = await userService.GetUserByIdAsync(user1.Id);
                logger.LogInformation("Retrieved user: {Name} ({Email})", 
                    retrievedUser.Name, retrievedUser.Email);

                // Test UpdateUser
                logger.LogInformation("\nUpdating user...");
                var updatedUser = await userService.UpdateUserAsync(user1.Id, "John Updated", "john.updated@example.com");
                logger.LogInformation("Updated user: {Name} ({Email})", 
                    updatedUser.Name, updatedUser.Email);

                // Test error handling - duplicate email
                logger.LogInformation("\nTesting duplicate email validation...");
                try
                {
                    await userService.CreateUserAsync("Test User", "jane.smith@example.com");
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogInformation("Caught expected error: {Message}", ex.Message);
                }

                // Test DeleteUser
                logger.LogInformation("\nDeleting user...");
                var deleteResult = await userService.DeleteUserAsync(user2.Id);
                logger.LogInformation("Delete successful: {Result}", deleteResult);

                // Test error handling - user not found
                logger.LogInformation("\nTesting user not found...");
                try
                {
                    await userService.GetUserByIdAsync(999);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogInformation("Caught expected error: {Message}", ex.Message);
                }

                logger.LogInformation("\n=== Demo completed successfully ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during demo execution");
                throw;
            }
        }
    }
}
