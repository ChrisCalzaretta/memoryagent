using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

var code = @"[HttpGet]
public ActionResult GetProducts(int pageNumber, int pageSize)
{
    return Ok();
}";

var tree = CSharpSyntaxTree.ParseText(code);
var root = tree.GetRoot();

Console.WriteLine($"Root type: {root.GetType().Name}");
Console.WriteLine($"Root is CompilationUnitSyntax: {root is CompilationUnitSyntax}");

if (root is CompilationUnitSyntax cu)
{
    Console.WriteLine($"Members count: {cu.Members.Count}");
    foreach (var member in cu.Members)
    {
        Console.WriteLine($"  Member type: {member.GetType().Name}");
        if (member is GlobalStatementSyntax gs)
        {
            Console.WriteLine($"    Statement type: {gs.Statement.GetType().Name}");
        }
    }
}

var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
Console.WriteLine($"\nDescendantNodes methods: {methods.Count()}");

if (root is CompilationUnitSyntax cu2)
{
    var topLevel = cu2.Members.OfType<MethodDeclarationSyntax>();
    Console.WriteLine($"CompilationUnit.Members methods: {topLevel.Count()}");
}

