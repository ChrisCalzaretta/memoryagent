# MemoryAgent Architecture

## Full System Communication & Context Flow

```mermaid
graph TB
    User[User/Cursor IDE]
    
    subgraph Router Layer :5010
        MCP[MCP Protocol Handler]
        Router[MemoryRouter Core]
        FG[FunctionGemma Client]
        HI[Hybrid Intelligence]
        BJ[Background Jobs]
    end
    
    subgraph Memory Layer :5000
        MA[MemoryAgent API]
        Lightning[Agent Lightning]
        Session[Session Manager]
        Search[Search Engine]
    end
    
    subgraph Orchestration Layer :5003
        CO[CodingOrchestrator]
        CA[CodingAgent :5001]
        VA[ValidationAgent :5002]
        DA[DesignAgent :5004]
    end
    
    subgraph Data Layer
        Ollama[Ollama :11434<br/>LLM Models]
        Qdrant[Qdrant :6333<br/>Vector Store]
        Neo4j[Neo4j :7474/7687<br/>Graph DB]
    end
    
    User -->|1KB - 50KB<br/>User request + files| MCP
    MCP -->|2KB - 100KB<br/>Request + context| Router
    
    Router -->|5KB - 200KB<br/>44 tools + user request| FG
    FG -->|10KB - 500KB<br/>System prompt + tools| Ollama
    Ollama -->|500B - 2KB<br/>Tool selection JSON| FG
    FG -->|1KB<br/>Selected tool| Router
    
    Router -->|1KB - 5KB<br/>Complexity estimate request| HI
    HI -->|5KB - 100KB<br/>Code + analysis prompt| Ollama
    Ollama -->|200B - 1KB<br/>Duration estimate| HI
    HI -->|500B<br/>Sync/Async decision| Router
    
    Router -->|2KB - 100KB<br/>Tool call + context + sessionId| MA
    MA -->|1KB - 10KB<br/>Session lookup| Session
    Session -->|500B - 5KB<br/>Recent history| MA
    
    MA -->|1KB - 50KB<br/>Query + embeddings| Search
    Search -->|2KB - 100KB<br/>Vector search| Qdrant
    Qdrant -->|5KB - 500KB<br/>Top 50 chunks| Search
    
    Search -->|1KB - 50KB<br/>Graph traversal| Neo4j
    Neo4j -->|2KB - 100KB<br/>Relationships| Search
    
    Search -->|10KB - 1MB<br/>Combined results| MA
    MA -->|500B - 2KB<br/>Tool usage metrics| Lightning
    Lightning -->|1KB - 10KB<br/>Store embeddings| Qdrant
    
    MA -->|10KB - 1MB<br/>Search results| Router
    Router -->|10KB - 1MB<br/>Results to user| User
    
    Router -->|5KB - 200KB<br/>Orchestrate request| CO
    CO -->|10KB - 500KB<br/>Search existing patterns| MA
    MA -->|50KB - 2MB<br/>Pattern matches| CO
    
    CO -->|20KB - 500KB<br/>Generate code + patterns| CA
    CA -->|50KB - 1MB<br/>Code generation prompt| Ollama
    Ollama -->|10KB - 500KB<br/>Generated code| CA
    CA -->|10KB - 500KB<br/>Generated code| CO
    
    CO -->|10KB - 500KB<br/>Validate code| VA
    VA -->|30KB - 800KB<br/>Code + validation rules| Ollama
    Ollama -->|2KB - 10KB<br/>Validation score + feedback| VA
    VA -->|2KB - 10KB<br/>Score + issues| CO
    
    CO -->|5KB - 100KB<br/>Design validation| DA
    DA -->|10KB - 200KB<br/>Design analysis| Ollama
    Ollama -->|2KB - 20KB<br/>Design compliance| DA
    DA -->|2KB - 20KB<br/>Design result| CO
    
    CO -->|20KB - 2MB<br/>Final artifacts| Router
    Router -->|20KB - 2MB<br/>Complete response| User
    
    BJ -.5KB - 50KB<br/>Background status polls.-> Router
    
    style User fill:#e1f5ff
    style Router fill:#ffe1e1
    style MA fill:#e1ffe1
    style CO fill:#fff4e1
    style Ollama fill:#f0e1ff
    style Qdrant fill:#fff9e1
    style Neo4j fill:#e1fff9
    
    classDef lightData stroke:#90EE90,stroke-width:2px
    classDef medData stroke:#FFD700,stroke-width:2px
    classDef heavyData stroke:#FF6B6B,stroke-width:3px
```

