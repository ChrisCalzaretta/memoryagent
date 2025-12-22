// Your C# code here
using System;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    
    public int Subtract(int a, int b) => a - b;
}

class Program
{
    static void Main()
    {
        Calculator calc = new Calculator();
        
        int sum = calc.Add(5, 3);
        Console.WriteLine($"Sum: {sum}");
        
        int difference = calc.Subtract(5, 3);
        Console.WriteLine($"Difference: {difference}");
    }
}