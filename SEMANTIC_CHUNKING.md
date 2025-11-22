# Semantic Chunking for ASP.NET Core

## 🎯 Overview

**Semantic chunking** goes beyond simple syntactic parsing to identify and chunk **meaningful patterns** specific to frameworks and architectures. This makes the code memory dramatically more useful for AI code generation and analysis.

---

## 🏗️ ASP.NET Core Semantic Patterns

### 1. **DI Registration Blocks**

#### What to Chunk:
```csharp
// In Program.cs or Startup.cs
services.AddScoped<IUserService, UserService>();
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddTransient<IEmailSender, SmtpEmailSender>();
services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(connectionString));
services.AddAuthentication().AddJwtBearer(options => { ... });
services.AddHttpClient<IWeatherService, WeatherService>();
```

#### Chunking Strategy:
- **One chunk per registration statement**
- Extract interface → implementation mapping
- Capture lifetime (Scoped/Singleton/Transient)
- Track configuration lambdas

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **REGISTERS** | Program | IUserService | `lifetime: Scoped` |
| **IMPLEMENTS_REGISTRATION** | IUserService | UserService | `lifetime: Scoped` |
| **CONFIGURES** | Program | AppDbContext | `provider: SqlServer` |

#### Metadata:
```json
{
  "chunk_type": "di_registration",
  "interface": "IUserService",
  "implementation": "UserService",
  "lifetime": "Scoped",
  "framework": "aspnet-core",
  "layer": "Infra",
  "file_path": "Program.cs"
}
```

#### Queries Enabled:
```cypher
// Find all Scoped services
MATCH (p:Program)-[r:REGISTERS]->(service)
WHERE r.lifetime = 'Scoped'
RETURN service.name

// Find what implements an interface
MATCH (interface)-[:IMPLEMENTS_REGISTRATION]->(impl)
WHERE interface.name = 'IUserService'
RETURN impl.name
```

---

### 2. **Action Method Chunks**

#### What to Chunk:
```csharp
[HttpGet("{id}")]
[Authorize(Roles = "Admin,Manager")]
[ProducesResponseType(typeof(UserDto), 200)]
[ProducesResponseType(404)]
public async Task<IActionResult> GetUser(
    [FromRoute] int id,
    [FromServices] IUserService userService)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
        
    var user = await userService.GetUserAsync(id);
    if (user == null)
        return NotFound();
        
    return Ok(user);
}
```

#### Chunking Strategy:
- **One chunk per action method**
- Include all attributes as part of the chunk
- Extract DTOs from parameters and return types
- Capture validation calls

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **EXPOSES** | Endpoint('/api/users/{id}') | UsersController.GetUser | `method: GET` |
| **AUTHORIZES** | GetUser | Role('Admin') | - |
| **AUTHORIZES** | GetUser | Role('Manager') | - |
| **ACCEPTSTYPE** | GetUser | int (id parameter) | `from: Route` |
| **INJECTS** | GetUser | IUserService | `from: Services` |
| **RETURNSTYPE** | GetUser | UserDto | `status_code: 200` |
| **CALLS** | GetUser | IUserService.GetUserAsync | - |
| **VALIDATES** | GetUser | ModelState | - |

#### Metadata:
```json
{
  "chunk_type": "action_method",
  "controller": "UsersController",
  "action": "GetUser",
  "route": "/api/users/{id}",
  "http_method": "GET",
  "auth_roles": ["Admin", "Manager"],
  "request_dto": "int",
  "response_dto": "UserDto",
  "status_codes": [200, 404],
  "validates_model": true,
  "framework": "aspnet-core",
  "layer": "API",
  "symbol_name": "UsersController.GetUser"
}
```

