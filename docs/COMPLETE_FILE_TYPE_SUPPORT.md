# Complete File Type Support - Memory Agent

## 🎯 Comprehensive Language & File Type Support

Memory Agent now provides **enterprise-grade indexing** for all major programming languages, configuration files, and infrastructure-as-code files.

---

## 📋 Supported File Types (Complete List)

### 🔷 .NET / C# Ecosystem
| File Type | Extension | Parser | Semantic Analysis | Key Features |
|-----------|-----------|--------|-------------------|--------------|
| **C#** | `.cs` | RoslynParser | ✅ Full | Classes, methods, properties, LINQ, DI, EF queries, API endpoints, validation |
| **VB.NET** | `.vb` | VBNetParser | ✅ Basic | Classes, modules, functions, subs, properties, inheritance |
| **Razor** | `.cshtml`, `.razor` | RazorParser | ✅ Advanced | @directives, HTML elements, forms, tables, components, styles |
| **Project Files** | `.csproj`, `.vbproj`, `.fsproj` | ProjectFileParser | ✅ Full | NuGet packages, project references, target frameworks, properties |
| **Solution Files** | `.sln` | ProjectFileParser | ✅ Full | Projects, solution folders, project relationships |
| **App Settings** | `appsettings*.json` | ConfigFileParser | ✅ Basic | Connection strings, logging config, app configuration |
| **Web Config** | `web.config` | ConfigFileParser | ✅ Basic | IIS configuration, app settings |

