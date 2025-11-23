# Razor Files Indexing Report

## Overview

The Memory Code Agent has successfully indexed **173 Razor files** from the `DataPrepPlatform.Web` project (plus 9 from other areas), with full semantic embeddings and code intelligence.

## Database Distribution

### Neo4j (Graph Database)
- **File Nodes**: 182 Razor/CSHTML files
- **Pattern Nodes**: 11,956 patterns including CSS, forms, HTML elements, etc.
- **Method Nodes**: 4,274 methods
- **Relationships**: 54,907 relationships (DEFINES, CALLS, USES, INHERITS, etc.)

### Qdrant (Vector Database)
- **Files Collection**: 182 Razor files with full content embeddings
- **Patterns Collection**: 27,528+ patterns from all Razor files
- **Methods Collection**: Multiple sections and code blocks per file
- **Classes Collection**: Component classes and code-behind

## Sample: AIWizardV3.razor

### File Location
`/workspace/CBC_AI/src/DataPrepPlatform.Web/Components/Pages/Entities/AIWizardV3.razor`

### Extracted Elements

#### 1. Patterns (5 elements)
- **Page Route**: `@page "/entities/ai-wizard-v3"`
- **Inline Styles**: CSS styling embedded in the component
- **Dependency Injections**: 
  - `AuthenticationStateProvider`
  - `PlatformDbContext`
  - `NavigationManager`

#### 2. HTML Sections (6 sections)
- "Describe Your Entity" - Step 1
- "Review Generated Schema" - Step 2
- "Configure Entity Settings" - Step 3
- "Create Entity" - Step 4
- Error states and loading states

#### 3. Semantic Content
All content is embedded with the `mxbai-embed-large` model, enabling:
- Natural language search ("find entity creation wizard")
- Similarity matching (find similar components)
- Context-aware queries (components that use DbContext)

## DataPrepPlatform.Web Razor Files (Sample)

### Core Pages
- `HomeV3.razor` - Main dashboard
- `OnboardingV3.razor` - User onboarding flow
- `AccessDenied.razor` - Security page

### Entity Management
- `AIWizardV3.razor` - AI-powered entity wizard
- `AIEntityWizardV2.razor` - Legacy wizard
- `CreateFromSource.razor` - Create from existing source

### Admin Pages
- `AdminDashboardV2.razor` - Admin dashboard
- `CompaniesV2.razor` - Company management
- `ConfigurationV2.razor` - System configuration

### Billing
- `InvoiceList.razor` - Invoice management
- Payment processing components

### Shared Components
- `BladePanel.razor` - Blade UI pattern
- `BreadcrumbNav.razor` - Navigation
- `CommandBar.razor` - Action bar
- `NotificationPanelV3.razor` - Notifications
- `CompanySelector.razor` - Company switcher

### Analytics
- `AnalyticsV2.razor` - Analytics dashboard
- `CompanyMetrics.razor` - Company metrics
- `CompanyUsage.razor` - Usage tracking

### Project Management
- `IndexV2.razor` - Project list
- `SearchSettingsV2.razor` - Search configuration

### Prompt Management
- `Index.razor` - Prompt templates

## Extraction Features

### HTML Elements
- ✅ Forms with action endpoints
- ✅ Tables
- ✅ Navigation elements
- ✅ Headers, footers, sections
- ✅ Articles, asides, main elements

### CSS & Styles
- ✅ `<style>` tag content
- ✅ Inline styles (if > 50 chars)
- ✅ CSS class patterns

### Code Elements
- ✅ Razor directives (`@page`, `@inject`, `@using`)
- ✅ C# code blocks (`@code`)
- ✅ Event handlers
- ✅ Component parameters
- ✅ Service injections

### Relationships
- ✅ **DEFINES**: File defines patterns/sections
- ✅ **CALLS**: Form submissions, method calls
- ✅ **USES**: Component dependencies
- ✅ **INJECTS**: Dependency injection

## Search Capabilities

### Example Queries

#### Semantic Search (Qdrant)
```
"Find components for entity creation"
→ Returns: AIWizardV3.razor, AIEntityWizardV2.razor, CreateFromSource.razor

"Components that use authentication"
→ Returns: All components with AuthenticationStateProvider

"Billing and invoice management"
→ Returns: InvoiceList.razor, payment components
```

#### Graph Search (Neo4j)
```cypher
// Find all Razor files
MATCH (f:File) 
WHERE f.path CONTAINS '.razor' 
RETURN f.name, f.path

// Find components that inject DbContext
MATCH (f:File)-[:DEFINES]->(p:Pattern)
WHERE p.name CONTAINS 'DbContext'
RETURN f.name, p.name

// Find form submission endpoints
MATCH (f:File)-[:CALLS]->(r:Reference)
WHERE f.path CONTAINS '.razor'
RETURN f.name, r.name
```

#### Smart Search (Hybrid)
```
"What components handle onboarding?"
→ Strategy: Semantic-first
→ Returns: OnboardingV3.razor with relationships

"Classes that implement wizard pattern"
→ Strategy: Graph-first
→ Returns: AIWizardV3.razor, AIEntityWizardV2.razor with dependencies
```

## Statistics

### File Coverage
- **C# Files**: 473
- **Razor/CSHTML Files**: 182 ✅
- **Markdown Files**: 225
- **Python Files**: 13
- **JSON Files**: 10
- **Total Files**: 918

### Code Elements
- **Patterns**: 11,956 (HTML, CSS, forms, Bicep resources)
- **Methods**: 4,274
- **Properties**: 9,908
- **Classes**: 708
- **References**: 11,165
- **Interfaces**: 58

### Embeddings
- **Total Vectors in Qdrant**: 43,700+
- **Embedding Model**: `mxbai-embed-large` (1024 dimensions)
- **Token Limit**: 512 tokens per chunk
- **Truncation Strategy**: Head (60%) + Tail (40%)

## Verification

### Check Razor Files in Neo4j
```cypher
MATCH (f:File) 
WHERE f.path CONTAINS 'DataPrepPlatform.Web' 
  AND (f.path CONTAINS '.razor' OR f.path CONTAINS '.cshtml')
RETURN f.name, f.path 
ORDER BY f.name
LIMIT 20
```

### Check Razor Embeddings in Qdrant
```bash
curl -X POST http://localhost:6431/collections/files/points/scroll \
  -H "Content-Type: application/json" \
  -d '{
    "limit": 10,
    "with_payload": true,
    "with_vector": false,
    "filter": {
      "must": [
        { "key": "file_path", "match": { "text": ".razor" } },
        { "key": "file_path", "match": { "text": "DataPrepPlatform.Web" } }
      ]
    }
  }'
```

### Search via MCP API
```bash
# Endpoint: http://localhost:5098/api/smartsearch
POST {
  "query": "entity wizard components",
  "context": "CBC_AI",
  "limit": 10
}
```

## Conclusion

✅ **All 182 Razor/CSHTML files are fully indexed**  
✅ **Embeddings are in Qdrant with full content**  
✅ **Graph relationships are in Neo4j**  
✅ **Semantic search works across all Razor components**  
✅ **Smart search can find components by meaning or structure**

The system has comprehensive coverage of:
- **LicenseServer**: 9 Razor files
- **DataPrepPlatform.Web**: 173 Razor files ✅
- **All extracted patterns, styles, HTML elements, and dependencies**

