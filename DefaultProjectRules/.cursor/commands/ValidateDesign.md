# Validate UI Design

Validate UI code against brand guidelines and accessibility standards.

## Steps

### 1. Check Brand Exists
```
design_list_brands
```

### 2. Get Brand Context
If brand exists, note the context name (e.g., "myapp", "fittrack-pro")

### 3. Validate Code
```
design_validate(
  context: "[brand-context]",
  code: "[paste UI code here - HTML, Blazor, React, etc.]"
)
```

### 4. Review Results
The validation returns:
- **Score**: 0-10 (must be >= 8 to pass)
- **Grade**: A/B/C/D/F
- **Issues**: List of problems with fixes

### 5. Fix Issues
Common issues and fixes:
| Issue | Fix |
|-------|-----|
| Hardcoded color | Use `var(--color-primary)` instead of `#3B82F6` |
| Missing button class | Add `class="btn-primary"` or brand styling |
| No focus styles | Add `:focus { outline: 2px solid var(--primary) }` |
| Missing alt text | Add `alt="description"` to images |
| Low contrast | Use brand color combinations |

### 6. Re-validate
After fixes, run validation again until score >= 8.

## Validation Checks
- ✅ Colors match design tokens
- ✅ Typography follows guidelines
- ✅ Spacing uses 8px grid
- ✅ Components follow patterns
- ✅ ARIA labels present
- ✅ Focus states visible
- ✅ Alt text on images
- ✅ Color contrast passes WCAG

## Grade Thresholds
- **9-10 (A)**: ✅ Excellent
- **8 (B)**: ✅ Good - Acceptable
- **7 (C)**: ⚠️ Fix before release
- **6 (D)**: ❌ Fix before continuing
- **0-5 (F)**: 🚨 Critical - Fix immediately

