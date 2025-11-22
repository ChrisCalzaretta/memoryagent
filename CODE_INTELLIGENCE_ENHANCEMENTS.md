# Code Intelligence Enhancements

## Overview

Major enhancements to the Memory Agent code analysis and tracking capabilities, including complexity metrics, test tracking, TODO/Plan management with validation rules, and comprehensive metadata extraction.

## 1. Code Complexity Metrics ✅

### What We Track

**For Every Method:**
- **Cyclomatic Complexity**: Number of independent paths through code (if, while, for, switch, etc.)
- **Cognitive Complexity**: Mental burden to understand code (nesting-aware)
- **Lines of Code**: Actual code lines (excluding comments/blank lines)
- **Code Smells**: Automatically detected anti-patterns
  - `long_method` (>50 lines)
  - `too_many_parameters` (>5 params)
  - `high_complexity` (>10 cyclomatic)
  - `deep_nesting` (>3 levels)
  - `async_without_error_handling`
- **Max Nesting Depth**: Deepest level of nesting

**For Every Class:**
- Lines of code
- Method count
- Property count
- Field count
- God class detection (>1000 lines)

### Usage Example

```json
{
  "name": "UserService.CreateUser",
  "metadata": {
    "cyclomatic_complexity": 7,
    "cognitive_complexity": 5,
    "lines_of_code": 45,
    "code_smells": ["high_complexity"],
    "code_smell_count": 1
  }
}
```

## 2. Test Tracking ✅

### New Code Type

Added `CodeMemoryType.Test` to distinguish test methods from production code.

### Auto-Detection

Automatically detects test methods by attributes:
- `[Test]` (NUnit)
- `[Fact]`, `[Theory]` (xUnit)
- `[TestMethod]` (MSTest)
- `[TestCase]` (NUnit parameterized)

### Test Metadata

```json
{
  "type": "Test",
  "name": "UserServiceTests.CreateUser_ValidInput_ReturnsUser",
  "metadata": {
    "is_test": true,
    "test_framework": "xunit",
    "assertion_count": 5,
    "tests_method": "UserService.CreateUser"
  }
}
```

## 3. TODO & Plan Management ✅

### NEW Endpoints

#### TODO Management

**Add TODO:**
```http
POST /api/todo/add
{
  "context": "MyProject",
  "title": "Fix N+1 query in GetUsers",
  "description": "Add eager loading",
  "priority": "High",
  "filePath": "/src/Services/UserService.cs",
  "lineNumber": 45,
  "assignedTo": "chris@example.com"
}
```

**Remove TODO:**
```http
DELETE /api/todo/remove/{todoId}
```

**List TODOs:**
```http
GET /api/todo/list?context=MyProject&status=Pending
```

**Search TODOs:**
```http
GET /api/todo/search?context=MyProject&status=Pending&priority=High&assignedTo=chris
```

**Update TODO Status:**
```http
PUT /api/todo/{todoId}/status
Body: "Completed"
```

#### Plan Management

**Add Development Plan:**
```http
POST /api/plan/add
{
  "context": "MyProject",
  "name": "User Authentication Refactor",
  "description": "Modernize auth system",
  "tasks": [
    {
      "title": "Update JWT library",
      "description": "Upgrade to v7",
      "orderIndex": 1,
      "dependencies": []
    },
    {
      "title": "Add refresh tokens",
      "description": "Implement token refresh",
      "orderIndex": 2,
      "dependencies": ["task-id-1"]
    }
  ]
}
```

**Update Plan:**
```http
PUT /api/plan/update
{
  "planId": "plan-123",
  "status": "Active",
  "tasks": [
    {
      "taskId": "task-1",
      "status": "Completed"
    }
  ]
}
```

**Complete Plan:**
```http
POST /api/plan/{planId}/complete
```

**Get Plan:**
```http
GET /api/plan/{planId}
```

**Get Plan Status:**
```http
GET /api/plan/{planId}/status
```

**List Plans:**
```http
GET /api/plan/list?context=MyProject
```

**Search Plans:**
```http
GET /api/plan/search?context=MyProject&status=Active
```

**Update Task Status:**
```http
PUT /api/plan/{planId}/task/{taskId}/status
Body: "InProgress"
```

### Plan Features

- **Task Dependencies**: Tasks can depend on other tasks
- **Auto-completion**: Plan auto-completes when all tasks are done
- **Order Tracking**: Tasks have explicit ordering
- **Status Management**: Track plan/task states independently

### Storage

TODOs and Plans are stored in Neo4j as nodes:
- `(:Todo)` nodes with properties
- `(:Plan)` and `(:PlanTask)` nodes
- `(:Plan)-[:HAS_TASK]->(:PlanTask)` relationships
- `(:PlanTask)-[:DEPENDS_ON]->(:PlanTask)` for dependencies

