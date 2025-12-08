# Get Design Tokens

Get design tokens (colors, fonts, spacing) from brand guidelines.

## Steps

### 1. Get Brand Definition
```
design_get_brand(context: "[brand-context]")
```

### 2. Extract Tokens
The brand includes these token categories:

#### Colors
```css
--color-primary: #3B82F6;
--color-primary-light: #60A5FA;
--color-primary-dark: #2563EB;
--color-success: #10B981;
--color-warning: #F59E0B;
--color-error: #EF4444;
```

#### Typography
```css
--font-sans: 'Inter', system-ui, sans-serif;
--font-mono: 'JetBrains Mono', monospace;
--text-xs: 0.75rem;
--text-sm: 0.875rem;
--text-base: 1rem;
--text-lg: 1.125rem;
```

#### Spacing (8px grid)
```css
--space-1: 0.25rem;  /* 4px */
--space-2: 0.5rem;   /* 8px */
--space-3: 0.75rem;  /* 12px */
--space-4: 1rem;     /* 16px */
```

#### Border Radius
```css
--radius-sm: 0.25rem;
--radius-md: 0.375rem;
--radius-lg: 0.5rem;
--radius-full: 9999px;
```

### 3. Use in Code

#### Blazor/CSS
```css
.my-button {
  background: var(--color-primary);
  color: white;
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  font-family: var(--font-sans);
}
```

#### React/Tailwind
Reference tokens in tailwind.config.js for custom classes.

## Token Categories
- **Colors**: Primary, secondary, semantic (success/warning/error), neutrals
- **Typography**: Font families, sizes, weights, line heights
- **Spacing**: 8px base unit scale
- **Borders**: Radius sizes, widths
- **Shadows**: Elevation levels
- **Z-Index**: Layer ordering

