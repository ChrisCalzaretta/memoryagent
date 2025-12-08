# Cursor Commands Reference

## 🚨 CRITICAL: MCP Tools Required on EVERY Query

The Memory Agent MCP tools MUST be used on every query. See `cursorrules.mdc` for mandatory rules.

---

## 🤖 Multi-Agent Code Generation (NEW!)

### `GenerateCode.md` - Start Multi-Agent Coding Task ⭐
Start a coding task with automatic validation loop.
- CodingAgent generates code using LLM (deepseek-v2:16b)
- ValidationAgent validates with rules + LLM (phi4)
- Iterates until score >= 8/10 or max iterations

### `CheckCodeTask.md` - Check Task Status
Monitor progress and get generated code when complete.
- Shows progress percentage, current phase, iteration
- Returns generated files when task completes

### `ListCodeTasks.md` - List All Tasks
See all active and recent coding tasks.
- Shows status: Running, Complete, Failed, Cancelled

### `CancelCodeTask.md` - Cancel Running Task
Stop a task that's no longer needed.

---

## 🧠 Agent Lightning Commands (USE EVERY SESSION)

### `StartSession.md` - Start Learning Session ⭐ REQUIRED
Start Agent Lightning session at the beginning of every conversation.
- Creates or retrieves active session
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
- Falls back to smartsearch if no match

### `ReviewImportance.md` - Review File Importance
See most important files based on learned patterns.
- Prioritize code review
- Find co-edited files and clusters

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

## 🎨 Design Agent Commands (NEW!)

### `CreateBrand.md` - Create Brand Guidelines ⭐ NEW
Create complete brand system with design tokens, components, themes, accessibility.

### `ValidateDesign.md` - Validate UI Against Brand ⭐ NEW
Validate UI code against brand guidelines. Must score >= 8/10.

### `GetDesignTokens.md` - Get Design Tokens ⭐ NEW
Get colors, fonts, spacing, and other tokens from brand.

---

## 🎨 Blazor/Razor Transformation Commands

### `TransformPage.md` - Transform Blazor/Razor Page
Complete page transformation: CSS modernization, component extraction, error handling.

### `TransformCSS.md` - Transform & Modernize CSS
CSS modernization: variables, Flexbox/Grid, responsive design, accessibility.

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
Full project transformation workflow with prioritization and validation.

### `ValidateTransformation.md` - Validate Transformation Quality
Comprehensive validation after transformations (CSS quality, complexity, security).

---

## 📊 Quality Thresholds

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

### For New Code Generation (Multi-Agent) ⭐ NEW
1. `GenerateCode.md` → start multi-agent task
2. `CheckCodeTask.md` → monitor progress
3. Wait for completion (30-120 seconds)
4. Review generated code, apply to project
5. `RecordContext.md` → track generated files
6. `StoreKnowledge.md` → save learnings

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

### For UI with Brand Guidelines ⭐ NEW
1. `CreateBrand.md` → create brand system (once per project)
2. Build UI using design tokens from brand
3. `ValidateDesign.md` → validate code against brand (score >= 8)
4. Fix issues from validation feedback
5. Re-validate until passing

### For Large Refactoring
1. `LearnTransformPattern.md` → learn from examples
2. `ApplyTransformPattern.md` → apply to similar files
3. `DetectComponents.md` → find reusable patterns
4. `ExtractComponent.md` → create shared components
5. `ValidateWork.md` → continuous validation
6. `PreFlightCheck.md` → final validation

### Ending Any Session (REQUIRED)
1. `StoreKnowledge.md` → save useful Q&A
2. `EndSession.md` → close with summary

---

## 📋 COMPLETE MCP TOOL LIST (35 Tools)

### 🤖 Multi-Agent Coding (5 tools) - `coding-orchestrator` MCP Server
| Tool | Description | When to Use |
|------|-------------|-------------|
| `orchestrate_task` | Start multi-agent coding (CodingAgent + ValidationAgent loop) | New services, complex implementations |
| `get_task_status` | Check progress, get generated files | After starting task |
| `apply_task_files` | Get files with write instructions | When task is complete |
| `cancel_task` | Cancel running task | When no longer needed |
| `list_tasks` | List active/recent tasks | See what's running |

### 🎨 Design Agent (6 tools) - `coding-orchestrator` MCP Server
| Tool | Description | When to Use |
|------|-------------|-------------|
| `design_questionnaire` | Get brand builder questions | Creating new brand |
| `design_create_brand` | Create brand from answers | After answering questions |
| `design_get_brand` | Get brand by context | Get design tokens |
| `design_list_brands` | List all brands | See available brands |
| `design_validate` | Validate code against brand | After UI changes |
| `design_update_brand` | Update brand settings | Changing brand colors/fonts |

### 🔍 Search (1 tool)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `smartsearch` | Unified smart search (auto-detects semantic/graph/pattern strategy) | ANY code question |

### 📥 Indexing (1 tool)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `index` | Index code (scope: file, directory, reindex) | After file changes |

### 🧠 Session & Learning (6 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `start_session` | Start learning session (returns existing if active) | Beginning of conversation |
| `end_session` | End session with summary | End of conversation |
| `record_file_discussed` | Record file was discussed | Any file mentioned |
| `record_file_edited` | Record file was edited | After file edits |
| `store_qa` | Store Q&A for recall | After useful answers |
| `find_similar_questions` | Find similar past questions | BEFORE answering |

