using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects ASP.NET Core Blazor patterns in C# Razor components
/// Covers: Components, Lifecycle, Data Binding, Dependency Injection, Forms, Routing, Render Modes, JS Interop
/// </summary>
public partial class BlazorPatternDetector
{
    // Azure documentation URLs
    private const string BlazorOverviewUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/";
    private const string BlazorComponentsUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/";
    private const string BlazorLifecycleUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle";
    private const string BlazorDataBindingUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding";
    private const string BlazorFormsUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/";
    private const string BlazorDIUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection";
    private const string BlazorRoutingUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing";
    private const string BlazorJSInteropUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/";
    private const string BlazorRenderModesUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes";

    /// <summary>
    /// Detects Blazor patterns in a Razor component file
    /// </summary>
    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            // Parse Razor component (contains both C# and Razor syntax)
            var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await tree.GetRootAsync(cancellationToken);

            // Detect Razor-specific patterns (using regex on source)
            patterns.AddRange(DetectRazorDirectives(sourceCode, filePath, context));
            
            // Detect C# code patterns (using Roslyn)
            patterns.AddRange(DetectComponentStructure(root, sourceCode, filePath, context));
            patterns.AddRange(DetectLifecycleMethods(root, sourceCode, filePath, context));
            patterns.AddRange(DetectParametersAndCallbacks(root, sourceCode, filePath, context));
            patterns.AddRange(DetectDependencyInjection(root, sourceCode, filePath, context));
            patterns.AddRange(DetectDataBinding(sourceCode, filePath, context));
            patterns.AddRange(DetectFormsAndValidation(root, sourceCode, filePath, context));
            patterns.AddRange(DetectJavaScriptInterop(root, sourceCode, filePath, context));
            patterns.AddRange(DetectRoutingPatterns(root, sourceCode, filePath, context));
            
            // Detect advanced patterns (CascadingValue, ErrorBoundary, Virtualize, Layouts, Generic Components, Authorization, Streaming)
            patterns.AddRange(DetectAdvancedPatterns(sourceCode, filePath, context, root));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting Blazor patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

