---
name: Extract Reusable Component
---
Follow this workflow:
1. Verify ComponentCandidate JSON is provided (from detect_reusable_components)
2. extract_component with appropriate output path
3. Show me the generated component code:
   - Component file
   - Interface (parameters, events)
   - Usage examples
4. Suggest refactoring existing usages to use new component
5. index_file on new component
6. Write integration tests for component
7. analyze_code_complexity on component (must be < 10 cyclomatic complexity)
8. validate_best_practices













