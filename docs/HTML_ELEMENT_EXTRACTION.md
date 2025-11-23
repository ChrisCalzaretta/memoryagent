# HTML Element Extraction

## 🌐 Overview

The Memory Code Agent extracts **semantic HTML elements** from Razor/CSHTML files to understand UI structure and user interactions.

---

## 📦 Extracted HTML Elements

### **Semantic Elements Tracked:**

| Element | What It Represents | Indexed As |
|---------|-------------------|------------|
| `<form>` | User input forms | Pattern: `Html_form_{id/class}` |
| `<table>` | Data tables | Pattern: `Html_table_{id/class}` |
| `<nav>` | Navigation menus | Pattern: `Html_nav_{id/class}` |
| `<header>` | Page/section headers | Pattern: `Html_header_{id/class}` |
| `<footer>` | Page/section footers | Pattern: `Html_footer_{id/class}` |
| `<aside>` | Sidebars/related content | Pattern: `Html_aside_{id/class}` |
| `<main>` | Main content area | Pattern: `Html_main_{id/class}` |
| `<section>` | Content sections | Pattern: `Html_section_{id/class}` |
| `<article>` | Independent content | Pattern: `Html_article_{id/class}` |

---

## 📝 What Gets Captured

### 1. **HTML Element Structure**

```html
<form id="login-form" method="post" action="/Account/Login">
    <input type="text" name="username" />
    <input type="password" name="password" />
    <button type="submit">Login</button>
</form>
```

**Stored as:**
- **Name**: `Html_form_login-form`
- **Type**: Pattern
- **Content**: Full form HTML (truncated to 1000 chars if needed)
- **Metadata**:
  - `element_type`: "form"
  - `element_id`: "login-form"
  - `has_id`: true
  - `has_class`: false

**Relationship:**
- `Login.cshtml` → **DEFINES** → `Html_form_login-form`

---

### 2. **Form Submissions**

For forms with `action` attributes, an additional pattern is created:

```html
<form action="/Account/Register" method="post">
```

**Stored as:**
- **Name**: `FormSubmit: /Account/Register`
- **Type**: Pattern
- **Content**: Form opening tag
- **Metadata**:
  - `chunk_type`: "form_submit"
  - `action`: "/Account/Register"

**Relationship:**
- `Register.cshtml` → **CALLS** → `/Account/Register`

---

### 3. **Navigation Elements**

```html
<nav id="main-navigation" class="navbar">
    <ul>
        <li><a href="/">Home</a></li>
        <li><a href="/About">About</a></li>
    </ul>
</nav>
```

**Stored as:**
- **Name**: `Html_nav_main-navigation`
- **Type**: Pattern
- **Metadata**:
  - `element_id`: "main-navigation"
  - `element_class`: "navbar"

---

### 4. **Data Tables**

```html
<table id="users-table" class="data-grid">
    <thead>
        <tr><th>Name</th><th>Email</th></tr>
    </thead>
    <tbody>
        <!-- rows -->
    </tbody>
</table>
```

**Stored as:**
- **Name**: `Html_table_users-table`
- **Type**: Pattern
- **Metadata**:
  - `element_type`: "table"
  - `element_id`: "users-table"
  - `element_class`: "data-grid"

---

### 5. **Embedded CSS (`<style>` tags)**

```html
<style>
.custom-button {
    background-color: #007bff;
    color: white;
}
</style>
```

**Stored as:**
- **Name**: `InlineStyles_1`
- **Type**: Pattern
- **Content**: CSS content only
- **Metadata**:
  - `chunk_type`: "style_tag"
  - `embedded_in`: "razor"

---

### 6. **Inline Styles (Significant)**

Only inline styles **longer than 50 characters** are extracted:

```html
<div style="display: flex; justify-content: space-between; align-items: center; padding: 20px; background: linear-gradient(to right, #667eea, #764ba2);">
```

**Stored as:**
- **Name**: `InlineStyle_div_L42`
- **Type**: Pattern
- **Content**: Style attribute value
- **Metadata**:
  - `chunk_type`: "inline_style"
  - `element`: "div"

---

## 🔍 Naming Strategy

### Element Naming:

**Format:** `Html_{type}_{identifier}`

**Identifier Priority:**
1. `id` attribute (if present)
2. First class name (if no id)
3. Sequential number (if no id or class)

