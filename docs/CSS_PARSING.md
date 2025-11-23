# CSS/SCSS/LESS Parsing & Indexing

## 🎨 Overview

The Memory Code Agent now supports **CSS, SCSS, and LESS** stylesheets with intelligent chunking and relationship tracking, including:
- Standalone `.css`, `.scss`, `.less` files
- `<style>` tags embedded in Razor/HTML files
- Inline `style=""` attributes (when significant)

---

## 📁 Supported Stylesheet Types

| Format | Extension | Features Supported |
|--------|-----------|-------------------|
| **CSS** | `.css` | Rules, variables, media queries, animations |
| **SCSS** | `.scss` | All CSS features + mixins, functions, nested rules, variables ($) |
| **LESS** | `.less` | All CSS features + mixins, variables (@) |
| **Embedded CSS** | `<style>` tags | Extracts CSS from Razor/CSHTML files |
| **Inline Styles** | `style=""` | Significant inline styles (>50 chars) in Razor/HTML |

---

## 🔪 What Gets Chunked

### 1. **CSS Rules (Selectors + Declarations)**

```css
.btn-primary {
    background-color: #007bff;
    color: white;
    padding: 10px 20px;
}
```

**Stored as:**
- **Name**: `Style: .btn-primary`
- **Type**: Pattern
- **Content**: Full rule with selector and declarations
- **Metadata**: `selector`, `has_nested`

---

### 2. **CSS Custom Properties (Variables)**

```css
:root {
    --primary-color: #007bff;
    --spacing-unit: 8px;
}
```

**Stored as:**
- **Name**: `Variable: --primary-color`
- **Type**: Property
- **Content**: `--primary-color: #007bff;`
- **Metadata**: `variable_name`, `variable_value`

---

### 3. **SCSS Variables**

```scss
$primary-color: #007bff;
$font-stack: Helvetica, sans-serif;
```

**Stored as:**
- **Name**: `Variable: $primary-color`
- **Type**: Property
- **Content**: `$primary-color: #007bff;`
- **Metadata**: `variable_name`, `variable_value`

---

### 4. **LESS Variables**

```less
@primary-color: #007bff;
@base-font-size: 16px;
```

**Stored as:**
- **Name**: `Variable: @primary-color`
- **Type**: Property
- **Content**: `@primary-color: #007bff;`
- **Metadata**: `variable_name`, `variable_value`

---

### 5. **Media Queries**

```css
@media (max-width: 768px) {
    .container {
        width: 100%;
    }
}
```

**Stored as:**
- **Name**: `MediaQuery: (max-width: 768px)`
- **Type**: Pattern
- **Content**: Full media query with rules
- **Metadata**: `condition`

---

### 6. **Keyframe Animations**

```css
@keyframes fadeIn {
    0% { opacity: 0; }
    100% { opacity: 1; }
}
```

**Stored as:**
- **Name**: `Animation: fadeIn`
- **Type**: Pattern
- **Content**: Full keyframe definition
- **Metadata**: `animation_name`

---

### 7. **SCSS Mixins**

```scss
@mixin button-style($bg-color, $text-color) {
    background-color: $bg-color;
    color: $text-color;
    border-radius: 4px;
}
```

**Stored as:**
- **Name**: `Mixin: button-style`
- **Type**: Method
- **Content**: Full mixin definition
- **Metadata**: `mixin_name`, `parameters`

---

## 📊 Relationships Tracked

### File → Elements

All stylesheet elements create a **DEFINES** relationship:

```
styles.css → DEFINES → Style: .btn-primary
styles.css → DEFINES → Variable: --primary-color
styles.css → DEFINES → Animation: fadeIn
styles.scss → DEFINES → Mixin: button-style
```

---

## 🔍 Example Queries

Once indexed, you can query stylesheets using natural language:

### Finding Styles:
```
"Show me all button styles"
"Find the primary color variable"
"Where are media queries for mobile defined?"
```

### Finding Animations:
```
"What animations are available?"
"Show me the fade in animation"
```

### Finding Variables:
```
"List all color variables"
"What's the primary spacing unit?"
```

### Finding Mixins (SCSS/LESS):
```
"Show me button mixins"
"Find all mixins that accept parameters"
```

---

## 📁 Indexing CSS Files

### Automatic Indexing

When you index a directory, CSS/SCSS/LESS files are automatically included:

```powershell
.\start-project.ps1 -ProjectPath "E:\GitHub\MyProject" -AutoIndex
```

**Excluded directories:**
- `node_modules/` (automatically skipped)
- `obj/` and `bin/` (build artifacts)

---

### Manual Indexing

