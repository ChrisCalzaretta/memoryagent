---
name: Pre-Flight Check (Before Commit/Deploy)
---
Run comprehensive validation before committing or deploying:
1. Build and compile entire project (must succeed)
2. Run all integration tests (must pass)
3. analyze_code_complexity on changed files (must be acceptable)
4. validate_best_practices (fix CRITICAL/HIGH issues)
5. validate_security (must be ≥ 8/10, fix all CRITICAL/HIGH vulnerabilities)
6. find_anti_patterns (fix all detected)
7. validate_project for comprehensive report
8. If transformations were done: analyze_css (all files must be ≥ 7/10)
9. index_file on all changed files
10. Show final quality report and confirm ready for commit/deploy





