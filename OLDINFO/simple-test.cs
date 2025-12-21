using System;

namespace SimpleTest
{
    public interface ILogger
    {
        void Log(string message);
    }

    public class MyService
    {
        private readonly ILogger _logger;

        public MyService(ILogger logger)
        {
            _logger = logger;
        }

        public void DoWork()
        {
            _logger.Log("Starting work");
            Console.WriteLine("Working...");
            _logger.Log("Work complete");
        }
    }
}



















