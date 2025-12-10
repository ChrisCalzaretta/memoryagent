using System;

namespace CalculatorApp
{
    public class Program
    {
        public static void RunDemo()
        {
            Console.WriteLine("=== Calculator Demo ===");
            Console.WriteLine();
            
            var calculator = new Calculator();
            
            // Demonstrate basic arithmetic operations
            Console.WriteLine("Basic Operations:");
            Console.WriteLine($"10 + 5 = {calculator.Add(10, 5)}");
            Console.WriteLine($"10 - 5 = {calculator.Subtract(10, 5)}");
            Console.WriteLine($"10 * 5 = {calculator.Multiply(10, 5)}");
            Console.WriteLine($"10 / 5 = {calculator.Divide(10, 5)}");
            Console.WriteLine();
            
            // Demonstrate advanced operations
            Console.WriteLine("Advanced Operations:");
            Console.WriteLine($"2^3 = {calculator.Power(2, 3)}");
            Console.WriteLine($"√16 = {calculator.SquareRoot(16)}");
            Console.WriteLine($"25% of 200 = {calculator.Percentage(200, 25)}");
            Console.WriteLine();
            
            // Demonstrate error handling
            Console.WriteLine("Error Handling:");
            try
            {
                Console.WriteLine($"10 / 0 = {calculator.Divide(10, 0)}");
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            try
            {
                Console.WriteLine($"√(-4) = {calculator.SquareRoot(-4)}");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Demo completed successfully!");
        }
    }
}
