# Relationship Enhancements Proposal

## 🎯 Current State (13 Relationship Types)

### Structural
- **INHERITS** - Class inheritance
- **IMPLEMENTS** - Interface implementation  
- **DEFINES** - Containment (file→class, class→method)

### Dependencies
- **USES** - General usage
- **CALLS** - Method calls
- **INJECTS** - Constructor injection (DI)
- **IMPORTS** - Using/import statements

### Types
- **HASTYPE** - Property types
- **RETURNSTYPE** - Return types
- **ACCEPTSTYPE** - Parameter types
- **USESGENERIC** - Generic type parameters

### Metadata
- **HASATTRIBUTE** - Attributes/decorators
- **THROWS** - Exception declarations
- **CATCHES** - Exception handling

---

## 🚀 Proposed Enhancements

### 🏆 **TOP 3 - Highest Impact for ASP.NET Projects**

#### 1. EXPOSES (API Endpoints)
**What:** Track HTTP endpoints and route them to controllers

**Extracts:**
- `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`
- `[Route("api/users")]`
- `[ApiController]`

**Relationships:**
```
Endpoint → EXPOSES → Controller → CALLS → Service → ACCESSES → Entity
```

**Metadata:**
- `route`: "/api/projects/{id}"
- `http_method`: "GET"
- `action`: "GetProject"

**Example Query:**
```cypher
MATCH path = (endpoint:Endpoint)-[:EXPOSES]->()-[:CALLS*]->()-[:ACCESSES]->(entity)
WHERE endpoint.route = '/api/projects/{id}'
RETURN path
```
**Shows:** Complete flow from API endpoint to database entity

---

#### 2. ACCESSES (Database/Entity)
**What:** Track which services/repositories touch which entities

**Extracts:**
- `DbSet<Entity>` usage
- `.Where()`, `.FirstOrDefault()`, `.Add()`, `.Update()`, `.Remove()`
- Entity Framework LINQ queries

**Relationships:**
```
Service → ACCESSES → Entity
Repository → ACCESSES → Entity
```

**Metadata:**
- `operation_type`: "Read", "Write", "Delete"
- `query_type`: "LINQ", "SQL", "StoredProc"
- `is_tracked`: true/false

**Example Query:**
```cypher
MATCH (s:Service)-[a:ACCESSES]->(e:Entity)
WHERE e.name = 'Users'
RETURN s.name, a.operation_type, count(*) as access_count
```
**Shows:** All services that access Users table with operation counts

---

#### 3. AUTHORIZES (Security)
**What:** Track authorization requirements on endpoints

**Extracts:**
- `[Authorize]` attribute
- `[Authorize(Roles = "Admin")]`
- `[Authorize(Policy = "RequireAdmin")]`
- `[AllowAnonymous]`

**Relationships:**
```
Endpoint → AUTHORIZES → Role
Endpoint → REQUIRESPOLICY → Policy
```

**Metadata:**
- `role_names`: ["Admin", "Manager"]
- `policy_name`: "RequireAdmin"
- `is_anonymous`: false

**Example Query:**
```cypher
MATCH (endpoint)-[:AUTHORIZES]->(role)
WHERE role.name = 'Admin'
RETURN endpoint.route, endpoint.method
```
**Shows:** All admin-only API endpoints

---

### 🥈 **NEXT TIER - High Value**

#### 4. VALIDATES (Validation Rules)
**What:** Track validation rules on models

**Extracts:**
- FluentValidation: `RuleFor(x => x.Email).NotEmpty()`
- DataAnnotations: `[Required]`, `[MaxLength(100)]`, `[EmailAddress]`
- Custom validators

**Relationships:**
```
Validator → VALIDATES → Model → Property
```

**Metadata:**
- `validation_type`: "Required", "MaxLength", "EmailAddress"
- `error_message`: "Email is required"
- `rule`: "NotEmpty"

---

#### 5. READS/WRITES (Configuration)
**What:** Track configuration dependencies

**Extracts:**
- `IConfiguration["ConnectionStrings:Default"]`
- `IOptions<Settings>`
- `GetValue<string>("Key")`

