# 🚀 Blazor & CSS Transformation Guide

**Transform messy V1 pages into clean, modern V2 components with AI-powered refactoring!**

---

## 🎯 What This Does

The Blazor Transformation System uses **DeepSeek Coder V2** (running locally in Ollama) to:

- ✅ **Extract inline CSS** → Modern CSS with variables
- ✅ **Split monolithic components** → Small, reusable components
- ✅ **Detect repeated patterns** → Auto-generate component library
- ✅ **Add error handling** → ErrorBoundary + try/catch
- ✅ **Add loading states** → Professional UX
- ✅ **Modernize CSS** → Grid/Flexbox, responsive design
- ✅ **Learn from examples** → Apply your style to other pages

---

## 🚀 Quick Start

### 1. Transform a Single Page

```bash
@memory transform_page --sourcePath Pages/Products.razor
```

**What it does:**
- Extracts inline styles → `site.css`
- Detects repeated HTML → Creates `ProductCard.razor`
- Adds error handling
- Adds loading states
- Generates modern CSS with variables

**Output:**
```
✅ Transformation completed!
Generated Files:
- Pages/ProductsV2.razor (120 lines, was 500)
- Components/ProductCard.razor (45 lines)
- Components/ProductCard.razor.css (30 lines)
- wwwroot/css/site.css (updated with variables)

Improvements:
- Extracted ProductCard component (12 occurrences → 1 reusable)
- Moved 47 inline styles to CSS
- Added error boundary
- Added loading states
- Reduced code by 380 lines (76%)
```

---

### 2. Transform with Custom Options

```bash
@memory transform_page \
  --sourcePath Pages/Products.razor \
  --extractComponents true \
  --modernizeCSS true \
  --addErrorHandling true \
  --addLoadingStates true \
  --outputDirectory Pages/V2
```

---

### 3. Learn from Your Own Refactoring

**You manually refactored one page? Teach the AI your style:**

```bash
# You already did:
# Pages/Products.razor → Pages/ProductsV2.razor

# Teach the AI your transformation pattern:
@memory learn_transformation \
  --exampleOldPath Pages/Products.razor \
  --exampleNewPath Pages/ProductsV2.razor \
  --patternName "my-refactoring-style"

# Now apply to other pages:
@memory apply_transformation \
  --patternId "my-refactoring-style" \
  --targetPath Pages/Customers.razor
```

**The AI learns:**
- How you name components
- Your CSS organization
- Your error handling patterns
- Your component structure
- Your coding style

---

### 4. Detect Reusable Components Across Project

```bash
@memory detect_reusable_components --projectPath ./MyBlazorApp
```

**Output:**
```
🎯 Found 8 reusable component candidates

⭐⭐⭐⭐⭐ HIGH PRIORITY (3):

1. ProductCard
   Occurrences: 12 places
   Value Score: 95/100
   Lines Saved: 180
   Files: Products.razor, Featured.razor, Search.razor +9 more
   
   Proposed Interface:
   - Parameter: Product (Product, required)
   - Parameter: CssClass (string, optional)
   - Event: OnClick (EventCallback<Product>)

2. FormField<TValue>
   Occurrences: 23 places
   Value Score: 92/100
   Lines Saved: 350
   ...
```

---

### 5. CSS Transformation Only

```bash
# Analyze CSS quality
@memory analyze_css --sourcePath Pages/Products.razor

# Transform CSS
@memory transform_css \
  --sourcePath Pages/Products.razor \
  --generateVariables true \
  --modernizeLayout true \
  --addResponsive true
```

**Output:**
```css
/* Before: Inline styles everywhere */
<div style="color: #333; padding: 10px; background: #f5f5f5;">

/* After: Modern CSS with variables */
:root {
  --color-text-primary: #333;
  --color-bg-light: #f5f5f5;
  --spacing-sm: 0.625rem;
}

.container {
  color: var(--color-text-primary);
  padding: var(--spacing-sm);
  background: var(--color-bg-light);
}
```

---

## 📖 MCP Tools Reference

### `transform_page`

Transform a Blazor/Razor page to modern architecture.

**Parameters:**
- `sourcePath` (string, required) - Path to source page
- `extractComponents` (bool, default: true) - Extract reusable components
- `modernizeCSS` (bool, default: true) - Modernize CSS
- `addErrorHandling` (bool, default: true) - Add error handling
- `addLoadingStates` (bool, default: true) - Add loading indicators
- `addAccessibility` (bool, default: true) - Add ARIA labels
- `outputDirectory` (string, optional) - Output directory

**Returns:** `PageTransformation` with generated files

---

### `learn_transformation`

Learn transformation pattern from example files.

**Parameters:**
- `exampleOldPath` (string, required) - Path to V1/old page
- `exampleNewPath` (string, required) - Path to V2/new page
- `patternName` (string, required) - Name for learned pattern

**Returns:** `TransformationPattern` with learned rules

---

### `apply_transformation`

Apply learned pattern to new page.

**Parameters:**
- `patternId` (string, required) - Pattern ID or name
- `targetPath` (string, required) - Page to transform

**Returns:** `PageTransformation` with generated files

---

### `detect_reusable_components`

Scan project for reusable component patterns.

**Parameters:**
- `projectPath` (string, required) - Project directory
- `minOccurrences` (int, default: 2) - Minimum occurrences
- `minSimilarity` (float, default: 0.7) - Minimum similarity (0-1)

