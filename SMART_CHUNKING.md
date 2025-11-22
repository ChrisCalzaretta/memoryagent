# Smart Chunking & Enhanced Relationships

## 🎯 Overview

The Memory Code Agent now supports **smart chunking** for multiple file types with **enhanced Neo4j relationship tracking** for better code intelligence.

---

## 📁 Supported File Types

| Extension | Parser | Chunking Strategy | Status |
|-----------|--------|-------------------|--------|
| `.cs` | Roslyn | Classes, methods, properties, interfaces | ✅ Original |
| `.cshtml` | RazorParser | Sections, code blocks, HTML sections, components | ✅ NEW |
| `.razor` | RazorParser | Same as .cshtml | ✅ NEW |
| `.py` | PythonParser | Classes, functions, decorators, imports | ✅ NEW |
| `.md` | MarkdownParser | Headers, sections, code blocks, links | ✅ NEW |
| `.css` | CssParser | Rules, variables, media queries, animations | ✅ **NEWEST** |
| `.scss` | CssParser | Same as CSS + mixins, functions, variables | ✅ **NEWEST** |
| `.less` | CssParser | Same as CSS + mixins, variables | ✅ **NEWEST** |
| `.json` | - | Not chunked (as requested) | ❌ Excluded |
| `.sql` | - | Not chunked (as requested) | ❌ Excluded |
| `.ps1` | - | Not chunked (as requested) | ❌ Excluded |

---

## 🔪 Razor/CSHTML Smart Chunking

### What Gets Chunked:

#### 1. **@section Blocks**
```cshtml
@section Scripts {
    <script src="..."></script>
}
```
- **Stored as:** `Section_Scripts`
- **Type:** Method
- **Metadata:** `razor_section`, `section_name`

#### 2. **@code Blocks** (Blazor/Razor Pages)
```razor
@code {
    private string message = "Hello";
    
    protected override void OnInitialized()
    {
        // ...
    }
}
```
- **Stored as:** `{FileName}_CodeBlock`
- **Type:** Class
- **Metadata:** `razor_code_block`

#### 3. **@functions Blocks**
```cshtml
@functions {
    public string GetFullName(string first, string last)
    {
        return $"{first} {last}";
    }
}
```
- **Stored as:** `{FileName}_Functions`
- **Type:** Method
- **Metadata:** `razor_functions_block`

#### 4. **HTML Sections** (by heading tags or divs with IDs)
```html
<div id="dashboard">
    <h2>Dashboard</h2>
    <!-- content -->
</div>
```
- **Stored as:** `HtmlSection_dashboard`
- **Type:** Method
- **Metadata:** `html_section`, `section_id`

#### 5. **Component Usages**
```razor
<WeatherForecast />
<DataTable Data="@items" />
```
- **Creates USES relationship:** `PageName → ComponentName`
- **Metadata:** `uses_component`

### Razor Relationships:

| Relationship | From | To | Example |
|--------------|------|-----|---------|
| **USES** (model) | Page | Model Type | `Index.cshtml` → `ProductViewModel` |
| **USES** (component) | Page | Component | `Dashboard.razor` → `DataTable` |

### Razor Metadata Captured:

- `@model` directive → `model_type`
- `@page` route → `page_route`
- File type → `.cshtml` or `.razor`
- `is_razor` flag → `true`

---

## 🐍 Python Smart Chunking

### What Gets Chunked:

#### 1. **Classes**
```python
class UserService(BaseService):
    def __init__(self, db: Database):
        self.db = db
```
- **Stored as:** `UserService`
- **Type:** Class
- **Metadata:** `language: python`, `base_classes: BaseService`
- **Creates:** INHERITS relationship to `BaseService`

#### 2. **Methods** (within classes)
```python
class UserService:
    def get_user(self, user_id: int) -> User:
        return self.db.query(User).get(user_id)
```
- **Stored as:** `UserService.get_user`
- **Type:** Method
- **Metadata:** `class_name`, `method_name`, `parameters`, `return_type`
- **Creates:** 
  - DEFINES: `UserService` → `UserService.get_user`
  - RETURNSTYPE: `get_user` → `User`

