# Cursor Commands Reference

## 📋 General Workflow Commands

### `FixBug.md` - Fix Bug Workflow
Systematic bug fixing with impact analysis and validation.

### `FollowRulesReminder.md` - Follow Rules Reminder
Quick reminder to review and follow cursorrules.mdc.

### `ValidateWork.md` - Validate Current Work ✨ NEW
Comprehensive validation of work in progress before committing.

### `PreFlightCheck.md` - Pre-Flight Check ✨ NEW
Final validation before commit/deploy (security, tests, quality, patterns).

### `RulesViolation.md` - Rules Violation Check & Fix ✨ NEW
Detect and fix cursorrules.mdc violations.

---

## 🎨 Blazor/Razor Transformation Commands ✨ NEW

### `TransformPage.md` - Transform Blazor/Razor Page
Complete page transformation workflow:
- Analyze CSS quality baseline
- Transform with all modernization options
- Extract components, add error handling
- Validate quality improvements (must be ≥ 7/10)

### `TransformCSS.md` - Transform & Modernize CSS
CSS modernization workflow:
- Extract inline styles
- Generate CSS variables
- Modernize layout (Flexbox/Grid)
- Add responsive design & accessibility

### `AnalyzeUIQuality.md` - Analyze UI/CSS Quality
Project-wide CSS quality analysis with prioritized recommendations.

---

## 🔄 Pattern Management Commands ✨ NEW

### `LearnTransformPattern.md` - Learn Transformation Pattern
Learn reusable transformation patterns from before/after examples.

### `ApplyTransformPattern.md` - Apply Transformation Pattern
Apply learned patterns to new files.

---

## 🧩 Component Extraction Commands ✨ NEW

### `DetectComponents.md` - Detect Reusable Components
Scan project for repeated UI patterns that should be extracted.

### `ExtractComponent.md` - Extract Reusable Component
Extract detected component candidates into reusable components.

---

## 🚀 Advanced Transformation Commands ✨ NEW

### `BatchTransformProject.md` - Batch Transform Entire Project
Full project transformation workflow:
- Analyze all pages
- Prioritize by quality scores
- Transform in batches
- Extract reusable components
- Final validation

### `ValidateTransformation.md` - Validate Transformation Quality
Comprehensive validation after transformations (CSS quality, complexity, security).

---

## 📊 Quality Thresholds

### CSS Quality Scores
- **9-10 (A)**: ✅ Production ready
- **7-8 (B/C)**: ✅ Acceptable
- **5-6 (D)**: ⚠️ Needs improvement
- **0-4 (F)**: 🚨 CRITICAL - fix immediately

### Pattern Quality Scores
- **9-10 (A)**: ✅ Ship it
- **8 (B)**: ✅ Good
- **7 (C)**: ⚠️ Address before release
- **6 (D)**: ❌ FIX BEFORE CONTINUING
- **0-5 (F)**: 🚨 CRITICAL - FIX IMMEDIATELY

### Security Scores
- **10/10**: 🔒 Perfect
- **8-9/10**: ✅ Acceptable
- **< 8/10**: 🚨 FIX CRITICAL/HIGH issues

### Code Complexity
- **< 10**: ✅ Excellent
- **10-15**: ✅ Acceptable
- **15-20**: ⚠️ Needs refactoring
- **> 20**: 🚨 MUST refactor

---

## 🎯 Command Usage Guide

### For Bug Fixes
1. `FixBug.md` → fix issue
2. `ValidateWork.md` → ensure quality
3. `PreFlightCheck.md` → before commit

### For UI Transformations
1. `AnalyzeUIQuality.md` → assess current state
2. `TransformPage.md` or `TransformCSS.md` → modernize
3. `ValidateTransformation.md` → verify improvements

### For Large Refactoring
1. `LearnTransformPattern.md` → learn from examples
2. `ApplyTransformPattern.md` → apply to similar files
3. `DetectComponents.md` → find reusable patterns
4. `ExtractComponent.md` → create shared components
5. `ValidateWork.md` → continuous validation
6. `PreFlightCheck.md` → final validation

### For Project-Wide Cleanup
1. `BatchTransformProject.md` → transform entire project
2. `RulesViolation.md` → fix rule violations
3. `PreFlightCheck.md` → ensure deployment readiness

---

## 🔧 MCP Tools Used

All commands use these MCP tools (ensure mcp-server is running):

**Core Tools:**
- `smartsearch`, `index_file`, `dependency_chain`, `impact_analysis`
- `validate_best_practices`, `validate_security`, `validate_project`
- `analyze_code_complexity`, `find_anti_patterns`

**Transformation Tools:** ✨ NEW
- `transform_page`, `transform_css`, `analyze_css`
- `learn_transformation`, `apply_transformation`, `list_transformation_patterns`
- `detect_reusable_components`, `extract_component`

---

## ✨ What's New

**12 New Commands Added:**
- 3 General workflow (ValidateWork, PreFlightCheck, RulesViolation)
- 9 Transformation commands (TransformPage, TransformCSS, etc.)

**8 New MCP Tools:**
- Full Blazor/Razor transformation suite
- CSS modernization and quality analysis
- Pattern learning and application
- Component detection and extraction

**Powered by:**
- DeepSeek Coder V2 (16B) for code transformations
- Neo4j for dependency graphs
- Qdrant for semantic search
- Ollama for LLM inference