#### Queries Enabled:
```cypher
// Find all endpoints that return a specific DTO
MATCH (action)-[:RETURNSTYPE]->(dto)
WHERE dto.name = 'UserDto'
RETURN action.route, action.http_method

// Find all endpoints requiring Admin role
MATCH (action)-[:AUTHORIZES]->(role)
WHERE role.name = 'Admin'
RETURN action.route, action.http_method

// Trace from endpoint to database
MATCH path = (endpoint)-[:EXPOSES]->()-[:CALLS*]->()-[:ACCESSES]->(entity)
WHERE endpoint.route = '/api/users/{id}'
RETURN path
```

---

### 3. **EF Query Chunks**

#### What to Chunk:
```csharp
// Complex LINQ query
var result = await db.Users
    .Include(u => u.Profile)
    .ThenInclude(p => p.Address)
    .Where(u => u.IsActive && u.CreatedDate > cutoffDate)
    .GroupBy(u => u.Department)
    .Select(g => new DepartmentSummaryDto
    {
        DepartmentName = g.Key,
        UserCount = g.Count(),
        ActiveUsers = g.Count(u => u.LastLoginDate > DateTime.Now.AddDays(-30))
    })
    .OrderByDescending(d => d.UserCount)
    .ToListAsync();
```

#### Chunking Strategy:
- **One chunk per complex query** (3+ LINQ operations)
- Extract entity relationships (Include/ThenInclude)
- Capture projections to DTOs
- Identify query patterns (GroupBy, Join, etc.)

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **QUERIES** | Method | Users (Entity) | `operation: Read` |
| **INCLUDES** | Query | Profile (Entity) | `eager_load: true` |
| **INCLUDES** | Query | Address (Entity) | `eager_load: true, nested: true` |
| **PROJECTS** | Query | DepartmentSummaryDto | `projection_type: Select` |
| **GROUPS_BY** | Query | Department | - |

#### Metadata:
```json
{
  "chunk_type": "ef_query",
  "primary_entity": "Users",
  "included_entities": ["Profile", "Address"],
  "projection_dto": "DepartmentSummaryDto",
  "query_operations": ["Include", "ThenInclude", "Where", "GroupBy", "Select", "OrderByDescending"],
  "is_async": true,
  "framework": "ef-core",
  "layer": "Data",
  "query_complexity": "high"
}
```

#### Queries Enabled:
```cypher
// Find all queries that touch an entity
MATCH (method)-[:QUERIES]->(entity)
WHERE entity.name = 'Users'
RETURN method.name, method.query_operations

// Find DTOs created from queries
MATCH (query)-[:PROJECTS]->(dto)
RETURN query.primary_entity, dto.name

// Find N+1 query risks (no Include)
MATCH (method)-[:QUERIES]->(entity)
WHERE NOT (method)-[:INCLUDES]->()
RETURN method.name, entity.name
```

---

### 4. **Validation Logic Chunks**

#### What to Chunk:

**DataAnnotations:**
```csharp
public class CreateUserDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; }
    
    [Required]
    [MinLength(8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)", ErrorMessage = "Password must contain uppercase, lowercase, and number")]
    public string Password { get; set; }
    
    [Range(18, 120)]
    public int Age { get; set; }
}
```

**FluentValidation:**
```csharp
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress()
            .MaximumLength(255);
            
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)")
            .WithMessage("Password must contain uppercase, lowercase, and number");
            
        RuleFor(x => x.Age)
            .InclusiveBetween(18, 120);
    }
}
```

#### Chunking Strategy:
- **One chunk per model/validator class**
- Extract each validation rule separately
- Capture error messages
- Identify custom validators

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **VALIDATES** | CreateUserValidator | CreateUserDto | `framework: FluentValidation` |
| **VALIDATES_PROPERTY** | Rule | Email | `rules: [NotEmpty, EmailAddress, MaxLength]` |
| **VALIDATES_PROPERTY** | Rule | Password | `rules: [NotEmpty, MinLength, Matches]` |