#### 3. **Top-Level Functions**
```python
def calculate_total(items: List[Item]) -> Decimal:
    return sum(item.price for item in items)
```
- **Stored as:** `calculate_total`
- **Type:** Method
- **Metadata:** `is_top_level: true`, `parameters`, `return_type`

#### 4. **Imports**
```python
from flask import Flask, request
import pandas as pd
```
- **Creates IMPORTS relationship:** `{FileName}` → `flask`, `pandas`
- **Metadata:** `line_number`, `import_statement`

#### 5. **Decorators**
```python
@app.route('/api/users')
@login_required
def get_users():
    pass
```
- **Creates HASATTRIBUTE relationship:** `get_users` → `app.route`, `login_required`
- **Metadata:** `decorator` name

#### 6. **Function Calls**
```python
def process_data():
    results = validate_input(data)
    save_to_database(results)
```
- **Creates CALLS relationship:** `process_data` → `validate_input`, `save_to_database`

### Python Relationships:

| Relationship | From | To | Example |
|--------------|------|-----|---------|
| **IMPORTS** | File | Module | `app.py` → `flask` |
| **INHERITS** | Class | Base Class | `UserService` → `BaseService` |
| **CALLS** | Function | Called Function | `process_data` → `validate_input` |
| **RETURNSTYPE** | Function | Return Type | `get_user` → `User` |
| **HASATTRIBUTE** | Function | Decorator | `get_users` → `login_required` |
| **DEFINES** | Class | Method | `UserService` → `UserService.get_user` |

### Python Metadata Captured:

- Language → `python`
- Parameters with type hints
- Return type annotations
- Base classes
- Top-level vs. class methods
- Decorator names

---

## 📝 Markdown Smart Chunking

### What Gets Chunked:

#### 1. **Headers & Sections**
```markdown
# Main Title

## Introduction
Content for the introduction section...

### Subsection 1
More detailed content...

### Subsection 2
Additional details...
```
- **Stored as:** `Section: Introduction`, `Section: Subsection 1`, etc.
- **Type:** Other
- **Metadata:** `chunk_type: section`, `header_level`, `section_title`, `line_count`
- **Chunking logic:** Each header starts a new section that includes all content until the next header of equal or higher level

#### 2. **Front Matter** (YAML or TOML)
```markdown
---
title: API Documentation
author: Development Team
date: 2025-11-22
tags: [api, docs, reference]
---

# API Documentation
...
```
- **Captured in metadata:** `fm_title`, `fm_author`, `fm_date`, `fm_tags`
- **Metadata:** `has_front_matter: true`

#### 3. **Code Blocks**
````markdown
Here's an example:

```python
def hello_world():
    print("Hello, World!")
```

```csharp
public class Example 
{
    public string Name { get; set; }
}
```
````
- **Stored as:** `CodeBlock_1_python`, `CodeBlock_2_csharp`
- **Type:** Other
- **Metadata:** `chunk_type: code_block`, `language`, `code_length`
- **Creates DEFINES relationship:** `{FileName}` → `CodeBlock_N_{language}`

#### 4. **Links & References**
```markdown
Check out the [API Guide](api-guide.md) for more details.

Visit [our website](https://example.com) for updates.

See [internal section](#architecture) below.
```
- **Creates USES relationship:** For `.md` file references: `README.md` → `api-guide.md`
- **Metadata captured:**
  - `link_count`: Total number of links
  - `links_external`: Count of http/https links
  - `links_markdown_reference`: Count of `.md` references
  - `links_internal_anchor`: Count of `#` anchor links
  - `links_relative`: Count of other relative links

### Markdown Relationships:

