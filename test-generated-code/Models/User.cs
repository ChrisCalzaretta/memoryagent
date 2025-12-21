using System;

namespace UserServiceDemo
{
    /// <summary>
    /// Represents a user entity
    /// </summary>
    public class User
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user's full name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's age
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the user was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the user was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