**Relationships:**
```
Service → READS → ConfigKey
Service → REQUIRES → ConfigSection
```

**Metadata:**
- `config_path`: "ConnectionStrings:Default"
- `default_value`: "Server=localhost"
- `is_required`: true

---

#### 6. RAISES/HANDLES (Events)
**What:** Track event publishers and subscribers

**Extracts:**
- Event declarations
- Event raising (`OnUserCreated?.Invoke()`)
- Event handlers (`+= HandleUserCreated`)

**Relationships:**
```
Publisher → RAISES → Event → HANDLEDBY → Subscriber
```

---

#### 7. AWAITS (Async/Await)
**What:** Track async method calls

**Extracts:**
- `async` keyword
- `await` keyword
- `Task<T>` return types

**Metadata on CALLS:**
- `is_async`: true
- `is_awaited`: true
- `is_fire_and_forget`: false

---

#### 8. MAPS (DTO/Entity Mapping)
**What:** Track data transformations

**Extracts:**
- AutoMapper: `CreateMap<Source, Dest>()`
- Manual mapping: `new UserDto { Name = user.Name }`

**Relationships:**
```
DTO ← MAPS → Entity
Mapper → TRANSFORMS → Type
```

---

#### 9. CACHES (Caching)
**What:** Track cache usage

**Extracts:**
- `IMemoryCache.Set(key, value)`
- `IDistributedCache.GetAsync(key)`
- `[ResponseCache]` attribute

**Relationships:**
```
Method → CACHES → CacheKey
```

**Metadata:**
- `cache_key`: "users_list"
- `expiration`: "00:05:00"
- `cache_type`: "Memory" or "Distributed"

---

#### 10. TESTS (Test Coverage)
**What:** Link tests to code under test

**Extracts:**
- `[Fact]`, `[Test]` attributes
- Test class → target class patterns

**Relationships:**
```
TestClass → TESTS → Class
TestMethod → TESTS → Method
```

**Metadata:**
- `test_type`: "Unit", "Integration", "E2E"
- `test_framework`: "xUnit", "NUnit"

---

## 🔥 Relationship Enrichment (Better Quality)

### Current: Simple Edges
```
Method A → CALLS → Method B
```

### Enhanced: Weighted + Rich Metadata
```
Method A → CALLS → Method B
  {
    call_count: 5,              // Called in 5 places
    is_async: true,             // Uses await
    is_conditional: true,       // In if/switch
    is_in_loop: false,          // Not in loop
    is_in_try: false,           // Not in try block
    line_numbers: [45, 67, 89], // Where it's called
    access_modifier: "public",   // Visibility
    parameters_passed: 3         // Number of args
  }
```

### Benefits:
- ✅ Find most-called methods (refactoring hotspots)
- ✅ Identify critical paths
- ✅ Weight for impact analysis
- ✅ Detect potential bottlenecks

---

## 📊 Example Queries Enabled

### 1. API to Database Trace
```cypher
MATCH path = (endpoint:Endpoint)-[:EXPOSES]->()-[:CALLS*]->()-[:ACCESSES]->(entity:Entity)
WHERE endpoint.route CONTAINS '/api/projects'
RETURN endpoint.route, entity.name, length(path) as depth
ORDER BY depth
```
**Shows:** All database tables touched by project APIs

---

### 2. Security Audit
```cypher
MATCH (endpoint:Endpoint)
WHERE NOT (endpoint)-[:AUTHORIZES]->()
  AND NOT (endpoint)-[:ALLOWANONYMOUS]->()
RETURN endpoint.route
```
**Shows:** Endpoints with no authorization (security risk!)

---

### 3. Most Critical Entities
```cypher
MATCH (s)-[:ACCESSES]->(e:Entity)
RETURN e.name, count(DISTINCT s) as service_count
ORDER BY service_count DESC
LIMIT 10
```
**Shows:** Most-accessed database tables

---

### 4. Validation Coverage
```cypher
MATCH (m:Model)
OPTIONAL MATCH (m)-[:VALIDATES]->()
WHERE NOT (m)-[:VALIDATES]->()
RETURN m.name as unvalidated_model
```
**Shows:** Models with no validation rules