#### Metadata:
```json
{
  "chunk_type": "validation",
  "validator_name": "CreateUserValidator",
  "model": "CreateUserDto",
  "validation_framework": "FluentValidation",
  "properties_validated": ["Email", "Password", "Age"],
  "validation_rules": {
    "Email": ["NotEmpty", "EmailAddress", "MaxLength"],
    "Password": ["NotEmpty", "MinLength", "Matches"],
    "Age": ["InclusiveBetween"]
  },
  "layer": "Domain"
}
```

#### Queries Enabled:
```cypher
// Find all validation for a DTO
MATCH (validator)-[:VALIDATES]->(dto)
WHERE dto.name = 'CreateUserDto'
RETURN validator.name, validator.validation_rules

// Find DTOs with no validation
MATCH (dto:Class)
WHERE dto.name ENDS WITH 'Dto'
  AND NOT ()-[:VALIDATES]->(dto)
RETURN dto.name
```

---

### 5. **Authorization Configuration**

#### What to Chunk:
```csharp
// In Program.cs
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
        
    options.AddPolicy("RequireEmailVerified", policy =>
        policy.RequireClaim("EmailVerified", "true"));
        
    options.AddPolicy("MinimumAge", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(18)));
});

// Custom handler
public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var age = context.User.FindFirst("Age")?.Value;
        if (int.TryParse(age, out var ageValue) && ageValue >= requirement.MinimumAge)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

#### Chunking Strategy:
- **One chunk per policy definition**
- **One chunk per custom handler**
- Extract requirements, roles, claims

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **DEFINES_POLICY** | Program | RequireAdmin | `type: Policy` |
| **REQUIRES_ROLE** | RequireAdmin | Admin | - |
| **REQUIRES_CLAIM** | RequireEmailVerified | EmailVerified | `value: true` |
| **HANDLES** | MinimumAgeHandler | MinimumAgeRequirement | - |

#### Metadata:
```json
{
  "chunk_type": "auth_policy",
  "policy_name": "RequireAdmin",
  "requirements": ["Role:Admin"],
  "framework": "aspnet-core",
  "layer": "Infra"
}
```

---

### 6. **Middleware Pipeline**

#### What to Chunk:
```csharp
var app = builder.Build();

// 1
app.UseHttpsRedirection();

// 2
app.UseStaticFiles();

// 3
app.UseCors();

// 4
app.UseAuthentication();

// 5
app.UseAuthorization();

// 6
app.UseRateLimiting();

// 7
app.MapControllers();
```

#### Chunking Strategy:
- **One chunk for entire pipeline**
- Preserve order (critical!)
- Extract middleware types

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **USES_MIDDLEWARE** | Pipeline | HttpsRedirection | `order: 1` |
| **USES_MIDDLEWARE** | Pipeline | StaticFiles | `order: 2` |
| **USES_MIDDLEWARE** | Pipeline | Authentication | `order: 4` |
| **USES_MIDDLEWARE** | Pipeline | Authorization | `order: 5` |
| **PRECEDES** | Authentication | Authorization | - |

#### Metadata:
```json
{
  "chunk_type": "middleware_pipeline",
  "middleware_stack": [
    "HttpsRedirection",
    "StaticFiles",
    "Cors",
    "Authentication",
    "Authorization",
    "RateLimiting"
  ],
  "framework": "aspnet-core",
  "layer": "Infra"
}
```

---

### 7. **Configuration Binding**

#### What to Chunk:
```csharp
// appsettings.json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "noreply@example.com",
    "ApiKey": "***"
  }
}

// Configuration class
public class EmailSettings
{
    public string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public string FromEmail { get; set; }
    public string ApiKey { get; set; }
}

// Registration
services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