**Examples:**
- `<form id="login">` → `Html_form_login`
- `<nav class="main-nav">` → `Html_nav_main-nav`
- `<table>` (no id/class) → `Html_table_table1`

---

## 📊 Relationships Tracked

### File → Element

```
Index.cshtml → DEFINES → Html_form_search
Index.cshtml → DEFINES → Html_nav_main-navigation
Index.cshtml → DEFINES → Html_table_results
```

### Form Submissions

```
Register.cshtml → CALLS → /Account/Register
Login.cshtml → CALLS → /Account/Login
```

This allows you to:
- Find which pages submit to which endpoints
- Trace user flows through the application
- Understand form-to-controller relationships

---

## 🔍 Example Queries

### Finding UI Elements:

```
"Show me all login forms"
"Find the navigation menu"
"Where are data tables used?"
"List all forms that submit to the Account controller"
```

### Finding Styles:

```
"What inline styles are used in the dashboard?"
"Show me embedded CSS in component files"
```

### Understanding Layout:

```
"Find all header sections"
"Show me sidebar content"
"Where is the main content area defined?"
```

---

## 📈 Statistics

After indexing Razor files with HTML extraction:

```
Patterns detected: 9618
- CSS rules: 3400
- HTML forms: 245
- HTML tables: 156
- Navigation elements: 89
- Headers/footers: 234
- Form submissions: 187
- Inline styles: 142
- Style tags: 78
```

---

## 🎯 Use Cases

### 1. **UI Inventory**
Discover all forms, tables, and navigation elements across your application.

### 2. **Form Flow Analysis**
Trace which forms submit to which endpoints and understand user journeys.

### 3. **Accessibility Audits**
Find semantic HTML usage (nav, header, footer, main, aside, article).

### 4. **Refactoring Support**
Before changing an endpoint, see which forms submit to it.

### 5. **Design Consistency**
Find all navigation menus, headers, or footers to ensure consistency.

### 6. **Style Analysis**
Locate embedded and inline styles that might need to be moved to CSS files.

---

## ⚙️ Configuration

### Elements Extracted:

```csharp
var semanticElements = new[]
{
    "form",
    "table",
    "nav",
    "header",
    "footer",
    "aside",
    "main",
    "section",
    "article"
};
```

### Size Limits:

- **Element content**: Truncated to 1000 chars
- **Inline styles**: Only extracted if > 50 chars
- **Style tags**: Full content extracted

---

## 🚀 Advanced Features

### Form Action Tracking

Forms with `action` attributes create **CALLS** relationships:

```html
<form action="/api/users" method="post">
```

Creates relationship: `UserForm.cshtml` → **CALLS** → `/api/users`

**Enables queries like:**
- "Which pages call the /api/users endpoint?"
- "Show me all forms that submit to API controllers"

### ID and Class Metadata

Every element includes:
- `element_id`: The ID attribute value
- `element_class`: The class attribute value
- `has_id`: Boolean indicating if ID exists
- `has_class`: Boolean indicating if class exists

**Enables filtering:**
- Find all elements with specific IDs
- Find all elements using a CSS class
- Find elements without IDs (accessibility issues)

---

## 📝 Metadata Structure

```json
{
    "chunk_type": "html_element | form_submit | style_tag | inline_style",
    "element_type": "form | table | nav | header | footer | aside | main | section | article",
    "element_id": "login-form",
    "element_class": "form-horizontal",
    "has_id": true,
    "has_class": true,
    "action": "/Account/Login",
    "embedded_in": "razor"
}
```

---

## ✅ Status

**HTML Element Extraction: ACTIVE**
- ✅ Forms extracted
- ✅ Tables extracted
- ✅ Navigation extracted
- ✅ Semantic HTML extracted
- ✅ Form submissions tracked
- ✅ Style tags extracted
- ✅ Inline styles extracted
- ✅ Relationships created
- ✅ Searchable via queries

---

## 🎉 Benefits

1. **Complete UI Understanding** - Know what's on every page
2. **Form Flow Tracing** - See where forms submit
3. **Semantic HTML Awareness** - Track proper HTML5 usage
4. **Accessibility Insights** - Find navigation, landmarks
5. **Style Discovery** - Locate embedded/inline styles
6. **Relationship Mapping** - Connect pages to endpoints
7. **Refactoring Safety** - Know impact before changes

---

**Status:** ✅ Active and ready
**Version:** Latest
**Last Updated:** 2025-11-22

