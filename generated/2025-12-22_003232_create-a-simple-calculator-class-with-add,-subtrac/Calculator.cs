using System;

public class Calculator
{
    public static double Add(double a, double b)
    {
        return a + b;
    }

    public static double Subtract(double a, double b)
    {
        return a - b;
    }

    public static double Multiply(double a, double b)
    {
        return a * b;
    }

    public static double Divide(double a, double b)
    {
        if (b == 0)
        {
            throw new ArgumentException("Cannot divide by zero.");
        }
        return a / b;
    }

    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Please provide exactly two numeric arguments.");
            return;
        }

        if (!double.TryParse(args[0], out double num1) || !double.TryParse(args[1], out double num2))
        {
            Console.WriteLine("Both arguments must be valid numbers.");
            return;
        }

        try
        {
            Console.WriteLine($"Add: {Add(num1, num2)}");
            Console.WriteLine($"Subtract: {Subtract(num1, num2)}");
            Console.WriteLine($"Multiply: {Multiply(num1, num2)}");
            Console.WriteLine($"Divide: {Divide(num1, num2)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}