# Cursor Commands Reference

## 🚨 CRITICAL: MCP Tools Required on EVERY Query

The Memory Agent MCP tools MUST be used on every query. See `cursorrules.mdc` for mandatory rules.

---

## 🧠 Agent Lightning Commands (NEW - USE EVERY SESSION)

### `StartSession.md` - Start Learning Session ⭐ REQUIRED
Start Agent Lightning session at the beginning of every conversation.
- Checks for active session
- Creates new session if needed
- Reviews recent context

### `EndSession.md` - End Learning Session ⭐ REQUIRED
End session with summary when conversation completes.
- Records accomplishments
- Updates importance rankings

### `RecordContext.md` - Track Files Discussed & Edited ⭐ REQUIRED
Record every file interaction for learning.
- Call after discussing any file
- Call after editing any file
- Tracks co-edit patterns

### `StoreKnowledge.md` - Save Q&A for Future Recall
Store useful Q&A pairs for instant recall later.
- Check for similar questions first
- Store with relevant file paths
- Builds project knowledge base

### `FindAnswer.md` - Check Knowledge Base First ⭐ REQUIRED
ALWAYS check cached answers before answering questions.
- Instant recall of previous answers
- Falls back to search if no match

### `ReviewImportance.md` - Review File Importance
See most important files based on learned patterns.
- Prioritize code review
- Understand file clusters
- Find co-edited files

---

## 📋 General Workflow Commands

### `FixBug.md` - Fix Bug Workflow
Systematic bug fixing with impact analysis and validation.

### `FollowRulesReminder.md` - Follow Rules Reminder
Quick reminder to review and follow cursorrules.mdc.

### `ValidateWork.md` - Validate Current Work
Comprehensive validation of work in progress before committing.

### `PreFlightCheck.md` - Pre-Flight Check
Final validation before commit/deploy (security, tests, quality, patterns).

### `RulesViolation.md` - Rules Violation Check & Fix
Detect and fix cursorrules.mdc violations.

---

## 🎨 Blazor/Razor Transformation Commands

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

## 🔄 Pattern Management Commands

### `LearnTransformPattern.md` - Learn Transformation Pattern
Learn reusable transformation patterns from before/after examples.

### `ApplyTransformPattern.md` - Apply Transformation Pattern
Apply learned patterns to new files.

---

## 🧩 Component Extraction Commands

### `DetectComponents.md` - Detect Reusable Components
Scan project for repeated UI patterns that should be extracted.

### `ExtractComponent.md` - Extract Reusable Component
Extract detected component candidates into reusable components.

---

## 🚀 Advanced Transformation Commands

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

### Starting Any Session (REQUIRED)
1. `StartSession.md` → begin learning session
2. `FindAnswer.md` → check existing knowledge first

### For Bug Fixes
1. `FixBug.md` → fix issue
2. `RecordContext.md` → track files touched
3. `ValidateWork.md` → ensure quality
4. `StoreKnowledge.md` → save learnings
5. `PreFlightCheck.md` → before commit
6. `EndSession.md` → end session

### For UI Transformations
1. `AnalyzeUIQuality.md` → assess current state
2. `TransformPage.md` or `TransformCSS.md` → modernize
3. `ValidateTransformation.md` → verify improvements
4. `RecordContext.md` → track changes

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

### Ending Any Session (REQUIRED)
1. `StoreKnowledge.md` → save useful Q&A
2. `EndSession.md` → close with summary

---

## 📋 COMPLETE MCP TOOL LIST (49 Tools)

### 🧠 Agent Lightning - Learning (13 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `start_session` | Start learning session | Beginning of conversation |
| `end_session` | End session with summary | End of conversation |
| `get_active_session` | Check if session exists | Before starting new session |
| `get_recent_sessions` | Review past work | Context recovery |
| `record_file_discussed` | Track file discussion | Any file mentioned |
| `record_file_edited` | Track file edit | After file changes |
| `get_important_files` | Most important files | Prioritize review |
| `get_coedited_files` | Files edited together | Find related files |
| `get_file_clusters` | Logical file groupings | Understand modules |
| `find_similar_questions` | Check for cached Q&A | BEFORE answering |
| `store_qa` | Save Q&A for recall | After useful answers |
| `detect_domains` | Tag business domains | Categorize files |
| `recalculate_importance` | Refresh rankings | Weekly maintenance |

### 🔍 Search & Index (6 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `smartsearch` | Smart code search | ANY question |
| `query` | Semantic search | Find by meaning |
| `index_file` | Index single file | After file change |
| `index_directory` | Index folder | New directories |
| `reindex` | Update all indexes | Major refactoring |
| `search_patterns` | Find patterns | Before writing code |

### 📊 Analysis (4 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `dependency_chain` | Class dependencies | Understanding structure |
| `impact_analysis` | Change impact | Before changes |
| `find_circular_dependencies` | Circular deps | Architecture review |
| `analyze_code_complexity` | Complexity metrics | Before commits |

### ✅ Validation (9 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `validate_best_practices` | Azure best practices | Task completion |
| `validate_security` | Security audit | Before deploy |
| `validate_pattern_quality` | Pattern scoring | Pattern review |
| `validate_project` | Full validation | Major features |
| `validate_task` | Task validation | Plan tasks |
| `find_anti_patterns` | Anti-patterns | Refactoring |
| `get_recommendations` | Get suggestions | After indexing |
| `get_migration_path` | Migration steps | Legacy patterns |
| `get_available_best_practices` | List all practices | See options |

### 📋 Planning & TODO (8 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `create_plan` | Create dev plan | Multi-step tasks |
| `get_plan_status` | Plan progress | Check status |
| `update_task_status` | Update task | Task changes |
| `complete_plan` | Complete plan | All done |
| `search_plans` | Find plans | Lookup |
| `add_todo` | Add TODO | Track debt |
| `search_todos` | Find TODOs | Find issues |
| `update_todo_status` | Update TODO | Fix issues |

### 🎨 Transformation (8 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `analyze_css` | CSS quality | Before CSS changes |
| `transform_css` | Modernize CSS | CSS updates |
| `transform_page` | Transform page | Blazor/Razor |
| `learn_transformation` | Learn pattern | From examples |
| `apply_transformation` | Apply pattern | Reuse patterns |
| `list_transformation_patterns` | List patterns | See available |
| `detect_reusable_components` | Find components | UI cleanup |
| `extract_component` | Extract component | Create shared |

### 🔧 Workspace (2 tools)

| Tool | Description | When to Use |
|------|-------------|-------------|
| `register_workspace` | Register workspace | Auto on startup |
| `unregister_workspace` | Unregister workspace | Auto on shutdown |

---

## ✨ What's New

**6 New Agent Lightning Commands:**
- StartSession, EndSession, RecordContext
- StoreKnowledge, FindAnswer, ReviewImportance

**13 New Agent Lightning MCP Tools:**
- Session management (start, end, get active, get recent)
- File tracking (record discussed, record edited)
- Importance scoring (get important, get co-edited, get clusters)
- Q&A learning (find similar, store Q&A, detect domains)
- Maintenance (recalculate importance)

**Total: 21 Commands, 49 MCP Tools**

---

## 🔧 Powered by

- **Neo4j** - Graph database for relationships and dependencies
- **Qdrant** - Vector database for semantic search + Lightning learning
- **Ollama** - LLM inference (mxbai-embed-large embeddings)
- **DeepSeek Coder V2** - Code transformations

---

**🎯 GOAL: Learn from every interaction to make future development faster and more accurate!**