### 📊 Analysis (4 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `impact_analysis` | What code is impacted | Before changes |
| `dependency_chain` | Dependencies + circular detection | Understanding relationships |
| `analyze_complexity` | Code complexity metrics | Before commits |
| `validate` | Unified validation (6 scopes) | Before task completion |

### 🎯 Intelligence (4 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `get_recommendations` | Architecture recommendations | After indexing |
| `get_important_files` | Most important files | Prioritize review |
| `get_coedited_files` | Files edited together + clusters | Find related files |
| `get_insights` | Metrics (7 categories incl. domains, recalculate) | View/refresh metrics |

### 📋 Planning (2 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `manage_plan` | Plans (6 actions: create, get_status, update_task, complete, search, validate_task) | Plan operations |
| `manage_todos` | TODOs (3 actions: add, search, update_status) | TODO operations |

### 🎨 Transformation (2 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `transform` | Transform code (8 types incl. list_patterns) | UI/Code modernization |
| `get_migration_path` | Migration path for legacy patterns | Upgrading legacy code |

### 🔄 Evolving System (3 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `manage_prompts` | Manage LLM prompts (10 actions) | Prompt management |
| `manage_patterns` | Manage patterns (8 actions) | Pattern management |
| `feedback` | Record prompt/pattern feedback | After using prompts/patterns |

### 🔧 Workspace (2 tools)
| Tool | Description | When to Use |
|------|-------------|-------------|
| `register_workspace` | Register workspace | Auto on startup |
| `unregister_workspace` | Unregister workspace | Auto on shutdown |

---

## 🔧 Tool Parameter Quick Reference

### `index`
```
scope: "file" | "directory" | "reindex"
path: string (required)
context: string (required)
```

### `validate`
```
scope: "best_practices" | "security" | "pattern_quality" | "anti_patterns" | "project" | "list_best_practices" | "all"
context: string (required except for list_best_practices)
```

### `manage_plan`
```
action: "create" | "get_status" | "update_task" | "complete" | "search" | "validate_task"
# For create: context, name, tasks (required)
# For others: planId, taskId, status as needed
```

### `manage_todos`
```
action: "add" | "search" | "update_status"
# For add: context, title (required)
# For update_status: todoId, status
# For search: context, priority, todoStatus (optional filters)
```

### `transform`
```
type: "page" | "css" | "analyze_css" | "learn_pattern" | "apply_pattern" | "detect_components" | "extract_component" | "list_patterns"
sourcePath: string (for most types)
```

### `get_insights`
```
category: "patterns" | "prompts" | "tools" | "sessions" | "domains" | "recalculate" | "all"
context: string (for sessions, domains, recalculate)
filePath: string (for domains)
```

---

## ✨ What's New (v3.1)

### 🎨 Design Agent (NEW!)
- **Brand Builder**: Create complete brand systems with questionnaire
- **Design Tokens**: Colors, typography, spacing, shadows, borders
- **Component Specs**: Button, Input, Card, Modal, Alert patterns
- **Theme Support**: Dark/light modes with semantic colors
- **Accessibility Built-in**: WCAG AA/AAA compliance checks
- **Design Validation**: Validate UI code against brand guidelines
- **6 new tools**: `design_questionnaire`, `design_create_brand`, `design_get_brand`, `design_list_brands`, `design_validate`, `design_update_brand`

### Previous (v3.0)

### 🤖 Multi-Agent Code Generation
- **CodingOrchestrator**: Coordinates multi-agent workflow
- **CodingAgent**: Generates/fixes code using LLM with smart model rotation
- **ValidationAgent**: Validates code quality using rules + LLM analysis
- **Smart Model Rotation**: Uses different models on retry for fresh perspectives
- **5 tools**: `orchestrate_task`, `get_task_status`, `apply_task_files`, `cancel_task`, `list_tasks`

### Previous (v2.0)
**Consolidated from 73+ tools to 25 tools:**
- Better AI decision-making with fewer, clearer options
- Parameterized tools (action/scope/type) for related functionality
- Auto-initialization of prompts and patterns on startup

**Key Consolidations:**
- `manage_plan` replaces create_plan + 5 other plan tools
- `manage_todos` replaces add_todo + 2 other todo tools
- `validate` replaces 6 validation tools (via scope param)
- `transform` replaces 8 transformation tools (via type param)
- `get_insights` replaces 7 insight tools (via category param)
- `start_session` now returns existing session if active

**Evolving System:**
- `manage_prompts`: LLM prompts with versioning & A/B testing
- `manage_patterns`: Patterns with usefulness tracking
- `feedback`: Record outcomes for learning

---

## 🔧 Powered by

- **Neo4j** - Graph database for relationships, dependencies, prompts, and patterns
- **Qdrant** - Vector database for semantic search + Lightning learning
- **Ollama** - Local LLM inference with multi-GPU support
  - `deepseek-v2:16b` - Primary code generation (pinned on 5070 Ti)
  - `phi4` - Code validation
  - `mxbai-embed-large` - Embeddings
- **Smart Model Rotation** - Uses different models on fix attempts for fresh perspectives

---

**🎯 GOAL: Learn from every interaction to make future development faster and more accurate!**
