using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects WPF, MAUI, and XAML-based UI patterns
/// Covers MVVM, Commands, DependencyProperties, DataBinding, Navigation, and Performance patterns
/// </summary>
public class WpfMauiPatternDetector
{
    private readonly ILogger<WpfMauiPatternDetector>? _logger;

    // Documentation URLs
    private const string MvvmUrl = "https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm";
    private const string CommunityToolkitUrl = "https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/";
    private const string WpfDataBindingUrl = "https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview";
    private const string MauiDataBindingUrl = "https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/data-binding/";
    private const string WpfDependencyPropertyUrl = "https://learn.microsoft.com/en-us/dotnet/desktop/wpf/properties/dependency-properties-overview";
    private const string MauiShellUrl = "https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/";
    private const string WpfStylesUrl = "https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview";
    private const string WpfVirtualizationUrl = "https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls";

    public WpfMauiPatternDetector(ILogger<WpfMauiPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detect all WPF/MAUI patterns in source code
    /// </summary>
    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken)
    {
        var patterns = new List<CodePattern>();
        
        if (string.IsNullOrWhiteSpace(sourceCode))
            return patterns;

        var isXaml = filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
        var isCSharp = filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        var isViewModel = filePath.Contains("ViewModel", StringComparison.OrdinalIgnoreCase);

        // C# patterns
        if (isCSharp)
        {
            patterns.AddRange(DetectMvvmPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectCommandPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectDependencyPropertyPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectCommunityToolkitPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectNavigationPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectMessagingPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectServicePatterns(sourceCode, filePath, context));
        }

        // XAML patterns
        if (isXaml)
        {
            patterns.AddRange(DetectXamlBindingPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectXamlStylePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectXamlTemplatePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectXamlResourcePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectXamlNavigationPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectXamlPerformancePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectMauiSpecificPatterns(sourceCode, filePath, context));
        }

        _logger?.LogDebug("Detected {Count} WPF/MAUI patterns in {File}", patterns.Count, filePath);
        
        return await Task.FromResult(patterns);
    }

    #region MVVM Patterns

    private List<CodePattern> DetectMvvmPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // INotifyPropertyChanged implementation
        if (sourceCode.Contains("INotifyPropertyChanged"))
        {
            var hasPropertyChanged = sourceCode.Contains("PropertyChanged?.Invoke") || 
                                     sourceCode.Contains("OnPropertyChanged") ||
                                     sourceCode.Contains("RaisePropertyChanged") ||
                                     sourceCode.Contains("SetProperty");
            
            patterns.Add(new CodePattern
            {
                Name = "MVVM_INotifyPropertyChanged",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = "INotifyPropertyChanged implementation for data binding",
                Confidence = hasPropertyChanged ? 0.95f : 0.7f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasPropertyChanged 
                    ? "Good: Properly implements PropertyChanged notification. Consider using CommunityToolkit.MVVM [ObservableProperty] for less boilerplate."
                    : "WARNING: INotifyPropertyChanged interface found but no notification method detected. Ensure PropertyChanged event is raised.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = hasPropertyChanged ? "Complete INPC implementation" : "Incomplete INPC implementation"
            });
        }

        // ViewModelBase / BaseViewModel pattern
        var viewModelBaseMatch = Regex.Match(sourceCode, 
            @"class\s+(\w*(?:ViewModel|ViewModelBase|BaseViewModel)\w*)\s*(?::\s*([^{]+))?",
            RegexOptions.Multiline);
        
        if (viewModelBaseMatch.Success)
        {
            var className = viewModelBaseMatch.Groups[1].Value;
            var baseClass = viewModelBaseMatch.Groups[2].Value;
            var isBase = className.Contains("Base") || className == "ViewModelBase";
            
            patterns.Add(new CodePattern
            {
                Name = isBase ? "MVVM_ViewModelBase" : "MVVM_ViewModel",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = isBase 
                    ? $"ViewModel base class: {className}"
                    : $"ViewModel implementation: {className}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = isBase
                    ? "Good: Base ViewModel class for shared functionality. Include: INotifyPropertyChanged, SetProperty helper, IsBusy property, navigation services."
                    : $"ViewModel {className} detected. Ensure it inherits from a base ViewModel and follows naming conventions.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = $"ViewModel: {className}" + (string.IsNullOrEmpty(baseClass) ? "" : $" : {baseClass.Trim()}")
            });
        }

        // ObservableCollection usage
        var observableMatches = Regex.Matches(sourceCode, 
            @"ObservableCollection<(\w+)>\s+(\w+)",
            RegexOptions.Multiline);
        
        foreach (Match match in observableMatches)
        {
            var itemType = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;
            
            patterns.Add(new CodePattern
            {
                Name = "MVVM_ObservableCollection",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = $"ObservableCollection<{itemType}> for {propertyName}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "ObservableCollection automatically notifies UI of Add/Remove. For bulk operations, consider ObservableRangeCollection or temporarily disable notifications.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = $"ObservableCollection<{itemType}>"
            });
        }

