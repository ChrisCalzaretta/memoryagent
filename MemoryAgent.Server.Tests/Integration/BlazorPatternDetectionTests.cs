using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Xunit;

namespace MemoryAgent.Server.Tests.Integration;

/// <summary>
/// Integration tests for Blazor pattern detection
/// Tests: Components, Lifecycle, Data Binding, Forms, Routing, JS Interop, Render Modes
/// </summary>
public class BlazorPatternDetectionTests
{
    [Fact]
    public async Task Blazor_DetectsPageDirective()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
@page ""/counter""
@page ""/counter/{Id:int}""

<h1>Counter</h1>
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Counter.razor", "test", code, CancellationToken.None);

        // Assert
        var pagePatterns = patterns.Where(p => p.Name == "Blazor_PageDirective").ToList();
        Assert.NotEmpty(pagePatterns);
        Assert.Contains(pagePatterns, p => p.Implementation.Contains("/counter"));
        Assert.Contains(pagePatterns, p => p.Metadata.ContainsKey("HasParameters") && (bool)p.Metadata["HasParameters"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsInjectDirective()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
@inject NavigationManager Navigation
@inject ILogger<Counter> Logger
@inject HttpClient Http

<h1>Component</h1>
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Component.razor", "test", code, CancellationToken.None);

        // Assert
        var injectPatterns = patterns.Where(p => p.Name == "Blazor_InjectDirective").ToList();
        Assert.True(injectPatterns.Count >= 3, $"Expected at least 3 inject directives, found {injectPatterns.Count}");
        Assert.Contains(injectPatterns, p => p.Metadata["ServiceType"].ToString().Contains("NavigationManager"));
        Assert.Contains(injectPatterns, p => p.Metadata["ServiceType"].ToString().Contains("ILogger"));
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsCodeBlock()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
<h1>Counter: @currentCount</h1>
<button @onclick=""IncrementCount"">Click me</button>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Counter.razor", "test", code, CancellationToken.None);

        // Assert
        var codePattern = patterns.FirstOrDefault(p => p.Name == "Blazor_CodeBlock");
        Assert.NotNull(codePattern);
        Assert.Equal(PatternType.Blazor, codePattern.Type);
        Assert.Equal(PatternCategory.ComponentModel, codePattern.Category);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsRenderModes()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
@page ""/interactive""
@rendermode InteractiveServer

<h1>Server Interactive</h1>
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Interactive.razor", "test", code, CancellationToken.None);

        // Assert
        var renderModePattern = patterns.FirstOrDefault(p => p.Name == "Blazor_RenderMode");
        Assert.NotNull(renderModePattern);
        Assert.Equal("InteractiveServer", renderModePattern.Metadata["RenderMode"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsComponentBase()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;

namespace MyApp.Components
{
    public partial class Counter : ComponentBase, IDisposable
    {
        private int currentCount = 0;

        public void Dispose()
        {
            // Cleanup
        }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Counter.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var componentPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_ComponentClass");
        Assert.NotNull(componentPattern);
        Assert.True((bool)componentPattern.Metadata["ImplementsDisposable"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsLifecycleMethods()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;

namespace MyApp.Components
{
    public partial class DataComponent : ComponentBase
    {
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            // Load data
        }

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            // React to parameter changes
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Initialize JS
            }
            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("DataComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var lifecyclePatterns = patterns.Where(p => p.Category == PatternCategory.Lifecycle).ToList();
        Assert.True(lifecyclePatterns.Count >= 3, $"Expected at least 3 lifecycle methods, found {lifecyclePatterns.Count}");
        
        var onAfterRender = patterns.FirstOrDefault(p => p.Name == "Blazor_OnAfterRenderAsync");
        Assert.NotNull(onAfterRender);
        Assert.True((bool)onAfterRender.Metadata["ChecksFirstRender"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsParameters()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Components
{
    public partial class ChildComponent : ComponentBase
    {
        [Parameter]
        [EditorRequired]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Parameter]
        public int Count { get; set; }

        [Parameter]
        public EventCallback<string> OnValueChanged { get; set; }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("ChildComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var parameterPatterns = patterns.Where(p => p.Name == "Blazor_Parameter").ToList();
        var eventCallbackPatterns = patterns.Where(p => p.Name == "Blazor_EventCallback").ToList();
        
        Assert.True(parameterPatterns.Count >= 2, $"Expected at least 2 parameters, found {parameterPatterns.Count}");
        Assert.NotEmpty(eventCallbackPatterns);
        
        var requiredParam = parameterPatterns.FirstOrDefault(p => p.Metadata["ParameterName"].ToString() == "Title");
        Assert.NotNull(requiredParam);
        Assert.True((bool)requiredParam.Metadata["IsRequired"]);
        Assert.True((bool)requiredParam.Metadata["HasValidation"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsDataBinding()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
<input type=""text"" @bind=""searchText"" @bind:after=""OnSearchChanged"" />
<input type=""number"" @bind-value=""count"" />
<button @onclick=""HandleClick"">Click</button>
<div @onchange=""HandleChange"" @oninput=""HandleInput""></div>
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Form.razor", "test", code, CancellationToken.None);

        // Assert
        var bindingPatterns = patterns.Where(p => p.Name == "Blazor_DataBinding").ToList();
        var eventPatterns = patterns.Where(p => p.Name == "Blazor_EventHandler").ToList();
        
        Assert.True(bindingPatterns.Count >= 2, $"Expected at least 2 data bindings, found {bindingPatterns.Count}");
        Assert.True(eventPatterns.Count >= 2, $"Expected at least 2 event handlers, found {eventPatterns.Count}");
        
        var bindAfter = bindingPatterns.FirstOrDefault(p => (bool)p.Metadata["HasAfterEvent"]);
        Assert.NotNull(bindAfter);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsEditForm()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
<EditForm Model=""@model"" OnValidSubmit=""HandleValidSubmit"" OnInvalidSubmit=""HandleInvalidSubmit"">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <InputText @bind-Value=""model.Name"" />
    <ValidationMessage For=""@(() => model.Name)"" />
    
    <InputNumber @bind-Value=""model.Age"" />
    <InputDate @bind-Value=""model.BirthDate"" />
    <InputCheckBox @bind-Value=""model.IsActive"" />
    <InputSelect @bind-Value=""model.Category"">
        <option>Option 1</option>
    </InputSelect>
    
    <button type=""submit"">Submit</button>
</EditForm>
";

        // Act
        var patterns = await detector.DetectPatternsAsync("Form.razor", "test", code, CancellationToken.None);

        // Assert
        var editFormPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_EditForm");
        Assert.NotNull(editFormPattern);
        Assert.True((bool)editFormPattern.Metadata["HasModel"]);
        Assert.True((bool)editFormPattern.Metadata["HasOnValidSubmit"]);
        
        var validatorPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_DataAnnotationsValidator");
        Assert.NotNull(validatorPattern);
        
        var summaryPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_ValidationSummary");
        Assert.NotNull(summaryPattern);
        
        var inputPatterns = patterns.Where(p => p.Name.StartsWith("Blazor_Input")).ToList();
        Assert.True(inputPatterns.Count >= 5, $"Expected at least 5 input components, found {inputPatterns.Count}");
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsJavaScriptInterop()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MyApp.Components
{
    public partial class JSComponent : ComponentBase
    {
        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync(""initializeMap"", ""mapId"");
                var result = await JS.InvokeAsync<string>(""getData"");
            }
        }

        [JSImport(""myFunction"", ""myModule"")]
        public static partial Task<string> MyJSFunction();

        [JSExport]
        public static string MyExportedMethod() => ""data"";
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("JSComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var jsInteropPatterns = patterns.Where(p => p.Category == PatternCategory.JavaScriptInterop).ToList();
        Assert.True(jsInteropPatterns.Count >= 3, $"Expected at least 3 JS interop patterns, found {jsInteropPatterns.Count}");
        
        var invokePatterns = patterns.Where(p => p.Name == "Blazor_JSInterop").ToList();
        Assert.True(invokePatterns.Count >= 2, $"Expected at least 2 InvokeAsync calls, found {invokePatterns.Count}");
        Assert.True(invokePatterns.All(p => (bool)p.Metadata["InOnAfterRender"]));
        
        var importPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_JSImport");
        Assert.NotNull(importPattern);
        
        var exportPattern = patterns.FirstOrDefault(p => p.Name == "Blazor_JSExport");
        Assert.NotNull(exportPattern);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsNavigation()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;

namespace MyApp.Components
{
    public partial class NavComponent : ComponentBase
    {
        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        private void NavigateToHome()
        {
            Navigation.NavigateTo(""/home"");
        }

        private void NavigateExternal()
        {
            Navigation.NavigateTo(""https://example.com"", forceLoad: true);
        }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("NavComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var navPatterns = patterns.Where(p => p.Name == "Blazor_NavigateTo").ToList();
        Assert.True(navPatterns.Count >= 2, $"Expected at least 2 NavigateTo calls, found {navPatterns.Count}");
        
        var forceLoadNav = navPatterns.FirstOrDefault(p => (bool)p.Metadata["ForceLoad"]);
        Assert.NotNull(forceLoadNav);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsRenderFragment()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;

namespace MyApp.Components
{
    public partial class TemplateComponent : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public RenderFragment<Item>? ItemTemplate { get; set; }

        [Parameter]
        public RenderFragment? Header { get; set; }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("TemplateComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var renderFragmentPatterns = patterns.Where(p => p.Name == "Blazor_RenderFragment").ToList();
        Assert.True(renderFragmentPatterns.Count >= 3, $"Expected at least 3 RenderFragments, found {renderFragmentPatterns.Count}");
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsInjectAttribute()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;

namespace MyApp.Components
{
    public partial class ServiceComponent : ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; } = default!;

        [Inject]
        private ILogger<ServiceComponent> Logger { get; set; } = default!;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("ServiceComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var injectPatterns = patterns.Where(p => p.Name == "Blazor_InjectAttribute").ToList();
        Assert.True(injectPatterns.Count >= 3, $"Expected at least 3 [Inject] attributes, found {injectPatterns.Count}");
        
        var httpPattern = injectPatterns.FirstOrDefault(p => p.Metadata["ServiceType"].ToString().Contains("HttpClient"));
        Assert.NotNull(httpPattern);
        Assert.Equal("HTTP Communication", httpPattern.Metadata["ServiceCategory"]);
        
        await Task.Delay(1);
    }

    [Fact]
    public async Task Blazor_DetectsCompleteLifecycleWithDisposal()
    {
        // Arrange
        var detector = new BlazorPatternDetector();
        var code = @"
using Microsoft.AspNetCore.Components;
using System;

namespace MyApp.Components
{
    public partial class CompleteComponent : ComponentBase, IAsyncDisposable
    {
        protected override void OnInitialized()
        {
            base.OnInitialized();
        }

        protected override Task OnInitializedAsync()
        {
            return base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Init
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        protected override bool ShouldRender()
        {
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            // Cleanup
            await Task.CompletedTask;
        }
    }
}
";

        // Act
        var patterns = await detector.DetectPatternsAsync("CompleteComponent.razor.cs", "test", code, CancellationToken.None);

        // Assert
        var lifecyclePatterns = patterns.Where(p => p.Category == PatternCategory.Lifecycle).ToList();
        Assert.True(lifecyclePatterns.Count >= 6, $"Expected at least 6 lifecycle patterns, found {lifecyclePatterns.Count}");
        
        // Verify specific lifecycle methods
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_OnInitialized");
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_OnInitializedAsync");
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_OnParametersSet");
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_OnAfterRenderAsync");
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_ShouldRender");
        Assert.Contains(lifecyclePatterns, p => p.Name == "Blazor_DisposeAsync");
        
        await Task.Delay(1);
    }
}