// Usage
public class EmailService
{
    private readonly EmailSettings _settings;
    
    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }
}
```

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **BINDS_CONFIG** | Program | EmailSettings | `section: EmailSettings` |
| **READS_CONFIG** | EmailService | EmailSettings | `via: IOptions` |

#### Metadata:
```json
{
  "chunk_type": "configuration_binding",
  "config_section": "EmailSettings",
  "config_class": "EmailSettings",
  "config_keys": ["SmtpServer", "SmtpPort", "FromEmail", "ApiKey"],
  "binding_method": "IOptions",
  "layer": "Infra"
}
```

---

### 8. **Background Jobs / Hosted Services**

#### What to Chunk:
```csharp
public class EmailProcessorJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingEmails();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}

// Hangfire job
[AutomaticRetry(Attempts = 3)]
public class ReportGenerator
{
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task GenerateDailyReport()
    {
        // ...
    }
}

// Registration
RecurringJob.AddOrUpdate<ReportGenerator>(
    "daily-report",
    x => x.GenerateDailyReport(),
    Cron.Daily);
```

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **SCHEDULES** | RecurringJob | ReportGenerator.GenerateDailyReport | `schedule: Daily` |
| **BACKGROUND_TASK** | EmailProcessorJob | ProcessPendingEmails | `interval: 5min` |

#### Metadata:
```json
{
  "chunk_type": "background_job",
  "job_name": "daily-report",
  "job_class": "ReportGenerator",
  "schedule": "Cron.Daily",
  "retry_attempts": 3,
  "concurrent_execution": false,
  "timeout_seconds": 600,
  "framework": "hangfire",
  "layer": "Infra"
}
```

---

### 9. **MediatR Handlers**

#### What to Chunk:
```csharp
// Command
public class CreateUserCommand : IRequest<UserDto>
{
    public string Email { get; set; }
    public string Password { get; set; }
}

// Handler
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _repository;
    
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User { Email = request.Email };
        await _repository.AddAsync(user);
        return new UserDto { Id = user.Id, Email = user.Email };
    }
}

// Query
public class GetUserQuery : IRequest<UserDto>
{
    public int Id { get; set; }
}

// Query Handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    // ...
}
```

#### Relationships Created:
| Relationship | From | To | Metadata |
|--------------|------|-----|----------|
| **HANDLES** | CreateUserCommandHandler | CreateUserCommand | `message_type: Command` |
| **RETURNSTYPE** | CreateUserCommandHandler | UserDto | - |
| **CALLS** | CreateUserCommandHandler | IUserRepository.AddAsync | - |

#### Metadata:
```json
{
  "chunk_type": "mediatr_handler",
  "handler": "CreateUserCommandHandler",
  "message": "CreateUserCommand",
  "message_type": "Command",
  "return_type": "UserDto",
  "framework": "mediatr",
  "layer": "Domain",
  "pattern": "CQRS"
}
```

---

## 🏷️ Aggressive Metadata Tagging

### Core Metadata (Every Chunk):
```json
{
  // Identity
  "chunk_type": "action_method | di_registration | ef_query | ...",
  "symbol_name": "UsersController.GetUser",
  "file_path": "/Controllers/UsersController.cs",
  "line_number": 45,
  
  // Classification
  "language": "csharp",
  "framework": "aspnet-core | ef-core | hangfire | mediatr",
  "layer": "API | Domain | Infra | Data | UI | Test",
  "bounded_context": "UserManagement | OrderProcessing | ...",
  "service_name": "UserService | OrderService | ...",
  
  // Relationships Summary
  "relationships_summary": {
    "CALLS": 3,
    "ACCESSES": 1,
    "INJECTS": 2,
    "AUTHORIZES": 1
  },
  
  // Semantic Tags
  "tags": ["authentication", "crud", "async", "validated"],
  "complexity": "low | medium | high",
  "is_async": true,
  "is_tested": true  // If test exists
}
```

### Layer Classification:
- **API**: Controllers, Endpoints, Filters
- **Domain**: Entities, ValueObjects, DomainServices, Validators
- **Infra**: Repositories, ExternalServices, BackgroundJobs
- **Data**: DbContext, Migrations, QueryExtensions
- **UI**: Views, Components, ViewModels
- **Test**: UnitTests, IntegrationTests

### Bounded Context:
Extracted from:
- Namespace (`MyApp.UserManagement.Controllers`)
- Folder structure
- Explicit configuration

---

## 📊 Qdrant Changes: NONE Required! ✅

**Why?** Qdrant stores:
1. **Vectors** (embeddings) - No schema
2. **Payload** (metadata JSON) - Schemaless

**What changes:**
- ✅ More chunks created (better granularity)
- ✅ Richer metadata in payloads (better filtering)
- ✅ More diverse embeddings (better semantic search)
- ✅ Same vector dimension (1024 from mxbai-embed-large)

**No schema migration needed!** Just reindex and you're done.

---

## 🔍 Powerful Queries Enabled

### 1. Find All Endpoints Using a Service
```cypher
MATCH (endpoint)-[:EXPOSES]->()-[:CALLS*]->()-[:INJECTS|CALLS]->(service)
WHERE service.name = 'IUserService'
RETURN endpoint.route, endpoint.http_method
```

### 2. Security Audit: Unvalidated Endpoints
```cypher
MATCH (action)
WHERE action.chunk_type = 'action_method'
  AND NOT action.validates_model = true
