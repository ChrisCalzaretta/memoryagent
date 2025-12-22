namespace CodingAgent.Server.Templates.Flutter;

/// <summary>
/// Template for Flutter iOS Application
/// Uses Cupertino design for native iOS look and feel
/// </summary>
public class FlutterIosTemplate : ProjectTemplateBase
{
    public override string TemplateId => "flutter-ios";
    public override string DisplayName => "Flutter iOS App";
    public override string Language => "flutter";
    public override string ProjectType => "FlutterIOS";
    public override string Description => "A Flutter application targeting iOS with Cupertino design";
    public override int Complexity => 6;
    
    public override string[] Keywords => new[]
    {
        "ios", "iphone", "ipad", "apple", "cupertino", 
        "mobile", "flutter", "dart", "app store"
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
        "ios",
        "assets"
    };
    
    public override string[] RequiredPackages => new[]
    {
        "provider",
        "dio",
        "flutter_secure_storage",
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
  cupertino_icons: ^1.0.6
  provider: ^6.1.1
  dio: ^5.4.0
  flutter_secure_storage: ^9.0.0
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
        ["lib/main.dart"] = @"import 'package:flutter/cupertino.dart';
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
      child: const CupertinoApp(
        title: '{{ProjectName}}',
        theme: CupertinoThemeData(
          primaryColor: CupertinoColors.activeBlue,
          brightness: Brightness.light,
        ),
        home: HomeScreen(),
        debugShowCheckedModeBanner: false,
      ),
    );
  }
}
",
        ["lib/screens/home_screen.dart"] = @"import 'package:flutter/cupertino.dart';

/// Home screen of the application
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CupertinoPageScaffold(
      navigationBar: const CupertinoNavigationBar(
        middle: Text('{{ProjectName}}'),
      ),
      child: SafeArea(
        child: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                CupertinoIcons.app,
                size: 64,
                color: CupertinoColors.activeBlue,
              ),
              const SizedBox(height: 16),
              Text(
                'Welcome to {{ProjectName}}!',
                style: CupertinoTheme.of(context).textTheme.navLargeTitleTextStyle,
              ),
              const SizedBox(height: 8),
              const Text(
                '{{Description}}',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),
              CupertinoButton.filled(
                onPressed: () {
                  // TODO: Add navigation or action
                },
                child: const Text('Get Started'),
              ),
            ],
          ),
        ),
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
ios/Pods/
ios/.symlinks/
android/.gradle/
"
    };
}



