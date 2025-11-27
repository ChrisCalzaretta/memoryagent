using Xunit;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MemoryAgent.Server.Tests;

public class PluginArchitecturePatternDetectorTests
{
    private readonly PluginArchitecturePatternDetector _detector;
    private readonly Mock<ILogger<PluginArchitecturePatternDetector>> _mockLogger;

    public PluginArchitecturePatternDetectorTests()
    {
        _mockLogger = new Mock<ILogger<PluginArchitecturePatternDetector>>();
        _detector = new PluginArchitecturePatternDetector(_mockLogger.Object);
    }

    private async Task<List<CodePattern>> Detect(string code, string filePath = "test.cs")
    {
        return await _detector.DetectPatternsAsync(filePath, "test_context", code);
    }

    // ===== CATEGORY 1: Plugin Loading & Isolation Patterns =====

    [Fact]
    public async Task Should_Detect_AssemblyLoadContext()
    {
        var code = @"
using System.Runtime.Loader;
using System.Reflection;

public class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;
    
    public PluginLoadContext(string pluginPath)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }
    
    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null) return LoadFromAssemblyPath(assemblyPath);
        return null;
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_AssemblyLoadContext");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.PluginArchitecture, pattern.Type);
        Assert.Equal(PatternCategory.PluginLoading, pattern.Category);
        Assert.True((bool)pattern.Metadata["HasLoadOverride"]);
    }

    [Fact]
    public async Task Should_Detect_AssemblyDependencyResolver()
    {
        var code = @"
using System.Runtime.Loader;

public class MyClass
{
    public void LoadPlugin(string pluginPath)
    {
        var resolver = new AssemblyDependencyResolver(pluginPath);
        string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
    }
}";
        var patterns = await Detect(code);
        var resolverPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_AssemblyDependencyResolver");
        var resolutionPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_DependencyResolution");
        Assert.NotNull(resolverPattern);
        Assert.NotNull(resolutionPattern);
        Assert.All(new[] { resolverPattern, resolutionPattern }, p =>
        {
            Assert.Equal(PatternType.PluginArchitecture, p.Type);
            Assert.Equal(PatternCategory.PluginLoading, p.Category);
        });
    }

    [Fact]
    public async Task Should_Detect_EnableDynamicLoading()
    {
        var code = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
</Project>";
        var patterns = await Detect(code, "test.csproj");
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_EnableDynamicLoading");
        Assert.NotNull(pattern);
        Assert.Equal(PatternType.PluginArchitecture, pattern.Type);
        Assert.Equal(PatternCategory.PluginLoading, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_CollectibleLoadContext()
    {
        var code = @"
using System.Runtime.Loader;

public class MyLoader
{
    public void LoadCollectible(string path)
    {
        var loadContext = new AssemblyLoadContext(path, isCollectible: true);
        loadContext.Unload();
    }
}";
        var patterns = await Detect(code);
        var collectiblePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_CollectibleLoadContext");
        var unloadPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_DynamicUnload");
        Assert.NotNull(collectiblePattern);
        Assert.NotNull(unloadPattern);
    }

    [Fact]
    public async Task Should_Detect_PrivateFalseReference()
    {
        var code = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\PluginBase\PluginBase.csproj"">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>
</Project>";
        var patterns = await Detect(code, "plugin.csproj");
        var privatePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_PrivateFalseReference");
        var excludePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_ExcludeRuntimeAssets");
        Assert.NotNull(privatePattern);
        Assert.NotNull(excludePattern);
    }

    [Fact]
    public async Task Should_Detect_NativeLibraryLoading()
    {
        var code = @"
using System.Runtime.Loader;
using System;

public class PluginLoadContext : AssemblyLoadContext
{
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null) return LoadUnmanagedDllFromPath(libraryPath);
        return IntPtr.Zero;
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_NativeLibraryLoading");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginLoading, pattern.Category);
    }

    // ===== CATEGORY 2: Plugin Discovery & Composition Patterns =====

    [Fact]
    public async Task Should_Detect_MEFCatalog()
    {
        var code = @"
using System.ComponentModel.Composition.Hosting;

public class PluginLoader
{
    public void LoadPlugins()
    {
        var catalog = new DirectoryCatalog(""./plugins"");
        var container = new CompositionContainer(catalog);
    }
}";
        var patterns = await Detect(code);
        var catalogPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_MEFCatalog");
        var containerPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_MEFCompositionContainer");
        Assert.NotNull(catalogPattern);
        Assert.NotNull(containerPattern);
        Assert.All(new[] { catalogPattern, containerPattern }, p =>
        {
            Assert.Equal(PatternType.PluginArchitecture, p.Type);
            Assert.Equal(PatternCategory.PluginComposition, p.Category);
        });
    }

    [Fact]
    public async Task Should_Detect_MEFImportExport()
    {
        var code = @"
using System.ComponentModel.Composition;

[Export(typeof(IPlugin))]
public class HelloPlugin : IPlugin
{
    public string Name => ""Hello"";
}

public class Host
{
    [ImportMany]
    public IEnumerable<IPlugin> Plugins { get; set; }
}";
        var patterns = await Detect(code);
        var exportPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_MEFExport");
        var importPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_MEFImport");
        Assert.NotNull(exportPattern);
        Assert.NotNull(importPattern);
    }

    [Fact]
    public async Task Should_Detect_MEFMetadata()
    {
        var code = @"
using System.ComponentModel.Composition;

[Export(typeof(IPlugin))]
[ExportMetadata(""Version"", ""1.0.0"")]
[ExportMetadata(""Priority"", 10)]
public class MyPlugin : IPlugin
{
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_MEFMetadata");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginComposition, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_LazyPluginLoading()
    {
        var code = @"
using System;
using System.ComponentModel.Composition;

public class PluginHost
{
    [ImportMany]
    public IEnumerable<Lazy<IPlugin, IPluginMetadata>> LazyPlugins { get; set; }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_LazyLoading");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginComposition, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginRegistry()
    {
        var code = @"
public interface IPluginRegistry
{
    void Register(string name, IPlugin plugin);
    IPlugin Get(string name);
    void Unregister(string name);
}

public class PluginManager : IPluginRegistry
{
    public void Register(string name, IPlugin plugin) { }
    public IPlugin Get(string name) => null;
    public void Unregister(string name) { }
}";
        var patterns = await Detect(code);
        var interfacePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_RegistryInterface");
        var implPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_RegistryImplementation");
        Assert.NotNull(interfacePattern);
        Assert.NotNull(implPattern);
    }

    [Fact]
    public async Task Should_Detect_TypeScanning()
    {
        var code = @"
using System;
using System.Reflection;
using System.Linq;

public class PluginLoader
{
    public void LoadPlugins(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t)))
        {
            var plugin = Activator.CreateInstance(type) as IPlugin;
        }
    }
}";
        var patterns = await Detect(code);
        var scanPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_TypeScanning");
        var activationPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_DynamicActivation");
        Assert.NotNull(scanPattern);
        Assert.NotNull(activationPattern);
    }

    [Fact]
    public async Task Should_Detect_ConfigurationBasedDiscovery()
    {
        var code = @"
{
  ""plugins"": [
    {
      ""name"": ""HelloPlugin"",
      ""path"": ""./plugins/HelloPlugin.dll"",
      ""enabled"": true
    }
  ]
}";
        var patterns = await Detect(code, "plugins.json");
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_ConfigurationDiscovery");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginComposition, pattern.Category);
    }

    // ===== CATEGORY 3: Plugin Lifecycle Management Patterns =====

    [Fact]
    public async Task Should_Detect_IPluginInterface()
    {
        var code = @"
using System;
using System.Threading.Tasks;

public interface IPlugin : IDisposable
{
    string Name { get; }
    void Initialize(IServiceProvider services);
    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_InterfaceContract");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginLifecycle, pattern.Category);
        Assert.True((bool)pattern.Metadata["HasLifecycleMethods"]);
    }

    [Fact]
    public async Task Should_Detect_StatelessPlugin()
    {
        var code = @"
public class StatelessPlugin : IPlugin
{
    private readonly ILogger _logger;
    
    public StatelessPlugin(ILogger logger)
    {
        _logger = logger;
    }
    
    public Task ExecuteAsync(ExecutionContext context)
    {
        // Stateless - no mutable fields
        return Task.CompletedTask;
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_StatelessDesign");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginLifecycle, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginHealthCheck()
    {
        var code = @"
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class PluginHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        return HealthCheckResult.Healthy();
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_HealthCheck");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginLifecycle, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginStartStop()
    {
        var code = @"
using Microsoft.Extensions.Hosting;

public class MyPlugin : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) { }
    public async Task StopAsync(CancellationToken cancellationToken) { }
}";
        var patterns = await Detect(code);
        var hostedPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_HostedService");
        var startStopPattern = patterns.Where(p => p.Name == "Plugin_StartStopLifecycle").ToList();
        Assert.NotNull(hostedPattern);
        Assert.Equal(2, startStopPattern.Count); // StartAsync and StopAsync
    }

    [Fact]
    public async Task Should_Detect_PluginDependencyInjection()
    {
        var code = @"
using Microsoft.Extensions.Logging;

public class MyPlugin : IPlugin
{
    private readonly ILogger<MyPlugin> _logger;
    private readonly IServiceProvider _services;
    
    public MyPlugin(ILogger<MyPlugin> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_DependencyInjection");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginLifecycle, pattern.Category);
    }

    // ===== CATEGORY 4: Plugin Communication Patterns =====

    [Fact]
    public async Task Should_Detect_EventBus()
    {
        var code = @"
using System;

public interface IEventBus
{
    void Publish<TEvent>(TEvent @event);
    void Subscribe<TEvent>(Action<TEvent> handler);
}

public class EventBus : IEventBus
{
    public void Publish<TEvent>(TEvent @event) { }
    public void Subscribe<TEvent>(Action<TEvent> handler) { }
}";
        var patterns = await Detect(code);
        var interfacePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_EventBus");
        var methodPatterns = patterns.Where(p => p.Name == "Plugin_PubSubMessaging").ToList();
        Assert.NotNull(interfacePattern);
        Assert.True(methodPatterns.Count >= 2); // Publish and Subscribe
        Assert.Equal(PatternCategory.PluginCommunication, interfacePattern.Category);
    }

    [Fact]
    public async Task Should_Detect_SharedServiceInterface()
    {
        var code = @"
using System.ComponentModel.Composition;

[Export(typeof(INotificationService))]
public class EmailPlugin : INotificationService
{
    public Task SendNotificationAsync(string message) => Task.CompletedTask;
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_SharedServiceInterface");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginCommunication, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginPipeline()
    {
        var code = @"
public interface IPluginPipeline
{
    IPluginPipeline Use(IPlugin plugin);
    Task InvokeAsync(PluginContext context);
}

public class PluginPipeline : IPluginPipeline
{
    public IPluginPipeline Use(IPlugin plugin) => this;
    public Task InvokeAsync(PluginContext context) => Task.CompletedTask;
}";
        var patterns = await Detect(code);
        var interfacePattern = patterns.FirstOrDefault(p => p.Name == "Plugin_PipelineInterface");
        var methodPattern = patterns.FirstOrDefault(p => p.Name == "Plugin_PipelineRegistration");
        Assert.NotNull(interfacePattern);
        Assert.NotNull(methodPattern);
        Assert.Equal(PatternCategory.PluginCommunication, interfacePattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginContext()
    {
        var code = @"
using System.Collections.Generic;

public class PluginContext
{
    public string CorrelationId { get; set; }
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_ContextObject");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginCommunication, pattern.Category);
        Assert.True((bool)pattern.Metadata["HasCorrelationId"]);
        Assert.True((bool)pattern.Metadata["HasProperties"]);
    }

    // ===== CATEGORY 5: Plugin Security & Governance Patterns =====

    [Fact]
    public async Task Should_Detect_GatekeeperPattern()
    {
        var code = @"
public class PluginGatekeeperMiddleware
{
    public async Task InvokeAsync(PluginContext context, IPlugin plugin)
    {
        if (!await AuthorizePluginAsync(plugin))
            throw new UnauthorizedAccessException();
        await plugin.ExecuteAsync(context);
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_GatekeeperPattern");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginSecurity, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_Sandboxing()
    {
        var code = @"
using System.Diagnostics;

public class PluginRunner
{
    public void RunInIsolation(string pluginPath)
    {
        var processInfo = new ProcessStartInfo(""dotnet"", $""run --plugin {pluginPath}"");
        Process.Start(processInfo);
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_ProcessIsolation");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginSecurity, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_CircuitBreakerPlugin()
    {
        var code = @"
using Polly;

public class PluginExecutor
{
    private readonly IAsyncPolicy _circuitBreaker;
    
    public PluginExecutor()
    {
        _circuitBreaker = Policy.Handle<Exception>().CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
    }
    
    public async Task ExecutePluginAsync(IPlugin plugin)
    {
        await _circuitBreaker.ExecuteAsync(() => plugin.ExecuteAsync(context));
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_CircuitBreaker");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginSecurity, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_BulkheadIsolation()
    {
        var code = @"
using Polly;

public class PluginExecutor
{
    public async Task ExecuteWithBulkheadAsync(IPlugin plugin)
    {
        var bulkhead = Policy.BulkheadAsync(maxParallelization: 10, maxQueuingActions: 20);
        await bulkhead.ExecuteAsync(() => plugin.ExecuteAsync(context));
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_BulkheadIsolation");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginSecurity, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_PluginSignatureVerification()
    {
        var code = @"
using System.Reflection;

public class PluginValidator
{
    public void ValidateSignature(string pluginPath)
    {
        var assemblyName = AssemblyName.GetAssemblyName(pluginPath);
        if (assemblyName.GetPublicKey() == null)
            throw new SecurityException(""Plugin not signed"");
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_SignatureVerification");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginSecurity, pattern.Category);
    }

    // ===== CATEGORY 6: Plugin Versioning & Compatibility Patterns =====

    [Fact]
    public async Task Should_Detect_SemanticVersioning()
    {
        var code = @"
[assembly: AssemblyVersion(""2.0.0"")]
[assembly: AssemblyFileVersion(""2.1.3"")]";
        var patterns = await Detect(code);
        var versionPatterns = patterns.Where(p => p.Name == "Plugin_SemanticVersioning").ToList();
        Assert.Equal(2, versionPatterns.Count); // AssemblyVersion and AssemblyFileVersion
        Assert.All(versionPatterns, p => Assert.Equal(PatternCategory.PluginVersioning, p.Category));
    }

    [Fact]
    public async Task Should_Detect_CompatibilityMatrix()
    {
        var code = @"
using System.ComponentModel.Composition;

[Export(typeof(IPlugin))]
[ExportMetadata(""MinHostVersion"", ""1.5.0"")]
[ExportMetadata(""MaxHostVersion"", ""2.0.0"")]
public class MyPlugin : IPlugin
{
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_CompatibilityMatrix");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginVersioning, pattern.Category);
    }

    [Fact]
    public async Task Should_Detect_SideBySideVersioning()
    {
        var code = @"
public class PluginHost
{
    public void LoadMultipleVersions()
    {
        var context_v1 = new PluginLoadContext(""./plugins/MyPlugin/v1.0/MyPlugin.dll"");
        var context_v2 = new PluginLoadContext(""./plugins/MyPlugin/v2.0/MyPlugin.dll"");
        var context_v3 = new PluginLoadContext(""./plugins/MyPlugin/v3.0/MyPlugin.dll"");
    }
}";
        var patterns = await Detect(code);
        var pattern = patterns.FirstOrDefault(p => p.Name == "Plugin_SideBySideVersioning");
        Assert.NotNull(pattern);
        Assert.Equal(PatternCategory.PluginVersioning, pattern.Category);
    }
}

