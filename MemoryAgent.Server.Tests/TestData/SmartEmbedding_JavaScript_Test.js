/**
 * @class UserService
 * @description Service for managing user data and operations
 * 
 * Handles CRUD operations for users including:
 * - User creation and validation
 * - Profile updates
 * - Password management
 * - User search and filtering
 * 
 * @example
 * const userService = new UserService(userRepo, validator, logger);
 * const user = await userService.createUser({ email: 'test@example.com', name: 'Test User' });
 */
export class UserService {
    /**
     * @param {IUserRepository} userRepository - Repository for user data access
     * @param {IValidator} validator - Validator for user input
     * @param {ILogger} logger - Logger instance
     */
    constructor(userRepository, validator, logger) {
        this.userRepository = userRepository;
        this.validator = validator;
        this.logger = logger;
    }

    /**
     * Creates a new user in the system
     * @description Validates user data, checks for duplicates, and creates the user record
     * 
     * @param {Object} userData - User data object
     * @param {string} userData.email - User's email address (unique)
     * @param {string} userData.name - User's full name
     * @param {string} userData.password - User's password (will be hashed)
     * @returns {Promise<User>} The created user object
     * @throws {ValidationError} If user data is invalid
     * @throws {DuplicateError} If email already exists
     */
    async createUser(userData) {
        // Validate input
        const validationResult = await this.validator.validate(userData);
        if (!validationResult.isValid) {
            this.logger.error('User validation failed', { errors: validationResult.errors });
            throw new ValidationError(validationResult.errors);
        }

        // Check for existing user
        const existingUser = await this.userRepository.findByEmail(userData.email);
        if (existingUser) {
            this.logger.warn('Duplicate user creation attempt', { email: userData.email });
            throw new DuplicateError('User with this email already exists');
        }

        // Create user
        const user = await this.userRepository.create(userData);
        this.logger.info('User created successfully', { userId: user.id });

        return user;
    }

    /**
     * Updates an existing user's profile information
     * @param {number} userId - ID of the user to update
     * @param {Object} updateData - Fields to update
     * @returns {Promise<User>} Updated user object
     */
    async updateProfile(userId, updateData) {
        const user = await this.userRepository.findById(userId);
        if (!user) {
            throw new NotFoundError('User not found');
        }

        const updated = await this.userRepository.update(userId, updateData);
        return updated;
    }

    /**
     * Searches for users matching the given criteria
     * @param {Object} searchCriteria - Search filters
     * @returns {Promise<Array<User>>} Array of matching users
     */
    async searchUsers(searchCriteria) {
        return await this.userRepository.search(searchCriteria);
    }
}

