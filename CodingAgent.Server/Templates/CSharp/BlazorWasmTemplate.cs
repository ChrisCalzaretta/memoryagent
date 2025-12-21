namespace CodingAgent.Server.Templates.CSharp;

/// <summary>
/// Template for Blazor WebAssembly Application
/// </summary>
public class BlazorWasmTemplate : ProjectTemplateBase
{
    public override string TemplateId => "csharp-blazor-wasm";
    public override string DisplayName => "Blazor WebAssembly";
    public override string Language => "csharp";
    public override string ProjectType => "BlazorWasm";
    public override string Description => "A Blazor WebAssembly single-page application";
    public override int Complexity => 7;
    
    public override string[] Keywords => new[]
    {
        "blazor", "wasm", "webassembly", "spa", "single page",
        "frontend", "web app", "pwa", "client-side", "browser"
    };
    
    public override string[] FolderStructure => new[]
    {
        "Components",
        "Pages",
        "Services",
        "Models",
        "Shared",
        "wwwroot",
        "wwwroot/css"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "Microsoft.AspNetCore.Components.WebAssembly"
    };
    
    public override Dictionary<string, string> Files => new()
    {
        ["Program.cs"] = @"using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using {{Namespace}};

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>(""#app"");
builder.RootComponents.Add<HeadOutlet>(""head::after"");

builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
});

// TODO: Register your services here
// builder.Services.AddScoped<IMyService, MyService>();

await builder.Build().RunAsync();
",
        ["App.razor"] = @"<Router AppAssembly=""@typeof(App).Assembly"">
    <Found Context=""routeData"">
        <RouteView RouteData=""@routeData"" DefaultLayout=""@typeof(Shared.MainLayout)"" />
        <FocusOnNavigate RouteData=""@routeData"" Selector=""h1"" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout=""@typeof(Shared.MainLayout)"">
            <p role=""alert"">Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
",
        ["_Imports.razor"] = @"@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using {{Namespace}}
@using {{Namespace}}.Shared
",
        ["Shared/MainLayout.razor"] = @"@inherits LayoutComponentBase

<div class=""page"">
    <div class=""sidebar"">
        <NavMenu />
    </div>

    <main>
        <div class=""top-row px-4"">
            <a href=""https://learn.microsoft.com/aspnet/core/"" target=""_blank"">About</a>
        </div>

        <article class=""content px-4"">
            @Body
        </article>
    </main>
</div>
",
        ["Shared/NavMenu.razor"] = @"<div class=""top-row ps-3 navbar navbar-dark"">
    <div class=""container-fluid"">
        <a class=""navbar-brand"" href="""">{{ProjectName}}</a>
        <button title=""Navigation menu"" class=""navbar-toggler"" @onclick=""ToggleNavMenu"">
            <span class=""navbar-toggler-icon""></span>
        </button>
    </div>
</div>

<div class=""@NavMenuCssClass nav-scrollable"" @onclick=""ToggleNavMenu"">
    <nav class=""flex-column"">
        <div class=""nav-item px-3"">
            <NavLink class=""nav-link"" href="""" Match=""NavLinkMatch.All"">
                <span class=""bi bi-house-door-fill-nav-menu"" aria-hidden=""true""></span> Home
            </NavLink>
        </div>
    </nav>
</div>

@code {
    private bool collapseNavMenu = true;
    private string? NavMenuCssClass => collapseNavMenu ? ""collapse"" : null;
    
    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }
}
",
        ["Pages/Index.razor"] = @"@page ""/""

<PageTitle>Home</PageTitle>

<h1>Welcome to {{ProjectName}}</h1>

<p>{{Description}}</p>
",
        ["wwwroot/index.html"] = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{{ProjectName}}</title>
    <base href=""/"" />
    <link href=""css/app.css"" rel=""stylesheet"" />
    <link href=""{{ProjectName}}.styles.css"" rel=""stylesheet"" />
</head>
<body>
    <div id=""app"">
        <svg class=""loading-progress"">
            <circle r=""40%"" cx=""50%"" cy=""50%"" />
            <circle r=""40%"" cx=""50%"" cy=""50%"" />
        </svg>
        <div class=""loading-progress-text""></div>
    </div>

    <div id=""blazor-error-ui"">
        An unhandled error has occurred.
        <a href="""" class=""reload"">Reload</a>
        <a class=""dismiss"">🗙</a>
    </div>
    <script src=""_framework/blazor.webassembly.js""></script>
</body>
</html>
",
        ["wwwroot/css/app.css"] = @"html, body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

h1:focus {
    outline: none;
}

.page {
    position: relative;
    display: flex;
    flex-direction: column;
}

main {
    flex: 1;
}

.sidebar {
    background-image: linear-gradient(180deg, rgb(5, 39, 103) 0%, #3a0647 70%);
}

.top-row {
    background-color: #f7f7f7;
    border-bottom: 1px solid #d6d5d5;
    justify-content: flex-end;
    height: 3.5rem;
    display: flex;
    align-items: center;
}

.content {
    padding-top: 1.1rem;
}

#blazor-error-ui {
    background: lightyellow;
    bottom: 0;
    box-shadow: 0 -1px 2px rgba(0, 0, 0, 0.2);
    display: none;
    left: 0;
    padding: 0.6rem 1.25rem 0.7rem 1.25rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
}

#blazor-error-ui .dismiss {
    cursor: pointer;
    position: absolute;
    right: 0.75rem;
    top: 0.5rem;
}

.loading-progress {
    position: relative;
    display: block;
    width: 8rem;
    height: 8rem;
    margin: 20vh auto 1rem auto;
}
",
        ["{{ProjectName}}.csproj"] = @"<Project Sdk=""Microsoft.NET.Sdk.BlazorWebAssembly"">

  <PropertyGroup>
    <TargetFramework>{{TargetFramework}}</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{{Namespace}}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.WebAssembly"" Version=""9.0.0"" />
    <PackageReference Include=""Microsoft.AspNetCore.Components.WebAssembly.DevServer"" Version=""9.0.0"" PrivateAssets=""all"" />
  </ItemGroup>

</Project>
",
        [".gitignore"] = @"bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
"
    };
}

