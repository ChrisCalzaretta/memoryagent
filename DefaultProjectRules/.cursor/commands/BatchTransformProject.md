---
name: Batch Transform Entire Project
---
Follow this workflow:
1. analyze_css on entire project to identify pages needing transformation
2. Show prioritized list (quality < 5 = CRITICAL, < 7 = HIGH, < 8 = MEDIUM)
3. Ask for confirmation to proceed with batch transformation
4. For each file in priority order:
   - transform_page (all options enabled)
   - analyze_css to verify improvement
   - index_file
5. After all transformations:
   - detect_reusable_components across project
   - Extract all CRITICAL/HIGH priority components
   - validate_best_practices
   - validate_security
6. Show final summary report with quality improvements
7. Run full test suite















