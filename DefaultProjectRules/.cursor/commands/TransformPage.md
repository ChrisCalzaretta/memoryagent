---
name: Transform Blazor/Razor Page
---
Follow this workflow:
1. analyze_css on the page to get baseline quality score
2. smartsearch to find similar existing components
3. transform_page with all options enabled (extractComponents, modernizeCSS, addErrorHandling, addLoadingStates, addAccessibility)
4. Show me the transformation results and quality improvements
5. index_file on all generated files
6. analyze_css to verify quality improved (must be ≥ 7/10)
7. Write integration tests for transformed components
8. validate_best_practices + validate_security
9. Build and compile to ensure no errors