| Relationship | From | To | Example |
|--------------|------|-----|---------|
| **USES** (reference) | Markdown File | Referenced .md File | `README.md` → `CONTRIBUTING.md` |
| **DEFINES** (code block) | Markdown File | Code Block | `Tutorial.md` → `CodeBlock_1_python` |

### Markdown Metadata Captured:

- **Front Matter:** All YAML/TOML key-value pairs prefixed with `fm_`
- **Title:** First H1 header → `title`
- **File Type:** `.md` → `file_type`, `is_markdown: true`
- **Links:** Categorized by type with counts
- **Code Blocks:** Language and count → `code_block_count`

### Example Chunked Output:

For a file `API_GUIDE.md`:
```markdown
---
version: 2.0
---

# API Guide

## Authentication
Use Bearer tokens for all API calls...

```python
headers = {"Authorization": "Bearer TOKEN"}
```

## Endpoints
See [endpoints reference](endpoints.md).
```

**Results in:**
1. **File node:** `API_GUIDE.md` with metadata `fm_version: 2.0`, `title: API Guide`, `code_block_count: 1`, `link_count: 1`
2. **Section chunk:** `Section: Authentication` (lines 5-11)
3. **Code block chunk:** `CodeBlock_1_python`
4. **Section chunk:** `Section: Endpoints` (lines 13-14)
5. **Relationship:** `API_GUIDE.md` → USES → `endpoints.md`
6. **Relationship:** `API_GUIDE.md` → DEFINES → `CodeBlock_1_python`

---

## 📊 Enhanced Neo4j Relationships

### Relationship Summary (All Languages):

| Type | C# | Razor | Python | Markdown | Total Use Cases |
|------|-----|-------|--------|----------|-----------------|
| **CALLS** | ✅ | ✅ | ✅ | - | Method calls across all languages |
| **IMPORTS** | ✅ | - | ✅ | - | Using directives, import statements |
| **INHERITS** | ✅ | - | ✅ | - | Class inheritance |
| **IMPLEMENTS** | ✅ | - | - | - | Interface implementation |
| **USES** | ✅ | ✅ | - | ✅ | General usage, components, .md references |
| **HASTYPE** | ✅ | - | - | - | Property types |
| **RETURNSTYPE** | ✅ | - | ✅ | - | Method return types |
| **ACCEPTSTYPE** | ✅ | - | - | - | Parameter types |
| **INJECTS** | ✅ | - | - | - | Constructor injection (DI) |
| **HASATTRIBUTE** | ✅ | - | ✅ | - | Attributes/decorators |
| **USESGENERIC** | ✅ | - | - | - | Generic type parameters |
| **THROWS** | ✅ | - | - | - | Exception declarations |
| **CATCHES** | ✅ | - | - | - | Exception handling |
| **DEFINES** | ✅ | ✅ | ✅ | ✅ | Containment relationships |

---

## 🎨 Chunking Size Limits

To prevent embedding generation failures and keep chunks manageable:

| Element Type | Max Lines | Reasoning |
|--------------|-----------|-----------|
| **Class (Python)** | 200 | Full class with methods |
| **Method (Python)** | 100 | Complete function body |
| **Razor @section** | Unlimited | Stopped at closing brace |
| **Razor @code** | Unlimited | Stopped at closing brace |
| **HTML Section** | 1500 chars | Preview of major sections |
| **File Summary** | 2000 chars | Overview of file |

---

## 🔍 Search Improvements

### Lowered Similarity Threshold

**Before:** `minimumScore = 0.7` (too restrictive)  
**After:** `minimumScore = 0.5` (better results)

**Impact:**
- More relevant results returned
- Better matches for similar but not identical code
- Improved AI context for code generation

### Multi-Language Search

Search now returns results from:
- ✅ C# classes and methods
- ✅ Razor sections and components
- ✅ Python classes and functions
- ✅ All in one unified result set

---

## 📈 Expected Indexing Results

For a typical ASP.NET + Razor project like DataPrepPlatform:

