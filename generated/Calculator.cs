using System;

namespace CalculatorApp
{
    public class Calculator
    {
        public double Add(double a, double b)
        {
            return a + b;
        }

        public double Subtract(double a, double b)
        {
            return a - b;
        }

        public double Multiply(double a, double b)
        {
            return a * b;
        }

        public double Divide(double a, double b)
        {
            if (Math.Abs(b) < double.Epsilon)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }

        public double Power(double baseNumber, double exponent)
        {
            return Math.Pow(baseNumber, exponent);
        }

        public double SquareRoot(double number)
        {
            if (number < 0)
            {
                throw new ArgumentException("Cannot calculate square root of a negative number.");
            }
            return Math.Sqrt(number);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Calculator calc = new Calculator();

            try
            {
                Console.WriteLine("=== Calculator Test Results ===");
                
                // Test Addition
                Console.WriteLine($"Add(10, 5) = {calc.Add(10, 5)}");
                
                // Test Subtraction
                Console.WriteLine($"Subtract(10, 5) = {calc.Subtract(10, 5)}");
                
                // Test Multiplication
                Console.WriteLine($"Multiply(10, 5) = {calc.Multiply(10, 5)}");
                
                // Test Division
                Console.WriteLine($"Divide(10, 5) = {calc.Divide(10, 5)}");
                
                // Test Power
                Console.WriteLine($"Power(2, 3) = {calc.Power(2, 3)}");
                
                // Test Square Root
                Console.WriteLine($"SquareRoot(16) = {calc.SquareRoot(16)}");
                
                Console.WriteLine("\n=== Error Handling Tests ===");
                
                // Test Division by Zero
                try
                {
                    calc.Divide(10, 0);
                }
                catch (DivideByZeroException ex)
                {
                    Console.WriteLine($"Division by zero handled: {ex.Message}");
                }
                
                // Test Negative Square Root
                try
                {
                    calc.SquareRoot(-16);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Negative square root handled: {ex.Message}");
                }
                
                Console.WriteLine("\n=== All tests completed successfully! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }
    }
}