### Task Validation Rules ✅

**NEW**: Tasks can have validation rules that must pass before marking complete!

#### Validation Rule Types

| Rule Type | Description | Auto-Fix | Purpose |
|-----------|-------------|----------|---------|
| `requires_test` | Task must have unit tests | ✅ | Ensures test coverage |
| `requires_file` | Specific file must exist | ✅ | Validates deliverables |
| `min_test_coverage` | Minimum % of methods tested | ✅ | Enforces coverage goals |
| `max_complexity` | Cyclomatic complexity limit | ❌ | Prevents complex code |
| `requires_documentation` | Public APIs need XML docs | ✅ | Ensures documentation |
| `no_code_smells` | No quality issues detected | ❌ | Maintains code quality |

#### Example: Create a Task with Validation

```json
POST /api/plan/add
{
  "context": "MyProject",
  "name": "Add Payment Processing",
  "tasks": [
    {
      "title": "Create PaymentService",
      "description": "Core payment logic",
      "orderIndex": 0,
      "validationRules": [
        {
          "ruleType": "requires_test",
          "target": "PaymentService",
          "autoFix": true
        },
        {
          "ruleType": "max_complexity",
          "target": "PaymentService",
          "parameters": {
            "max_complexity": 10
          },
          "autoFix": false
        },
        {
          "ruleType": "requires_documentation",
          "target": "PaymentService",
          "autoFix": true
        }
      ]
    }
  ]
}
```

#### Validation Workflow

**When Marking Task Complete:**
1. System checks all validation rules
2. If any fail:
   - Auto-fix enabled? → Try to fix automatically
   - Auto-fix disabled? → Block completion with error
3. If auto-fix successful, re-validate
4. Only mark complete if all rules pass

**Example Validation Failure:**
```
❌ Task 'Create PaymentService' failed validation:

• requires_test: No tests found for PaymentService
  💡 Auto-fix available: Create PaymentServiceTests.cs

  📋 Details:
     Create tests for 5 method(s) in PaymentService. Focus on public methods first.

     Methods needing tests:
       - ProcessPayment
       - ValidateCard
       - RefundPayment
       - GetPaymentStatus
       - CancelPayment

     Example test names:
       - ProcessPayment_ShouldReturnTrue_WhenPaymentSucceeds
       - ValidateCard_ShouldThrow_WhenCardExpired
       - RefundPayment_ShouldUpdateStatus_WhenRefunded

• max_complexity: Method ProcessPayment has complexity 15 (limit: 10)
  💡 Refactor: ProcessPayment, ValidateCard

🔧 Attempting auto-fix...
✅ Created PaymentServiceTests.cs
❌ Complexity issue requires manual refactoring

💡 Cursor can now use the provided context to write the actual test implementations!
```

#### Auto-Fix Capabilities

**What Auto-Fix Does:**
- ✅ Creates test file scaffolding with xUnit template
- ✅ Generates XML documentation stubs
- ✅ Creates missing files from templates
- ✅ **Provides detailed context to AI agents (Cursor)**:
  - Lists all methods that need tests
  - Suggests test names (e.g., `CreateUser_Should...`)
  - Shows method signatures
  - Prioritizes public methods
  - Gives example test structure

**What It Doesn't Do (But Guides You):**
- 🤖 **Doesn't write full test logic** BUT provides enough context for Cursor AI to write it
- ❌ Doesn't refactor complex methods (requires human judgment)
- ❌ Doesn't fix code smells automatically (needs design decisions)

**How It Helps AI Agents:**
When validation fails, the response includes **actionable context** that Cursor can use:
```json
{
  "target_class": "UserService",
  "methods_to_test": [
    { "Name": "CreateUser", "Signature": "async Task<User> CreateUser(...)" },
    { "Name": "DeleteUser", "Signature": "async Task<bool> DeleteUser(...)" }
  ],
  "suggestion": "Create tests for 5 method(s). Focus on public methods first.",
  "example_test_names": [
    "CreateUser_Should...",
    "DeleteUser_Should..."
  ]
}
```

This means **Cursor can read the context and write the actual tests for you!** 🎉

#### MCP Integration

**Validate Task Before Completion:**
```javascript
// Cursor MCP tool: validate_task
{
  "planId": "plan-123",
  "taskId": "task-456",
  "autoFix": true  // Automatically fix issues
}
```

**Response:**
```
✅ Task 'Create PaymentService' passed all validation rules!

Checks:
  ✅ requires_test: PaymentServiceTests.cs found
  ✅ max_complexity: All methods < 10
  ✅ requires_documentation: All public methods documented

Task is ready to be marked as completed.
```

