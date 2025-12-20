---
name: Transform & Modernize CSS
---
Follow this workflow:
1. analyze_css to get baseline quality score
2. Show me the current issues (inline styles, legacy layout, missing variables, accessibility)
3. transform_css with all options (generateVariables=true, modernizeLayout=true, addResponsive=true, addAccessibility=true)
4. Show me before/after comparison and quality score delta
5. analyze_css to verify quality improved to ≥ 7/10
6. If quality < 7/10: Re-run transform_css with adjusted parameters
7. index_file on CSS files
8. Validate CSS compiles and renders correctly

















