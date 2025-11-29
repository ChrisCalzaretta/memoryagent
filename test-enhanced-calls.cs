using System;
using System.Threading.Tasks;

namespace TestEnhancedCalls
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(int id);
        Task SaveAsync(User user);
        void Delete(User user);
    }

    public interface ILogger
    {
        void LogInformation(string message);
        void LogError(string message);
    }

    public interface ICacheService
    {
        T? GetValue<T>(string key);
        void SetValue<T>(string key, T value);
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test class to verify enhanced method call tracking with DI
    /// </summary>
    public class UserService
    {
        private readonly IUserRepository _repository;
        private readonly ILogger _logger;
        private readonly ICacheService _cache;

        public UserService(IUserRepository repository, ILogger logger, ICacheService cache)
        {
            _repository = repository;
            _logger = logger;
            _cache = cache;
        }

        public async Task<User?> GetUserAsync(int userId)
        {
            // Test: Should track _cache.GetValue as ICacheService.GetValue
            var cached = _cache.GetValue<User>($"user_{userId}");
            if (cached != null)
            {
                // Test: Should track _logger.LogInformation as ILogger.LogInformation
                _logger.LogInformation($"Cache hit for user {userId}");
                return cached;
            }

            // Test: Should track _repository.GetByIdAsync as IUserRepository.GetByIdAsync
            var user = await _repository.GetByIdAsync(userId);
            
            if (user != null)
            {
                // Test: Should track _cache.SetValue as ICacheService.SetValue
                _cache.SetValue($"user_{userId}", user);
                _logger.LogInformation($"Loaded user {userId} from database");
            }
            else
            {
                // Test: Should track _logger.LogError as ILogger.LogError
                _logger.LogError($"User {userId} not found");
            }

            return user;
        }

        public async Task UpdateUserAsync(User user)
        {
            // Test: Should track _repository.SaveAsync as IUserRepository.SaveAsync
            await _repository.SaveAsync(user);
            
            // Test: Should track _cache.SetValue as ICacheService.SetValue
            _cache.SetValue($"user_{user.Id}", user);
            
            // Test: Should track _logger.LogInformation as ILogger.LogInformation
            _logger.LogInformation($"Updated user {user.Id}");
        }

        public void DeleteUser(int userId)
        {
            var user = new User { Id = userId };
            
            // Test: Should track _repository.Delete as IUserRepository.Delete
            _repository.Delete(user);
            
            // Test: Simple method call without object - should just be "LogInformation"
            LogInformation($"Deleted user {userId}");
        }

        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }
    }
}

