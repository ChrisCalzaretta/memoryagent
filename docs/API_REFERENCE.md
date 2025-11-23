# Memory Agent API Reference

## TODO Endpoints

### Add TODO
```http
POST /api/todo/add
Content-Type: application/json

{
  "context": "MyProject",
  "title": "Fix bug in UserService",
  "description": "Add null check",
  "priority": "High",  // Low, Medium, High, Critical
  "filePath": "/src/Services/UserService.cs",
  "lineNumber": 45,
  "assignedTo": "chris@example.com"
}
```

### Remove TODO
```http
DELETE /api/todo/remove/{todoId}
```

### Get TODO
```http
GET /api/todo/{todoId}
```

### List TODOs
```http
GET /api/todo/list?context=MyProject&status=Pending
```

**Query Parameters:**
- `context` (optional): Filter by context
- `status` (optional): Pending, InProgress, Completed, Cancelled

### Search TODOs
```http
GET /api/todo/search?context=MyProject&status=Pending&priority=High&assignedTo=chris
```

**Query Parameters:**
- `context` (optional): Filter by context
- `status` (optional): Pending, InProgress, Completed, Cancelled
- `priority` (optional): Low, Medium, High, Critical
- `assignedTo` (optional): Filter by assignee (partial match)

### Update TODO Status
```http
PUT /api/todo/{todoId}/status
Content-Type: application/json

"Completed"  // Pending, InProgress, Completed, Cancelled
```

---

## Plan Endpoints

### Add Plan
```http
POST /api/plan/add
Content-Type: application/json

{
  "context": "MyProject",
  "name": "User Authentication Refactor",
  "description": "Modernize auth system to use OAuth2",
  "tasks": [
    {
      "title": "Research OAuth2 providers",
      "description": "Evaluate Auth0, Okta, AWS Cognito",
      "orderIndex": 1,
      "dependencies": []
    },
    {
      "title": "Implement OAuth2 client",
      "description": "Add OAuth2 library and configuration",
      "orderIndex": 2,
      "dependencies": ["<task-id-from-step-1>"]
    },
    {
      "title": "Migrate existing users",
      "description": "Create migration script",
      "orderIndex": 3,
      "dependencies": ["<task-id-from-step-2>"]
    }
  ]
}
```

### Update Plan
```http
PUT /api/plan/update
Content-Type: application/json

{
  "planId": "plan-abc-123",
  "name": "Updated Plan Name",  // Optional
  "description": "Updated description",  // Optional
  "status": "Active",  // Optional: Draft, Active, Completed, Cancelled, OnHold
  "tasks": [  // Optional
    {
      "taskId": "task-xyz-456",
      "title": "Updated task title",  // Optional
      "description": "Updated description",  // Optional
      "status": "Completed"  // Optional: Pending, InProgress, Blocked, Completed, Cancelled
    }
  ]
}
```

### Complete Plan
```http
POST /api/plan/{planId}/complete
```
Marks the plan as completed and cancels all incomplete tasks.

### Get Plan
```http
GET /api/plan/{planId}
```

**Response:**
```json
{
  "id": "plan-abc-123",
  "context": "MyProject",
  "name": "User Authentication Refactor",
  "description": "...",
  "status": "Active",
  "tasks": [
    {
      "id": "task-1",
      "title": "Research OAuth2 providers",
      "description": "...",
      "status": "Completed",
      "orderIndex": 1,
      "dependencies": [],
      "completedAt": "2025-11-22T10:30:00Z"
    }
  ],
  "createdAt": "2025-11-22T09:00:00Z",
  "completedAt": null
}
```

### Get Plan Status
```http
GET /api/plan/{planId}/status
```

**Response:**
```json
{
  "planId": "plan-abc-123",
  "name": "User Authentication Refactor",
  "status": "Active",
  "progress": 33.33,
  "totalTasks": 3,
  "completedTasks": 1,
  "inProgressTasks": 1,
  "pendingTasks": 1,
  "blockedTasks": 0,
  "createdAt": "2025-11-22T09:00:00Z",
  "completedAt": null,
  "tasks": [
    {
      "id": "task-1",
      "title": "Research OAuth2 providers",
      "status": "Completed",
      "orderIndex": 1,
      "completedAt": "2025-11-22T10:30:00Z"
    },
    {
      "id": "task-2",
      "title": "Implement OAuth2 client",
      "status": "InProgress",
      "orderIndex": 2,
      "completedAt": null
    }
  ]
}
```

### List Plans
```http
GET /api/plan/list?context=MyProject
```

**Query Parameters:**
- `context` (optional): Filter by context

