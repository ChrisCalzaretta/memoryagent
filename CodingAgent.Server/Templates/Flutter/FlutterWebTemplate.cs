namespace CodingAgent.Server.Templates.Flutter;

/// <summary>
/// Template for Flutter Web Application
/// Responsive design for browser deployment
/// </summary>
public class FlutterWebTemplate : ProjectTemplateBase
{
    public override string TemplateId => "flutter-web";
    public override string DisplayName => "Flutter Web App";
    public override string Language => "flutter";
    public override string ProjectType => "FlutterWeb";
    public override string Description => "A Flutter application for web browsers with responsive design";
    public override int Complexity => 7;
    
    public override string[] Keywords => new[]
    {
        "web", "browser", "website", "responsive", "pwa",
        "flutter", "dart", "spa", "single page"
    };
    
    public override string[] FolderStructure => new[]
    {
        "lib",
        "lib/models",
        "lib/providers",
        "lib/screens",
        "lib/widgets",
        "lib/services",
        "lib/utils",
        "test",
        "web",
        "assets"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "provider",
        "dio",
        "go_router",
        "responsive_framework"
    };
    
    public override Dictionary<string, string> Files => new()
    {
        ["pubspec.yaml"] = @"name: {{project_name}}
description: {{Description}}
version: 1.0.0+1
publish_to: 'none'

environment:
  sdk: '>=3.0.0 <4.0.0'

dependencies:
  flutter:
    sdk: flutter
  provider: ^6.1.1
  dio: ^5.4.0
  go_router: ^13.0.0
  responsive_framework: ^1.1.1
  equatable: ^2.0.5

dev_dependencies:
  flutter_test:
    sdk: flutter
  flutter_lints: ^3.0.0

flutter:
  uses-material-design: true
  
  assets:
    - assets/
",
        ["lib/main.dart"] = @"import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:responsive_framework/responsive_framework.dart';
import 'screens/home_screen.dart';

void main() {
  runApp(const MyApp());
}

/// Main application widget
class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        // TODO: Add your providers here
        // ChangeNotifierProvider(create: (_) => MyProvider()),
      ],
      child: MaterialApp(
        title: '{{ProjectName}}',
        theme: ThemeData(
          colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
          useMaterial3: true,
        ),
        darkTheme: ThemeData(
          colorScheme: ColorScheme.fromSeed(
            seedColor: Colors.indigo,
            brightness: Brightness.dark,
          ),
          useMaterial3: true,
        ),
        themeMode: ThemeMode.system,
        builder: (context, child) => ResponsiveBreakpoints.builder(
          child: child!,
          breakpoints: [
            const Breakpoint(start: 0, end: 450, name: MOBILE),
            const Breakpoint(start: 451, end: 800, name: TABLET),
            const Breakpoint(start: 801, end: 1920, name: DESKTOP),
            const Breakpoint(start: 1921, end: double.infinity, name: '4K'),
          ],
        ),
        home: const HomeScreen(),
        debugShowCheckedModeBanner: false,
      ),
    );
  }
}
",
        ["lib/screens/home_screen.dart"] = @"import 'package:flutter/material.dart';
import 'package:responsive_framework/responsive_framework.dart';

