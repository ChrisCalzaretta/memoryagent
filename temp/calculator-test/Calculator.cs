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
            if (b == 0)
                throw new DivideByZeroException("Cannot divide by zero");
            return a / b;
        }

        public double Power(double baseNumber, double exponent)
        {
            return Math.Pow(baseNumber, exponent);
        }

        public double SquareRoot(double number)
        {
            if (number < 0)
                throw new ArgumentException("Cannot calculate square root of negative number");
            return Math.Sqrt(number);
        }

        public double Percentage(double value, double percentage)
        {
            return (value * percentage) / 100;
        }

        public static void Main(string[] args)
        {
            Calculator calculator = new Calculator();
            
            Console.WriteLine("Calculator Operations Demonstration:");
            Console.WriteLine("====================================");
            
            try
            {
                // Addition
                double result1 = calculator.Add(10, 5);
                Console.WriteLine($"Addition: 10 + 5 = {result1}");
                
                // Subtraction
                double result2 = calculator.Subtract(10, 5);
                Console.WriteLine($"Subtraction: 10 - 5 = {result2}");
                
                // Multiplication
                double result3 = calculator.Multiply(10, 5);
                Console.WriteLine($"Multiplication: 10 * 5 = {result3}");
                
                // Division
                double result4 = calculator.Divide(10, 5);
                Console.WriteLine($"Division: 10 / 5 = {result4}");
                
                // Power
                double result5 = calculator.Power(2, 3);
                Console.WriteLine($"Power: 2^3 = {result5}");
                
                // Square Root
                double result6 = calculator.SquareRoot(25);
                Console.WriteLine($"Square Root: √25 = {result6}");
                
                // Percentage
                double result7 = calculator.Percentage(200, 15);
                Console.WriteLine($"Percentage: 15% of 200 = {result7}");
                
                Console.WriteLine("\nAll calculator operations completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
