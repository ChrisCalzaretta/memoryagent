---
name: Validate Transformation Quality
---
Follow this workflow:
1. analyze_css on transformed files
2. analyze_code_complexity on transformed code
3. validate_best_practices on project
4. validate_security on project
5. Show comprehensive quality report:
   - CSS quality score (must be ≥ 7/10)
   - Code complexity (must be acceptable)
   - Best practices compliance
   - Security score (must be ≥ 8/10)
6. If any CRITICAL issues found: Fix immediately
7. If any HIGH issues found: Show and fix before continuing
8. Run all tests to ensure nothing broke
9. Build and compile successfully