#### Benefits

1. **Quality Gates**: Can't mark task complete without tests
2. **Automation**: Auto-generates boilerplate (tests, docs)
3. **Objective Criteria**: Clear definition of "done"
4. **Prevention**: Stops technical debt at the source
5. **Guidance**: Tells you exactly what's missing

#### See Also

- `TASK_VALIDATION.md` - Complete validation system documentation
- `TASK_VALIDATION_EXAMPLE.md` - Practical examples and workflows

## 4. Enhanced API Metadata ✅

### What We Track

**For Controllers and Endpoints:**
- HTTP method (GET, POST, PUT, DELETE)
- Route pattern
- Authorization requirements
- Rate limiting
- API version
- Public vs Internal API

**For All Methods:**
- Public API flag
- Access modifier
- Return type
- Parameter count

### Example

```json
{
  "name": "UserController.CreateUser",
  "metadata": {
    "is_public_api": true,
    "access_modifier": "public",
    "http_method": "POST",
    "route": "/api/v1/users",
    "requires_auth": true,
    "has_rate_limiting": true
  }
}
```

## 5. Async Pattern Detection ✅

### What We Track

- `is_async`: Method uses async/await
- Async without error handling detection (code smell)
- Task-based return types
- ConfigureAwait usage

```json
{
  "metadata": {
    "is_async": true,
    "return_type": "Task<UserDto>",
    "code_smells": ["async_without_error_handling"]
  }
}
```

## 6. Database Access Detection ✅

### Patterns Detected

- Entity Framework calls: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`
- ADO.NET calls: `ExecuteReader`, `ExecuteNonQuery`, `ExecuteScalar`
- Query methods: `Query`, `Execute`, `Add`, `Update`, `Remove`

### Metadata

```json
{
  "metadata": {
    "database_calls": 3,
    "has_database_access": true
  }
}
```

### Use Cases

- Identify N+1 query problems
- Find methods that should be transactional
- Detect missing async on database calls

## 7. HTTP Call Detection ✅

### Patterns Detected

- HttpClient usage
- REST calls: `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`
- Third-party API clients

```json
{
  "metadata": {
    "has_http_calls": true
  }
}
```

## 8. Exception Type Extraction ✅

### What We Track

All exception types that a method can throw:

```json
{
  "metadata": {
    "exception_types": [
      "ValidationException",
      "UserNotFoundException",
      "DuplicateEmailException"
    ],
    "throws_exceptions": true
  }
}
```

### Benefits

- Document error handling requirements
- Find missing try-catch blocks
- Identify exception-heavy code

## 9. Logging Detection ✅

### What We Track

```json
{
  "metadata": {
    "has_logging": true
  }
}
```

### Patterns Detected

- ILogger usage
- LogInformation, LogError, LogWarning, LogDebug calls
- Third-party logging frameworks

## 10. New Code Element Types ✅

### Added Types

```csharp
public enum CodeMemoryType
{
    // Existing
    File, Class, Method, Property, Interface, Pattern,
    
    // NEW
    Test,           // Unit/Integration tests
    Enum,           // Enumerations
    Record,         // C# records
    Struct,         // Value types
    Delegate,       // Delegates
    Event,          // Events
    Constant,       // Constants
    
    // Architecture
    Repository,     // Data access
    Service,        // Service layer
    Controller,     // API controllers
    Middleware,     // ASP.NET middleware
    Filter,         // Action/Exception filters
    
    // Data
    DbContext,      // EF DbContext
    Entity,         // Database entities
    Migration,      // DB migrations
    
    // Frontend
    Component,      // React/Vue/Angular
    Hook,           // React hooks
    
