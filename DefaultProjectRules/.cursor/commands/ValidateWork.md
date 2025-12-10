---
name: Validate Current Work
---
Validate work in progress to ensure quality:
1. Build and compile (must succeed)
2. Run integration tests for modified code
3. analyze_code_complexity on changed files
4. validate_pattern_quality on detected patterns (must be ≥ 6/10, ideally ≥ 8/10)
5. If UI/CSS changes: analyze_css (must be ≥ 7/10)
6. validate_security (check for new vulnerabilities)
7. get_recommendations for current context
8. Show quality summary:
   - Code complexity grades
   - Pattern quality scores
   - Security status
   - Best practices compliance
9. Fix CRITICAL/HIGH issues immediately
10. index_file on validated files