/// Home screen of the web application
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isDesktop = ResponsiveBreakpoints.of(context).largerThan(TABLET);
    
    return Scaffold(
      appBar: AppBar(
        title: const Text('{{ProjectName}}'),
        centerTitle: !isDesktop,
        actions: [
          if (isDesktop) ...[
            TextButton(
              onPressed: () {},
              child: const Text('Home'),
            ),
            TextButton(
              onPressed: () {},
              child: const Text('About'),
            ),
            TextButton(
              onPressed: () {},
              child: const Text('Contact'),
            ),
            const SizedBox(width: 16),
          ],
          IconButton(
            icon: const Icon(Icons.dark_mode),
            onPressed: () {
              // TODO: Toggle theme
            },
          ),
        ],
      ),
      drawer: isDesktop ? null : const _AppDrawer(),
      body: SingleChildScrollView(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1200),
            child: Padding(
              padding: EdgeInsets.all(isDesktop ? 48.0 : 24.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  SizedBox(height: isDesktop ? 80 : 40),
                  Icon(
                    Icons.web,
                    size: isDesktop ? 120 : 80,
                    color: theme.colorScheme.primary,
                  ),
                  const SizedBox(height: 32),
                  Text(
                    'Welcome to {{ProjectName}}',
                    style: isDesktop
                        ? theme.textTheme.displayMedium
                        : theme.textTheme.headlineLarge,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 600),
                    child: Text(
                      '{{Description}}',
                      style: theme.textTheme.titleLarge?.copyWith(
                        color: theme.colorScheme.onSurface.withOpacity(0.7),
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  const SizedBox(height: 48),
                  Wrap(
                    spacing: 16,
                    runSpacing: 16,
                    alignment: WrapAlignment.center,
                    children: [
                      FilledButton.icon(
                        onPressed: () {},
                        icon: const Icon(Icons.rocket_launch),
                        label: const Text('Get Started'),
                      ),
                      OutlinedButton.icon(
                        onPressed: () {},
                        icon: const Icon(Icons.info_outline),
                        label: const Text('Learn More'),
                      ),
                    ],
                  ),
                  SizedBox(height: isDesktop ? 120 : 60),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _AppDrawer extends StatelessWidget {
  const _AppDrawer();

  @override
  Widget build(BuildContext context) {
    return Drawer(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          DrawerHeader(
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.primary,
            ),
            child: Text(
              '{{ProjectName}}',
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                color: Theme.of(context).colorScheme.onPrimary,
              ),
            ),
          ),
          ListTile(
            leading: const Icon(Icons.home),
            title: const Text('Home'),
            onTap: () => Navigator.pop(context),
          ),
          ListTile(
            leading: const Icon(Icons.info),
            title: const Text('About'),
            onTap: () => Navigator.pop(context),
          ),
          ListTile(
            leading: const Icon(Icons.contact_mail),
            title: const Text('Contact'),
            onTap: () => Navigator.pop(context),
          ),
        ],
      ),
    );
  }
}
",
        ["lib/models/.gitkeep"] = "",
        ["lib/providers/.gitkeep"] = "",
        ["lib/widgets/.gitkeep"] = "",
        ["lib/services/.gitkeep"] = "",
        ["lib/utils/.gitkeep"] = "",
        ["test/widget_test.dart"] = @"import 'package:flutter_test/flutter_test.dart';
import 'package:{{project_name}}/main.dart';

void main() {
  testWidgets('App starts correctly', (WidgetTester tester) async {
    await tester.pumpWidget(const MyApp());
    
    expect(find.text('{{ProjectName}}'), findsOneWidget);
    expect(find.text('Get Started'), findsOneWidget);
  });
}
",
        ["web/index.html"] = @"<!DOCTYPE html>
<html>
<head>
  <base href=""$FLUTTER_BASE_HREF"">
  <meta charset=""UTF-8"">
  <meta content=""IE=Edge"" http-equiv=""X-UA-Compatible"">
  <meta name=""description"" content=""{{Description}}"">
  <meta name=""apple-mobile-web-app-capable"" content=""yes"">
  <meta name=""apple-mobile-web-app-status-bar-style"" content=""black"">
  <meta name=""apple-mobile-web-app-title"" content=""{{ProjectName}}"">
  <link rel=""apple-touch-icon"" href=""icons/Icon-192.png"">
  <link rel=""icon"" type=""image/png"" href=""favicon.png""/>
  <title>{{ProjectName}}</title>
  <link rel=""manifest"" href=""manifest.json"">
  <script>
    var serviceWorkerVersion = null;
  </script>
  <script src=""flutter_bootstrap.js"" async></script>
</head>
<body>
</body>
</html>
",
        ["web/manifest.json"] = @"{
  ""name"": ""{{ProjectName}}"",
  ""short_name"": ""{{ProjectName}}"",
  ""start_url"": ""."",
  ""display"": ""standalone"",
  ""background_color"": ""#0175C2"",
  ""theme_color"": ""#0175C2"",
  ""description"": ""{{Description}}"",
  ""orientation"": ""portrait-primary"",
  ""prefer_related_applications"": false,
  ""icons"": [
    {
      ""src"": ""icons/Icon-192.png"",
      ""sizes"": ""192x192"",
      ""type"": ""image/png""
    },
    {
      ""src"": ""icons/Icon-512.png"",
      ""sizes"": ""512x512"",
      ""type"": ""image/png""
    }
  ]
}
",
        ["analysis_options.yaml"] = @"include: package:flutter_lints/flutter.yaml

linter:
  rules:
    prefer_const_constructors: true
    prefer_const_declarations: true
    avoid_print: true
    prefer_final_locals: true
    sort_constructors_first: true
    unawaited_futures: true
",
        [".gitignore"] = @".dart_tool/
.packages
build/
*.iml
.idea/
.pub/
pubspec.lock
"
    };
}