        // Model-View separation check
        if (sourceCode.Contains("BindingContext") || sourceCode.Contains("DataContext"))
        {
            var hasProperSeparation = !sourceCode.Contains("MessageBox.Show") && 
                                       !sourceCode.Contains("DisplayAlert") &&
                                       !sourceCode.Contains("Navigation.Push");
            
            patterns.Add(new CodePattern
            {
                Name = "MVVM_ViewModelBinding",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "ViewModel binding to View",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasProperSeparation
                    ? "Good: ViewModel doesn't contain direct UI calls. Use services for navigation/dialogs."
                    : "WARNING: ViewModel contains UI calls (MessageBox/DisplayAlert/Navigation). Use INavigationService and IDialogService for proper separation.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = "DataContext/BindingContext assignment"
            });
        }

        return patterns;
    }

    #endregion

    #region Command Patterns

    private List<CodePattern> DetectCommandPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // ICommand implementations
        var commandTypes = new[]
        {
            ("RelayCommand", "CommunityToolkit/MVVM Light style relay command"),
            ("DelegateCommand", "Prism-style delegate command"),
            ("AsyncRelayCommand", "Async command with cancellation support"),
            ("AsyncCommand", "Async command implementation"),
            ("Command", "Generic ICommand implementation")
        };

        foreach (var (commandType, description) in commandTypes)
        {
            var pattern = $@"(?:public|private|protected)\s+(?:readonly\s+)?(?:I?{commandType}|ICommand)\s+(\w+)";
            var matches = Regex.Matches(sourceCode, pattern);
            
            foreach (Match match in matches)
            {
                var commandName = match.Groups[1].Value;
                var isAsync = commandType.Contains("Async");
                
                patterns.Add(new CodePattern
                {
                    Name = $"MVVM_Command_{commandType}",
                    Type = PatternType.StateManagement,
                    Category = PatternCategory.ComponentModel,
                    Content = $"{description}: {commandName}",
                    Confidence = 0.9f,
                    FilePath = filePath,
                    Context = context,
                    BestPractice = isAsync
                        ? $"AsyncCommand {commandName}: Ensure proper error handling and consider exposing IsBusy property. Use CancellationToken for cancellable operations."
                        : $"Command {commandName}: Consider async version for I/O operations. Implement CanExecute for proper button state.",
                    AzureBestPracticeUrl = CommunityToolkitUrl,
                    Implementation = $"{commandType} {commandName}"
                });
            }
        }

        // [RelayCommand] attribute (CommunityToolkit.MVVM)
        var relayCommandAttrMatches = Regex.Matches(sourceCode, 
            @"\[RelayCommand(?:\(([^)]*)\))?\]\s*(?:private\s+)?(?:async\s+)?(?:Task|void)\s+(\w+)",
            RegexOptions.Multiline);
        
        foreach (Match match in relayCommandAttrMatches)
        {
            var options = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            var hasCanExecute = options.Contains("CanExecute");
            
            patterns.Add(new CodePattern
            {
                Name = "MVVM_RelayCommandAttribute",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = $"[RelayCommand] on {methodName}",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasCanExecute
                    ? $"Good: {methodName}Command with CanExecute. CommunityToolkit.MVVM source-generates the command property."
                    : $"{methodName}Command generated. Consider adding CanExecute parameter for button state management: [RelayCommand(CanExecute = nameof(CanExecuteMethod))]",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = $"[RelayCommand] {methodName} → {methodName}Command"
            });
        }

        // CanExecute pattern
        if (Regex.IsMatch(sourceCode, @"CanExecute\s*[=\(]|bool\s+Can\w+\s*\("))
        {
            patterns.Add(new CodePattern
            {
                Name = "MVVM_CanExecute",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "Command CanExecute implementation",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: CanExecute logic enables/disables UI controls. Call NotifyCanExecuteChanged() when conditions change.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = "CanExecute pattern"
            });
        }

        return patterns;
    }

    #endregion

    #region Dependency Property Patterns

    private List<CodePattern> DetectDependencyPropertyPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Standard DependencyProperty
        var dpMatches = Regex.Matches(sourceCode, 
            @"public\s+static\s+readonly\s+DependencyProperty\s+(\w+)Property\s*=\s*DependencyProperty\.Register(?:Attached)?\s*\(\s*(?:nameof\((\w+)\)|""(\w+)"")",
            RegexOptions.Multiline);
        
        foreach (Match match in dpMatches)
        {
            var dpName = match.Groups[1].Value;
            var propertyName = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            var isAttached = sourceCode.Contains($"RegisterAttached(\"{propertyName}\"") || 
                             sourceCode.Contains($"RegisterAttached(nameof({propertyName})");
            
            patterns.Add(new CodePattern
            {
                Name = isAttached ? "WPF_AttachedProperty" : "WPF_DependencyProperty",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = isAttached 
                    ? $"Attached Property: {dpName}"
                    : $"Dependency Property: {dpName}",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = isAttached
                    ? $"Attached property {dpName}: Use Get{dpName}/Set{dpName} static methods. Good for extending existing controls."
                    : $"DependencyProperty {dpName}: Use nameof() for property name, provide metadata with default value and PropertyChangedCallback.",
                AzureBestPracticeUrl = WpfDependencyPropertyUrl,
                Implementation = $"DependencyProperty.Register{(isAttached ? "Attached" : "")}(\"{propertyName}\")"
            });
        }

        // BindableProperty (MAUI)
        var bindableMatches = Regex.Matches(sourceCode, 
            @"public\s+static\s+readonly\s+BindableProperty\s+(\w+)Property\s*=\s*BindableProperty\.Create(?:Attached)?\s*\(",
            RegexOptions.Multiline);
        
        foreach (Match match in bindableMatches)
        {
            var propertyName = match.Groups[1].Value;
            var isAttached = sourceCode.Contains($"BindableProperty.CreateAttached");
            
            patterns.Add(new CodePattern
            {
                Name = isAttached ? "MAUI_AttachedBindableProperty" : "MAUI_BindableProperty",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = $"MAUI BindableProperty: {propertyName}",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"BindableProperty {propertyName}: Provide defaultValue, propertyChanged callback, and validateValue for validation. Consider coerceValue for range constraints.",
                AzureBestPracticeUrl = MauiDataBindingUrl,
                Implementation = $"BindableProperty.Create{(isAttached ? "Attached" : "")}()"
            });
        }

        // PropertyMetadata patterns
        if (sourceCode.Contains("PropertyMetadata") || sourceCode.Contains("FrameworkPropertyMetadata"))
        {
            var hasCallback = sourceCode.Contains("PropertyChangedCallback") || 
                              Regex.IsMatch(sourceCode, @"new\s+(?:Framework)?PropertyMetadata\([^)]*,\s*\w+\)");
            var hasCoerce = sourceCode.Contains("CoerceValueCallback");
            var hasValidate = sourceCode.Contains("ValidateValueCallback");
            
            patterns.Add(new CodePattern
            {
                Name = "WPF_PropertyMetadata",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "DependencyProperty metadata configuration",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"PropertyMetadata: Callback={hasCallback}, Coerce={hasCoerce}, Validate={hasValidate}. " +
                              (hasCallback ? "Good: PropertyChangedCallback for side effects. " : "Consider PropertyChangedCallback for change handling. ") +
                              (hasCoerce ? "Good: CoerceValueCallback for range enforcement." : ""),
                AzureBestPracticeUrl = WpfDependencyPropertyUrl,
                Implementation = "PropertyMetadata with callbacks"
            });
        }

        return patterns;
    }

    #endregion

    #region CommunityToolkit.MVVM Patterns

    private List<CodePattern> DetectCommunityToolkitPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // [ObservableProperty] attribute
        var observablePropMatches = Regex.Matches(sourceCode, 
            @"\[ObservableProperty\]\s*(?:\[[\w\(\)""=,\s]+\]\s*)*private\s+(\w+(?:<[^>]+>)?)\s+(\w+);",
            RegexOptions.Multiline);
        
        foreach (Match match in observablePropMatches)
        {
            var type = match.Groups[1].Value;
            var fieldName = match.Groups[2].Value;
            var propertyName = char.ToUpper(fieldName.TrimStart('_')[0]) + fieldName.TrimStart('_').Substring(1);
            
            patterns.Add(new CodePattern
            {
                Name = "CommunityToolkit_ObservableProperty",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = $"[ObservableProperty] {fieldName} → {propertyName}",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"CommunityToolkit generates {propertyName} property with INotifyPropertyChanged. Use [NotifyPropertyChangedFor] for dependent properties, [NotifyCanExecuteChangedFor] for command updates.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = $"[ObservableProperty] {type} {fieldName}"
            });
        }

        // ObservableObject base class
        if (Regex.IsMatch(sourceCode, @":\s*ObservableObject\b"))
        {
            patterns.Add(new CodePattern
            {
                Name = "CommunityToolkit_ObservableObject",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "Inherits from CommunityToolkit ObservableObject",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: ObservableObject provides SetProperty, OnPropertyChanged, and source generation support. Use [ObservableProperty] for automatic property generation.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = ": ObservableObject"
            });
        }

        // ObservableRecipient (for messaging)
        if (Regex.IsMatch(sourceCode, @":\s*ObservableRecipient\b"))
        {
            patterns.Add(new CodePattern
            {
                Name = "CommunityToolkit_ObservableRecipient",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "Inherits from ObservableRecipient for messaging",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = "ObservableRecipient enables WeakReferenceMessenger integration. Override OnActivated/OnDeactivated for message registration lifecycle.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = ": ObservableRecipient"
            });
        }

        // [NotifyPropertyChangedFor] attribute
        var notifyForMatches = Regex.Matches(sourceCode, 
            @"\[NotifyPropertyChangedFor\(nameof\((\w+)\)\)\]",
            RegexOptions.Multiline);
        
        foreach (Match match in notifyForMatches)
        {
            var dependentProperty = match.Groups[1].Value;
            
            patterns.Add(new CodePattern
            {
                Name = "CommunityToolkit_NotifyPropertyChangedFor",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = $"Notifies {dependentProperty} when source property changes",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"Good: {dependentProperty} will be notified automatically. Use for computed properties that depend on this value.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = $"[NotifyPropertyChangedFor(nameof({dependentProperty}))]"
            });
        }

        // [NotifyCanExecuteChangedFor] attribute
        var notifyCommandMatches = Regex.Matches(sourceCode, 
            @"\[NotifyCanExecuteChangedFor\(nameof\((\w+)\)\)\]",
            RegexOptions.Multiline);
        
        foreach (Match match in notifyCommandMatches)
        {
            var commandName = match.Groups[1].Value;
            
            patterns.Add(new CodePattern
            {
                Name = "CommunityToolkit_NotifyCanExecuteChangedFor",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = $"Updates {commandName} CanExecute when property changes",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"Good: {commandName}.NotifyCanExecuteChanged() called automatically. Ensures button states update when this property changes.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = $"[NotifyCanExecuteChangedFor(nameof({commandName}))]"
            });
        }

        return patterns;
    }

    #endregion

    #region Navigation Patterns

    private List<CodePattern> DetectNavigationPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // INavigationService pattern
        if (sourceCode.Contains("INavigationService") || sourceCode.Contains("INavigationAware"))
        {
            patterns.Add(new CodePattern
            {
                Name = "MVVM_NavigationService",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = "Navigation service abstraction",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: Abstracting navigation allows ViewModel testing and platform independence. Implement NavigateTo<TViewModel>() with parameter passing support.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = "INavigationService pattern"
            });
        }

        // MAUI Shell navigation
        if (sourceCode.Contains("Shell.Current.GoToAsync") || sourceCode.Contains("Shell.Current.Navigation"))
        {
            var hasParameters = sourceCode.Contains("GoToAsync") && 
                               (sourceCode.Contains("new Dictionary") || sourceCode.Contains("?") || sourceCode.Contains("QueryProperty"));
            
            patterns.Add(new CodePattern
            {
                Name = "MAUI_ShellNavigation",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = "MAUI Shell navigation",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasParameters
                    ? "Good: Shell navigation with parameters. Use [QueryProperty] on target page for automatic property binding."
                    : "Shell navigation detected. Consider passing parameters via query string: await Shell.Current.GoToAsync($\"page?id={id}\");",
                AzureBestPracticeUrl = MauiShellUrl,
                Implementation = "Shell.Current.GoToAsync"
            });
        }

        // WPF Frame navigation
        if (sourceCode.Contains("Frame.Navigate") || sourceCode.Contains("NavigationService.Navigate"))
        {
            patterns.Add(new CodePattern
            {
                Name = "WPF_FrameNavigation",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = "WPF Frame-based navigation",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Frame navigation detected. Consider using a navigation service abstraction for better testability and ViewModel-first navigation.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = "Frame/NavigationService.Navigate"
            });
        }

        // Prism navigation
        if (sourceCode.Contains("IRegionManager") || sourceCode.Contains("regionManager.RequestNavigate"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Prism_RegionNavigation",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = "Prism region-based navigation",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: Prism region navigation allows modular UI composition. Implement INavigationAware for navigation lifecycle hooks.",
                AzureBestPracticeUrl = "https://prismlibrary.com/docs/wpf/region-navigation/",
                Implementation = "IRegionManager.RequestNavigate"
            });
        }

        return patterns;
    }

    #endregion

    #region Messaging Patterns

    private List<CodePattern> DetectMessagingPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // WeakReferenceMessenger (CommunityToolkit)
        if (sourceCode.Contains("WeakReferenceMessenger") || sourceCode.Contains("StrongReferenceMessenger"))
        {
            var isWeak = sourceCode.Contains("WeakReferenceMessenger");
            
            patterns.Add(new CodePattern
            {
                Name = isWeak ? "CommunityToolkit_WeakReferenceMessenger" : "CommunityToolkit_StrongReferenceMessenger",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = $"{(isWeak ? "Weak" : "Strong")} reference messenger for pub/sub",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = isWeak
                    ? "Good: WeakReferenceMessenger prevents memory leaks. Use for ViewModels that may be garbage collected."
                    : "StrongReferenceMessenger requires manual unsubscription. Prefer WeakReferenceMessenger unless you need guaranteed delivery.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = $"{(isWeak ? "Weak" : "Strong")}ReferenceMessenger.Default"
            });
        }

        // IMessenger.Register pattern
        if (Regex.IsMatch(sourceCode, @"\.Register<\w+>"))
        {
            var unregisters = sourceCode.Contains("Unregister") || sourceCode.Contains("UnregisterAll");
            
            patterns.Add(new CodePattern
            {
                Name = "Messaging_Registration",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = "Message registration pattern",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = unregisters
                    ? "Good: Message unregistration found. Proper cleanup prevents memory leaks."
                    : "WARNING: Message registration without unregistration detected. Call Unregister() in Dispose() or use WeakReferenceMessenger.",
                AzureBestPracticeUrl = CommunityToolkitUrl,
                Implementation = "IMessenger.Register<TMessage>"
            });
        }

        // Event Aggregator (Prism)
        if (sourceCode.Contains("IEventAggregator") || sourceCode.Contains("eventAggregator.GetEvent"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Prism_EventAggregator",
                Type = PatternType.StateManagement,
                Category = PatternCategory.StateManagement,
                Content = "Prism Event Aggregator for pub/sub",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Prism EventAggregator: Use PubSubEvent<T> for typed events. Keep SubscriptionToken and unsubscribe in Dispose(). Use ThreadOption.UIThread for UI updates.",
                AzureBestPracticeUrl = "https://prismlibrary.com/docs/event-aggregator.html",
                Implementation = "IEventAggregator.GetEvent<TEvent>()"
            });
        }

        return patterns;
    }

    #endregion

    #region Service Patterns

    private List<CodePattern> DetectServicePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // IDialogService
        if (sourceCode.Contains("IDialogService") || sourceCode.Contains("IMessageService"))
        {
            patterns.Add(new CodePattern
            {
                Name = "MVVM_DialogService",
                Type = PatternType.DependencyInjection,
                Category = PatternCategory.ComponentModel,
                Content = "Dialog/Message service abstraction",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: Dialog service allows ViewModel to show dialogs without UI coupling. Implement ShowAsync<TResult>() for dialog results.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = "IDialogService pattern"
            });
        }

        // IDispatcherService
        if (sourceCode.Contains("IDispatcher") || sourceCode.Contains("Dispatcher.") || sourceCode.Contains("MainThread."))
        {
            var isAbstracted = sourceCode.Contains("IDispatcher");
            
            patterns.Add(new CodePattern
            {
                Name = "MVVM_DispatcherService",
                Type = PatternType.DependencyInjection,
                Category = PatternCategory.Performance,
                Content = "UI thread dispatcher usage",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = isAbstracted
                    ? "Good: Dispatcher abstracted via interface for testability."
                    : "Direct Dispatcher/MainThread usage. Consider abstracting for unit testing: IDispatcher.InvokeAsync().",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = isAbstracted ? "IDispatcher abstraction" : "Direct Dispatcher usage"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Binding Patterns

    private List<CodePattern> DetectXamlBindingPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // {Binding} expressions
        var bindingMatches = Regex.Matches(sourceCode, 
            @"\{Binding\s+(?:Path=)?(\w+(?:\.\w+)*)",
            RegexOptions.IgnoreCase);
        
        if (bindingMatches.Count > 0)
        {
            var hasMode = sourceCode.Contains("Mode=");
            var hasTwoWay = sourceCode.Contains("Mode=TwoWay");
            var hasConverter = sourceCode.Contains("Converter=");
            var hasFallback = sourceCode.Contains("FallbackValue=") || sourceCode.Contains("TargetNullValue=");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_DataBinding",
                Type = PatternType.StateManagement,
                Category = PatternCategory.DataBinding,
                Content = $"Data binding ({bindingMatches.Count} bindings)",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"Bindings: {bindingMatches.Count}. Mode={hasMode}, TwoWay={hasTwoWay}, Converter={hasConverter}, Fallback={hasFallback}. " +
                              (hasFallback ? "Good: FallbackValue handles null. " : "Consider FallbackValue for null handling. ") +
                              (!hasMode ? "Specify binding Mode explicitly for clarity." : ""),
                AzureBestPracticeUrl = WpfDataBindingUrl,
                Implementation = $"{bindingMatches.Count} bindings"
            });
        }

        // x:Bind (UWP/WinUI compiled bindings)
        var xBindMatches = Regex.Matches(sourceCode, @"\{x:Bind\s+", RegexOptions.IgnoreCase);
        if (xBindMatches.Count > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_CompiledBinding",
                Type = PatternType.StateManagement,
                Category = PatternCategory.DataBinding,
                Content = $"Compiled x:Bind ({xBindMatches.Count} bindings)",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: x:Bind is compiled for better performance and compile-time error checking. Default mode is OneTime (use Mode=OneWay/TwoWay as needed).",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/windows/uwp/data-binding/x-bind-markup-extension",
                Implementation = $"{xBindMatches.Count} compiled bindings"
            });
        }

        // IValueConverter usage
        if (sourceCode.Contains("Converter=") && sourceCode.Contains("StaticResource"))
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_ValueConverter",
                Type = PatternType.StateManagement,
                Category = PatternCategory.DataBinding,
                Content = "Value converter in binding",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Value converters should be stateless and thread-safe. Define as StaticResource for reuse. Consider MultiBinding for complex scenarios.",
                AzureBestPracticeUrl = WpfDataBindingUrl,
                Implementation = "Converter={StaticResource ...}"
            });
        }

        // Command binding
        var commandBindings = Regex.Matches(sourceCode, @"Command\s*=\s*""\{Binding\s+(\w+)""", RegexOptions.IgnoreCase);
        foreach (Match match in commandBindings)
        {
            var commandName = match.Groups[1].Value;
            var hasParameter = sourceCode.Contains($"CommandParameter=");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_CommandBinding",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = $"Command binding: {commandName}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasParameter
                    ? $"Good: {commandName} with CommandParameter. Ensure ViewModel command handles the parameter type."
                    : $"Command {commandName} bound. Consider CommandParameter if action needs context data.",
                AzureBestPracticeUrl = MvvmUrl,
                Implementation = $"Command={{Binding {commandName}}}"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Style Patterns

    private List<CodePattern> DetectXamlStylePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Style definitions
        var styleMatches = Regex.Matches(sourceCode, 
            @"<Style\s+(?:x:Key=""(\w+)""\s+)?TargetType=""(?:\{x:Type\s+)?(\w+)",
            RegexOptions.IgnoreCase);
        
        foreach (Match match in styleMatches)
        {
            var key = match.Groups[1].Value;
            var targetType = match.Groups[2].Value;
            var isImplicit = string.IsNullOrEmpty(key);
            var hasBasedOn = sourceCode.Contains("BasedOn=");
            
            patterns.Add(new CodePattern
            {
                Name = isImplicit ? "XAML_ImplicitStyle" : "XAML_ExplicitStyle",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = isImplicit 
                    ? $"Implicit style for {targetType}"
                    : $"Explicit style '{key}' for {targetType}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = isImplicit
                    ? $"Implicit style applies to all {targetType}. Use sparingly as it affects entire scope."
                    : $"Named style '{key}': " + (hasBasedOn ? "Good: Inherits from base style." : "Consider BasedOn for style inheritance."),
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = isImplicit ? $"<Style TargetType=\"{targetType}\">" : $"<Style x:Key=\"{key}\">"
            });
        }

        // Triggers
        if (sourceCode.Contains("<Trigger") || sourceCode.Contains("<DataTrigger") || sourceCode.Contains("<EventTrigger"))
        {
            var triggerTypes = new List<string>();
            if (sourceCode.Contains("<Trigger")) triggerTypes.Add("Property");
            if (sourceCode.Contains("<DataTrigger")) triggerTypes.Add("Data");
            if (sourceCode.Contains("<EventTrigger")) triggerTypes.Add("Event");
            if (sourceCode.Contains("<MultiTrigger")) triggerTypes.Add("Multi");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_Triggers",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = $"Style triggers: {string.Join(", ", triggerTypes)}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Triggers enable declarative state changes. Use DataTrigger for ViewModel properties, PropertyTrigger for control properties, EventTrigger for animations.",
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = $"Triggers: {string.Join(", ", triggerTypes)}"
            });
        }

        // VisualStateManager
        if (sourceCode.Contains("VisualStateManager") || sourceCode.Contains("<VisualState"))
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_VisualStateManager",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = "VisualStateManager for state-based styling",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "VisualStateManager is preferred for complex state transitions. Define states in VisualStateGroups, use GoToState() to transition. Better for animations than Triggers.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/customizing-the-appearance-of-an-existing-control",
                Implementation = "VisualStateManager"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Template Patterns

    private List<CodePattern> DetectXamlTemplatePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // DataTemplate
        if (sourceCode.Contains("<DataTemplate"))
        {
            var hasDataType = sourceCode.Contains("DataType=");
            var hasKey = Regex.IsMatch(sourceCode, @"<DataTemplate\s+x:Key=");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_DataTemplate",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = "DataTemplate for data visualization",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasDataType
                    ? "Good: DataType specified enables implicit template selection. Template automatically applies to matching types."
                    : "Consider specifying DataType for automatic template resolution: DataType=\"{x:Type vm:MyViewModel}\"",
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = hasDataType ? "Typed DataTemplate" : "Keyed DataTemplate"
            });
        }

        // ControlTemplate
        if (sourceCode.Contains("<ControlTemplate"))
        {
            var hasTemplateParts = sourceCode.Contains("PART_") || sourceCode.Contains("x:Name=");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_ControlTemplate",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = "ControlTemplate for custom control appearance",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasTemplateParts
                    ? "Good: Template parts defined. Document required parts with TemplatePart attribute on control class."
                    : "ControlTemplate detected. Use PART_ naming convention for required elements. Include ContentPresenter/ItemsPresenter.",
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = "ControlTemplate"
            });
        }

        // ItemsPanelTemplate
        if (sourceCode.Contains("<ItemsPanelTemplate"))
        {
            var panelType = Regex.Match(sourceCode, @"<ItemsPanelTemplate>\s*<(\w+Panel)");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_ItemsPanelTemplate",
                Type = PatternType.Blazor,
                Category = PatternCategory.UserExperience,
                Content = $"ItemsPanelTemplate{(panelType.Success ? $": {panelType.Groups[1].Value}" : "")}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "ItemsPanelTemplate defines container layout. Use VirtualizingStackPanel for large lists, WrapPanel for flowing layout, Canvas for absolute positioning.",
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = "ItemsPanelTemplate"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Resource Patterns

    private List<CodePattern> DetectXamlResourcePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // ResourceDictionary
        if (sourceCode.Contains("<ResourceDictionary"))
        {
            var hasMerged = sourceCode.Contains("MergedDictionaries");
            var hasSource = sourceCode.Contains("Source=");
            
            patterns.Add(new CodePattern
            {
                Name = "XAML_ResourceDictionary",
                Type = PatternType.Configuration,
                Category = PatternCategory.UserExperience,
                Content = "ResourceDictionary for shared resources",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasMerged
                    ? "Good: MergedDictionaries for modular resources. Load order matters - later dictionaries override earlier ones."
                    : "Consider organizing resources into separate dictionaries by theme/feature for maintainability.",
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = hasMerged ? "MergedDictionaries" : "ResourceDictionary"
            });
        }

        // StaticResource vs DynamicResource
        var staticCount = Regex.Matches(sourceCode, @"\{StaticResource\s+", RegexOptions.IgnoreCase).Count;
        var dynamicCount = Regex.Matches(sourceCode, @"\{DynamicResource\s+", RegexOptions.IgnoreCase).Count;
        
        if (staticCount > 0 || dynamicCount > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_ResourceReferences",
                Type = PatternType.Configuration,
                Category = PatternCategory.Performance,
                Content = $"Resource references: {staticCount} Static, {dynamicCount} Dynamic",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"StaticResource ({staticCount}): Faster, resolved once at load. DynamicResource ({dynamicCount}): Updates when resource changes, use for theme switching. " +
                              (dynamicCount > staticCount ? "High dynamic ratio - ensure this is intentional (theming)." : "Good: Prefer StaticResource for performance."),
                AzureBestPracticeUrl = WpfStylesUrl,
                Implementation = $"Static: {staticCount}, Dynamic: {dynamicCount}"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Navigation Patterns

    private List<CodePattern> DetectXamlNavigationPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // MAUI Shell definition
        if (sourceCode.Contains("<Shell") || sourceCode.Contains("Shell."))
        {
            var hasFlyout = sourceCode.Contains("<FlyoutItem") || sourceCode.Contains("FlyoutBehavior");
            var hasTabs = sourceCode.Contains("<TabBar") || sourceCode.Contains("<Tab");
            var hasRoutes = sourceCode.Contains("Route=");
            
            patterns.Add(new CodePattern
            {
                Name = "MAUI_ShellDefinition",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = $"MAUI Shell{(hasFlyout ? " with Flyout" : "")}{(hasTabs ? " with Tabs" : "")}",
                Confidence = 0.95f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasRoutes
                    ? "Good: Routes defined for URI-based navigation. Register routes in AppShell constructor for implicit pages."
                    : "Define Route property on ShellContent for URI-based navigation: GoToAsync(\"//routename\")",
                AzureBestPracticeUrl = MauiShellUrl,
                Implementation = $"Shell: Flyout={hasFlyout}, Tabs={hasTabs}"
            });
        }

        // NavigationPage
        if (sourceCode.Contains("<NavigationPage") || sourceCode.Contains("NavigationPage."))
        {
            patterns.Add(new CodePattern
            {
                Name = "MAUI_NavigationPage",
                Type = PatternType.Configuration,
                Category = PatternCategory.Routing,
                Content = "NavigationPage for stack-based navigation",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "NavigationPage provides page stack with back navigation. Consider Shell for complex navigation patterns with tabs/flyout.",
                AzureBestPracticeUrl = MauiShellUrl,
                Implementation = "NavigationPage"
            });
        }

        return patterns;
    }

    #endregion

    #region XAML Performance Patterns

    private List<CodePattern> DetectXamlPerformancePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // VirtualizingStackPanel
        if (sourceCode.Contains("VirtualizingStackPanel") || sourceCode.Contains("VirtualizingPanel"))
        {
            var isEnabled = !sourceCode.Contains("VirtualizationMode=\"Standard\"") && 
                           !sourceCode.Contains("IsVirtualizing=\"False\"");
            
            patterns.Add(new CodePattern
            {
                Name = "WPF_Virtualization",
                Type = PatternType.Performance,
                Category = PatternCategory.Performance,
                Content = "UI virtualization for lists",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = isEnabled
                    ? "Good: Virtualization enabled. For best performance use VirtualizationMode=\"Recycling\" and set ScrollViewer.CanContentScroll=\"True\"."
                    : "WARNING: Virtualization disabled. Large lists will have poor performance. Enable VirtualizingPanel.IsVirtualizing.",
                AzureBestPracticeUrl = WpfVirtualizationUrl,
                Implementation = isEnabled ? "Virtualization enabled" : "Virtualization disabled"
            });
        }

        // CollectionView (MAUI)
        if (sourceCode.Contains("<CollectionView"))
        {
            var hasItemSizing = sourceCode.Contains("ItemSizingStrategy=");
            
            patterns.Add(new CodePattern
            {
                Name = "MAUI_CollectionView",
                Type = PatternType.Performance,
                Category = PatternCategory.Performance,
                Content = "MAUI CollectionView for virtualized lists",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasItemSizing
                    ? "Good: ItemSizingStrategy specified. Use MeasureFirstItem for uniform items, MeasureAllItems only if items vary significantly."
                    : "CollectionView: Consider ItemSizingStrategy=\"MeasureFirstItem\" for better performance with uniform items.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/",
                Implementation = "CollectionView"
            });
        }

        // x:Load / x:DeferLoadStrategy
        if (sourceCode.Contains("x:Load=") || sourceCode.Contains("x:DeferLoadStrategy"))
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_DeferredLoading",
                Type = PatternType.Performance,
                Category = PatternCategory.Performance,
                Content = "Deferred element loading",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Good: Deferred loading improves startup time. Element is created when x:Load becomes true or FindName() is called.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/windows/uwp/xaml-platform/x-load-attribute",
                Implementation = "x:Load deferred loading"
            });
        }

        // Compiled bindings suggestion
        var bindingCount = Regex.Matches(sourceCode, @"\{Binding\s+", RegexOptions.IgnoreCase).Count;
        if (bindingCount > 10 && !sourceCode.Contains("x:Bind"))
        {
            patterns.Add(new CodePattern
            {
                Name = "XAML_BindingPerformance",
                Type = PatternType.Performance,
                Category = PatternCategory.Performance,
                Content = $"High binding count ({bindingCount})",
                Confidence = 0.7f,
                FilePath = filePath,
                Context = context,
                BestPractice = $"File has {bindingCount} traditional bindings. For UWP/WinUI, consider x:Bind for compiled bindings with better performance and compile-time checking.",
                AzureBestPracticeUrl = WpfDataBindingUrl,
                Implementation = $"{bindingCount} bindings - consider optimization"
            });
        }

        return patterns;
    }

    #endregion

    #region MAUI Specific Patterns

    private List<CodePattern> DetectMauiSpecificPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // ContentPage
        if (sourceCode.Contains("<ContentPage"))
        {
            var hasViewModel = sourceCode.Contains("x:DataType=") || sourceCode.Contains("BindingContext");
            
            patterns.Add(new CodePattern
            {
                Name = "MAUI_ContentPage",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "MAUI ContentPage",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = hasViewModel
                    ? "Good: ViewModel binding configured. Use x:DataType for compiled bindings."
                    : "ContentPage without explicit ViewModel binding. Add x:DataType for compiled bindings: x:DataType=\"vm:MyPageViewModel\"",
                AzureBestPracticeUrl = MauiDataBindingUrl,
                Implementation = "ContentPage"
            });
        }

        // Behaviors
        if (sourceCode.Contains("<Behavior") || sourceCode.Contains(".Behaviors>"))
        {
            patterns.Add(new CodePattern
            {
                Name = "MAUI_Behaviors",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "MAUI Behaviors for reusable control logic",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Behaviors encapsulate reusable functionality. Use EventToCommandBehavior to bind events to commands. Consider CommunityToolkit.Maui for common behaviors.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/behaviors",
                Implementation = "Behaviors"
            });
        }

        // Platform-specific (OnPlatform/OnIdiom)
        if (sourceCode.Contains("<OnPlatform") || sourceCode.Contains("<OnIdiom"))
        {
            var hasPlatform = sourceCode.Contains("<OnPlatform");
            var hasIdiom = sourceCode.Contains("<OnIdiom");
            
            patterns.Add(new CodePattern
            {
                Name = "MAUI_PlatformSpecific",
                Type = PatternType.Configuration,
                Category = PatternCategory.Configuration,
                Content = $"Platform-specific values: {(hasPlatform ? "OnPlatform " : "")}{(hasIdiom ? "OnIdiom" : "")}",
                Confidence = 0.9f,
                FilePath = filePath,
                Context = context,
                BestPractice = "OnPlatform/OnIdiom enable adaptive UI. Keep platform differences minimal - prefer responsive layouts. Extract platform values to ResourceDictionary for reuse.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/customize-ui-appearance",
                Implementation = $"Platform: {hasPlatform}, Idiom: {hasIdiom}"
            });
        }

        // Handlers
        if (sourceCode.Contains("Handler=") || sourceCode.Contains(".Handler"))
        {
            patterns.Add(new CodePattern
            {
                Name = "MAUI_Handlers",
                Type = PatternType.StateManagement,
                Category = PatternCategory.ComponentModel,
                Content = "MAUI Handler customization",
                Confidence = 0.85f,
                FilePath = filePath,
                Context = context,
                BestPractice = "Handlers map cross-platform controls to native controls. Customize via Handler property or HandlerChanged event. Prefer this over Effects for native customization.",
                AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize",
                Implementation = "Handler customization"
            });
        }

        return patterns;
    }

    #endregion
}