    /// <summary>
    /// Detects Razor directives: @page, @inject, @code, @rendermode, @using, @inherits
    /// </summary>
    private List<CodePattern> DetectRazorDirectives(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // @page directive (routing)
            if (Regex.IsMatch(line, @"^@page\s+""[^""]+""", RegexOptions.IgnoreCase))
            {
                var routeMatch = Regex.Match(line, @"@page\s+""([^""]+)""");
                var route = routeMatch.Success ? routeMatch.Groups[1].Value : "";
                var hasParameters = route.Contains("{");

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_PageDirective",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Routing,
                    Implementation = $"@page \"{route}\"",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 3),
                    BestPractice = hasParameters 
                        ? "Routable component with route parameters. Ensure parameter constraints and validation."
                        : "Routable component. Marks this component as a page accessible via routing.",
                    AzureBestPracticeUrl = BlazorRoutingUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Route"] = route,
                        ["HasParameters"] = hasParameters,
                        ["PatternSubType"] = "PageDirective"
                    }
                });
            }

            // @inject directive (dependency injection)
            if (Regex.IsMatch(line, @"^@inject\s+\w+", RegexOptions.IgnoreCase))
            {
                var injectMatch = Regex.Match(line, @"@inject\s+([\w<>,\[\]\.]+)\s+(\w+)");
                if (injectMatch.Success)
                {
                    var serviceType = injectMatch.Groups[1].Value;
                    var propertyName = injectMatch.Groups[2].Value;

                    patterns.Add(new CodePattern
                    {
                        Name = "Blazor_InjectDirective",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.General,
                    Implementation = $"@inject {serviceType}",
                        Language = "C#",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        EndLineNumber = i + 1,
                        Content = GetContext(lines, i, 2),
                        BestPractice = $"Dependency injection via @inject directive for {serviceType}. Prefer constructor injection in code-behind when possible.",
                        AzureBestPracticeUrl = BlazorDIUrl,
                        Confidence = 0.95f,
                        Context = context ?? string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["ServiceType"] = serviceType,
                            ["PropertyName"] = propertyName,
                            ["PatternSubType"] = "InjectDirective"
                        }
                    });
                }
            }

            // @code directive (component logic)
            if (Regex.IsMatch(line, @"^@code\s*\{", RegexOptions.IgnoreCase))
            {
                patterns.Add(new CodePattern
                {
                    Name = "Blazor_CodeBlock",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = "@code",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 5),
                    BestPractice = "@code block for component logic. For complex components, consider using code-behind (.razor.cs) files for better separation of concerns.",
                    AzureBestPracticeUrl = BlazorComponentsUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["PatternSubType"] = "CodeBlock"
                    }
                });
            }

            // @rendermode directive (Blazor 8.0+)
            if (Regex.IsMatch(line, @"@rendermode\s+(InteractiveServer|InteractiveWebAssembly|InteractiveAuto|Static)", RegexOptions.IgnoreCase))
            {
                var renderModeMatch = Regex.Match(line, @"@rendermode\s+(\w+)");
                var mode = renderModeMatch.Success ? renderModeMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_RenderMode",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Rendering,
                    Implementation = $"@rendermode {mode}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = $"Render mode: {mode}. " + mode switch
                    {
                        "InteractiveServer" => "SignalR-based interactivity. Best for real-time updates with server state.",
                        "InteractiveWebAssembly" => "Client-side interactivity via WebAssembly. Best for offline scenarios.",
                        "InteractiveAuto" => "Automatic mode selection. Uses WASM after initial load.",
                        "Static" => "Static server-side rendering. No interactivity. Best for performance.",
                        _ => "Specifies how the component is rendered and where it executes."
                    },
                    AzureBestPracticeUrl = BlazorRenderModesUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["RenderMode"] = mode,
                        ["PatternSubType"] = "RenderMode"
                    }
                });
            }

            // @inherits directive (custom base class)
            if (Regex.IsMatch(line, @"^@inherits\s+\w+", RegexOptions.IgnoreCase))
            {
                var inheritsMatch = Regex.Match(line, @"@inherits\s+([\w<>,\[\]\.]+)");
                if (inheritsMatch.Success)
                {
                    var baseClass = inheritsMatch.Groups[1].Value;
                    var isComponentBase = baseClass.Contains("ComponentBase");

                    patterns.Add(new CodePattern
                    {
                        Name = "Blazor_InheritsDirective",
                        Type = PatternType.Blazor,
                        Category = PatternCategory.ComponentModel,
                        Implementation = $"@inherits {baseClass}",
                        Language = "C#",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        EndLineNumber = i + 1,
                        Content = GetContext(lines, i, 2),
                        BestPractice = isComponentBase 
                            ? "Custom component base class. Ensure it properly implements component lifecycle."
                            : $"Inheriting from {baseClass}. Verify it's compatible with Blazor component model.",
                        AzureBestPracticeUrl = BlazorComponentsUrl,
                        Confidence = 0.90f,
                        Context = context ?? string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["BaseClass"] = baseClass,
                            ["IsComponentBase"] = isComponentBase,
                            ["PatternSubType"] = "InheritsDirective"
                        }
                    });
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects component structure: ComponentBase inheritance, component classes
    /// </summary>
    private List<CodePattern> DetectComponentStructure(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // Detect classes inheriting from ComponentBase or IComponent
        var componentClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => 
                t.ToString().Contains("ComponentBase") || 
                t.ToString().Contains("IComponent") ||
                t.ToString().Contains("OwningComponentBase")) == true);

        foreach (var classDecl in componentClasses)
        {
            var lineNumber = sourceCode.Take(classDecl.SpanStart).Count(c => c == '\n') + 1;
            var className = classDecl.Identifier.ToString();
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.ToString() ?? "ComponentBase";

            // Check for IDisposable/IAsyncDisposable
            var implementsDisposable = classDecl.BaseList?.Types.Any(t => 
                t.ToString().Contains("IDisposable") || 
                t.ToString().Contains("IAsyncDisposable")) == true;

            patterns.Add(new CodePattern
            {
                Name = "Blazor_ComponentClass",
                Type = PatternType.Blazor,
                Category = PatternCategory.ComponentModel,
                Implementation = $"class {className} : {baseType}",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber + classDecl.ToString().Split('\n').Length - 1,
                Content = GetCodeSnippet(sourceCode, lineNumber, 10),
                BestPractice = implementsDisposable
                    ? $"Component {className} implements IDisposable. Ensure proper cleanup in Dispose method."
                    : $"Component {className} inherits from {baseType}. Consider implementing IDisposable if using subscriptions or unmanaged resources.",
                AzureBestPracticeUrl = BlazorComponentsUrl,
                Confidence = 0.95f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["ClassName"] = className,
                    ["BaseType"] = baseType,
                    ["ImplementsDisposable"] = implementsDisposable,
                    ["PatternSubType"] = "ComponentClass"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects lifecycle methods: OnInitialized, OnParametersSet, OnAfterRender, Dispose
    /// </summary>
    private List<CodePattern> DetectLifecycleMethods(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        var lifecycleMethods = new Dictionary<string, (string Description, string BestPractice)>
        {
            ["OnInitialized"] = ("Component initialization", "Use for one-time setup. For async operations, use OnInitializedAsync."),
            ["OnInitializedAsync"] = ("Async component initialization", "Use for async data loading. Called once when component is created."),
            ["OnParametersSet"] = ("Parameters set", "Called when parameters change. Avoid heavy operations here."),
            ["OnParametersSetAsync"] = ("Async parameters set", "Use for async operations when parameters change. Consider caching to avoid redundant work."),
            ["OnAfterRender"] = ("After render", "Use for JS interop after render is complete. Check firstRender parameter."),
            ["OnAfterRenderAsync"] = ("Async after render", "Use for async JS interop. Critical: Check firstRender to avoid redundant calls."),
            ["Dispose"] = ("Component disposal", "Clean up subscriptions, event handlers, and unmanaged resources."),
            ["DisposeAsync"] = ("Async disposal", "Clean up async resources. Prefer this over Dispose for async cleanup."),
            ["StateHasChanged"] = ("Manual re-render trigger", "Forces component re-render. Use sparingly - most updates are automatic."),
            ["ShouldRender"] = ("Render optimization", "Return false to prevent re-render. Use for performance optimization."),
            ["SetParametersAsync"] = ("Custom parameter processing", "Advanced: Override to customize how parameters are set.")
        };

        foreach (var (methodName, (description, bestPractice)) in lifecycleMethods)
        {
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.ToString() == methodName);

            foreach (var method in methods)
            {
                var lineNumber = sourceCode.Take(method.SpanStart).Count(c => c == '\n') + 1;
                var isAsync = method.Modifiers.Any(m => m.ToString() == "async");
                var isOverride = method.Modifiers.Any(m => m.ToString() == "override");
                
                // Check for base call
                var hasBaseCall = method.Body?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(inv => inv.ToString().StartsWith("base.")) == true;

                // Special checks for OnAfterRender
                var checksFirstRender = false;
                if (methodName.Contains("OnAfterRender"))
                {
                    checksFirstRender = method.Body?.ToString().Contains("firstRender") == true;
                }

                patterns.Add(new CodePattern
                {
                    Name = $"Blazor_{methodName}",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Lifecycle,
                    Implementation = methodName,
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    EndLineNumber = lineNumber + method.ToString().Split('\n').Length - 1,
                    Content = GetCodeSnippet(sourceCode, lineNumber, 8),
                    BestPractice = bestPractice + 
                        (methodName.Contains("OnAfterRender") && !checksFirstRender 
                            ? " WARNING: Should check firstRender parameter to avoid redundant calls." 
                            : "") +
                        (isOverride && !hasBaseCall && !methodName.Contains("Dispose") && !methodName.Contains("StateHasChanged") && !methodName.Contains("ShouldRender")
                            ? " WARNING: Missing base.{methodName}() call." 
                            : ""),
                    AzureBestPracticeUrl = BlazorLifecycleUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["MethodName"] = methodName,
                        ["IsAsync"] = isAsync,
                        ["IsOverride"] = isOverride,
                        ["HasBaseCall"] = hasBaseCall,
                        ["ChecksFirstRender"] = checksFirstRender,
                        ["Description"] = description,
                        ["PatternSubType"] = "LifecycleMethod"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects component parameters and event callbacks
    /// </summary>
    private List<CodePattern> DetectParametersAndCallbacks(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // [Parameter] attribute
        var parameters = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.AttributeLists.Any(al => al.ToString().Contains("Parameter")));

        foreach (var param in parameters)
        {
            var lineNumber = sourceCode.Take(param.SpanStart).Count(c => c == '\n') + 1;
            var paramName = param.Identifier.ToString();
            var paramType = param.Type.ToString();
            
            var isEventCallback = paramType.Contains("EventCallback");
            var isRequired = param.AttributeLists.Any(al => al.ToString().Contains("EditorRequired"));
            var hasValidation = param.AttributeLists.Any(al => 
                al.ToString().Contains("Required") || 
                al.ToString().Contains("Range") ||
                al.ToString().Contains("StringLength"));

            if (isEventCallback)
            {
                patterns.Add(new CodePattern
                {
                    Name = "Blazor_EventCallback",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.EventHandling,
                    Implementation = $"EventCallback {paramName}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    EndLineNumber = lineNumber,
                    Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                    BestPractice = $"EventCallback parameter '{paramName}'. Use InvokeAsync to notify parent components of events. Ensures proper async handling and UI updates.",
                    AzureBestPracticeUrl = BlazorComponentsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ParameterName"] = paramName,
                        ["ParameterType"] = paramType,
                        ["IsRequired"] = isRequired,
                        ["PatternSubType"] = "EventCallback"
                    }
                });
            }
            else
            {
                patterns.Add(new CodePattern
                {
                    Name = "Blazor_Parameter",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = $"[Parameter] {paramType} {paramName}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    EndLineNumber = lineNumber,
                    Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                    BestPractice = $"Component parameter '{paramName}' of type {paramType}. " +
                        (isRequired ? "Marked as required." : "Consider adding [EditorRequired] for required parameters.") +
                        (hasValidation ? " Has validation attributes." : ""),
                    AzureBestPracticeUrl = BlazorComponentsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ParameterName"] = paramName,
                        ["ParameterType"] = paramType,
                        ["IsRequired"] = isRequired,
                        ["HasValidation"] = hasValidation,
                        ["PatternSubType"] = "ComponentParameter"
                    }
                });
            }
        }

        // RenderFragment parameters
        var renderFragments = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Type.ToString().Contains("RenderFragment"));

        foreach (var fragment in renderFragments)
        {
            var lineNumber = sourceCode.Take(fragment.SpanStart).Count(c => c == '\n') + 1;
            var fragmentName = fragment.Identifier.ToString();
            var fragmentType = fragment.Type.ToString();

            patterns.Add(new CodePattern
            {
                Name = "Blazor_RenderFragment",
                Type = PatternType.Blazor,
                Category = PatternCategory.ComponentModel,
                Implementation = $"{fragmentType} {fragmentName}",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = $"RenderFragment '{fragmentName}' for templated components. Allows parent to provide custom UI content.",
                AzureBestPracticeUrl = BlazorComponentsUrl,
                Confidence = 0.95f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["FragmentName"] = fragmentName,
                    ["FragmentType"] = fragmentType,
                    ["PatternSubType"] = "RenderFragment"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects dependency injection patterns
    /// </summary>
    private List<CodePattern> DetectDependencyInjection(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // [Inject] attribute on properties
        var injectedProps = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.AttributeLists.Any(al => al.ToString().Contains("Inject")));

        foreach (var prop in injectedProps)
        {
            var lineNumber = sourceCode.Take(prop.SpanStart).Count(c => c == '\n') + 1;
            var propName = prop.Identifier.ToString();
            var propType = prop.Type.ToString();

            // Check for common services
            var serviceCategory = propType switch
            {
                var t when t.Contains("HttpClient") => "HTTP Communication",
                var t when t.Contains("NavigationManager") => "Navigation",
                var t when t.Contains("IJSRuntime") => "JavaScript Interop",
                var t when t.Contains("ILogger") => "Logging",
                var t when t.Contains("AuthenticationStateProvider") => "Authentication",
                var t when t.Contains("IConfiguration") => "Configuration",
                _ => "Custom Service"
            };

            patterns.Add(new CodePattern
            {
                Name = "Blazor_InjectAttribute",
                Type = PatternType.Blazor,
                Category = PatternCategory.General,
                Implementation = $"[Inject] {propType}",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = $"Injected service '{propName}' ({serviceCategory}). " +
                    "Ensure service is registered in Program.cs/Startup.cs. " +
                    "For .razor files, consider using @inject directive instead for better clarity.",
                AzureBestPracticeUrl = BlazorDIUrl,
                Confidence = 0.95f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["PropertyName"] = propName,
                    ["ServiceType"] = propType,
                    ["ServiceCategory"] = serviceCategory,
                    ["PatternSubType"] = "InjectAttribute"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects data binding patterns: @bind, @bind-value, two-way binding
    /// </summary>
    private List<CodePattern> DetectDataBinding(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // @bind directive (two-way binding)
            if (Regex.IsMatch(line, @"@bind[:\-]", RegexOptions.IgnoreCase))
            {
                var bindMatch = Regex.Match(line, @"@bind(?:[:\-](\w+))?(?:[:=]([^"">\s]+))?");
                var bindType = bindMatch.Groups[1].Success ? bindMatch.Groups[1].Value : "value";
                var hasAfterEvent = line.Contains("@bind:after") || line.Contains("@bind-value:after");
                var hasFormat = line.Contains("@bind:format") || line.Contains("@bind-value:format");

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_DataBinding",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.DataBinding,
                    Implementation = hasAfterEvent ? "@bind with :after" : "@bind",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 3),
                    BestPractice = "Two-way data binding with @bind. " +
                        (hasAfterEvent ? "Uses @bind:after for change notifications (recommended)." : "Consider @bind:after for better control over when updates occur.") +
                        (hasFormat ? " Uses custom format." : ""),
                    AzureBestPracticeUrl = BlazorDataBindingUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["BindType"] = bindType,
                        ["HasAfterEvent"] = hasAfterEvent,
                        ["HasFormat"] = hasFormat,
                        ["PatternSubType"] = "TwoWayBinding"
                    }
                });
            }

            // @onclick, @onchange, @oninput event handlers
            if (Regex.IsMatch(line, @"@on(click|change|input|submit|focus|blur|keydown|keyup|keypress)", RegexOptions.IgnoreCase))
            {
                var eventMatch = Regex.Match(line, @"@on(\w+)\s*=\s*[""']?([^""'>]+)", RegexOptions.IgnoreCase);
                if (eventMatch.Success)
                {
                    var eventName = eventMatch.Groups[1].Value;
                    var handler = eventMatch.Groups[2].Value;
                    var isAsync = handler.Contains("Async") || line.Contains("async");
                    var preventsDefault = line.Contains(":preventDefault");
                    var stopsProgagation = line.Contains(":stopPropagation");

                    patterns.Add(new CodePattern
                    {
                        Name = "Blazor_EventHandler",
                        Type = PatternType.Blazor,
                        Category = PatternCategory.EventHandling,
                        Implementation = $"@on{eventName}",
                        Language = "C#",
                        FilePath = filePath,
                        LineNumber = i + 1,
                        EndLineNumber = i + 1,
                        Content = GetContext(lines, i, 2),
                        BestPractice = $"Event handler for '{eventName}' event. " +
                            (isAsync ? "Uses async handler (recommended for async operations)." : "") +
                            (preventsDefault ? " Prevents default browser behavior." : "") +
                            (stopsProgagation ? " Stops event propagation." : ""),
                        AzureBestPracticeUrl = BlazorComponentsUrl,
                        Confidence = 0.92f,
                        Context = context ?? string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["EventName"] = eventName,
                            ["Handler"] = handler,
                            ["IsAsync"] = isAsync,
                            ["PreventDefault"] = preventsDefault,
                            ["StopPropagation"] = stopsProgagation,
                            ["PatternSubType"] = "EventHandler"
                        }
                    });
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects forms and validation patterns
    /// </summary>
    private List<CodePattern> DetectFormsAndValidation(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // EditForm component
            if (Regex.IsMatch(line, @"<EditForm", RegexOptions.IgnoreCase))
            {
                var hasModel = line.Contains("Model=") || line.Contains("@bind-Model");
                var hasOnValidSubmit = line.Contains("OnValidSubmit") || sourceCode.Contains("OnValidSubmit");
                var hasOnInvalidSubmit = line.Contains("OnInvalidSubmit") || sourceCode.Contains("OnInvalidSubmit");

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_EditForm",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Forms,
                    Implementation = "EditForm",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 5),
                    BestPractice = "EditForm for data entry. " +
                        (hasModel ? "Uses Model binding." : "WARNING: Missing Model binding.") +
                        (hasOnValidSubmit ? " Has OnValidSubmit handler." : " Consider OnValidSubmit for form processing.") +
                        (hasOnInvalidSubmit ? " Has OnInvalidSubmit for validation errors." : ""),
                    AzureBestPracticeUrl = BlazorFormsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["HasModel"] = hasModel,
                        ["HasOnValidSubmit"] = hasOnValidSubmit,
                        ["HasOnInvalidSubmit"] = hasOnInvalidSubmit,
                        ["PatternSubType"] = "EditForm"
                    }
                });
            }

            // DataAnnotationsValidator
            if (Regex.IsMatch(line, @"<DataAnnotationsValidator", RegexOptions.IgnoreCase))
            {
                patterns.Add(new CodePattern
                {
                    Name = "Blazor_DataAnnotationsValidator",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Validation,
                    Implementation = "DataAnnotationsValidator",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = "DataAnnotationsValidator enables validation using data annotations on model properties. Ensure model has appropriate [Required], [Range], [StringLength] attributes.",
                    AzureBestPracticeUrl = BlazorFormsUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["PatternSubType"] = "Validator"
                    }
                });
            }

            // ValidationSummary / ValidationMessage
            if (Regex.IsMatch(line, @"<Validation(Summary|Message)", RegexOptions.IgnoreCase))
            {
                var isMessage = line.Contains("ValidationMessage");
                var forMatch = Regex.Match(line, @"For=""@\(\(\)\s*=>\s*([^)]+)\)""");
                var fieldName = forMatch.Success ? forMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = isMessage ? "Blazor_ValidationMessage" : "Blazor_ValidationSummary",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Validation,
                    Implementation = isMessage ? "ValidationMessage" : "ValidationSummary",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = isMessage 
                        ? $"Field-specific validation message{(string.IsNullOrEmpty(fieldName) ? "" : $" for {fieldName}")}. Shows errors for individual fields."
                        : "ValidationSummary displays all validation errors. Place inside EditForm for best results.",
                    AzureBestPracticeUrl = BlazorFormsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["IsMessage"] = isMessage,
                        ["FieldName"] = fieldName,
                        ["PatternSubType"] = isMessage ? "ValidationMessage" : "ValidationSummary"
                    }
                });
            }

            // Input components (InputText, InputNumber, InputDate, InputCheckbox, InputSelect)
            if (Regex.IsMatch(line, @"<Input(Text|Number|Date|CheckBox|Select|TextArea|File)", RegexOptions.IgnoreCase))
            {
                var inputMatch = Regex.Match(line, @"<Input(\w+)", RegexOptions.IgnoreCase);
                var inputType = inputMatch.Success ? inputMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = $"Blazor_Input{inputType}",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Forms,
                    Implementation = $"Input{inputType}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = $"Input{inputType} component for form binding. Automatically integrates with EditForm validation. Use @bind-Value for two-way binding.",
                    AzureBestPracticeUrl = BlazorFormsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["InputType"] = inputType,
                        ["PatternSubType"] = "InputComponent"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects JavaScript interop patterns
    /// </summary>
    private List<CodePattern> DetectJavaScriptInterop(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // IJSRuntime usage
        var jsRuntimeUsages = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("InvokeAsync") || inv.ToString().Contains("InvokeVoidAsync"));

        foreach (var invocation in jsRuntimeUsages)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var isVoid = invocation.ToString().Contains("InvokeVoidAsync");
            var methodText = invocation.ToString();
            
            // Extract JS function name
            var functionMatch = Regex.Match(methodText, @"Invoke(?:Void)?Async(?:<[^>]+>)?\s*\(\s*[""']([^""']+)[""']");
            var jsFunction = functionMatch.Success ? functionMatch.Groups[1].Value : "";

            // Check if in OnAfterRender
            var method = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var inOnAfterRender = method?.Identifier.ToString().Contains("OnAfterRender") == true;

            patterns.Add(new CodePattern
            {
                Name = "Blazor_JSInterop",
                Type = PatternType.Blazor,
                Category = PatternCategory.JavaScriptInterop,
                Implementation = isVoid ? "InvokeVoidAsync" : "InvokeAsync",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = $"JavaScript interop calling '{jsFunction}'. " +
                    (inOnAfterRender ? "Correctly placed in OnAfterRender." : "WARNING: Ensure DOM is ready before calling JS. Use OnAfterRender lifecycle method.") +
                    (isVoid ? " Uses InvokeVoidAsync (no return value)." : " Uses InvokeAsync with return value."),
                AzureBestPracticeUrl = BlazorJSInteropUrl,
                Confidence = 0.90f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["JSFunction"] = jsFunction,
                    ["IsVoid"] = isVoid,
                    ["InOnAfterRender"] = inOnAfterRender,
                    ["PatternSubType"] = "JSInterop"
                }
            });
        }

        // [JSImport] / [JSExport] attributes (Blazor 8.0+)
        var jsImportMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.AttributeLists.Any(al => al.ToString().Contains("JSImport") || al.ToString().Contains("JSExport")));

        foreach (var method in jsImportMethods)
        {
            var lineNumber = sourceCode.Take(method.SpanStart).Count(c => c == '\n') + 1;
            var isImport = method.AttributeLists.Any(al => al.ToString().Contains("JSImport"));
            var methodName = method.Identifier.ToString();

            patterns.Add(new CodePattern
            {
                Name = isImport ? "Blazor_JSImport" : "Blazor_JSExport",
                Type = PatternType.Blazor,
                Category = PatternCategory.JavaScriptInterop,
                Implementation = isImport ? "[JSImport]" : "[JSExport]",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 5),
                BestPractice = isImport 
                    ? $"JSImport for '{methodName}'. Direct JS function import (Blazor 8.0+). Better performance than IJSRuntime."
                    : $"JSExport for '{methodName}'. Exports C# method to JavaScript. Enables JS to call .NET directly.",
                AzureBestPracticeUrl = BlazorJSInteropUrl,
                Confidence = 0.98f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["MethodName"] = methodName,
                    ["IsImport"] = isImport,
                    ["PatternSubType"] = isImport ? "JSImport" : "JSExport"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects routing patterns: NavigationManager usage
    /// </summary>
    private List<CodePattern> DetectRoutingPatterns(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // NavigationManager.NavigateTo usage
        var navigateCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("NavigateTo"));

        foreach (var invocation in navigateCalls)
        {
            var lineNumber = sourceCode.Take(invocation.SpanStart).Count(c => c == '\n') + 1;
            var invocationText = invocation.ToString();
            var forceLoad = invocationText.Contains("forceLoad: true") || invocationText.Contains(", true");

            patterns.Add(new CodePattern
            {
                Name = "Blazor_NavigateTo",
                Type = PatternType.Blazor,
                Category = PatternCategory.Routing,
                Implementation = "NavigationManager.NavigateTo",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = "Programmatic navigation using NavigationManager. " +
                    (forceLoad ? "Uses forceLoad=true (full page reload). Only use when necessary." : "Client-side navigation (recommended)."),
                AzureBestPracticeUrl = BlazorRoutingUrl,
                Confidence = 0.92f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["ForceLoad"] = forceLoad,
                    ["PatternSubType"] = "NavigateTo"
                }
            });
        }

        return patterns;
    }

    // Helper methods
    private string GetContext(string[] lines, int index, int contextLines)
    {
        var start = Math.Max(0, index - contextLines);
        var end = Math.Min(lines.Length - 1, index + contextLines);
        return string.Join("\n", lines[start..(end + 1)]);
    }

    private string GetCodeSnippet(string sourceCode, int lineNumber, int contextLines)
    {
        var lines = sourceCode.Split('\n');
        var start = Math.Max(0, lineNumber - 1 - contextLines);
        var end = Math.Min(lines.Length - 1, lineNumber - 1 + contextLines);
        return string.Join("\n", lines[start..(end + 1)]);
    }
}

