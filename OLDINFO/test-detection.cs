using MemoryAgent.Server.CodeAnalysis;

var detector = new CSharpPatternDetectorEnhanced();

var code1 = @"
[HttpGet]
public ActionResult GetProducts(int pageNumber, int pageSize)
{
    return Ok();
}";

var code2 = @"
public class ProductsController
{
    [HttpGet]
    public ActionResult GetProducts(int pageNumber, int pageSize)
    {
        return Ok();
    }
}";

var patterns1 = detector.DetectPatterns(code1, "Test.cs", "test");
var patterns2 = detector.DetectPatterns(code2, "Test.cs", "test");

Console.WriteLine($"Code without class wrapper: {patterns1.Count} patterns");
Console.WriteLine($"Code with class wrapper: {patterns2.Count} patterns");

foreach (var p in patterns2)
{
    Console.WriteLine($"  - {p.Name}");
}

