---
name: Rules Violation Check & Fix
---
Check for and fix rules violations:
1. Review cursorrules.mdc for all applicable rules
2. Check for common violations:
   - Files > 1500 lines (refactor immediately)
   - Missing index_file after changes
   - No integration tests for methods
   - Hardcoded prompts (should use IPromptTemplateService)
   - Hardcoded schemas (should use INFORMATION_SCHEMA)
   - Pattern quality < 6/10 (fix before continuing)
   - Security score < 8/10 (fix CRITICAL/HIGH issues)
   - CSS quality < 7/10 (transform immediately)
3. find_anti_patterns to detect code smell violations
4. analyze_code_complexity (flag files with complexity > 15)
5. Show all violations with priority (CRITICAL/HIGH/MEDIUM)
6. Fix CRITICAL violations immediately
7. Ask for approval to fix HIGH violations
8. Document MEDIUM violations for future work