**Before** (C# only):
- 536 .cs files
- 577 classes
- 1,526 methods

**After** (with Razor + Python + Markdown):
- **~720+ files** (536 .cs + 169 .cshtml/.razor + Python files + .md files)
- **~750+ classes** (C# classes + Razor @code blocks + Python classes)
- **~2,600+ methods** (C# methods + Razor sections/functions + Python functions + Markdown sections)
- **50,000+ relationships** (all relationship types combined)

---

## 🚀 Usage Examples

### Search Across All Languages:

```json
{
  "query": "user authentication",
  "context": "MyProject",
  "limit": 20,
  "minimumScore": 0.5
}
```

**Returns:**
- C# authentication services
- Razor login pages
- Python auth decorators
- Markdown documentation sections about authentication
- All ranked by relevance

### Find Component Usage:

```cypher
// In Neo4j
MATCH (page)-[:USES {relationship_type: 'uses_component'}]->(component)
WHERE component.name = 'DataTable'
RETURN page.name
```

**Returns all Razor pages/components that use `<DataTable>`**

### Trace Python Dependencies:

```cypher
MATCH (file)-[:IMPORTS]->(module)
WHERE module.name = 'flask'
RETURN DISTINCT file.name
```

**Returns all Python files that import Flask**

### Find Markdown References:

```cypher
MATCH (md1:File)-[:USES {relationship_subtype: 'markdown_reference'}]->(md2)
WHERE md1.file_type = '.md'
RETURN md1.name, collect(md2.name) as references
```

**Returns all Markdown files and what other .md files they reference**

---

## 🎯 Best Practices

### For Razor/CSHTML:

1. **Use @section blocks** - Each section gets indexed separately
2. **Use IDs on major divs** - Helps HTML section chunking
3. **Keep @code blocks focused** - Better chunking granularity

### For Python:

1. **Use type hints** - Creates better RETURNSTYPE relationships
2. **Document decorators** - Tracked as HASATTRIBUTE
3. **Organize imports at top** - All captured in IMPORTS relationships

### For C#:

1. **Use dependency injection** - Creates INJECTS relationships
2. **Document exceptions** - Creates THROWS relationships
3. **Use interfaces** - Creates IMPLEMENTS relationships

### For Markdown:

1. **Use clear header hierarchy** - Each header creates a searchable section
2. **Link to other .md files** - Creates USES relationships for navigation
3. **Use front matter** - Metadata gets indexed for better organization
4. **Fence code blocks with language** - Helps with code block categorization

---

## 🔧 Configuration

No configuration needed! The parser automatically:
- Detects file type by extension
- Routes to appropriate chunker
- Stores all metadata
- Creates all relationships

---

## 📝 Metadata Fields

Each indexed element includes:

### Common Fields:
- `name` - Element name
- `content` - Code snippet
- `file_path` - Source file
- `context` - Project context
- `line_number` - Starting line
- `indexed_at` - Timestamp

### Language-Specific:
- **Razor:** `is_razor`, `model_type`, `page_route`, `element_type`
- **Python:** `language`, `base_classes`, `parameters`, `return_type`, `is_top_level`
- **Markdown:** `is_markdown`, `has_front_matter`, `fm_*` (front matter fields), `title`, `header_level`, `section_title`, `link_count`, `code_block_count`, `language` (for code blocks)
- **C#:** (all Roslyn metadata from existing implementation)

---

## 🎉 Summary

**Smart chunking now provides:**
- ✅ Multi-language support (C#, Razor, Python, **Markdown**)
- ✅ Intelligent section detection (code sections + **documentation sections**)
- ✅ 50,000+ relationships tracked
- ✅ Better search relevance (0.5 threshold)
- ✅ Component and decorator tracking
- ✅ Cross-language dependency graphs
- ✅ **Documentation indexing** (Markdown files with front matter, headers, code blocks, links)
- ✅ Comprehensive metadata preservation

**Next steps:** Start your project with `.\start-project.ps1 -ProjectPath "path" -AutoIndex` and watch it index **everything** - code AND documentation! 🚀

