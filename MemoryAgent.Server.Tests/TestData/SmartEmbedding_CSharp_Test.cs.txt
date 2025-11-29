using System.Threading.Tasks;

namespace MemoryAgent.Tests.TestData;

/// <summary>
/// Service for authenticating users and managing JWT tokens
/// </summary>
/// <remarks>
/// This service handles user login, logout, token refresh, and password validation.
/// Implements secure authentication with bcrypt password hashing.
/// </remarks>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user with username and password
    /// </summary>
    /// <param name="username">The user's email address</param>
    /// <param name="password">The user's password in plaintext</param>
    /// <returns>Authentication result with JWT token if successful</returns>
    /// <exception cref="ArgumentException">Thrown when username or password is empty</exception>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));
            
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        _logger.LogInformation("Login attempt for user: {Username}", username);

        // Fetch user from database
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found {Username}", username);
            return AuthResult.Failed("Invalid credentials");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: Invalid password for {Username}", username);
            return AuthResult.Failed("Invalid credentials");
        }

        // Generate JWT token
        var token = await _jwtTokenService.GenerateTokenAsync(user);
        _logger.LogInformation("Login successful for user: {Username}", username);

        return AuthResult.Success(token, user);
    }

    /// <summary>
    /// Refreshes an expired JWT token
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <returns>New JWT token pair</returns>
    public async Task<TokenRefreshResult> RefreshTokenAsync(string refreshToken)
    {
        var userId = await _jwtTokenService.ValidateRefreshTokenAsync(refreshToken);
        if (userId == null)
        {
            return TokenRefreshResult.Invalid();
        }

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return TokenRefreshResult.Invalid();
        }

        var newToken = await _jwtTokenService.GenerateTokenAsync(user);
        return TokenRefreshResult.Success(newToken);
    }
}

public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<TokenRefreshResult> RefreshTokenAsync(string refreshToken);
}

