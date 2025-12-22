namespace CodingAgent.Server.Templates.CSharp;

/// <summary>
/// Template for C# Web API (REST API)
/// </summary>
public class WebApiTemplate : ProjectTemplateBase
{
    public override string TemplateId => "csharp-webapi";
    public override string DisplayName => "C# Web API";
    public override string Language => "csharp";
    public override string ProjectType => "WebAPI";
    public override string Description => "A REST API using ASP.NET Core with Swagger";
    public override int Complexity => 5;
    
    public override string[] Keywords => new[]
    {
        "api", "rest", "webapi", "web api", "http", "endpoint",
        "service", "backend", "server", "microservice", "swagger"
    };
    
    public override string[] FolderStructure => new[]
    {
        "Controllers",
        "Services",
        "Models",
        "DTOs",
        "Middleware"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "Swashbuckle.AspNetCore",
        "Microsoft.AspNetCore.OpenApi"
    };
    
    public override Dictionary<string, string> Files => new()
    {
        ["Program.cs"] = @"using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc(""v1"", new OpenApiInfo 
    { 
        Title = ""{{ProjectName}} API"", 
        Version = ""v1"",
        Description = ""{{Description}}""
    });
});

// TODO: Register your services here
// builder.Services.AddScoped<IMyService, MyService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint(""/swagger/v1/swagger.json"", ""{{ProjectName}} API v1""));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
",
        ["Controllers/HealthController.cs"] = @"using Microsoft.AspNetCore.Mvc;

namespace {{Namespace}}.Controllers;

/// <summary>
/// Health check endpoints
/// </summary>
[ApiController]
[Route(""api/[controller]"")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if the API is healthy
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        _logger.LogInformation(""Health check requested"");
        
        return Ok(new HealthResponse
        {
            Status = ""Healthy"",
            Timestamp = DateTime.UtcNow,
            Version = ""1.0.0""
        });
    }
}

public record HealthResponse
{
    public required string Status { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Version { get; init; }
}
",
        ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>{{TargetFramework}}</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{{Namespace}}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>

</Project>
",
        ["appsettings.json"] = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}
",
        ["appsettings.Development.json"] = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Debug"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  }
}
",
        [".gitignore"] = @"bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
appsettings.*.local.json
"
    };
}