### Search Plans
```http
GET /api/plan/search?context=MyProject&status=Active
```

**Query Parameters:**
- `context` (optional): Filter by context
- `status` (optional): Draft, Active, Completed, Cancelled, OnHold

### Delete Plan
```http
DELETE /api/plan/{planId}
```

### Update Task Status
```http
PUT /api/plan/{planId}/task/{taskId}/status
Content-Type: application/json

"InProgress"  // Pending, InProgress, Blocked, Completed, Cancelled
```

---

## Index Endpoints

### Index File
```http
POST /api/index/file
Content-Type: application/json

{
  "filePath": "E:\\GitHub\\MyProject\\UserService.cs",
  "context": "MyProject"
}
```

### Index Directory
```http
POST /api/index/directory
Content-Type: application/json

{
  "directoryPath": "E:\\GitHub\\MyProject\\Services",
  "recursive": true,
  "context": "MyProject"
}
```

### Reindex (Smart)
```http
POST /api/index/reindex
Content-Type: application/json

{
  "context": "MyProject",
  "path": "E:\\GitHub\\MyProject",
  "removeStale": true
}
```

### Query Code
```http
POST /api/index/query
Content-Type: application/json

{
  "query": "user authentication with JWT tokens",
  "context": "MyProject",
  "limit": 10,
  "minimumScore": 0.5
}
```

---

## Smart Search Endpoints

### Smart Search
```http
POST /api/smartsearch/search
Content-Type: application/json

{
  "query": "How does user login work?",
  "context": "MyProject",
  "searchMode": "hybrid",  // semantic, graph, hybrid
  "maxResults": 10,
  "includeRelated": true
}
```

---

## Enums Reference

### TodoPriority
- `Low`
- `Medium`
- `High`
- `Critical`

### TodoStatus
- `Pending`
- `InProgress`
- `Completed`
- `Cancelled`

### PlanStatus
- `Draft`
- `Active`
- `Completed`
- `Cancelled`
- `OnHold`

### TaskStatus
- `Pending`
- `InProgress`
- `Blocked`
- `Completed`
- `Cancelled`

---

## PowerShell Examples

### Add a TODO
```powershell
$todo = @{
    context = "CBC_AI"
    title = "Refactor UserService"
    description = "Extract validation logic"
    priority = "High"
    filePath = "/src/Services/UserService.cs"
    lineNumber = 45
    assignedTo = "chris@example.com"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5098/api/todo/add" `
    -Method Post `
    -Body $todo `
    -ContentType "application/json"
```

### Search TODOs
```powershell
Invoke-RestMethod -Uri "http://localhost:5098/api/todo/search?context=CBC_AI&status=Pending&priority=High"
```

### Create a Plan
```powershell
$plan = @{
    context = "CBC_AI"
    name = "Payment System Upgrade"
    description = "Migrate to Stripe v3"
    tasks = @(
        @{
            title = "Update Stripe SDK"
            description = "Upgrade to v3"
            orderIndex = 1
            dependencies = @()
        },
        @{
            title = "Test payment flow"
            description = "E2E tests"
            orderIndex = 2
            dependencies = @()
        }
    )
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5098/api/plan/add" `
    -Method Post `
    -Body $plan `
    -ContentType "application/json"
```

### Get Plan Status
```powershell
Invoke-RestMethod -Uri "http://localhost:5098/api/plan/{planId}/status"
```

### Complete Plan
```powershell
Invoke-RestMethod -Uri "http://localhost:5098/api/plan/{planId}/complete" `
    -Method Post
```

### Mark Task as Completed
```powershell
Invoke-RestMethod -Uri "http://localhost:5098/api/plan/{planId}/task/{taskId}/status" `
    -Method Put `
    -Body '"Completed"' `
    -ContentType "application/json"
```

---

## Bash/curl Examples

### Add TODO
```bash
curl -X POST http://localhost:5098/api/todo/add \
  -H "Content-Type: application/json" \
  -d '{
    "context": "CBC_AI",
    "title": "Fix bug",
    "priority": "High"
  }'
```

### Search Plans
```bash
curl "http://localhost:5098/api/plan/search?context=CBC_AI&status=Active"
```

### Complete Plan
```bash
curl -X POST http://localhost:5098/api/plan/abc-123/complete
```

---

## Response Formats

### Success Response
```json
{
  "id": "todo-123",
  "context": "MyProject",
  "title": "Fix bug",
  "status": "Pending",
  "...": "..."
}
```

### Error Response
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "TODO not found: todo-123"
}
```

### List Response
```json
[
  { "id": "1", "...": "..." },
  { "id": "2", "...": "..." }
]
```