RETURN action.route, action.http_method
```

### 3. Find All Background Jobs
```cypher
MATCH (job)
WHERE job.chunk_type = 'background_job'
RETURN job.job_name, job.schedule, job.framework
```

### 4. Find Scoped Services Used in Singletons (DI Bug!)
```cypher
MATCH (singleton)-[r1:REGISTERS]->()
WHERE r1.lifetime = 'Singleton'
MATCH (singleton)-[:INJECTS]->(dep)<-[r2:REGISTERS]-()
WHERE r2.lifetime = 'Scoped'
RETURN singleton.name, dep.name
```
**This is a CRITICAL BUG DETECTOR!**

### 5. Find Complex EF Queries (Performance Review)
```cypher
MATCH (query)
WHERE query.chunk_type = 'ef_query'
  AND query.query_complexity = 'high'
RETURN query.symbol_name, query.query_operations
```

### 6. API Documentation Generation
```cypher
MATCH (endpoint)-[:EXPOSES]->(action)
OPTIONAL MATCH (action)-[:AUTHORIZES]->(role)
OPTIONAL MATCH (action)-[:RETURNSTYPE]->(dto)
RETURN 
  endpoint.route,
  endpoint.http_method,
  collect(DISTINCT role.name) as required_roles,
  dto.name as response_type
ORDER BY endpoint.route
```

### 7. Find Handlers for a Command (MediatR)
```cypher
MATCH (handler)-[:HANDLES]->(command)
WHERE command.name = 'CreateUserCommand'
RETURN handler.name, handler.layer
```

---

## 🚀 Implementation Priority

### Phase 1: High Impact (2-3 hours)
1. ✅ **Action Method Chunks** - Enables EXPOSES, AUTHORIZES
2. ✅ **DI Registration** - Enables REGISTERS, tracks lifetimes
3. ✅ **Validation Logic** - Enables VALIDATES

### Phase 2: Medium Impact (2-3 hours)
4. ✅ **EF Query Chunks** - Enables QUERIES, PROJECTS
5. ✅ **Authorization Config** - Enables policy tracking
6. ✅ **Configuration Binding** - Enables READS_CONFIG

### Phase 3: Advanced (3-4 hours)
7. ✅ **Middleware Pipeline** - Enables USES_MIDDLEWARE
8. ✅ **Background Jobs** - Enables SCHEDULES
9. ✅ **MediatR Handlers** - Enables HANDLES

---

## 📝 Next Steps

Want me to implement:
1. **Just Phase 1** (Action Methods + DI + Validation) - 2-3 hours
2. **All 9 Patterns** - Full semantic chunking - 6-8 hours
3. **Custom Selection** - Pick specific patterns

This will transform the code memory into a **framework-aware semantic code knowledge graph**! 🔥