## Complete System Architecture

```mermaid
graph TB
    User[User in Cursor]
    MCP[MCP Protocol]
    
    Router[MemoryRouter :5010]
    FunctionGemma[FunctionGemma AI]
    HybridIntel[Hybrid Intelligence]
    PerfTracker[Performance Tracker]
    JobManager[Background Jobs]
    
    MemoryAgent[MemoryAgent :5000]
    Session[Session Manager]
    Lightning[Agent Lightning Learning]
    
    CodingOrch[CodingOrchestrator :5003]
    CodingAgent[CodingAgent :5001]
    ValidationAgent[ValidationAgent :5002]
    DesignAgent[DesignAgent :5004]
    
    Ollama[Ollama :11434]
    Qdrant[Qdrant :6333 Vector DB]
    Neo4j[Neo4j :7474/7687 Graph DB]
    
    User --> MCP
    MCP --> Router
    
    Router <--> FunctionGemma
    Router <--> HybridIntel
    Router --> PerfTracker
    Router --> JobManager
    
    Router --> MemoryAgent
    Router --> CodingOrch
    
    MemoryAgent <--> Session
    MemoryAgent <--> Lightning
    MemoryAgent --> Qdrant
    MemoryAgent --> Neo4j
    MemoryAgent --> Ollama
    
    CodingOrch --> CodingAgent
    CodingOrch --> ValidationAgent
    CodingOrch --> DesignAgent
    
    CodingAgent --> Ollama
    ValidationAgent --> Ollama
    DesignAgent --> Ollama
    FunctionGemma --> Ollama
    HybridIntel --> Ollama
    
    Lightning --> Qdrant
    PerfTracker --> Lightning
    
    style User fill:#e1f5ff
    style Router fill:#ffe1e1
    style MemoryAgent fill:#e1ffe1
    style CodingOrch fill:#fff4e1
    style Ollama fill:#f0e1ff
    style Lightning fill:#fff9e1
    style HybridIntel fill:#fff9e1
```

## Intelligence Stack

```mermaid
graph TB
    subgraph MemoryRouter Intelligence
        FG[FunctionGemma AI<br/>Tool Selection]
        HI[Hybrid Intelligence<br/>Execution Decisions]
        PT[Performance Tracker<br/>Statistical Learning]
        BJ[Background Job Manager<br/>Async Execution]
        
        FG --> HI
        HI --> PT
        HI --> BJ
        PT -.learns from.-> HI
    end
    
    subgraph MemoryAgent Intelligence
        AL[Agent Lightning<br/>Tool Usage Tracking]
        SM[Session Manager<br/>Context Memory]
        PS[Pattern Search<br/>Code Intelligence]
        
        AL --> SM
        AL --> PS
        SM -.context.-> AL
    end
    
    subgraph AI Models
        Ollama[Ollama LLMs]
        FGModel[FunctionGemma<br/>Tool Selection]
        DSModel[DeepSeek<br/>Complexity Analysis]
        CodeModel[Code Models<br/>Generation/Validation]
        
        Ollama --> FGModel
        Ollama --> DSModel
        Ollama --> CodeModel
    end
    
    FG --> FGModel
    HI --> DSModel
    AL --> Ollama
    
    style AL fill:#fff9e1
    style HI fill:#fff9e1
    style PT fill:#fff9e1
```

