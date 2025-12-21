using System;

namespace UserServiceDemo
{
    /// <summary>
    /// Exception thrown when a requested user is not found
    /// </summary>
    public class UserNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the UserNotFoundException class
        /// </summary>
        public UserNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the UserNotFoundException class with a specified error message
        /// </summary>
        /// <param name="message">The error message</param>
        public UserNotFoundException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the UserNotFoundException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public UserNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
