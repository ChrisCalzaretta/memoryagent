namespace CodingAgent.Server.Templates.Flutter;

/// <summary>
/// Template for Flutter Android Application
/// Uses Material Design for native Android look and feel
/// </summary>
public class FlutterAndroidTemplate : ProjectTemplateBase
{
    public override string TemplateId => "flutter-android";
    public override string DisplayName => "Flutter Android App";
    public override string Language => "flutter";
    public override string ProjectType => "FlutterAndroid";
    public override string Description => "A Flutter application targeting Android with Material Design";
    public override int Complexity => 6;
    
    public override string[] Keywords => new[]
    {
        "android", "google", "material", "play store",
        "mobile", "flutter", "dart", "phone", "tablet"
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
        "android",
        "assets"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "provider",
        "dio",
        "shared_preferences",
        "get_it"
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
  material_design_icons_flutter: ^7.0.7296
  provider: ^6.1.1
  dio: ^5.4.0
  shared_preferences: ^2.2.2
  get_it: ^7.6.4
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
          colorScheme: ColorScheme.fromSeed(seedColor: Colors.blue),
          useMaterial3: true,
        ),
        darkTheme: ThemeData(
          colorScheme: ColorScheme.fromSeed(
            seedColor: Colors.blue,
            brightness: Brightness.dark,
          ),
          useMaterial3: true,
        ),
        themeMode: ThemeMode.system,
        home: const HomeScreen(),
        debugShowCheckedModeBanner: false,
      ),
    );
  }
}
",
        ["lib/screens/home_screen.dart"] = @"import 'package:flutter/material.dart';

/// Home screen of the application
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    
    return Scaffold(
      appBar: AppBar(
        title: const Text('{{ProjectName}}'),
        centerTitle: true,
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                Icons.flutter_dash,
                size: 64,
                color: theme.colorScheme.primary,
              ),
              const SizedBox(height: 16),
              Text(
                'Welcome to {{ProjectName}}!',
                style: theme.textTheme.headlineMedium,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                '{{Description}}',
                style: theme.textTheme.bodyLarge,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),
              FilledButton.icon(
                onPressed: () {
                  // TODO: Add navigation or action
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Button pressed!')),
                  );
                },
                icon: const Icon(Icons.rocket_launch),
                label: const Text('Get Started'),
              ),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () {
          // TODO: Add action
        },
        child: const Icon(Icons.add),
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
android/.gradle/
android/local.properties
"
    };
}