## Request Flow with All Intelligence

```mermaid
sequenceDiagram
    autonumber
    participant User
    participant Router
    participant FunctionGemma
    participant HybridIntel
    participant Memory
    participant Lightning
    participant Session
    
    User->>Router: Find authentication code
    
    Note over Router,FunctionGemma: Layer 1: AI Tool Selection
    Router->>FunctionGemma: Request + 44 tools + context
    FunctionGemma->>Router: Tool: semantic_search + params
    
    Note over Router,HybridIntel: Layer 2: Execution Decision
    Router->>HybridIntel: Analyze complexity
    HybridIntel->>HybridIntel: AI + Statistical analysis
    HybridIntel->>Router: Decision: SYNC (fast task)
    
    Note over Router,Memory: Layer 3: Execute
    Router->>Memory: semantic_search(query, context, sessionId)
    
    Memory->>Session: Get session context
    Session-->>Memory: Recent files + history
    
    Memory->>Memory: Execute search with context
    Memory->>Router: Results (15 files)
    
    Note over Router,Lightning: Layer 4: Learn
    Router->>Lightning: Track tool usage + performance
    Lightning->>Lightning: Store: tool, duration, success, context
    
    Router->>Session: Update session history
    Router->>User: Found 15 files
```

## Agent Lightning Learning Flow

```mermaid
sequenceDiagram
    participant Tool as Tool Call
    participant McpService
    participant Lightning as Agent Lightning
    participant Qdrant as Vector Store
    participant Analytics as Analytics Engine
    
    Tool->>McpService: Execute tool
    McpService->>McpService: Track start time
    
    Note over McpService: Tool executes...
    
    McpService->>Lightning: Record invocation<br/>tool, args, duration, result
    
    Lightning->>Lightning: Extract patterns<br/>- Tool usage frequency<br/>- Success rate<br/>- Common parameters<br/>- User patterns
    
    Lightning->>Qdrant: Store embeddings<br/>for pattern search
    
    Lightning->>Analytics: Update statistics<br/>- Popular tools<br/>- Tool sequences<br/>- Performance metrics
    
    Analytics-->>Lightning: Insights generated
    
    Note over Lightning: Learning complete<br/>Patterns stored for<br/>future recommendations
```

## Hybrid Intelligence Decision Making

```mermaid
graph TB
    ToolSelected[Tool Selected by FunctionGemma]
    
    HybridIntel[Hybrid Intelligence Analyzer]
    
    StatEngine[Statistical Engine]
    HistData[Historical Performance Data]
    PatternMatch[Pattern Matching]
    
    AIEngine[AI Complexity Analyzer]
    DeepSeek[DeepSeek Model]
    CodeAnalysis[Code Analysis]
    
    Decision{Estimated Duration?}
    
    Sync[Execute Synchronously<br/>Wait for result]
    Async[Execute Async<br/>Return job ID immediately]
    
    PerfTracker[Performance Tracker]
    Learn[Update Learning Model]
    
    ToolSelected --> HybridIntel
    
    HybridIntel --> StatEngine
    HybridIntel --> AIEngine
    
    StatEngine --> HistData
    StatEngine --> PatternMatch
    
    AIEngine --> DeepSeek
    AIEngine --> CodeAnalysis
    
    StatEngine --> Decision
    AIEngine --> Decision
    
    Decision -->|Under 15s| Sync
    Decision -->|Over 15s| Async
    
    Sync --> PerfTracker
    Async --> PerfTracker
    
    PerfTracker --> Learn
    Learn -.feedback.-> StatEngine
    
    style HybridIntel fill:#fff9e1
    style StatEngine fill:#e1ffe1
    style AIEngine fill:#ffe1e1
    style Learn fill:#fff9e1
```

## Multi-Agent Orchestration

