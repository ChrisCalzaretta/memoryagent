---
description: 🔍 Discover tools by specific category
alwaysApply: false
---

# Discover Tools By Category

Quickly find tools for specific tasks using the 6 core categories.

## Quick Reference

```javascript
// 🔍 Need to search or analyze code?
list_available_tools({ category: "discovery" })

// 🚀 Need to generate new code or designs?
list_available_tools({ category: "generation" })

// ✅ Need to validate or check quality?
list_available_tools({ category: "validation" })

// 📋 Need to plan or manage tasks?
list_available_tools({ category: "planning" })

// 🧠 Need to store or retrieve knowledge?
list_available_tools({ category: "knowledge" })

// 📊 Need to check status or control jobs?
list_available_tools({ category: "management" })
```

## Category Guide

### 🔍 Discovery
**When to use:** Need to find existing code or understand systems
- Search codebase semantically
- Analyze code structure and dependencies
- Explain how features work
- Find patterns and examples

### 🚀 Generation
**When to use:** Need to create something new
- Generate code from scratch
- Build REST APIs or services
- Create UI components
- Design brand systems

### ✅ Validation
**When to use:** Need to ensure quality
- Review code quality
- Check security vulnerabilities
- Validate best practices
- Audit compliance

### 📋 Planning
**When to use:** Need to organize work
- Create execution plans
- Break down complex projects
- Manage todos and tasks
- Track work items

### 🧠 Knowledge
**When to use:** Need to manage information
- Index workspace for search
- Store Q&A and decisions
- Learn from conversations
- Retrieve historical context

### 📊 Management
**When to use:** Need to monitor or control
- Check task/job status
- Monitor running operations
- Control background jobs
- Cancel long-running tasks

## Best Practice

**Most of the time, you don't need this command!**

Just use `execute_task` with natural language:
```javascript
execute_task({ request: "Find all authentication code" })
```

The AI automatically chooses the right tools from the right categories.