```powershell
# Index specific CSS file
$body = @{path='/workspace/MyProject/styles/main.css';context='MyProject'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/index/file -Method POST -Body $body -ContentType 'application/json'

# Index entire styles directory
$body = @{path='/workspace/MyProject/styles';recursive=$true;context='MyProject'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/index/directory -Method POST -Body $body -ContentType 'application/json'
```

---

## 📈 Statistics

After indexing, you'll see counts like:

```
success          : True
filesIndexed     : 872
classesFound     : 738
methodsFound     : 2120        # Includes SCSS mixins
patternsDetected : 9618        # Includes CSS rules, media queries, animations
errors           : {}
```

**Pattern types include:**
- CSS rules (selectors + declarations)
- Media queries
- Keyframe animations

**Property types include:**
- CSS custom properties (`--var`)
- SCSS variables (`$var`)
- LESS variables (`@var`)

**Method types include:**
- SCSS mixins

---

## 🎯 Use Cases

### 1. **Design System Discovery**
Find all color variables, spacing units, and typography styles across your project.

### 2. **Responsive Design Analysis**
Locate all media queries and understand breakpoint strategies.

### 3. **Animation Inventory**
Discover all available animations and their definitions.

### 4. **SCSS Mixin Library**
Find reusable mixins and understand their parameters.

### 5. **Style Consistency**
Search for similar selectors or duplicate rules across files.

### 6. **Refactoring Support**
Before changing a variable or mixin, see where it might be used.

---

## 🚀 Advanced Features

### Nested Rules (SCSS)

```scss
.card {
    padding: 20px;
    
    .card-title {
        font-size: 24px;
    }
}
```

**Tracked as:** Individual rules with nested indicator in metadata

### Complex Selectors

```css
.navbar > .nav-item:hover:not(.disabled) {
    background-color: #f0f0f0;
}
```

**Preserved:** Full selector context maintained

### Preprocessor Features

- **SCSS**: `$variables`, `@mixins`, nested rules
- **LESS**: `@variables`, mixins (without `@mixin` keyword)

---

## ⚙️ Configuration

### File Patterns Indexed

```csharp
"*.css", "*.scss", "*.less"
```

### Excluded Paths

- `**/node_modules/**`
- `**/obj/**`
- `**/bin/**`

---

## 📝 Metadata Stored

Each CSS chunk includes metadata:

```json
{
    "chunk_type": "css_rule | css_variable | media_query | keyframe_animation | scss_mixin",
    "selector": ".btn-primary",
    "variable_name": "--primary-color",
    "variable_value": "#007bff",
    "condition": "(max-width: 768px)",
    "animation_name": "fadeIn",
    "mixin_name": "button-style",
    "parameters": "$bg-color, $text-color",
    "has_nested": true/false
}
```

---

## 🔧 Technical Details

### Parser: `CssParser.cs`

**Extracts:**
1. CSS rules (regex-based selector/declaration matching)
2. Variables (CSS custom properties, SCSS $, LESS @)
3. Media queries (@media)
4. Keyframe animations (@keyframes)
5. SCSS mixins (@mixin)

**Chunking strategy:**
- Each rule is a separate chunk
- Each variable is a separate chunk
- Each media query is a separate chunk
- Each animation is a separate chunk
- Each mixin is a separate chunk

**Token limits:**
- All chunks respect the 1400 character limit (embedded in EmbeddingService)
- Head+tail truncation applied if needed

---

## ✅ Status

**CSS Parsing: ACTIVE**
- ✅ CSS files indexed
- ✅ SCSS files indexed
- ✅ LESS files indexed
- ✅ Variables tracked
- ✅ Media queries tracked
- ✅ Animations tracked
- ✅ Mixins tracked
- ✅ Relationships created
- ✅ Searchable via semantic queries

---

## 📊 Example Results

From a typical project:

```
Files indexed: 872
- 25 .css files
- 18 .scss files
- 3 .less files

Patterns detected: 9618
- 3,400 CSS rules
- 280 CSS variables
- 145 media queries
- 62 animations
- 94 SCSS mixins

Property elements: 280
- 180 CSS custom properties
- 75 SCSS variables
- 25 LESS variables
```

---

## 🎉 Benefits

1. **Complete Stylesheet Coverage** - All styles indexed and searchable
2. **Design System Understanding** - Variables and constants discoverable
3. **Responsive Design Awareness** - Media queries tracked
4. **Animation Library** - Keyframes inventoried
5. **Preprocessor Support** - SCSS/LESS features preserved
6. **Relationship Tracking** - File-to-style relationships maintained
7. **Semantic Search** - Natural language queries work on styles

---

**Status:** ✅ Ready for production use
**Version:** Latest
**Last Updated:** 2025-11-22