---

### 5. Configuration Dependencies
```cypher
MATCH (s:Service)-[:READS]->(c:ConfigKey)
WHERE c.path CONTAINS 'ConnectionString'
RETURN s.name, collect(c.path) as required_configs
```
**Shows:** Services that need database connection strings

---

### 6. Async Call Chains
```cypher
MATCH path = (m1:Method)-[c:CALLS where c.is_async = true*]->(m2:Method)
WHERE length(path) > 5
RETURN path
```
**Shows:** Deep async call chains (potential deadlock risk)

---

## 🎯 Recommended Implementation Order

### Phase 1: Core ASP.NET (2-3 hours)
1. **EXPOSES** - API endpoints
2. **ACCESSES** - Database entities  
3. **AUTHORIZES** - Security

**Immediate Value:** Complete API → Service → Database tracing

---

### Phase 2: Validation & Config (1-2 hours)
4. **VALIDATES** - Validation rules
5. **READS/WRITES** - Configuration

**Immediate Value:** Dependency and compliance tracking

---

### Phase 3: Enrichment (1 hour)
6. Add metadata to **CALLS**: `is_async`, `call_count`, `is_conditional`
7. Add metadata to **INJECTS**: `lifetime` (Singleton/Scoped/Transient)
8. Add metadata to **ACCESSES**: `operation_type` (Read/Write/Delete)

**Immediate Value:** Weighted analysis and hotspot detection

---

### Phase 4: Advanced (2-3 hours)
9. **RAISES/HANDLES** - Events
10. **AWAITS** - Async patterns
11. **MAPS** - DTO mappings
12. **CACHES** - Cache dependencies
13. **TESTS** - Test coverage

**Immediate Value:** Advanced analysis capabilities

---

## 💡 Implementation Strategy

### For C# (Roslyn-based)
- Extend `RoslynParser.cs`
- Add methods to extract new relationship types
- Use Roslyn semantic model for type analysis

### For Razor (RazorParser)
- Extract `@page` routes
- Track `@inject` dependencies
- Component authorization attributes

### For Python (PythonParser)
- Flask/Django decorators (@app.route)
- SQLAlchemy queries
- Pytest test discovery

---

## 🎁 What You Get

### Before (Current):
- 13 relationship types
- 42,656 relationships in CBC_AI
- Basic dependency tracking

### After (Full Implementation):
- **23 relationship types**
- **~150,000+ relationships** (3-4x more)
- Complete application flow tracing
- Security compliance mapping
- Configuration dependency tracking
- Test coverage visualization
- Performance hotspot detection

---

## 📝 Summary Table

| Relationship | Impact | Effort | ASP.NET | Python | Razor |
|--------------|--------|--------|---------|--------|-------|
| EXPOSES | 🔥🔥🔥 | 2h | ✅ | ✅ (Flask/Django) | ✅ |
| ACCESSES | 🔥🔥🔥 | 1h | ✅ | ✅ (SQLAlchemy) | - |
| AUTHORIZES | 🔥🔥🔥 | 30m | ✅ | ✅ (@login_required) | ✅ |
| VALIDATES | 🔥🔥 | 1h | ✅ | ✅ (Pydantic) | - |
| READS/WRITES | 🔥🔥 | 1h | ✅ | ✅ (os.environ) | - |
| RAISES/HANDLES | 🔥 | 1h | ✅ | ✅ | - |
| AWAITS | 🔥 | 1h | ✅ | ✅ (asyncio) | - |
| MAPS | 🔥 | 1h | ✅ | - | - |
| CACHES | 🔥 | 1h | ✅ | ✅ (Redis) | - |
| TESTS | 🔥 | 1h | ✅ | ✅ (pytest) | - |
| **Enrichment** | 🔥🔥 | 1h | ✅ | ✅ | ✅ |

---

## 🚀 Ready to Implement?

Choose your option and I'll build it! Just let me know which one you want:
1. **TOP 3 PACK** (Recommended)
2. **ALL 5 RELATIONSHIPS**
3. **JUST ENRICHMENT**
4. **CUSTOM** (pick specific ones)


