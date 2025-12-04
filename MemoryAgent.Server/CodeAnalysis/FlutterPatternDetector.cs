using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Pattern detector for Flutter framework - covers widgets, state management, performance, and best practices
/// Based on: https://docs.flutter.dev/ and Flutter performance best practices
/// </summary>
public class FlutterPatternDetector
{
    private readonly ILogger<FlutterPatternDetector>? _logger;

    public FlutterPatternDetector(ILogger<FlutterPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            // Skip non-Dart files
            if (!filePath.EndsWith(".dart", StringComparison.OrdinalIgnoreCase))
                return patterns;

            // Check if this is a Flutter file (imports flutter packages)
            var isFlutterFile = sourceCode.Contains("package:flutter/") || 
                               sourceCode.Contains("material.dart") ||
                               sourceCode.Contains("cupertino.dart") ||
                               sourceCode.Contains("widgets.dart");

            if (!isFlutterFile)
                return patterns;

            patterns.AddRange(DetectWidgetPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectStateManagementPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectPerformancePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectNavigationPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectLifecyclePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectUIPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectNetworkingPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectTestingPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectAccessibilityPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectAnimationPatterns(sourceCode, filePath, context));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error detecting Flutter patterns in {FilePath}", filePath);
        }