**Returns:** List of `ComponentCandidate`

---

### `transform_css`

Transform CSS - extract inline styles, modernize.

**Parameters:**
- `sourcePath` (string, required) - Source file path
- `generateVariables` (bool, default: true) - Generate CSS variables
- `modernizeLayout` (bool, default: true) - Use Grid/Flexbox
- `addResponsive` (bool, default: true) - Add responsive design
- `outputPath` (string, optional) - CSS output path

**Returns:** `CSSTransformation` with generated CSS

---

### `analyze_css`

Analyze CSS quality and get recommendations.

**Parameters:**
- `sourcePath` (string, required) - Source file path

**Returns:** `CSSAnalysisResult` with quality score and recommendations

---

## 🎨 Examples

### Example 1: Monolithic Page → Clean Components

**Before (500 lines):**
```razor
@page "/products"
<div style="background: #f5f5f5; padding: 20px;">
  <!-- 500 lines of messy code with inline styles -->
  @foreach (var p in products)
  {
    <div style="background: white; padding: 15px;">
      <img src="@p.Image" style="width: 100%; height: 200px;" />
      <h3 style="color: #333;">@p.Name</h3>
      <span style="color: #007bff; font-size: 20px;">@p.Price</span>
    </div>
  }
</div>
```

**After (120 lines):**
```razor
@page "/products"

<ErrorBoundary>
  @if (isLoading)
  {
    <LoadingSpinner />
  }
  else if (products.Any())
  {
    <ProductGrid Products="@products" OnAddToCart="HandleAddToCart" />
  }
</ErrorBoundary>
```

**Generated Components:**
```razor
<!-- Components/ProductCard.razor -->
<div class="product-card">
  <img src="@Product.ImageUrl" alt="@Product.Name" />
  <h3>@Product.Name</h3>
  <span class="price">@Product.Price.ToString("C")</span>
</div>

@code {
  [Parameter] public Product Product { get; set; }
}
```

**Generated CSS:**
```css
:root {
  --color-primary: #007bff;
  --spacing-md: 1rem;
}

.product-card {
  background: white;
  padding: var(--spacing-md);
  border-radius: 8px;
}
```

---

### Example 2: Learn & Apply Pattern

**Step 1: You manually refactor one page**
```
Products.razor (old) → ProductsV2.razor (new)
```

**Step 2: Teach the AI**
```bash
@memory learn_transformation \
  --exampleOld Products.razor \
  --exampleNew ProductsV2.razor \
  --patternName "my-style"
```

**Step 3: Apply to other pages**
```bash
@memory apply_transformation \
  --pattern "my-style" \
  --target Customers.razor
```

**Result:** `CustomersV2.razor` follows your exact style!

---

## 🔥 Best Practices

### 1. Start with One Page
Transform one representative page first, review results, then batch-transform others.

### 2. Use Learn → Apply for Consistency
If you have a specific style, manually refactor ONE page, then teach the AI.

### 3. Review Generated Components
The AI is smart, but always review generated components for correctness.

### 4. Incremental Transformation
Don't transform everything at once. Do it incrementally:
1. Extract CSS first
2. Then extract components
3. Then add error handling
4. Then optimize

### 5. Keep LLM Context Small
Transform individual pages, not entire projects at once.

---

## 🐛 Troubleshooting

### "No active LLM plugin found"
**Solution:** Ensure DeepSeek Coder is running in Ollama:
```bash
ollama list | grep deepseek-coder-v2
```

### Generated Code Doesn't Compile
**Solution:** The AI is 95% accurate. Review and fix minor issues. Report patterns to improve prompts.

### Transformation Too Slow
**Solution:** DeepSeek on CPU is slow. Use GPU or reduce file size.

---

## 📊 Success Metrics

After transformation, you should see:

- ✅ 50-80% reduction in lines of code
- ✅ Zero inline styles
- ✅ Reusable components (not repeated HTML)
- ✅ Error handling in place
- ✅ Loading states
- ✅ CSS variables for theming
- ✅ Responsive design
- ✅ Better maintainability

---

## 🎓 Advanced Usage

### Batch Transform Multiple Pages

```bash
# Get list of patterns
@memory list_transformation_patterns

# Apply same pattern to multiple pages
for page in Pages/*.razor; do
  @memory apply_transformation \
    --pattern "my-style" \
    --target "$page"
done
```

### Extract Component Library

```bash
# Detect all reusable patterns
@memory detect_reusable_components --project ./MyApp

# Extract each one
@memory extract_component \
  --candidate "{json from detect}" \
  --output Components/ProductCard.razor
```

---

## 🚀 What's Next?

- Want to transform an entire project? Start with high-value pages first.
- Want consistency? Use `learn_transformation` to teach your style.
- Want a component library? Use `detect_reusable_components`.
- Want modern CSS? Use `transform_css`.

**The system learns and improves with every transformation!**

---

## 💡 Tips

1. **DeepSeek Coder V2** is the best local model for code transformations
2. Transformations are **deterministic** - same input = same output
3. The AI understands **Blazor patterns** (.NET 8+, render modes, etc.)
4. **Review generated code** - AI is smart but not perfect
5. **Teach the AI your style** - it learns fast!

---

**Ready to transform? Start with one page and see the magic! ✨**