```mermaid
sequenceDiagram
    participant Router
    participant Orch as CodingOrchestrator
    participant Coding as CodingAgent
    participant Valid as ValidationAgent
    participant Design as DesignAgent
    participant Memory as MemoryAgent
    
    Router->>Orch: orchestrate_task(Create REST API)
    
    Note over Orch: Search Before Write
    Orch->>Memory: Search existing patterns
    Memory-->>Orch: Found 3 similar implementations
    
    loop Until Valid (max 100 iterations)
        Note over Orch,Coding: Generate
        Orch->>Coding: Generate code + context
        Coding->>Coding: Use existing patterns
        Coding-->>Orch: Generated code
        
        Note over Orch,Valid: Validate
        Orch->>Valid: Validate code
        Valid->>Valid: Check quality, security, patterns
        Valid-->>Orch: Score: 8.5/10 + feedback
        
        alt Score >= 8
            Note over Orch: Success!
        else Score < 8
            Note over Orch: Iterate with feedback
        end
    end
    
    Note over Orch,Design: Optional: Design Validation
    Orch->>Design: Validate against brand
    Design-->>Orch: Design compliance check
    
    Orch->>Memory: Record generated code in session
    Orch->>Router: Complete with all artifacts
```

## Background Job Architecture

```mermaid
graph TB
    Request[User Request]
    
    Router[MemoryRouter]
    HybridIntel[Hybrid Intelligence]
    
    Decision{Duration Estimate}
    
    SyncPath[Synchronous Path]
    AsyncPath[Asynchronous Path]
    
    JobManager[Background Job Manager]
    JobQueue[Job Queue]
    JobWorker[Job Worker Pool]
    
    SyncExec[Execute Tool<br/>Wait for result]
    AsyncExec[Start Background Job<br/>Return job ID]
    
    ResultStore[Result Storage]
    
    User[User Gets Response]
    
    Request --> Router
    Router --> HybridIntel
    HybridIntel --> Decision
    
    Decision -->|Under 15s| SyncPath
    Decision -->|Over 15s| AsyncPath
    
    SyncPath --> SyncExec
    SyncExec --> User
    
    AsyncPath --> JobManager
    JobManager --> JobQueue
    JobQueue --> JobWorker
    JobWorker --> AsyncExec
    AsyncExec --> ResultStore
    
    JobManager -.immediate response.-> User
    ResultStore -.poll status.-> User
    
    style HybridIntel fill:#fff9e1
    style JobManager fill:#e1ffe1
    style AsyncPath fill:#ffe1e1
```

## Tool Distribution

### MemoryAgent (33+ tools)
- **Search**: semantic_search, smart_search, graph_search
- **Analysis**: explain_code, impact_analysis, complexity_analysis
- **Validation**: validate_pattern, search_patterns
- **Learning**: start_session, record_context, get_insights, get_tool_usage
- **Planning**: create_plan, estimate_complexity
- **Transform**: transform_page, transform_css
- **Intelligence**: get_recommendations, get_popular_tools

### CodingOrchestrator (11+ tools)
- **Generation**: orchestrate_task, get_task_status, cancel_task
- **Design**: design_create_brand, design_validate, design_get_brand
- **Management**: list_tasks, get_generated_files

### MemoryRouter (2 MCP tools)
- **execute_task**: Smart routing with FunctionGemma
- **list_available_tools**: Discover all tools

## Intelligence Features

### Agent Lightning
- Tool usage tracking
- Performance metrics
- Pattern detection
- User behavior learning
- Tool recommendations
- Success rate analysis

### Hybrid Intelligence
- Statistical learning from history
- AI complexity estimation
- Automatic sync/async decisions
- Performance prediction
- Continuous improvement

### Session Intelligence
- Context memory across requests
- File discussion tracking
- Query history
- Smart follow-up handling
- Conversation continuity

### Search Intelligence
- Semantic vector search
- Graph relationship traversal
- Pattern matching
- Context-aware ranking
- Multi-strategy optimization
