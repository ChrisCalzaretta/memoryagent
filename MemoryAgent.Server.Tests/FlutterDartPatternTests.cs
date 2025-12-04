using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Integration tests for Flutter and Dart pattern detection
/// Tests cover: happy path, edge cases, error handling, and all pattern categories
/// </summary>
public class FlutterDartPatternTests
{
    private readonly ITestOutputHelper _output;
    private readonly DartPatternDetector _dartDetector;
    private readonly FlutterPatternDetector _flutterDetector;

    public FlutterDartPatternTests(ITestOutputHelper output)
    {
        _output = output;
        _dartDetector = new DartPatternDetector();
        _flutterDetector = new FlutterPatternDetector();
    }

    #region Dart Async Pattern Tests

    [Fact]
    public void DetectPatterns_DartAsyncAwait_ReturnsAsyncPattern()
    {
        // Arrange
        var code = @"
import 'dart:async';

Future<String> fetchData() async {
  final response = await http.get(Uri.parse('https://api.example.com'));
  return response.body;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        Assert.NotEmpty(patterns);
        var asyncPattern = patterns.FirstOrDefault(p => p.Name == "Dart_AsyncAwait");
        Assert.NotNull(asyncPattern);
        Assert.Equal(PatternType.Dart, asyncPattern.Type);
        Assert.Equal("Dart", asyncPattern.Language);
        _output.WriteLine($"Found pattern: {asyncPattern.Name} with confidence {asyncPattern.Confidence}");
    }

    [Fact]
    public void DetectPatterns_DartFuture_ReturnsFuturePattern()
    {
        // Arrange
        var code = @"
Future<int> computeValue() {
  return Future.delayed(Duration(seconds: 1), () => 42);
}

Future<List<String>> fetchAll() async {
  return await Future.wait([fetchA(), fetchB()]);
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var futurePattern = patterns.FirstOrDefault(p => p.Name == "Dart_Future");
        Assert.NotNull(futurePattern);
        _output.WriteLine($"Found Future pattern at line {futurePattern.LineNumber}");
    }

    [Fact]
    public void DetectPatterns_DartStream_ReturnsStreamPattern()
    {
        // Arrange
        var code = @"
Stream<int> countStream(int max) async* {
  for (int i = 0; i < max; i++) {
    yield i;
  }
}

class MyWidget {
  StreamController<String> _controller = StreamController<String>();
  
  void dispose() {
    _controller.close();
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var streamPattern = patterns.FirstOrDefault(p => p.Name == "Dart_Stream");
        Assert.NotNull(streamPattern);
        Assert.True(streamPattern.Metadata.ContainsKey("has_cancel"));
        _output.WriteLine($"Stream pattern has_cancel: {streamPattern.Metadata["has_cancel"]}");
    }

    [Fact]
    public void DetectPatterns_DartIsolate_ReturnsIsolatePattern()
    {
        // Arrange
        var code = @"
Future<int> heavyComputation() async {
  return await Isolate.run(() {
    int sum = 0;
    for (int i = 0; i < 1000000; i++) {
      sum += i;
    }
    return sum;
  });
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var isolatePattern = patterns.FirstOrDefault(p => p.Name == "Dart_Isolate");
        Assert.NotNull(isolatePattern);
        Assert.Equal(PatternCategory.Performance, isolatePattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartThenChain_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: chained .then() calls
        // Regex: \.then\([^)]+\)\.then\( - looks for .then(...).then( where ... has no )
        var code = @"
void fetchData() {
  http.get(url).then(parse).then(process);
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Dart_ThenChain_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.True((bool)antiPattern.Metadata["is_anti_pattern"]);
        _output.WriteLine($"Anti-pattern detected: {antiPattern.BestPractice}");
    }

    #endregion

    #region Dart Null Safety Pattern Tests

    [Fact]
    public void DetectPatterns_DartNullAwareOperators_ReturnsPattern()
    {
        // Arrange
        var code = @"
String? getName() => _user?.name;
String displayName = user?.name ?? 'Unknown';
int length = text!.length;
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var nullAwarePattern = patterns.FirstOrDefault(p => p.Name == "Dart_NullAwareOperators");
        Assert.NotNull(nullAwarePattern);
        Assert.Equal(PatternCategory.Correctness, nullAwarePattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartExcessiveBangOperator_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: excessive ! operator usage
        // The regex matches !\s*[;,\)\.[] - bang followed by certain characters
        var code = @"
void processData(List<String> items) {
  final a = data!;
  final b = items[0]!;
  final c = items[1]!;
  final d = getValue()!;
  final e = other!;
  final f = test!;
  final g = more!;
  final h = stuff!;
  final i = here!;
  final j = there!;
  final k = everywhere!;
  final l = final!;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Dart_ExcessiveBangOperator_AntiPattern");
        Assert.NotNull(antiPattern);
        var bangCount = Convert.ToInt32(antiPattern.Metadata["bang_count"]);
        Assert.True(bangCount > 10, $"Expected > 10 bang operators but found {bangCount}");
        _output.WriteLine($"Bang operator count: {bangCount}");
    }

    [Fact]
    public void DetectPatterns_DartLateKeyword_ReturnsPattern()
    {
        // Arrange
        var code = @"
class MyClass {
  late final String name;
  late int count;
  
  void init() {
    name = 'Test';
    count = 0;
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var latePattern = patterns.FirstOrDefault(p => p.Name == "Dart_LateKeyword");
        Assert.NotNull(latePattern);
        Assert.True(latePattern.Metadata.ContainsKey("late_count"));
    }

    [Fact]
    public void DetectPatterns_DartRequiredParameter_ReturnsPattern()
    {
        // Arrange
        var code = @"
class User {
  final String name;
  final int age;
  
  User({required String name, required int age}) 
    : name = name, age = age;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var requiredPattern = patterns.FirstOrDefault(p => p.Name == "Dart_RequiredParameter");
        Assert.NotNull(requiredPattern);
    }

    #endregion

    #region Dart Performance Pattern Tests

    [Fact]
    public void DetectPatterns_DartConstConstructor_ReturnsPattern()
    {
        // Arrange
        var code = @"
class Point {
  final int x;
  final int y;
  
  const Point(this.x, this.y);
}

final origin = const Point(0, 0);
final colors = const ['red', 'green', 'blue'];
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var constPattern = patterns.FirstOrDefault(p => p.Name == "Dart_ConstConstructor");
        Assert.NotNull(constPattern);
        Assert.Equal(PatternCategory.Performance, constPattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartStringBuffer_ReturnsPattern()
    {
        // Arrange
        var code = @"
String buildHtml(List<String> items) {
  final buffer = StringBuffer();
  buffer.write('<ul>');
  for (final item in items) {
    buffer.write('<li>$item</li>');
  }
  buffer.write('</ul>');
  return buffer.toString();
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var bufferPattern = patterns.FirstOrDefault(p => p.Name == "Dart_StringBuffer");
        Assert.NotNull(bufferPattern);
    }

    [Fact]
    public void DetectPatterns_DartStringConcatInLoop_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: string concatenation in loop
        var code = @"
String buildText(List<String> items) {
  String result = '';
  for (int i = 0; i < items.length; i++) {
    result += items[i];
    result += ' ';
  }
  return result;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Dart_StringConcatInLoop_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal("high", antiPattern.Metadata["severity"]);
    }

    [Fact]
    public void DetectPatterns_DartSyncIO_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: synchronous file I/O
        var code = @"
void loadConfig() {
  final file = File('config.json');
  final content = file.readAsStringSync();
  final config = jsonDecode(content);
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Dart_SyncIO_AntiPattern");
        Assert.NotNull(antiPattern);
    }

    #endregion

    #region Dart Security Pattern Tests

    [Fact]
    public void DetectPatterns_DartHardcodedCredentials_ReturnsSecurityAntiPattern()
    {
        // Arrange - CRITICAL: hardcoded credentials
        var code = @"
class ApiClient {
  final apiKey = 'sk-1234567890abcdef';
  final password = 'mysecretpassword123';
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var securityPattern = patterns.FirstOrDefault(p => p.Name == "Dart_HardcodedCredentials_AntiPattern");
        Assert.NotNull(securityPattern);
        Assert.Equal("critical", securityPattern.Metadata["severity"]);
        Assert.Equal("CWE-798", securityPattern.Metadata["cwe"]);
        Assert.Equal(PatternCategory.Security, securityPattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartInsecureHttp_ReturnsSecurityAntiPattern()
    {
        // Arrange - Security issue: HTTP instead of HTTPS
        var code = @"
final apiUrl = 'http://api.example.com/data';
final response = await http.get(Uri.parse('http://external-api.com'));
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var httpPattern = patterns.FirstOrDefault(p => p.Name == "Dart_InsecureHttp_AntiPattern");
        Assert.NotNull(httpPattern);
        Assert.Equal("CWE-319", httpPattern.Metadata["cwe"]);
    }

    [Fact]
    public void DetectPatterns_DartSecureStorage_ReturnsPositivePattern()
    {
        // Arrange - Good practice: secure storage
        var code = @"
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthService {
  final _storage = FlutterSecureStorage();
  
  Future<void> saveToken(String token) async {
    await _storage.write(key: 'auth_token', value: token);
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var securePattern = patterns.FirstOrDefault(p => p.Name == "Dart_SecureStorage");
        Assert.NotNull(securePattern);
        Assert.Equal(PatternCategory.Security, securePattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartSQLInjection_ReturnsSecurityAntiPattern()
    {
        // Arrange - SQL injection risk
        var code = @"
Future<List<User>> searchUsers(String name) async {
  final result = await db.rawQuery('SELECT * FROM users WHERE name = ""$name""');
  return result.map((r) => User.fromMap(r)).toList();
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var sqlPattern = patterns.FirstOrDefault(p => p.Name == "Dart_SQLInjection_AntiPattern");
        Assert.NotNull(sqlPattern);
        Assert.Equal("CWE-89", sqlPattern.Metadata["cwe"]);
    }

    [Fact]
    public void DetectPatterns_DartDisabledCertVerification_ReturnsCriticalAntiPattern()
    {
        // Arrange - Critical: disabled certificate verification
        var code = @"
HttpClient createClient() {
  final client = HttpClient();
  client.badCertificateCallback = (cert, host, port) => true;
  return client;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var certPattern = patterns.FirstOrDefault(p => p.Name == "Dart_DisabledCertVerification_AntiPattern");
        Assert.NotNull(certPattern);
        Assert.Equal("critical", certPattern.Metadata["severity"]);
        Assert.Equal("CWE-295", certPattern.Metadata["cwe"]);
    }

    #endregion

    #region Dart Error Handling Pattern Tests

    [Fact]
    public void DetectPatterns_DartTryCatch_ReturnsPattern()
    {
        // Arrange
        var code = @"
Future<void> fetchData() async {
  try {
    final response = await http.get(url);
    processResponse(response);
  } on SocketException catch (e) {
    logger.error('Network error: $e');
  } on FormatException catch (e) {
    logger.error('Parse error: $e');
  } finally {
    cleanup();
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var tryCatchPattern = patterns.FirstOrDefault(p => p.Name == "Dart_TryCatch");
        Assert.NotNull(tryCatchPattern);
        Assert.True((bool)tryCatchPattern.Metadata["has_typed_catch"]);
        Assert.True((bool)tryCatchPattern.Metadata["has_finally"]);
    }

    [Fact]
    public void DetectPatterns_DartEmptyCatch_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: empty catch block
        var code = @"
void process() {
  try {
    riskyOperation();
  } catch (e) {
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var emptyPattern = patterns.FirstOrDefault(p => p.Name == "Dart_EmptyCatch_AntiPattern");
        Assert.NotNull(emptyPattern);
    }

    [Fact]
    public void DetectPatterns_DartCustomException_ReturnsPattern()
    {
        // Arrange
        var code = @"
class NetworkException extends Exception {
  final int statusCode;
  final String message;
  
  NetworkException(this.statusCode, this.message);
  
  @override
  String toString() => 'NetworkException($statusCode): $message';
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var exceptionPattern = patterns.FirstOrDefault(p => p.Name == "Dart_CustomException");
        Assert.NotNull(exceptionPattern);
    }

    [Fact]
    public void DetectPatterns_DartResultType_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:fpdart/fpdart.dart';

Either<NetworkError, User> fetchUser(String id) {
  try {
    final user = api.getUser(id);
    return Right(user);
  } catch (e) {
    return Left(NetworkError(e.toString()));
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var resultPattern = patterns.FirstOrDefault(p => p.Name == "Dart_ResultType");
        Assert.NotNull(resultPattern);
    }

    #endregion

    #region Dart Code Quality Pattern Tests

    [Fact]
    public void DetectPatterns_DartExtensionMethod_ReturnsPattern()
    {
        // Arrange
        var code = @"
extension StringExtension on String {
  String capitalize() {
    if (isEmpty) return this;
    return this[0].toUpperCase() + substring(1);
  }
  
  bool get isValidEmail => RegExp(r'^[\w-\.]+@[\w-]+\.\w+$').hasMatch(this);
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var extensionPattern = patterns.FirstOrDefault(p => p.Name == "Dart_ExtensionMethod");
        Assert.NotNull(extensionPattern);
        Assert.Equal(PatternCategory.CodeQuality, extensionPattern.Category);
    }

    [Fact]
    public void DetectPatterns_DartMixin_ReturnsPattern()
    {
        // Arrange
        var code = @"
mixin Loggable {
  void log(String message) => print('[LOG] $message');
}

class MyService with Loggable {
  void doWork() {
    log('Starting work...');
  }
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var mixinPattern = patterns.FirstOrDefault(p => p.Name == "Dart_Mixin");
        Assert.NotNull(mixinPattern);
    }

    [Fact]
    public void DetectPatterns_DartSealedClass_ReturnsPattern()
    {
        // Arrange
        var code = @"
sealed class Result<T> {}

class Success<T> extends Result<T> {
  final T value;
  Success(this.value);
}

class Failure<T> extends Result<T> {
  final String error;
  Failure(this.error);
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var sealedPattern = patterns.FirstOrDefault(p => p.Name == "Dart_SealedClass");
        Assert.NotNull(sealedPattern);
    }

    [Fact]
    public void DetectPatterns_DartFactoryConstructor_ReturnsPattern()
    {
        // Arrange
        var code = @"
class Logger {
  static final _instance = Logger._internal();
  
  Logger._internal();
  
  factory Logger() => _instance;
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var factoryPattern = patterns.FirstOrDefault(p => p.Name == "Dart_FactoryConstructor");
        Assert.NotNull(factoryPattern);
    }

    #endregion

    #region Flutter Widget Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterStatelessWidget_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class MyButton extends StatelessWidget {
  final String label;
  final VoidCallback onPressed;
  
  const MyButton({super.key, required this.label, required this.onPressed});
  
  @override
  Widget build(BuildContext context) {
    return ElevatedButton(onPressed: onPressed, child: Text(label));
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var widgetPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_StatelessWidget");
        Assert.NotNull(widgetPattern);
        Assert.Equal(PatternType.Flutter, widgetPattern.Type);
        Assert.Equal("MyButton", widgetPattern.Metadata["widget_name"]);
        Assert.True((bool)widgetPattern.Metadata["has_const_constructor"]);
    }

    [Fact]
    public void DetectPatterns_FlutterStatefulWidget_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class Counter extends StatefulWidget {
  const Counter({super.key});
  
  @override
  State<Counter> createState() => _CounterState();
}

class _CounterState extends State<Counter> {
  int _count = 0;
  
  @override
  Widget build(BuildContext context) {
    return Text('Count: $_count');
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var statefulPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_StatefulWidget");
        Assert.NotNull(statefulPattern);
        Assert.True((bool)statefulPattern.Metadata["has_state_class"]);
    }

    [Fact]
    public void DetectPatterns_FlutterSetStateInBuild_ReturnsCriticalAntiPattern()
    {
        // Arrange - Critical anti-pattern: setState in build
        var code = @"
import 'package:flutter/material.dart';

class BadWidget extends StatefulWidget {
  @override
  _BadWidgetState createState() => _BadWidgetState();
}

class _BadWidgetState extends State<BadWidget> {
  @override
  Widget build(BuildContext context) {
    setState(() {
      // This causes infinite loop!
    });
    return Container();
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_SetStateInBuild_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal("critical", antiPattern.Metadata["severity"]);
    }

    #endregion

    #region Flutter State Management Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterProvider_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

class MyApp extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => UserModel()),
      ],
      child: Consumer<UserModel>(
        builder: (context, user, child) => Text(user.name),
      ),
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var providerPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_Provider");
        Assert.NotNull(providerPattern);
        Assert.Equal(PatternCategory.StateManagement, providerPattern.Category);
    }

    [Fact]
    public void DetectPatterns_FlutterRiverpod_ReturnsPattern()
    {
        // Arrange - Include flutter import to trigger Flutter detection
        var code = @"
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

final counterProvider = StateNotifierProvider<CounterNotifier, int>((ref) {
  return CounterNotifier();
});

class MyWidget extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final count = ref.watch(counterProvider);
    return Text('$count');
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var riverpodPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_Riverpod");
        Assert.NotNull(riverpodPattern);
    }

    [Fact]
    public void DetectPatterns_FlutterBLoC_ReturnsPattern()
    {
        // Arrange - Include flutter/material.dart to trigger Flutter detection
        var code = @"
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class CounterCubit extends Cubit<int> {
  CounterCubit() : super(0);
  void increment() => emit(state + 1);
}

class MyWidget extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => CounterCubit(),
      child: BlocBuilder<CounterCubit, int>(
        builder: (context, count) => Text('$count'),
      ),
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var blocPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_BLoC");
        Assert.NotNull(blocPattern);
    }

    [Fact]
    public void DetectPatterns_FlutterExcessiveSetState_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: too many setState calls (need > 5)
        var code = @"
import 'package:flutter/material.dart';

class BadStateWidget extends StatefulWidget {
  @override
  _BadStateWidgetState createState() => _BadStateWidgetState();
}

class _BadStateWidgetState extends State<BadStateWidget> {
  void updateAll() {
    setState(() { _a = 1; });
    setState(() { _b = 2; });
    setState(() { _c = 3; });
    setState(() { _d = 4; });
    setState(() { _e = 5; });
    setState(() { _f = 6; });
    setState(() { _g = 7; });
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_ExcessiveSetState_AntiPattern");
        Assert.NotNull(antiPattern);
        var setStateCount = Convert.ToInt32(antiPattern.Metadata["setState_count"]);
        Assert.True(setStateCount > 5, $"Expected > 5 setState calls but found {setStateCount}");
    }

    #endregion

    #region Flutter Performance Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterListViewBuilder_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class ItemList extends StatelessWidget {
  final List<Item> items;
  
  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      itemCount: items.length,
      itemBuilder: (context, index) => ListTile(title: Text(items[index].name)),
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var listPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_LazyListBuilder");
        Assert.NotNull(listPattern);
        Assert.Equal(PatternCategory.Performance, listPattern.Category);
    }

    [Fact]
    public void DetectPatterns_FlutterListViewChildren_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: ListView with children (not lazy)
        var code = @"
import 'package:flutter/material.dart';

class BadList extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return ListView(
      children: [
        Text('Item 1'),
        Text('Item 2'),
        // ... many more items
      ],
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_ListViewChildren_AntiPattern");
        Assert.NotNull(antiPattern);
    }

    [Fact]
    public void DetectPatterns_FlutterRepaintBoundary_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class OptimizedWidget extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: AnimatedContainer(
        duration: Duration(milliseconds: 300),
        color: Colors.blue,
      ),
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var repaintPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_RepaintBoundary");
        Assert.NotNull(repaintPattern);
    }

    [Fact]
    public void DetectPatterns_FlutterCompute_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/foundation.dart';

Future<List<Item>> parseItems(String json) async {
  return await compute(_parseJson, json);
}

List<Item> _parseJson(String json) {
  // Heavy computation
  return jsonDecode(json).map((e) => Item.fromJson(e)).toList();
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var computePattern = patterns.FirstOrDefault(p => p.Name == "Flutter_ComputeIsolate");
        Assert.NotNull(computePattern);
    }

    #endregion

    #region Flutter Lifecycle Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterInitState_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class MyWidget extends StatefulWidget {
  @override
  _MyWidgetState createState() => _MyWidgetState();
}

class _MyWidgetState extends State<MyWidget> {
  @override
  void initState() {
    super.initState();
    _loadData();
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var initPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_InitState");
        Assert.NotNull(initPattern);
        Assert.True((bool)initPattern.Metadata["calls_super"]);
    }

    [Fact]
    public void DetectPatterns_FlutterDispose_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class MyWidget extends StatefulWidget {
  @override
  _MyWidgetState createState() => _MyWidgetState();
}

class _MyWidgetState extends State<MyWidget> {
  final _controller = TextEditingController();
  
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var disposePattern = patterns.FirstOrDefault(p => p.Name == "Flutter_Dispose");
        Assert.NotNull(disposePattern);
        Assert.True((bool)disposePattern.Metadata["calls_super"]);
    }

    [Fact]
    public void DetectPatterns_FlutterMissingDispose_ReturnsAntiPattern()
    {
        // Arrange - Anti-pattern: controller without dispose
        var code = @"
import 'package:flutter/material.dart';

class BadWidget extends StatefulWidget {
  @override
  _BadWidgetState createState() => _BadWidgetState();
}

class _BadWidgetState extends State<BadWidget> {
  final TextEditingController _nameController = TextEditingController();
  final AnimationController _animController;
  
  @override
  Widget build(BuildContext context) {
    return TextField(controller: _nameController);
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_MissingDispose_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal("high", antiPattern.Metadata["severity"]);
    }

    #endregion

    #region Flutter Navigation Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterGoRouter_ReturnsPattern()
    {
        // Arrange - Include flutter import to trigger Flutter detection
        var code = @"
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

final router = GoRouter(
  routes: [
    GoRoute(path: '/', builder: (context, state) => HomeScreen()),
    GoRoute(path: '/details/:id', builder: (context, state) => DetailsScreen(id: state.params['id']!)),
  ],
);

// Navigate
context.go('/details/123');
context.push('/settings');
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var routerPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_GoRouter");
        Assert.NotNull(routerPattern);
        Assert.Equal(PatternCategory.Routing, routerPattern.Category);
    }

    #endregion

    #region Flutter Animation Pattern Tests

    [Fact]
    public void DetectPatterns_FlutterAnimationController_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class AnimatedWidget extends StatefulWidget {
  @override
  _AnimatedWidgetState createState() => _AnimatedWidgetState();
}

class _AnimatedWidgetState extends State<AnimatedWidget> 
    with SingleTickerProviderStateMixin {
  late AnimationController _controller;
  
  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: Duration(seconds: 1));
  }
  
  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var animPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_AnimationController");
        Assert.NotNull(animPattern);
        Assert.True((bool)animPattern.Metadata["disposes_controller"]);
    }

    [Fact]
    public void DetectPatterns_FlutterImplicitAnimation_ReturnsPattern()
    {
        // Arrange
        var code = @"
import 'package:flutter/material.dart';

class MyWidget extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: Duration(milliseconds: 300),
      width: isExpanded ? 200 : 100,
      child: AnimatedOpacity(
        opacity: isVisible ? 1.0 : 0.0,
        duration: Duration(milliseconds: 200),
        child: Text('Hello'),
      ),
    );
  }
}
";
        // Act
        var patterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        var implicitPattern = patterns.FirstOrDefault(p => p.Name == "Flutter_ImplicitAnimation");
        Assert.NotNull(implicitPattern);
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Fact]
    public void DetectPatterns_EmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var code = "";

        // Act
        var dartPatterns = _dartDetector.DetectPatterns(code, "test.dart", "test");
        var flutterPatterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        Assert.Empty(dartPatterns);
        Assert.Empty(flutterPatterns);
    }

    [Fact]
    public void DetectPatterns_NonDartFile_ReturnsEmptyList()
    {
        // Arrange
        var code = "public class MyClass { }";

        // Act
        var dartPatterns = _dartDetector.DetectPatterns(code, "test.cs", "test");
        var flutterPatterns = _flutterDetector.DetectPatterns(code, "test.cs", "test");

        // Assert
        Assert.Empty(dartPatterns);
        Assert.Empty(flutterPatterns);
    }

    [Fact]
    public void DetectPatterns_NonFlutterDartFile_ReturnsOnlyDartPatterns()
    {
        // Arrange - Dart file without Flutter imports
        var code = @"
Future<int> compute() async {
  return await Future.value(42);
}
";
        // Act
        var dartPatterns = _dartDetector.DetectPatterns(code, "test.dart", "test");
        var flutterPatterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // Assert
        Assert.NotEmpty(dartPatterns);
        Assert.Empty(flutterPatterns); // No Flutter imports
    }

    [Fact]
    public void DetectPatterns_MalformedCode_DoesNotThrow()
    {
        // Arrange - Syntactically invalid code
        var code = @"
class { 
  void broken( {
    if (true {
";
        // Act & Assert - Should not throw
        var dartPatterns = _dartDetector.DetectPatterns(code, "test.dart", "test");
        var flutterPatterns = _flutterDetector.DetectPatterns(code, "test.dart", "test");

        // May or may not find patterns, but should not throw
        Assert.NotNull(dartPatterns);
        Assert.NotNull(flutterPatterns);
    }

    [Fact]
    public void DetectPatterns_NullContext_UsesDefault()
    {
        // Arrange
        var code = @"
Future<void> test() async {
  await Future.value();
}
";
        // Act
        var patterns = _dartDetector.DetectPatterns(code, "test.dart", null);

        // Assert
        Assert.NotEmpty(patterns);
        // Context should be null (or default)
    }

    [Fact]
    public void DetectPatterns_LargeFile_CompletesInReasonableTime()
    {
        // Arrange - Generate a large file
        var codeBuilder = new System.Text.StringBuilder();
        codeBuilder.AppendLine("import 'package:flutter/material.dart';");
        
        for (int i = 0; i < 100; i++)
        {
            codeBuilder.AppendLine($@"
class Widget{i} extends StatelessWidget {{
  const Widget{i}({{super.key}});
  
  @override
  Widget build(BuildContext context) {{
    return Container(child: Text('Widget {i}'));
  }}
}}
");
        }

        var code = codeBuilder.ToString();
        _output.WriteLine($"Generated code size: {code.Length} characters");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var patterns = _flutterDetector.DetectPatterns(code, "large.dart", "test");
        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(patterns);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Detection took too long: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Detection completed in {stopwatch.ElapsedMilliseconds}ms, found {patterns.Count} patterns");
    }

    #endregion
}