### 🔶 JavaScript / TypeScript Ecosystem
| File Type | Extension | Parser | Semantic Analysis | Key Features |
|-----------|-----------|--------|-------------------|--------------|
| **JavaScript** | `.js`, `.jsx` | JavaScriptParser | ✅ Basic | Classes, functions, imports, exports, React components |
| **TypeScript** | `.ts`, `.tsx` | JavaScriptParser | ✅ Basic | All JS features + interfaces, types, generics |
| **Package Config** | `package.json` | ConfigFileParser | ✅ Full | NPM dependencies, devDependencies, scripts, project metadata |
| **Package Lock** | `package-lock.json` | ConfigFileParser | ✅ Basic | Lock file tracking (optimized, doesn't index full tree) |
| **TS Config** | `tsconfig.json` | ConfigFileParser | ✅ Basic | TypeScript compiler configuration |

### 🐍 Python Ecosystem
| File Type | Extension | Parser | Semantic Analysis | Key Features |
|-----------|-----------|--------|-------------------|--------------|
| **Python** | `.py` | PythonParser | ⚠️ Basic | Classes, functions, decorators, imports |

> **Note**: Python parsing is regex-based. Consider upgrading to AST-based parsing for better accuracy and framework detection (Django, Flask, FastAPI).

### 🎨 Styling & Markup
| File Type | Extension | Parser | Semantic Analysis | Key Features |
|-----------|-----------|--------|-------------------|--------------|
| **CSS** | `.css` | CssParser | ✅ Full | Rules, selectors, custom properties, media queries, animations |
| **SCSS** | `.scss` | CssParser | ✅ Full | All CSS features + variables, mixins, nesting |
| **LESS** | `.less` | CssParser | ✅ Full | All CSS features + variables, mixins |
| **Markdown** | `.md`, `.markdown` | MarkdownParser | ✅ Basic | Headings, code blocks, lists, tables, links |

### 🐳 Infrastructure & DevOps
| File Type | Extension | Parser | Semantic Analysis | Key Features |
|-----------|-----------|--------|-------------------|--------------|
| **Dockerfile** | `Dockerfile`, `*.dockerfile` | DockerfileParser | ✅ Full | Base images, stages, exposed ports, env vars, entrypoint, workdir |
| **Docker Compose** | `docker-compose.yml/yaml` | DockerfileParser | ✅ Basic | Services, images, dependencies |

---

## 🔗 Relationship Tracking

The system automatically detects and creates relationships in **Neo4j**:

### Project Dependencies
- **NuGet Packages**: `.csproj` → Package (DependsOn)
- **NPM Packages**: `package.json` → Package (DependsOn)
- **Project References**: Project A → Project B (References)
- **Solution Structure**: Solution → Projects (Contains)

### Code Relationships
- **Inheritance**: Class → BaseClass (Inherits)
- **Interface Implementation**: Class → Interface (Implements)
- **Method Calls**: Method → OtherMethod (Calls)
- **Import/Using**: File → Module/Namespace (Imports)
- **Dependency Injection**: Class → Service (Injects)

### Infrastructure Dependencies
- **Docker Images**: Dockerfile → BaseImage (DependsOn)
- **Docker Services**: Service → Image (DependsOn)

---

## 📊 Indexing Performance Optimizations

### User's Performance Improvements
✅ **Increased concurrency**: 5 → 8 parallel file indexes  
✅ **Parallel deletion**: Old files removed in parallel during reindex  
✅ **Parallel new file indexing**: New files indexed in parallel  
✅ **Parallel modified file reindexing**: Changed files reindexed in parallel  
✅ **Thread-safe counters**: Using `Interlocked.Increment` for concurrent updates  
✅ **Locked error collection**: Thread-safe error aggregation

### File Exclusions
Automatically excluded from indexing:
- `**/bin/**`, `**/obj/**` - Build artifacts
- `**/node_modules/**` - NPM dependencies
- `.cshtml.cs`, `.razor.cs` files when matched by view patterns (picked up by `*.cs` instead)

---

## 🔍 Smart Chunking Strategies

### By Language
- **C#**: Class-level, method-level, LINQ queries, DI patterns, EF queries, validation rules, API endpoints
- **Razor**: @directives, HTML semantic elements, forms, tables, sections, components
- **CSS**: Rules, variables, media queries, animations, mixins
- **JavaScript/TypeScript**: Classes, functions, React components, interfaces, types
- **VB.NET**: Classes, modules, methods, properties
- **Python**: Classes, functions, decorators
- **Project Files**: Project metadata, package dependencies, project references
- **Dockerfiles**: Stages, base images, exposed ports, environment variables

---

## 📈 Example Indexing Statistics

```
Found 1,247 code files to index:
  - 476 .cs files
  - 8 .vb files  
  - 176 .cshtml/.razor files
  - 13 .py files
  - 223 .md files
  - 38 .css/.scss/.less files
  - 92 .js/.ts/.jsx/.tsx files
  - 45 .csproj/.sln files
  - 128 config files (JSON, YAML)
  - 48 Docker files

Total indexed: 1,247 files
  - Classes: 2,341
  - Methods: 8,923
  - Patterns: 4,156
  - Relationships: 12,487
  - Dependencies: 847 packages
```

---

## 🚀 Qu

eries You Can Now Run

### Project Structure Queries
```cypher
// Find all projects and their NuGet dependencies
MATCH (p:Project)-[r:DEPENDS_ON]->(pkg)
WHERE r.dependency_type = 'nuget'
RETURN p.name, pkg.name, r.version

// Find solution structure
MATCH (s:Solution)-[:CONTAINS]->(p:Project)
RETURN s.name, collect(p.name)

// Find project-to-project references
MATCH (p1:Project)-[:REFERENCES]->(p2:Project)
RETURN p1.name, p2.name
```

### Infrastructure Queries
```cypher
// Find all Docker services and their base images
MATCH (df:Dockerfile)-[:DEPENDS_ON]->(img)
WHERE img.dependency_type = 'docker_image'
RETURN df.name, img.name

// Find all exposed ports across Dockerfiles
MATCH (df:Dockerfile)
WHERE df.exposed_ports IS NOT NULL
RETURN df.name, df.exposed_ports
```

### Dependency Queries
```cypher
// Find all NPM packages used
MATCH (pkg)-[:DEPENDS_ON]->(npm)
WHERE npm.dependency_type = 'npm'
RETURN pkg.name, npm.name, npm.version

// Find most-used NuGet packages
MATCH (p)-[:DEPENDS_ON]->(pkg)
WHERE pkg.dependency_type = 'nuget'
RETURN pkg.name, count(p) as usage_count
ORDER BY usage_count DESC
```

---

## 🎯 Smart Search Examples

### Find Configuration
```powershell
# Find all connection strings
$body = @{query='connection string database config';context='MyProject'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/smartsearch -Method POST -Body $body -ContentType 'application/json'
```

### Find Dependencies
```powershell
# Find Entity Framework usage
$body = @{query='Entity Framework DbContext packages';context='MyProject'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/smartsearch -Method POST -Body $body -ContentType 'application/json'
```

### Find Docker Configuration
```powershell
# Find all Docker services
$body = @{query='Docker services containers';context='MyProject'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/smartsearch -Method POST -Body $body -ContentType 'application/json'
```

---

## 🔮 Future Enhancements

### Additional Languages (Priority)
- [ ] **Go** (`.go`) - Growing in popularity
- [ ] **Rust** (`.rs`) - Systems programming
- [ ] **Java** (`.java`) - Enterprise applications
- [ ] **PHP** (`.php`) - Web development
- [ ] **Ruby** (`.rb`) - Rails applications

### Additional Config Files
- [ ] **Terraform** (`.tf`) - Infrastructure as Code
- [ ] **Kubernetes** (`.yaml` manifests) - Container orchestration
- [ ] **GitHub Actions** (`.github/workflows/*.yml`) - CI/CD
- [ ] **GitLab CI** (`.gitlab-ci.yml`) - CI/CD
- [ ] **Ansible** (`.yml` playbooks) - Configuration management

### Python Enhancements
- [ ] AST-based parsing (vs current regex)
- [ ] Django model detection
- [ ] Flask route detection
- [ ] FastAPI endpoint detection
- [ ] Type hint analysis
- [ ] requirements.txt / pyproject.toml parsing

---

## 📝 Summary

**Total Supported File Types**: 25+  
**Languages**: 7 (C#, VB.NET, JavaScript, TypeScript, Python, CSS/SCSS/LESS, Markdown)  
**Project/Config Files**: 12 types  
**Infrastructure Files**: 2 types  
**Relationship Types**: 8 (Inherits, Implements, References, DependsOn, Imports, Calls, Injects, Contains)

**Performance**: 8 concurrent file indexes, parallel reindexing, optimized for large codebases

---

Last Updated: November 22, 2025  
Version: 2.0 - Enterprise Edition

