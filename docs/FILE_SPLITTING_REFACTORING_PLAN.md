# 🔧 File Splitting Refactoring Plan

## ⚠️ CRITICAL: 7 Files Violating 800-Line Rule

**Rule Violated:** "ALWAYS Keep files under 800 lines - refactor if exceeding"

---

## 📊 Files Requiring Split

| File | Current Lines | Target Files | Lines Per File |
|------|---------------|--------------|----------------|
| **PythonPatternDetector.cs** | 2945 | 6 partial files | ~500 each |
| **AgentFrameworkPatternDetector.cs** | 2569 | 4 partial files | ~650 each |
| **AIAgentPatternDetector.cs** | 2156 | 3 partial files | ~720 each |
| **AGUIPatternDetector.cs** | 2017 | 3 partial files | ~670 each |
| **PluginArchitecturePatternDetector.cs** | 1349 | 2 partial files | ~675 each |
| **StateManagementPatternDetector.cs** | 1238 | 2 partial files | ~620 each |
| **VBNetPatternDetector.cs** | 1093 | 2 partial files | ~550 each |

---

## 🎯 Python Pattern Detector Split Plan

### Current Structure (2945 lines)
```
PythonPatternDetector.cs
├── Caching Patterns (lines 97-204)
├── Retry Patterns (lines 205-301)
├── Validation Patterns (lines 302-386)
├── Dependency Injection (lines 387-444)
├── Logging Patterns (lines 445-502)
├── Error Handling (lines 503-599)
├── API Design (lines 600-660)
├── Publisher-Subscriber (lines 661-988)
├── Azure Web PubSub (lines 989-1207)
├── Azure Architecture Patterns (lines 1208-1815) - 36 patterns!
├── Helper Methods (lines 1816-1868)
├── State Management (lines 1869-2200)
└── Decorator Patterns (lines 2201-2945) - 13 patterns!
```

### Target Structure (6 files, ~500 lines each)

1. **PythonPatternDetector.cs** (Main - 172 lines) ✅ DONE
   - Interface implementation
   - DetectPatterns orchestration
   - Shared helper methods

2. **PythonPatternDetector.CorePatterns.cs** (~700 lines)
   - Caching Patterns
   - Retry Patterns
   - Validation Patterns
   - Dependency Injection
   - Logging Patterns
   - Error Handling
   - API Design

3. **PythonPatternDetector.Messaging.cs** (~400 lines)
   - Publisher-Subscriber Patterns
   - Azure Web PubSub Patterns

4. **PythonPatternDetector.StateManagement.cs** (~330 lines)
   - All 16 state management patterns

5. **PythonPatternDetector.Decorators.cs** (~745 lines)
   - Authentication Decorators
   - Authorization Decorators
   - Database Transaction Decorators
   - OOP Decorators (@staticmethod, @classmethod, @property)
   - Async Decorators
   - Flask Lifecycle Decorators
   - Dataclass Decorators
   - Context Manager Decorators
   - Deprecation Decorators
   - Decorator Utilities

6. **PythonPatternDetector.AzureArchitecture.cs** (~600 lines)
   - 36 Azure Architecture Patterns (CQRS, Event Sourcing, Saga, etc.)

---

## ⚡ Execution Strategy

### Phase 1: Python (HIGHEST PRIORITY - 3.7x over limit)
- [x] Create main orchestrator file
- [ ] Extract CorePatterns partial class
- [ ] Extract Messaging partial class
- [ ] Extract StateManagement partial class
- [ ] Extract Decorators partial class
- [ ] Extract AzureArchitecture partial class
- [ ] Build and verify

### Phase 2: Agent Framework (3.2x over limit)
- [ ] Split into 4 partial files

### Phase 3: AI Agent (2.7x over limit)
- [ ] Split into 3 partial files

### Phase 4: AG-UI (2.5x over limit)
- [ ] Split into 3 partial files

### Phase 5: Plugin Architecture (1.7x over limit)
- [ ] Split into 2 partial files

### Phase 6: State Management (1.5x over limit)
- [ ] Split into 2 partial files

### Phase 7: VB.NET (1.4x over limit)
- [ ] Split into 2 partial files

---

## ✅ Benefits After Refactoring

1. **Maintainability**: Each file focuses on one concern
2. **Readability**: ~500-700 lines per file (easy to navigate)
3. **Rule Compliance**: All files under 800-line limit
4. **Testability**: Easier to unit test individual pattern categories
5. **Performance**: No performance impact (same compiled output)
6. **Collaboration**: Reduces merge conflicts

---

## 🚀 Status

- **Started**: [Current Date/Time]
- **Phase**: 1 (Python) - Main file created
- **Completion**: 1/6 files (17%)

---

**Next Steps**: Extract remaining 5 Python partial class files, then move to Phase 2.