        return patterns;
    }

    #region Widget Patterns

    private List<CodePattern> DetectWidgetPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: StatelessWidget
        var statelessMatches = Regex.Matches(sourceCode, @"class\s+(\w+)\s+extends\s+StatelessWidget");
        foreach (Match match in statelessMatches)
        {
            var widgetName = match.Groups[1].Value;
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var hasConstConstructor = Regex.IsMatch(sourceCode, $@"const\s+{widgetName}\s*\(");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_StatelessWidget",
                Type = PatternType.Flutter,
                Category = PatternCategory.ComponentModel,
                Implementation = $"StatelessWidget: {widgetName}",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = hasConstConstructor 
                    ? "StatelessWidget with const constructor - excellent for performance!"
                    : "Consider adding const constructor for better performance.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/development/ui/widgets-intro",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["widget_name"] = widgetName,
                    ["has_const_constructor"] = hasConstConstructor
                }
            });
        }

        // Pattern: StatefulWidget
        var statefulMatches = Regex.Matches(sourceCode, @"class\s+(\w+)\s+extends\s+StatefulWidget");
        foreach (Match match in statefulMatches)
        {
            var widgetName = match.Groups[1].Value;
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var stateClassName = $"_{widgetName}State";
            var hasState = sourceCode.Contains(stateClassName) || Regex.IsMatch(sourceCode, $@"class\s+_\w*State\s+extends\s+State<{widgetName}>");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_StatefulWidget",
                Type = PatternType.Flutter,
                Category = PatternCategory.ComponentModel,
                Implementation = $"StatefulWidget: {widgetName}",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Use StatefulWidget only when you need mutable state. Consider StatelessWidget + state management.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/development/ui/interactive",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["widget_name"] = widgetName,
                    ["has_state_class"] = hasState
                }
            });
        }

        // Pattern: const Widget constructor
        var constWidgetMatches = Regex.Matches(sourceCode, @"const\s+(\w+)\s*\(\s*\{");
        if (constWidgetMatches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ConstWidget",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = $"const Widget constructors ({constWidgetMatches.Count} found)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {constWidgetMatches.Count} const widget constructors",
                Confidence = 0.95f,
                BestPractice = "const constructors enable widget reuse and prevent unnecessary rebuilds. Excellent!",
                AzureBestPracticeUrl = "https://docs.flutter.dev/perf/best-practices#use-const-widgets-when-possible",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["const_widget_count"] = constWidgetMatches.Count
                }
            });
        }

        // Pattern: Key usage
        if (Regex.IsMatch(sourceCode, @"Key\s*\(|ValueKey|ObjectKey|GlobalKey|UniqueKey"))
        {
            var match = Regex.Match(sourceCode, @"Key\s*\(|ValueKey|ObjectKey|GlobalKey|UniqueKey");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_WidgetKey",
                Type = PatternType.Flutter,
                Category = PatternCategory.ComponentModel,
                Implementation = "Widget Key usage",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "Use Keys for widgets in lists, animations, or when preserving state. ValueKey for data, ObjectKey for objects.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/development/ui/widgets-intro#keys",
                Context = context
            });
        }

        // Anti-pattern: setState in build method
        if (Regex.IsMatch(sourceCode, @"@override\s+Widget\s+build\([^)]*\)\s*\{[^}]*setState\s*\("))
        {
            var match = Regex.Match(sourceCode, @"setState\s*\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_SetStateInBuild_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "setState called in build method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.85f,
                BestPractice = "ANTI-PATTERN: Never call setState in build(). This causes infinite rebuild loops.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/perf/best-practices",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "critical"
                }
            });
        }

        return patterns;
    }

    #endregion

    #region State Management Patterns

    private List<CodePattern> DetectStateManagementPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Provider
        if (sourceCode.Contains("package:provider/") || Regex.IsMatch(sourceCode, @"Provider\.of|Consumer<|ChangeNotifierProvider|MultiProvider"))
        {
            var match = Regex.Match(sourceCode, @"Provider\.of|Consumer<|ChangeNotifierProvider|MultiProvider");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Provider",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "Provider state management",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Provider is recommended by Flutter team. Use context.read() for actions, context.watch() for rebuilds.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/data-and-backend/state-mgmt/simple",
                Context = context
            });
        }

        // Pattern: Riverpod
        if (sourceCode.Contains("package:flutter_riverpod/") || Regex.IsMatch(sourceCode, @"ProviderScope|ConsumerWidget|ref\.watch|ref\.read|StateNotifier"))
        {
            var match = Regex.Match(sourceCode, @"ref\.watch|ref\.read|ConsumerWidget|StateNotifier");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Riverpod",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "Riverpod state management",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Riverpod provides compile-safe state. Use ref.watch for reactive updates, ref.read for one-time access.",
                AzureBestPracticeUrl = "https://riverpod.dev/docs/introduction/getting_started",
                Context = context
            });
        }

        // Pattern: BLoC
        if (sourceCode.Contains("package:flutter_bloc/") || Regex.IsMatch(sourceCode, @"BlocProvider|BlocBuilder|BlocListener|Cubit<|Bloc<"))
        {
            var match = Regex.Match(sourceCode, @"BlocProvider|BlocBuilder|BlocListener|Cubit<|Bloc<");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_BLoC",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "BLoC/Cubit state management",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "BLoC separates business logic from UI. Use Cubit for simple state, Bloc for event-driven state.",
                AzureBestPracticeUrl = "https://bloclibrary.dev/",
                Context = context
            });
        }

        // Pattern: GetX
        if (sourceCode.Contains("package:get/") || Regex.IsMatch(sourceCode, @"GetMaterialApp|GetBuilder|Obx\(|\.obs\b|GetxController"))
        {
            var match = Regex.Match(sourceCode, @"GetBuilder|Obx\(|\.obs\b|GetxController");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_GetX",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "GetX state management",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "GetX provides reactive state with .obs. Be mindful of memory leaks - dispose controllers properly.",
                AzureBestPracticeUrl = "https://pub.dev/packages/get",
                Context = context
            });
        }

        // Pattern: ValueNotifier
        if (Regex.IsMatch(sourceCode, @"ValueNotifier<|ValueListenableBuilder"))
        {
            var match = Regex.Match(sourceCode, @"ValueNotifier<|ValueListenableBuilder");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ValueNotifier",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "ValueNotifier for simple state",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "ValueNotifier is great for simple, local state. Lighter than ChangeNotifier for single values.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/foundation/ValueNotifier-class.html",
                Context = context
            });
        }

        // Pattern: ChangeNotifier
        if (Regex.IsMatch(sourceCode, @"extends\s+ChangeNotifier|with\s+ChangeNotifier|notifyListeners\(\)"))
        {
            var match = Regex.Match(sourceCode, @"extends\s+ChangeNotifier|with\s+ChangeNotifier|notifyListeners\(\)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var hasDispose = sourceCode.Contains("@override") && sourceCode.Contains("dispose");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ChangeNotifier",
                Type = PatternType.Flutter,
                Category = PatternCategory.StateManagement,
                Implementation = "ChangeNotifier pattern",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = hasDispose 
                    ? "ChangeNotifier with dispose - good practice!"
                    : "WARNING: Ensure dispose() is called to prevent memory leaks.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/foundation/ChangeNotifier-class.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["has_dispose"] = hasDispose
                }
            });
        }

        // Anti-pattern: Multiple setState calls in sequence
        var setStateCount = Regex.Matches(sourceCode, @"setState\s*\(").Count;
        if (setStateCount > 5)
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ExcessiveSetState_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = $"Excessive setState calls ({setStateCount})",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {setStateCount} setState calls - consider using state management",
                Confidence = 0.80f,
                BestPractice = "ANTI-PATTERN: Too many setState calls. Consider using Provider, Riverpod, or BLoC for complex state.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/data-and-backend/state-mgmt/options",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "medium",
                    ["setState_count"] = setStateCount
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Performance Patterns

    private List<CodePattern> DetectPerformancePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: ListView.builder (lazy loading)
        if (Regex.IsMatch(sourceCode, @"ListView\.builder|GridView\.builder|SliverList|SliverGrid"))
        {
            var match = Regex.Match(sourceCode, @"ListView\.builder|GridView\.builder|SliverList|SliverGrid");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_LazyListBuilder",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "Lazy list/grid builder",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Excellent! ListView.builder only creates visible items. Always use for large/infinite lists.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/lists/long-lists",
                Context = context
            });
        }

        // Anti-pattern: ListView with children (not builder)
        if (Regex.IsMatch(sourceCode, @"ListView\s*\(\s*children:") && !sourceCode.Contains("ListView.builder"))
        {
            var match = Regex.Match(sourceCode, @"ListView\s*\(\s*children:");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ListViewChildren_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "ListView with children array",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.85f,
                BestPractice = "ANTI-PATTERN: For lists with many items, use ListView.builder for lazy loading.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/perf/best-practices#use-listviewbuilder-for-long-lists",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "medium"
                }
            });
        }

        // Pattern: RepaintBoundary
        if (sourceCode.Contains("RepaintBoundary"))
        {
            var match = Regex.Match(sourceCode, @"RepaintBoundary");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_RepaintBoundary",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "RepaintBoundary for render optimization",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "RepaintBoundary isolates repaints. Use around frequently updating widgets to prevent cascading repaints.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/perf/best-practices#use-repaintboundary-widgets",
                Context = context
            });
        }

        // Pattern: CachedNetworkImage
        if (sourceCode.Contains("CachedNetworkImage") || sourceCode.Contains("cached_network_image"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_CachedNetworkImage",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "Cached network images",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "CachedNetworkImage for efficient image loading",
                Confidence = 0.95f,
                BestPractice = "CachedNetworkImage prevents re-downloading images. Always use for network images.",
                AzureBestPracticeUrl = "https://pub.dev/packages/cached_network_image",
                Context = context
            });
        }

        // Anti-pattern: Image.network without caching
        if (sourceCode.Contains("Image.network") && !sourceCode.Contains("CachedNetworkImage"))
        {
            var match = Regex.Match(sourceCode, @"Image\.network");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_UncachedNetworkImage_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "Image.network without caching",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.80f,
                BestPractice = "ANTI-PATTERN: Use CachedNetworkImage instead of Image.network to cache images.",
                AzureBestPracticeUrl = "https://pub.dev/packages/cached_network_image",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "low"
                }
            });
        }

        // Pattern: compute() for heavy work
        if (sourceCode.Contains("compute(") || sourceCode.Contains("Isolate.run"))
        {
            var match = Regex.Match(sourceCode, @"compute\(|Isolate\.run");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ComputeIsolate",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "compute/Isolate for background processing",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Excellent! Using compute/Isolate keeps UI responsive during heavy computations.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/perf/isolates",
                Context = context
            });
        }

        // Pattern: AutomaticKeepAliveClientMixin
        if (sourceCode.Contains("AutomaticKeepAliveClientMixin"))
        {
            var match = Regex.Match(sourceCode, @"AutomaticKeepAliveClientMixin");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_KeepAlive",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "AutomaticKeepAliveClientMixin for tab persistence",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "KeepAlive preserves state in TabBarView/PageView. Use sparingly to avoid memory bloat.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/AutomaticKeepAliveClientMixin-mixin.html",
                Context = context
            });
        }

        // Anti-pattern: Future/async in build method
        if (Regex.IsMatch(sourceCode, @"Widget\s+build\([^)]*\)\s*(async\s*)?\{[^}]*await\s+"))
        {
            var match = Regex.Match(sourceCode, @"await\s+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_AsyncInBuild_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Performance,
                Implementation = "async/await in build method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.85f,
                BestPractice = "ANTI-PATTERN: build() must be synchronous. Use FutureBuilder or move async to initState.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/networking/fetch-data",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high"
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Navigation Patterns

    private List<CodePattern> DetectNavigationPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: GoRouter
        if (sourceCode.Contains("package:go_router/") || Regex.IsMatch(sourceCode, @"GoRouter|GoRoute|context\.go\(|context\.push\("))
        {
            var match = Regex.Match(sourceCode, @"GoRouter|GoRoute|context\.go\(|context\.push\(");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_GoRouter",
                Type = PatternType.Flutter,
                Category = PatternCategory.Routing,
                Implementation = "GoRouter declarative navigation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "GoRouter is recommended for declarative routing. Supports deep linking and web URLs.",
                AzureBestPracticeUrl = "https://pub.dev/packages/go_router",
                Context = context
            });
        }

        // Pattern: Navigator 2.0
        if (Regex.IsMatch(sourceCode, @"RouterDelegate|RouteInformationParser|Router\("))
        {
            var match = Regex.Match(sourceCode, @"RouterDelegate|RouteInformationParser|Router\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Navigator2",
                Type = PatternType.Flutter,
                Category = PatternCategory.Routing,
                Implementation = "Navigator 2.0 declarative API",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Navigator 2.0 provides full control over navigation. Consider GoRouter for simpler use cases.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/navigation",
                Context = context
            });
        }

        // Pattern: Named routes
        if (Regex.IsMatch(sourceCode, @"Navigator\.pushNamed|routes:\s*\{|onGenerateRoute"))
        {
            var match = Regex.Match(sourceCode, @"Navigator\.pushNamed|routes:\s*\{|onGenerateRoute");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_NamedRoutes",
                Type = PatternType.Flutter,
                Category = PatternCategory.Routing,
                Implementation = "Named routes navigation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.85f,
                BestPractice = "Named routes centralize navigation. Consider GoRouter for type-safe route parameters.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/navigation/named-routes",
                Context = context
            });
        }

        // Pattern: Deep linking
        if (Regex.IsMatch(sourceCode, @"initialRoute|uni_links|app_links|DeepLinkingHandler"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_DeepLinking",
                Type = PatternType.Flutter,
                Category = PatternCategory.Routing,
                Implementation = "Deep linking setup",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Deep linking configuration detected",
                Confidence = 0.85f,
                BestPractice = "Deep linking enables direct navigation to app content. Test on both iOS and Android.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/navigation/deep-linking",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Lifecycle Patterns

    private List<CodePattern> DetectLifecyclePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: initState
        if (Regex.IsMatch(sourceCode, @"@override\s+void\s+initState\s*\(\)"))
        {
            var match = Regex.Match(sourceCode, @"@override\s+void\s+initState\s*\(\)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var callsSuper = Regex.IsMatch(sourceCode, @"super\.initState\(\)");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_InitState",
                Type = PatternType.Flutter,
                Category = PatternCategory.Lifecycle,
                Implementation = "initState lifecycle method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = callsSuper 
                    ? "initState properly calls super.initState(). Use for one-time initialization."
                    : "WARNING: Always call super.initState() first in initState().",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/State/initState.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["calls_super"] = callsSuper
                }
            });
        }

        // Pattern: dispose
        if (Regex.IsMatch(sourceCode, @"@override\s+void\s+dispose\s*\(\)"))
        {
            var match = Regex.Match(sourceCode, @"@override\s+void\s+dispose\s*\(\)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var callsSuper = Regex.IsMatch(sourceCode, @"super\.dispose\(\)");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Dispose",
                Type = PatternType.Flutter,
                Category = PatternCategory.Lifecycle,
                Implementation = "dispose lifecycle method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = callsSuper 
                    ? "dispose properly calls super.dispose(). Ensure all controllers/subscriptions are disposed."
                    : "WARNING: Always call super.dispose() last in dispose().",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/State/dispose.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["calls_super"] = callsSuper
                }
            });
        }

        // Anti-pattern: Missing dispose for controllers
        var hasController = Regex.IsMatch(sourceCode, @"(TextEditingController|AnimationController|ScrollController|TabController)\s+\w+");
        var hasDispose = sourceCode.Contains("@override") && sourceCode.Contains("void dispose");
        
        if (hasController && !hasDispose)
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_MissingDispose_AntiPattern",
                Type = PatternType.Flutter,
                Category = PatternCategory.Lifecycle,
                Implementation = "Controller without dispose",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Controller found but no dispose() method",
                Confidence = 0.85f,
                BestPractice = "MEMORY LEAK: Controllers must be disposed. Add dispose() and call controller.dispose().",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/State/dispose.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high"
                }
            });
        }

        // Pattern: didChangeDependencies
        if (Regex.IsMatch(sourceCode, @"@override\s+void\s+didChangeDependencies\s*\(\)"))
        {
            var match = Regex.Match(sourceCode, @"@override\s+void\s+didChangeDependencies\s*\(\)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_DidChangeDependencies",
                Type = PatternType.Flutter,
                Category = PatternCategory.Lifecycle,
                Implementation = "didChangeDependencies lifecycle method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Use didChangeDependencies when you need InheritedWidget data. Called after initState.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/State/didChangeDependencies.html",
                Context = context
            });
        }

        // Pattern: WidgetsBindingObserver
        if (sourceCode.Contains("WidgetsBindingObserver"))
        {
            var match = Regex.Match(sourceCode, @"WidgetsBindingObserver");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_AppLifecycle",
                Type = PatternType.Flutter,
                Category = PatternCategory.Lifecycle,
                Implementation = "WidgetsBindingObserver for app lifecycle",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "WidgetsBindingObserver tracks app state (resumed, paused, etc.). Remember to removeObserver in dispose.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/WidgetsBindingObserver-class.html",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region UI Patterns

    private List<CodePattern> DetectUIPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Responsive layout (MediaQuery/LayoutBuilder)
        if (Regex.IsMatch(sourceCode, @"MediaQuery\.of|MediaQuery\.sizeOf|LayoutBuilder|OrientationBuilder"))
        {
            var match = Regex.Match(sourceCode, @"MediaQuery\.of|MediaQuery\.sizeOf|LayoutBuilder|OrientationBuilder");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ResponsiveLayout",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Responsive layout handling",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Use MediaQuery.sizeOf for performance. LayoutBuilder provides parent constraints.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/layout/responsive",
                Context = context
            });
        }

        // Pattern: Theme usage
        if (Regex.IsMatch(sourceCode, @"Theme\.of\(|ThemeData|ColorScheme"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Theming",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Theme/ThemeData usage",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Theming patterns detected",
                Confidence = 0.90f,
                BestPractice = "Use Theme.of(context) for consistent styling. Define colors in ColorScheme for M3.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/design/themes",
                Context = context
            });
        }

        // Pattern: Form handling
        if (Regex.IsMatch(sourceCode, @"Form\(|GlobalKey<FormState>|FormState|TextFormField"))
        {
            var match = Regex.Match(sourceCode, @"Form\(|GlobalKey<FormState>");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            var hasValidation = sourceCode.Contains("validator:");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_FormHandling",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = hasValidation ? "Form with validation" : "Form without validation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = hasValidation 
                    ? "Form with validation - good practice!"
                    : "WARNING: Add validators to form fields for user input validation.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/forms/validation",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["has_validation"] = hasValidation
                }
            });
        }

        // Pattern: SafeArea
        if (sourceCode.Contains("SafeArea"))
        {
            var match = Regex.Match(sourceCode, @"SafeArea");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_SafeArea",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "SafeArea for device-safe padding",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "SafeArea prevents content from being obscured by notches, status bars, etc. Use on main scaffolds.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/SafeArea-class.html",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Networking Patterns

    private List<CodePattern> DetectNetworkingPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Dio HTTP client
        if (sourceCode.Contains("package:dio/") || Regex.IsMatch(sourceCode, @"Dio\(\)|dio\.get|dio\.post"))
        {
            var match = Regex.Match(sourceCode, @"Dio\(\)|dio\.get|dio\.post");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Dio",
                Type = PatternType.Flutter,
                Category = PatternCategory.DataAccess,
                Implementation = "Dio HTTP client",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Dio provides interceptors, timeout, cancel, and retry. Use for complex HTTP needs.",
                AzureBestPracticeUrl = "https://pub.dev/packages/dio",
                Context = context
            });
        }

        // Pattern: http package
        if (sourceCode.Contains("package:http/") || Regex.IsMatch(sourceCode, @"http\.get|http\.post|Client\(\)"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_HttpPackage",
                Type = PatternType.Flutter,
                Category = PatternCategory.DataAccess,
                Implementation = "http package",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "http package for simple HTTP requests",
                Confidence = 0.90f,
                BestPractice = "http package is good for simple requests. Consider Dio for interceptors and advanced features.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/networking/fetch-data",
                Context = context
            });
        }

        // Pattern: Retrofit
        if (sourceCode.Contains("package:retrofit/") || Regex.IsMatch(sourceCode, @"@RestApi|@GET|@POST|@PUT|@DELETE"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Retrofit",
                Type = PatternType.Flutter,
                Category = PatternCategory.DataAccess,
                Implementation = "Retrofit type-safe HTTP",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Retrofit annotations detected",
                Confidence = 0.90f,
                BestPractice = "Retrofit generates type-safe HTTP clients. Combine with json_serializable for complete type safety.",
                AzureBestPracticeUrl = "https://pub.dev/packages/retrofit",
                Context = context
            });
        }

        // Pattern: FutureBuilder for async data
        if (Regex.IsMatch(sourceCode, @"FutureBuilder<"))
        {
            var match = Regex.Match(sourceCode, @"FutureBuilder<");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var handlesError = sourceCode.Contains("snapshot.hasError") || sourceCode.Contains("AsyncSnapshot");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_FutureBuilder",
                Type = PatternType.Flutter,
                Category = PatternCategory.DataAccess,
                Implementation = handlesError ? "FutureBuilder with error handling" : "FutureBuilder without error handling",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = handlesError 
                    ? "FutureBuilder properly handles loading/error states."
                    : "WARNING: Always handle connectionState, hasError, and hasData in FutureBuilder.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/FutureBuilder-class.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["handles_error"] = handlesError
                }
            });
        }

        // Pattern: StreamBuilder for real-time data
        if (Regex.IsMatch(sourceCode, @"StreamBuilder<"))
        {
            var match = Regex.Match(sourceCode, @"StreamBuilder<");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_StreamBuilder",
                Type = PatternType.Flutter,
                Category = PatternCategory.DataAccess,
                Implementation = "StreamBuilder for real-time data",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "StreamBuilder automatically updates UI on stream events. Handle all ConnectionState values.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/widgets/StreamBuilder-class.html",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Testing Patterns

    private List<CodePattern> DetectTestingPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Only check test files
        if (!filePath.Contains("test") && !filePath.Contains("_test.dart"))
            return patterns;

        // Pattern: Widget test
        if (Regex.IsMatch(sourceCode, @"testWidgets\(|pumpWidget|WidgetTester"))
        {
            var match = Regex.Match(sourceCode, @"testWidgets\(|pumpWidget");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var usesPumpAndSettle = sourceCode.Contains("pumpAndSettle");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_WidgetTest",
                Type = PatternType.Flutter,
                Category = PatternCategory.Testing,
                Implementation = "Widget testing",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = usesPumpAndSettle 
                    ? "Widget test uses pumpAndSettle for animations. Good practice!"
                    : "Consider using pumpAndSettle() to wait for animations to complete.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/testing/widget/introduction",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["uses_pump_and_settle"] = usesPumpAndSettle
                }
            });
        }

        // Pattern: Unit test
        if (Regex.IsMatch(sourceCode, @"test\(|group\(|expect\("))
        {
            var match = Regex.Match(sourceCode, @"test\(|group\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_UnitTest",
                Type = PatternType.Flutter,
                Category = PatternCategory.Testing,
                Implementation = "Unit testing",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Unit tests for business logic. Use group() to organize related tests.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/testing/unit/introduction",
                Context = context
            });
        }

        // Pattern: Mocking
        if (Regex.IsMatch(sourceCode, @"Mock\w+|when\(|verify\(|mockito|mocktail"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Mocking",
                Type = PatternType.Flutter,
                Category = PatternCategory.Testing,
                Implementation = "Mocking dependencies",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Mocking patterns detected",
                Confidence = 0.90f,
                BestPractice = "Use mocktail (null-safe) or mockito for mocking. Mock only external dependencies.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/testing/unit/mocking",
                Context = context
            });
        }

        // Pattern: Integration test
        if (sourceCode.Contains("IntegrationTestWidgetsFlutterBinding") || sourceCode.Contains("integration_test"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Flutter_IntegrationTest",
                Type = PatternType.Flutter,
                Category = PatternCategory.Testing,
                Implementation = "Integration testing",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Integration test setup detected",
                Confidence = 0.90f,
                BestPractice = "Integration tests run on real devices. Use for critical user flows.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/testing/integration/introduction",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Accessibility Patterns

    private List<CodePattern> DetectAccessibilityPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Semantics widget
        if (Regex.IsMatch(sourceCode, @"Semantics\(|MergeSemantics|ExcludeSemantics"))
        {
            var match = Regex.Match(sourceCode, @"Semantics\(|MergeSemantics|ExcludeSemantics");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Semantics",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Semantics for screen readers",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Semantics provides accessibility info for screen readers. Essential for inclusive apps.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/accessibility-and-internationalization/accessibility",
                Context = context
            });
        }

        // Pattern: Tooltip
        if (sourceCode.Contains("Tooltip("))
        {
            var match = Regex.Match(sourceCode, @"Tooltip\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_Tooltip",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Tooltip for accessibility",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.85f,
                BestPractice = "Tooltips provide context for icon buttons. Great for accessibility.",
                AzureBestPracticeUrl = "https://api.flutter.dev/flutter/material/Tooltip-class.html",
                Context = context
            });
        }

        // Pattern: excludeFromSemantics
        if (sourceCode.Contains("excludeFromSemantics: true"))
        {
            var match = Regex.Match(sourceCode, @"excludeFromSemantics:\s*true");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ExcludeSemantics",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Excluding from semantics tree",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.80f,
                BestPractice = "Use excludeFromSemantics for decorative images only. Don't exclude interactive elements.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/accessibility-and-internationalization/accessibility",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Animation Patterns

    private List<CodePattern> DetectAnimationPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: AnimationController
        if (Regex.IsMatch(sourceCode, @"AnimationController|TickerProviderStateMixin|SingleTickerProviderStateMixin"))
        {
            var match = Regex.Match(sourceCode, @"AnimationController");
            var lineNumber = match.Success ? GetLineNumber(sourceCode, match.Index) : 1;
            var disposesController = sourceCode.Contains("_controller.dispose()") || sourceCode.Contains("controller.dispose()");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_AnimationController",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = disposesController ? "AnimationController with dispose" : "AnimationController without dispose",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = disposesController 
                    ? "AnimationController properly disposed. Use SingleTickerProviderStateMixin for single controller."
                    : "MEMORY LEAK: AnimationController must be disposed in dispose() method.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/animations/tutorial",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["disposes_controller"] = disposesController,
                    ["is_anti_pattern"] = !disposesController,
                    ["severity"] = disposesController ? "none" : "high"
                }
            });
        }

        // Pattern: Implicit animations
        if (Regex.IsMatch(sourceCode, @"AnimatedContainer|AnimatedOpacity|AnimatedPositioned|AnimatedAlign|AnimatedPadding|TweenAnimationBuilder"))
        {
            var match = Regex.Match(sourceCode, @"Animated\w+|TweenAnimationBuilder");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Flutter_ImplicitAnimation",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Implicit animation widget",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Implicit animations are simple and efficient. Great choice for most UI animations.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/animations/implicit-animations",
                Context = context
            });
        }

        // Pattern: Hero animation
        if (sourceCode.Contains("Hero("))
        {
            var match = Regex.Match(sourceCode, @"Hero\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            var hasTag = sourceCode.Contains("tag:");
            
            patterns.Add(new CodePattern
            {
                Name = "Flutter_HeroAnimation",
                Type = PatternType.Flutter,
                Category = PatternCategory.UserExperience,
                Implementation = "Hero transition animation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = hasTag 
                    ? "Hero animation with tag for smooth transitions between routes."
                    : "WARNING: Hero widget requires a unique tag property.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/ui/animations/hero-animations",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["has_tag"] = hasTag
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private int GetLineNumber(string sourceCode, int charIndex)
    {
        if (charIndex < 0 || charIndex >= sourceCode.Length)
            return 1;

        return sourceCode.Substring(0, charIndex).Count(c => c == '\n') + 1;
    }

    private string GetCodeSnippet(string[] lines, int lineNumber, int contextLines)
    {
        var startLine = Math.Max(0, lineNumber - 1 - contextLines);
        var endLine = Math.Min(lines.Length - 1, lineNumber - 1 + contextLines);

        var snippetLines = lines.Skip(startLine).Take(endLine - startLine + 1);
        return string.Join("\n", snippetLines);
    }

    #endregion
}