    // API
    Endpoint       // API endpoints
}
```

## Usage Scenarios

### 1. Find Complex Code

```cypher
// Neo4j query
MATCH (m:Method)
WHERE m.cyclomatic_complexity > 10
RETURN m.name, m.cyclomatic_complexity
ORDER BY m.cyclomatic_complexity DESC
```

### 2. Find Untested Code

```cypher
MATCH (m:Method)
WHERE m.is_public_api = true
AND NOT EXISTS {
  MATCH (t:Test)
  WHERE t.tests_method = m.name
}
RETURN m.name
```

### 3. Find Performance Risks

```cypher
MATCH (m:Method)
WHERE m.database_calls > 5
OR (m.database_calls > 0 AND m.code_smells CONTAINS 'high_complexity')
RETURN m.name, m.database_calls, m.cyclomatic_complexity
```

### 4. Track Technical Debt

```cypher
MATCH (m:Method)
WHERE m.code_smell_count > 0
RETURN m.name, m.code_smells, m.lines_of_code, m.cyclomatic_complexity
ORDER BY m.code_smell_count DESC
```

### 5. Find Missing Error Handling

```cypher
MATCH (m:Method)
WHERE m.is_async = true
AND m.code_smells CONTAINS 'async_without_error_handling'
AND m.has_database_access = true
RETURN m.name, m.file_path
```

### 6. API Impact Analysis

```cypher
MATCH (m:Method)
WHERE m.is_public_api = true
RETURN m.name, m.http_method, m.route, m.requires_auth
```

## Benefits

1. **Code Quality**: Track and improve code quality metrics
2. **Test Coverage**: Identify untested code
3. **Performance**: Find database/HTTP bottlenecks
4. **Security**: Track public APIs and error handling
5. **Technical Debt**: Quantify and prioritize tech debt
6. **Documentation**: Auto-document exception types and patterns
7. **Project Management**: Track TODOs and development plans
8. **Impact Analysis**: Understand dependencies and risks

## Implementation Details

### Files Modified

- `Models/CodeMemory.cs`: Added new CodeMemoryType enum values
- `Models/TodoModels.cs`: NEW - TODO and Plan models
- `Services/ITodoService.cs`: NEW - TODO service interface
- `Services/TodoService.cs`: NEW - TODO service implementation
- `Services/IPlanService.cs`: NEW - Plan service interface
- `Services/PlanService.cs`: NEW - Plan service implementation
- `Services/IGraphService.cs`: Added TODO/Plan methods
- `Services/GraphService.cs`: Implemented TODO/Plan storage
- `Controllers/TodoController.cs`: NEW - REST endpoints
- `CodeAnalysis/ComplexityAnalyzer.cs`: NEW - Complexity calculations
- `CodeAnalysis/RoslynParser.cs`: Enhanced with all new metadata
- `Program.cs`: Registered new services

### Database Schema

**Neo4j Nodes:**
- `(:Todo)` - TODO items
- `(:Plan)` - Development plans
- `(:PlanTask)` - Individual tasks within plans

**Neo4j Relationships:**
- `(:Plan)-[:HAS_TASK]->(:PlanTask)`
- `(:PlanTask)-[:DEPENDS_ON]->(:PlanTask)`

## Python Support ✅

### Same Metrics as C#

Python code now gets the **exact same analysis** as C# code:

**All Complexity Metrics:**
- Cyclomatic complexity
- Cognitive complexity
- Lines of code
- Code smells detection
- Max nesting depth

**All Quality Tracking:**
- Database/ORM calls (SQLAlchemy, Django ORM, etc.)
- HTTP calls (requests, httpx, aiohttp)
- Exception types (raise statements)
- Logging detection
- Async detection (async def, await)
- Test detection (pytest, unittest)

**Example Python Method Metadata:**
```json
{
  "name": "UserService.create_user",
  "type": "Method",
  "metadata": {
    "language": "python",
    "cyclomatic_complexity": 6,
    "cognitive_complexity": 4,
    "lines_of_code": 35,
    "code_smells": [],
    "is_async": true,
    "database_calls": 2,
    "has_http_calls": false,
    "has_logging": true,
    "exception_types": ["ValidationError", "UserExistsError"],
    "is_test": false
  }
}
```

**Example Python Test Detection:**
```python
@pytest.mark.asyncio
async def test_create_user_valid_input():
    # This will be detected as Test type
    user = await service.create_user(...)
    assert user.email == "test@example.com"
```

Stored as:
```json
{
  "type": "Test",
  "metadata": {
    "is_test": true,
    "test_framework": "pytest",
    "assertion_count": 1,
    "is_async": true
  }
}
```

### Supported Python Patterns

**ORM Detection:**
- SQLAlchemy: `.query()`, `.filter()`, `.all()`, `.first()`
- Django: `.objects.`, `.filter()`, `.get()`, `.create()`
- Raw SQL: `.execute()`, `.fetchall()`, `.commit()`

**HTTP Detection:**
- `requests.get/post/put/delete`
- `httpx.AsyncClient`
- `aiohttp.ClientSession`
- `urllib`

**Test Frameworks:**
- pytest (decorators + `test_` prefix)
- unittest (`TestCase` classes)
- Assertion counting for all frameworks

## Future Enhancements

Potential additions:
- [ ] Real-time test execution results
- [ ] Code coverage integration
- [ ] Performance profiling data
- [ ] Git blame integration (who wrote this code)
- [ ] Dependency vulnerability scanning
- [ ] Architecture rule validation
- [ ] Auto-generated sequence diagrams
- [ ] Impact analysis AI suggestions

