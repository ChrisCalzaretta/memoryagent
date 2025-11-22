# File Type Support in Memory Agent

## Supported Languages & File Types

Memory Agent now supports comprehensive indexing of multiple programming languages and file types with semantic chunking.

### ✅ Fully Supported Languages

#### 1. **C# (.cs)**
- **Parser**: RoslynParser (Microsoft Roslyn)
- **Features**:
  - Full semantic analysis with Roslyn compiler
  - Classes, interfaces, structs, enums
  - Methods, properties, fields, events
  - Namespaces and using directives
  - Inheritance and interface implementation
  - Dependency injection patterns
  - LINQ queries  
  - Validation attributes
  - Entity Framework queries
  - Endpoint detection (API controllers)
- **Chunking**: Advanced semantic chunking with relationship tracking
- **Note**: Includes .cshtml.cs and .razor.cs code-behind files

#### 2. **VB.NET (.vb)**
- **Parser**: VBNetParser
- **Features**:
  - Classes, modules, structures, interfaces
  - Functions and subroutines
  - Properties
  - Imports statements
  - Inheritance (Inherits/Implements)
  - Access modifiers
- **Chunking**: Semantic chunking with VB-specific syntax

#### 3. **Razor (.cshtml, .razor)**
- **Parser**: RazorParser + RazorSemanticAnalyzer
- **Features**:
  - @model and @page directives
  - @section blocks
  - @code and @functions blocks
  - HTML sections (h1, h2, h3)
  - Semantic HTML elements (form, table, nav, header, footer, aside, main, section, article)
  - Form actions and endpoints
  - Razor component usage
  - Style tags and inline styles
  - Model binding relationships
- **Chunking**: Advanced HTML/Razor chunking with semantic analysis

#### 4. **CSS (.css, .scss, .less)**
- **Parser**: CssParser
- **Features**:
  - CSS rules (selectors and declarations)
  - CSS custom properties (--var-name)
  - SCSS/LESS variables ($var or @var)
  - Media queries
  - Keyframe animations
  - SCSS mixins and functions
- **Chunking**: Style-based chunking with selector tracking

#### 5. **JavaScript (.js, .jsx)**
- **Parser**: JavaScriptParser
- **Features**:
  - ES6 classes and methods
  - Functions (function declarations, arrow functions, function expressions)
  - React components (class and functional)
  - Import/export statements (ES6 modules and CommonJS)
  - Async functions
- **Chunking**: Semantic chunking with React awareness

#### 6. **TypeScript (.ts, .tsx)**
- **Parser**: JavaScriptParser (TypeScript-aware)
- **Features**:
  - All JavaScript features
  - Interfaces and type aliases
  - Type definitions
  - React components with TypeScript
  - Generic types
- **Chunking**: Type-aware semantic chunking

#### 7. **Python (.py)**
- **Parser**: PythonParser
- **Features**:
  - Classes and methods
  - Functions (top-level and nested)
  - Decorators
  - Import statements
  - Docstrings
- **Chunking**: Semantic chunking with Python syntax

#### 8. **Markdown (.md, .markdown)**
- **Parser**: MarkdownParser
- **Features**:
  - Headings (H1-H6)
  - Code blocks with language detection
  - Lists and tables
  - Links and references
- **Chunking**: Document structure-based chunking

## Comparison: Python vs C# Parsing

### C# (RoslynParser)
- ✅ **Full compiler integration** (Microsoft Roslyn)
- ✅ **Complete semantic analysis** (understands code meaning)
- ✅ **Type inference and resolution**
- ✅ **Pattern detection** (DI, LINQ, EF queries, validations, endpoints)
- ✅ **Relationship tracking** (inheritance, implementations, dependencies)
- ✅ **Framework-specific features** (ASP.NET, EF Core)

### Python (PythonParser)  
- ⚠️ **Regex-based parsing** (pattern matching, not full parsing)
- ✅ **Basic structure extraction** (classes, functions, decorators)
- ✅ **Import tracking**
- ❌ **No type inference** (limited without type hints analysis)
- ❌ **No framework-specific patterns** (e.g., Django, Flask patterns not detected)
- ⚠️ **Limited relationship tracking**

**Recommendation**: Python parsing is adequate for basic code understanding but could be enhanced with:
- AST (Abstract Syntax Tree) parsing for better accuracy
- Type hint analysis
- Framework pattern detection (Django models, Flask routes, FastAPI endpoints)
- More sophisticated relationship tracking

## File Pattern Exclusions

The following patterns are **automatically excluded** from indexing:
- `**/obj/**` - Build artifacts
- `**/bin/**` - Build outputs
- `**/node_modules/**` - NPM dependencies
- `.cshtml.cs` files when matched by `*.cshtml` pattern (picked up by `*.cs` instead)
- `.razor.cs` files when matched by `*.razor` pattern (picked up by `*.cs` instead)

## Indexing Statistics

When indexing a directory, the system reports:
```
Found 926 code files to index:
  - 476 .cs files
  - 8 .vb files
  - 176 .cshtml/.razor files  
  - 13 .py files
  - 223 .md files
  - 38 .css/.scss/.less files
  - 92 .js/.ts/.jsx/.tsx files
```

## Adding New File Types

To add support for a new file type:

1. Create a new parser class in `MemoryAgent.Server/CodeAnalysis/`:
   ```csharp
   public class MyLanguageParser
   {
       public static ParseResult ParseMyLanguageFile(string filePath, string? context = null)
       {
           // Implementation
       }
   }
   ```

2. Add routing in `RoslynParser.ParseFileAsync()`:
   ```csharp
   ".mylang" => await Task.Run(() => MyLanguageParser.ParseMyLanguageFile(filePath, context), cancellationToken),
   ```

3. Add pattern to `IndexingService.cs`:
   ```csharp
   var patterns = new[] { ..., "*.mylang" };
   ```

4. Add pattern to `ReindexService.cs`:
   ```csharp
   var patterns = new[] { ..., "*.mylang" };
   ```

5. Update logging in `IndexingService.cs` to include the new file type count.

## Future Enhancements

- **Go** (.go)  
- **Rust** (.rs)
- **Java** (.java)
- **PHP** (.php)
- **Ruby** (.rb)
- **HTML** (.html) - Pure HTML files (not Razor)
- **JSON** (.json) - Configuration files
- **YAML** (.yaml, .yml) - Configuration files
- **XML** (.xml) - Configuration and data files

---

Last Updated: November 22, 2025

