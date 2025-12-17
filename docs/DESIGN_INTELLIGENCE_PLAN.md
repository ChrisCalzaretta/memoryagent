# 🎨 Design Intelligence System - Implementation Plan

> **Vision**: A fully autonomous, self-improving design learning system that continuously discovers, analyzes, and learns from the best web designs on the internet.

---

## 📋 Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Core Components](#core-components)
4. [LLM Integration](#llm-integration)
5. [Learning Systems](#learning-systems)
6. [Design Detection](#design-detection)
7. [Data Models](#data-models)
8. [Neo4j Schema](#neo4j-schema)
9. [Prompts Inventory](#prompts-inventory)
10. [Configuration](#configuration)
11. [Implementation Phases](#implementation-phases)
12. [API/MCP Tools](#apimcp-tools)

---

## Executive Summary

### What We're Building

An **autonomous design intelligence system** that:

- 🔍 **Discovers** design sources dynamically via search engines (Google, Bing)
- 🕷️ **Crawls** websites intelligently, using LLM to select the most valuable pages
- 🧠 **Analyzes** designs using LLaVA vision model with page-type-specific prompts
- 📊 **Learns** from patterns, user feedback, and temporal trends
- 🏆 **Maintains** a "Top 100" leaderboard that continuously improves
- 🎨 **Generates** A2UI components (Google's Agent-to-User Interface format)
- 🐢 **Runs** as a low-resource background service (<30% CPU)

### Key Principles

| Principle | Implementation |
|-----------|----------------|
| **100% Autonomous** | No human in the loop unless they request to see designs |
| **LLM-Driven Everything** | All decisions made by LLM, not hardcoded rules |
| **Prompts in Lightning** | All prompts seeded in Neo4j, evolvable over time |
| **Self-Improving** | Learns from feedback, evolves prompts, improves scoring |
| **Resource-Conscious** | Background service that doesn't interfere with other work |
| **Quality over Quantity** | Only keeps top designs, discards the rest |
| **A2UI Output** | Generates Google A2UI format for universal rendering |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    DESIGN INTELLIGENCE SYSTEM                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐ │
│   │  DISCOVERY  │ →  │   CAPTURE   │ →  │   ANALYZE   │ →  │    STORE    │ │
│   │    Agent    │    │    Agent    │    │    Agent    │    │   Service   │ │
│   │             │    │             │    │             │    │             │ │
│   │ • Search    │    │ • Playwright│    │ • LLaVA     │    │ • Neo4j     │ │
│   │ • Evaluate  │    │ • Multi-page│    │ • Page-type │    │ • Qdrant    │ │
│   │ • Rank      │    │ • DOM parse │    │   prompts   │    │ • A2UI      │ │
│   └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘ │
│         │                  │                  │                  │          │
│         └──────────────────┴──────────────────┴──────────────────┘          │
│                                    │                                         │
│                                    ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                         LEARNING ENGINE                              │   │
│   │                                                                      │   │
│   │   • User Preferences    • Pattern Co-occurrence   • Trend Detection │   │
│   │   • Source Quality      • Score Calibration       • Prompt Evolution│   │
│   │   • Pattern → A2UI      • Component Library       • Design Tokens   │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│                                    ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                         TOP 100 LEADERBOARD                          │   │
│   │                     + A2UI PATTERN LIBRARY                           │   │
│   │                                                                      │   │
│   │   #1  linear.app        9.8    │    Floor rises over time:          │   │
│   │   #2  vercel.com        9.7    │    Day 1:   7.0                     │   │
│   │   #3  stripe.com        9.6    │    Month 1: 7.8                     │   │
│   │   ...                          │    Month 3: 8.5                     │   │
│   │   #100 current-floor    7.x    │    Year 1:  9.0+                    │   │
│   │                                                                      │   │
│   │   A2UI Templates: 250+         │    Stored as reusable patterns      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│                                    ▼                                         │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                       A2UI GENERATOR                                 │   │
│   │                                                                      │   │
│   │   User: "Generate pricing page for FitTrack"                         │   │
│   │          ↓                                                           │   │
│   │   Brand + Patterns → A2UI JSON                                       │   │
│   │          ↓                                                           │   │
│   │   { "designTokens": {...}, "a2ui": {...} }                           │   │
│   │          ↓                                                           │   │
│   │   Claude/Cursor renders or converts to code                          │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. Discovery Agent

**Purpose**: Find new design sources and sites to analyze

**Capabilities**:
- Generate search queries using LLM
- Search Google/Bing for design inspiration sites
- Evaluate if a discovered site is a design aggregator
- Track source quality (yield rates)
- Evolve queries based on success rates

**Key Files**:
- `DesignAgent.Server/Services/DiscoveryAgent.cs`
- `DesignAgent.Server/Services/SearchService.cs`

### 2. Capture Agent

**Purpose**: Screenshot and extract data from websites

**Capabilities**:
- Take screenshots at multiple breakpoints (mobile, tablet, desktop)
- Extract DOM elements (colors, fonts, spacing)
- Parse internal links for LLM evaluation
- Extract CSS variables and computed styles
- Capture short videos for animation detection (future)

**Key Files**:
- `DesignAgent.Server/Services/CaptureService.cs`
- `DesignAgent.Server/Services/DomExtractorService.cs`

### 3. Analysis Agent

**Purpose**: Use LLM to understand and score designs

**Capabilities**:
- Link evaluation (which pages to crawl)
- Page-type-specific analysis (homepage, pricing, features, etc.)
- Site synthesis (combine page analyses into DNA)
- Component extraction
- Color/typography detection

**Key Files**:
- `DesignAgent.Server/Services/LlmAnalysisService.cs`
- `DesignAgent.Server/Services/PageAnalyzerService.cs`

### 4. Leaderboard Service

**Purpose**: Maintain the Top 100 quality designs

**Capabilities**:
- Track rankings and scores
- Handle new challengers
- Evict lowest-ranked designs
- Track leaderboard statistics
- Freshness checks (re-evaluate old entries)

**Key Files**:
- `DesignAgent.Server/Services/LeaderboardService.cs`
- `DesignAgent.Server/Services/DesignStorageService.cs`

### 5. Background Hunter Service

**Purpose**: Run the autonomous crawl loop

**Capabilities**:
- Continuous background operation
- Resource monitoring and throttling
- Time-of-day awareness
- Adaptive pacing
- Error recovery

**Key Files**:
- `DesignAgent.Server/Services/DesignHunterService.cs`
- `DesignAgent.Server/Services/ResourceMonitor.cs`
- `DesignAgent.Server/Services/ThrottleController.cs`

### 6. A2UI Generator Service

**Purpose**: Generate A2UI components from learned patterns

**Capabilities**:
- Convert learned patterns to A2UI templates
- Generate brand-specific A2UI components
- Combine design tokens with A2UI structure
- Support multiple component types (hero, pricing, features, etc.)
- Generate complete pages or individual components

**Key Files**:
- `DesignAgent.Server/Services/A2UIGeneratorService.cs`
- `DesignAgent.Server/Services/A2UITemplateLibrary.cs`
- `DesignAgent.Server/Services/PatternToA2UIConverter.cs`

---

## A2UI Integration (Google Agent-to-User Interface)

### What is A2UI?

[A2UI](https://github.com/google/A2UI) is Google's open standard for AI-generated UIs. It's a declarative JSON format that separates UI structure from implementation, allowing agents to "speak UI" in a way that any framework can render.

**Key Benefits**:
- **Universal**: Same JSON works in Blazor, React, Vue, Flutter, etc.
- **LLM-Friendly**: Easy for AI to generate incrementally
- **Safe**: Declarative data, not executable code
- **Industry Standard**: Backed by Google, adopted by CopilotKit, GenUI SDK

### Our A2UI Strategy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    DESIGN INTELLIGENCE → A2UI PIPELINE                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   STAGE 1: LEARNING (Screenshots → Patterns)                                │
│   ─────────────────────────────────────────────────────────────             │
│   linear.app screenshot → "gradient-hero with bento grid"                   │
│   stripe.com screenshot → "three-tier pricing with toggle"                  │
│                                                                              │
│            ↓ Convert to A2UI templates                                       │
│                                                                              │
│   STAGE 2: PATTERN LIBRARY (A2UI Templates)                                 │
│   ─────────────────────────────────────────────────────────────             │
│   patterns/                                                                  │
│     hero-gradient-bento.a2ui.json                                            │
│     pricing-three-tier.a2ui.json                                             │
│     dashboard-dark-sidebar.a2ui.json                                         │
│     features-alternating.a2ui.json                                           │
│                                                                              │
│            ↓ User requests component                                         │
│                                                                              │
│   STAGE 3: GENERATION (Brand + Pattern → A2UI)                              │
│   ─────────────────────────────────────────────────────────────             │
│   Input:  brand="fittrack", component="pricing"                             │
│   Output: {                                                                  │
│             "designTokens": { colors, spacing, fonts },                      │
│             "a2ui": { type: "section", children: [...] }                     │
│           }                                                                  │
│                                                                              │
│            ↓ Claude/Cursor uses it                                           │
│                                                                              │
│   STAGE 4: RENDERING (Universal Output)                                     │
│   ─────────────────────────────────────────────────────────────             │
│   • Claude renders preview                                                   │
│   • Convert to Blazor/React/Vue/HTML                                         │
│   • User can tweak and customize                                             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### A2UI Output Format

```json
{
  "brand": "fittrack",
  "designTokens": {
    ":root": {
      "--color-primary": "#7C3AED",
      "--color-secondary": "#EC4899",
      "--spacing-4": "16px",
      "--spacing-6": "24px",
      "--font-family": "Inter, sans-serif",
      "--radius-lg": "12px"
    }
  },
  "a2ui": {
    "type": "section",
    "id": "pricing",
    "style": {
      "backgroundColor": "var(--color-surface)",
      "padding": "var(--spacing-20) var(--spacing-4)"
    },
    "children": [
      {
        "type": "container",
        "style": { "maxWidth": "1200px", "margin": "0 auto" },
        "children": [
          {
            "type": "text",
            "variant": "h2",
            "value": "Simple, Transparent Pricing",
            "style": { "textAlign": "center" }
          },
          {
            "type": "grid",
            "columns": 3,
            "gap": "var(--spacing-6)",
            "children": [
              {
                "type": "pricing-card",
                "tier": "Basic",
                "price": { "monthly": 9, "annual": 7 },
                "features": ["Feature 1", "Feature 2"]
              }
            ]
          }
        ]
      }
    ]
  }
}
```

**Why CSS Variables + Inline Styles**:
- Self-contained components (portable)
- No external stylesheet dependencies
- Theme-able (change one var, whole UI updates)
- LLM-friendly format
- Safe (no CSS injection)

---

## LLM Integration

### Models Used

| Task | Model | Why |
|------|-------|-----|
| Visual Analysis | `llava:13b` | Vision capability for screenshots |
| Reasoning/Evaluation | `phi4:latest` | Good for structured analysis |
| CSS/Code Analysis | `deepseek-coder-v2:16b` | Code understanding |
| Quick Classifications | `qwen2.5:7b` | Fast, good enough for simple tasks |

### LLM Tasks

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         LLM TASK BREAKDOWN                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   DISCOVERY PHASE:                                                           │
│   ├── Generate search queries              → phi4                           │
│   └── Evaluate if site is aggregator       → phi4                           │
│                                                                              │
│   CAPTURE PHASE:                                                             │
│   └── Select which pages to crawl          → phi4 (link evaluation)         │
│                                                                              │
│   ANALYSIS PHASE:                                                            │
│   ├── Homepage analysis                    → LLaVA                          │
│   ├── Pricing page analysis                → LLaVA                          │
│   ├── Features page analysis               → LLaVA                          │
│   ├── Dashboard analysis                   → LLaVA                          │
│   ├── Blog/article analysis                → LLaVA                          │
│   ├── Generic page analysis                → LLaVA                          │
│   └── Site synthesis (combine pages)       → phi4                           │
│                                                                              │
│   DETECTION PHASE:                                                           │
│   ├── Design system detection              → phi4 + code analysis           │
│   ├── Color harmony analysis               → phi4                           │
│   ├── Typography detection                 → phi4                           │
│   ├── Component extraction                 → LLaVA                          │
│   └── Animation detection                  → LLaVA (video)                  │
│                                                                              │
│   LEARNING PHASE:                                                            │
│   ├── Feedback analysis (mismatch)         → phi4                           │
│   ├── Trend detection                      → phi4                           │
│   └── Prompt evolution                     → phi4                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Learning Systems

### 1. Source Quality Learning

Track which sources yield quality designs:

```csharp
public class SourceLearning
{
    public string SourceUrl { get; set; }
    public int DesignsFound { get; set; }
    public int QualityDesignsFound { get; set; }  // Score >= threshold
    public double YieldRate { get; set; }          // quality / total
    public double AverageScore { get; set; }
    public int TrustScore { get; set; }            // 1-10, auto-adjusted
}
```

### 2. Query Evolution

Track which search queries work:

```csharp
public class QueryLearning
{
    public string Query { get; set; }
    public int TimesUsed { get; set; }
    public int QualityDesignsFound { get; set; }
    public double YieldRate { get; set; }
    public string[] SpawnedQueries { get; set; }   // LLM-generated variants
    public bool IsRetired { get; set; }            // Low yield = retired
}
```

### 3. User Preference Profiles

Learn what individual users like:

```csharp
public class UserPreference
{
    public string UserId { get; set; }
    public string[] PreferredStyles { get; set; }      // ["minimalist", "dark-mode"]
    public string[] PreferredPatterns { get; set; }    // ["gradient-hero", "bento-grid"]
    public string[] DislikedPatterns { get; set; }
    public double AverageScoreLiked { get; set; }
    public double AverageScoreDisliked { get; set; }
    public int TotalRatings { get; set; }
}
```

### 4. Pattern Co-occurrence

Learn which patterns go together:

```csharp
public class PatternCoOccurrence
{
    public string Pattern1 { get; set; }
    public string Pattern2 { get; set; }
    public double Frequency { get; set; }           // 0-1, how often they appear together
    public double AverageCombinedScore { get; set; }
    public int SampleSize { get; set; }
}
```

### 5. Temporal Trends

Track pattern popularity over time:

```csharp
public class DesignTrend
{
    public string PatternName { get; set; }
    public string Period { get; set; }              // "2024-Q1"
    public int OccurrenceCount { get; set; }
    public double GrowthRate { get; set; }          // vs previous period
    public double AverageScore { get; set; }
    public string TrendDirection { get; set; }      // "rising", "stable", "declining"
}
```

### 6. Score Calibration

Learn LLM scoring bias:

```csharp
public class ScoreCalibration
{
    public string Model { get; set; }
    public string PageType { get; set; }
    public double AverageBias { get; set; }         // LLM score - Human score
    public double Accuracy { get; set; }            // Correlation with human ratings
    public int SampleSize { get; set; }
}
```

### 7. Prompt Evolution

Evolve prompts based on feedback:

```csharp
public class PromptEvolution
{
    public string PromptName { get; set; }
    public int CurrentVersion { get; set; }
    public string EvolutionReason { get; set; }
    public string[] LearnedPreferences { get; set; }
    public string[] IdentifiedBlindSpots { get; set; }
    public DateTime EvolvedAt { get; set; }
}
```

---

## Design Detection

### Detection Capabilities

| Detection Type | What We Extract | How It's Used |
|----------------|-----------------|---------------|
| **Design System** | Tailwind, Bootstrap, shadcn, custom | Tag designs, correlate with quality |
| **Color Harmony** | Palette, harmony type (analogous, etc.) | Learn which palettes score well |
| **Typography** | Font families, scale ratio, pairing | Learn which pairings work |
| **Grid System** | Column count, gutters, max-width | Understand layout structure |
| **Icon Library** | Lucide, Heroicons, custom | Tag and correlate |
| **Image Style** | Photo, illustration, 3D | Understand visual approaches |
| **Dark/Light Mode** | Theme support, implementation quality | Score theme handling |
| **Responsive** | Mobile-first vs desktop-first | Quality of responsive implementation |
| **Animations** | Load, scroll, hover, micro-interactions | Motion pattern library |
| **Components** | Button, card, nav patterns | Build component library |

### Detection Flow

```
Screenshot + DOM → LLaVA/phi4 → Detected Elements → Neo4j Storage

Example output:
{
  "design_system": "tailwind",
  "color_harmony": "analogous",
  "palette_name": "Midnight Purple",
  "typography": {
    "heading": "Inter",
    "body": "Inter",
    "pairing_quality": 8.5
  },
  "icon_library": "lucide",
  "theme": "dark",
  "has_responsive": true,
  "layout_type": "bento-grid"
}
```

---

## Data Models

### Core Models

```csharp
// A site in the leaderboard
public class QualityDesign
{
    public string Id { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }                    // Auto-generated or human-named
    public double OverallScore { get; set; }
    public int Rank { get; set; }                       // 1-100
    public DateTime DiscoveredAt { get; set; }
    public DateTime LastRefreshed { get; set; }
    
    // Source tracking
    public string DiscoveredFrom { get; set; }          // "awwwards.com" or search query
    
    // Pages captured
    public List<PageCapture> Pages { get; set; }
    
    // Design DNA
    public DesignDNA DNA { get; set; }
    
    // Detection results
    public DetectionResults Detections { get; set; }
    
    // Human feedback (optional)
    public int? HumanRating { get; set; }               // 1 (👎) or 5 (👍)
    public string? HumanName { get; set; }
}

// A captured page
public class PageCapture
{
    public string Url { get; set; }
    public string PageType { get; set; }                // "homepage", "pricing", etc.
    public double Score { get; set; }
    
    // Screenshots
    public string ScreenshotDesktop { get; set; }
    public string ScreenshotTablet { get; set; }
    public string ScreenshotMobile { get; set; }
    
    // Analysis
    public PageAnalysis Analysis { get; set; }
    
    // Extracted elements
    public ExtractedElements Elements { get; set; }
    
    // Patterns found
    public List<string> PatternsDetected { get; set; }
}

// Page analysis from LLM
public class PageAnalysis
{
    public Dictionary<string, double> Scores { get; set; }  // Category scores
    public double OverallScore { get; set; }
    public List<string> Strengths { get; set; }
    public List<string> Weaknesses { get; set; }
    public string AutoName { get; set; }
    public string Summary { get; set; }
}

// Site-level design DNA
public class DesignDNA
{
    public string PrimaryStyle { get; set; }            // "minimalist", "bold", etc.
    public string ColorPhilosophy { get; set; }
    public string TypographySystem { get; set; }
    public string SpacingPhilosophy { get; set; }       // "generous", "balanced", "dense"
    public string InteractionStyle { get; set; }        // "subtle", "moderate", "rich"
    public string TargetAudience { get; set; }
    public List<string> StandoutElements { get; set; }
    public List<string> RecommendedFor { get; set; }    // Industries/use cases
}

// Detection results
public class DetectionResults
{
    public string DesignSystem { get; set; }            // "tailwind", "bootstrap", "custom"
    public string ComponentLibrary { get; set; }        // "shadcn", "radix", "custom"
    public ColorPalette ColorPalette { get; set; }
    public TypographySystem Typography { get; set; }
    public string IconLibrary { get; set; }
    public string ImageStyle { get; set; }
    public string Theme { get; set; }                   // "dark", "light", "both"
    public bool IsResponsive { get; set; }
    public List<DetectedComponent> Components { get; set; }
}

// Seen URLs (for deduplication)
public class SeenUrl
{
    public string UrlHash { get; set; }                 // SHA256
    public string OriginalUrl { get; set; }
    public DateTime FirstSeen { get; set; }
    public bool WasProcessed { get; set; }
    public bool? WasKept { get; set; }                  // null = not scored, true = kept, false = discarded
    public double? Score { get; set; }
    public string? DiscardReason { get; set; }
}
```

---

## Neo4j Schema

```cypher
// ═══════════════════════════════════════════════════════════
// CORE ENTITIES
// ═══════════════════════════════════════════════════════════

// Design (a captured site in the leaderboard)
CREATE (d:Design {
    id: "uuid",
    url: "https://linear.app",
    name: "Linear",
    overall_score: 9.2,
    rank: 3,
    discovered_at: datetime(),
    discovered_from: "awwwards.com",
    last_refreshed: datetime(),
    human_rating: null,
    human_name: null
})

// Page (sub-page of a design)
CREATE (p:Page {
    id: "uuid",
    url: "https://linear.app/pricing",
    page_type: "pricing",
    score: 9.4,
    screenshot_desktop: "/data/screenshots/...",
    screenshot_tablet: "/data/screenshots/...",
    screenshot_mobile: "/data/screenshots/...",
    captured_at: datetime()
})

// Pattern (reusable design pattern)
CREATE (pat:Pattern {
    name: "gradient-hero",
    category: "homepage",
    description: "Hero section with gradient background",
    occurrence_count: 145,
    avg_score: 8.3,
    trend_direction: "rising"
})

// Source (design aggregator/gallery)
CREATE (s:Source {
    url: "https://awwwards.com",
    name: "Awwwards",
    type: "awards",
    trust_score: 10,
    yield_rate: 0.72,
    designs_found: 450,
    quality_designs_found: 324,
    last_crawled: datetime()
})

// Query (search query)
CREATE (q:Query {
    text: "best web design 2024",
    times_used: 45,
    quality_designs_found: 28,
    yield_rate: 0.35,
    is_retired: false,
    created_at: datetime()
})

// Prompt (LLM prompt - managed by Lightning)
// Already exists in Lightning system

// ═══════════════════════════════════════════════════════════
// DETECTION ENTITIES
// ═══════════════════════════════════════════════════════════

// Design System
CREATE (ds:DesignSystem {
    name: "tailwind",
    type: "css_framework",
    detected_count: 230
})

// Color Palette
CREATE (cp:ColorPalette {
    id: "uuid",
    primary: "#7C3AED",
    secondary: ["#EC4899", "#06B6D4"],
    harmony_type: "analogous",
    warmth: "cool",
    palette_name: "Midnight Purple"
})

// Typography System
CREATE (ts:TypographySystem {
    id: "uuid",
    heading_font: "Inter",
    body_font: "Inter",
    scale_ratio: 1.25,
    pairing_quality: 8.5,
    style: "geometric"
})

// Component
CREATE (c:Component {
    id: "uuid",
    name: "gradient-button",
    type: "button",
    variants: ["primary", "secondary", "ghost"],
    specs: "{...json...}",
    occurrence_count: 45
})

// ═══════════════════════════════════════════════════════════
// LEARNING ENTITIES
// ═══════════════════════════════════════════════════════════

// User Preference
CREATE (up:UserPreference {
    user_id: "chris",
    preferred_styles: ["minimalist", "dark-mode"],
    preferred_patterns: ["gradient-hero", "bento-grid"],
    disliked_patterns: ["carousel", "popup"],
    avg_score_liked: 8.4,
    avg_score_disliked: 6.2,
    total_ratings: 47,
    updated_at: datetime()
})

// Industry Preference
CREATE (ip:IndustryPreference {
    industry: "SaaS",
    top_patterns: ["pricing-three-tier", "feature-grid", "gradient-hero"],
    avg_homepage_score: 8.2,
    avg_pricing_score: 8.5,
    sample_size: 85
})

// Trend
CREATE (t:Trend {
    pattern: "bento-grid",
    period: "2024-Q1",
    occurrence_count: 89,
    growth_rate: 0.45,
    avg_score: 8.7
})

// Model Performance
CREATE (mp:ModelPerformance {
    model: "llava:13b",
    task: "design_analysis_homepage",
    accuracy: 0.82,
    bias: -0.3,
    sample_size: 450
})

// Pattern Co-occurrence
CREATE (pc:PatternCoOccurrence {
    pattern1: "dark-sidebar",
    pattern2: "card-metrics",
    frequency: 0.85,
    avg_combined_score: 8.3,
    sample_size: 120
})

// ═══════════════════════════════════════════════════════════
// RELATIONSHIPS
// ═══════════════════════════════════════════════════════════

// Design relationships
(d:Design)-[:HAS_PAGE]->(p:Page)
(d:Design)-[:HAS_DNA]->(dna:DesignDNA)
(d:Design)-[:USES_SYSTEM]->(ds:DesignSystem)
(d:Design)-[:HAS_PALETTE]->(cp:ColorPalette)
(d:Design)-[:HAS_TYPOGRAPHY]->(ts:TypographySystem)
(d:Design)-[:DISCOVERED_FROM]->(s:Source)
(d:Design)-[:DISCOVERED_VIA]->(q:Query)

// Page relationships
(p:Page)-[:EXHIBITS {confidence: 0.9}]->(pat:Pattern)
(p:Page)-[:CONTAINS]->(c:Component)

// Pattern relationships
(pat1:Pattern)-[:CO_OCCURS {frequency: 0.85}]->(pat2:Pattern)
(ip:IndustryPreference)-[:PREFERS {correlation: 0.78}]->(pat:Pattern)

// User relationships
(up:UserPreference)-[:LIKES]->(d:Design)
(up:UserPreference)-[:DISLIKES]->(d:Design)

// Similarity
(d1:Design)-[:SIMILAR_TO {score: 0.87}]->(d2:Design)

// Source/Query relationships
(q:Query)-[:FOUND]->(d:Design)
(s:Source)-[:LISTED]->(d:Design)
```

---

## Prompts Inventory

### All Prompts (23 total)

| # | Name | Category | Purpose | Model |
|---|------|----------|---------|-------|
| **Discovery Phase** |
| 1 | `design_source_discovery` | discovery | Generate search queries to find sources | phi4 |
| 2 | `design_source_evaluation` | discovery | Evaluate if site is a design aggregator | phi4 |
| **Capture Phase** |
| 3 | `design_link_evaluation` | capture | Select which pages to crawl (KEY!) | phi4 |
| **Analysis Phase** |
| 4 | `design_analysis_homepage` | analysis | Homepage-specific patterns | LLaVA |
| 5 | `design_analysis_pricing` | analysis | Pricing page patterns | LLaVA |
| 6 | `design_analysis_features` | analysis | Features page patterns | LLaVA |
| 7 | `design_analysis_dashboard` | analysis | App UI/dashboard patterns | LLaVA |
| 8 | `design_analysis_blog` | analysis | Blog/article page patterns | LLaVA |
| 9 | `design_analysis_generic` | analysis | Fallback for other pages | LLaVA |
| 10 | `design_site_synthesis` | analysis | Combine pages into site DNA | phi4 |
| **Detection Phase** |
| 11 | `design_system_detection` | detection | Detect CSS framework/design system | phi4 |
| 12 | `design_color_harmony` | detection | Analyze color palette | phi4 |
| 13 | `design_typography_detection` | detection | Detect typography system | phi4 |
| 14 | `design_component_extraction` | detection | Extract reusable components | LLaVA |
| 15 | `design_tech_detection` | detection | Detect tech stack | phi4 |
| **UX/Copy Phase** |
| 16 | `design_copy_analysis` | ux | Analyze UX copy/microcopy | phi4 |
| 17 | `design_accessibility_audit` | ux | Accessibility evaluation | phi4 |
| **Animation Phase** |
| 18 | `design_animation_analysis` | motion | Detect animations (video) | LLaVA |
| **Learning Phase** |
| 19 | `design_feedback_analysis` | learning | Analyze human feedback mismatch | phi4 |
| 20 | `design_trend_detection` | learning | Detect emerging trends | phi4 |
| **A2UI Generation Phase** |
| 21 | `design_generate_a2ui` | a2ui | Generate A2UI from brand + pattern + request | phi4 |
| 22 | `design_pattern_to_a2ui` | a2ui | Convert visual pattern to A2UI template | phi4 + LLaVA |
| 23 | `design_a2ui_to_code` | a2ui | Convert A2UI to framework code (optional) | phi4 |

### Prompt Seed File Location

`DesignAgent.Server/Data/Seeds/design_prompts.json`

---

## Configuration

### DesignHunterOptions

```csharp
public class DesignHunterOptions
{
    // ═══════════════════════════════════════════════════════════
    // LEADERBOARD
    // ═══════════════════════════════════════════════════════════
    public int LeaderboardSize { get; set; } = 100;
    public double InitialThreshold { get; set; } = 7.0;
    
    // ═══════════════════════════════════════════════════════════
    // RESOURCE LIMITS
    // ═══════════════════════════════════════════════════════════
    public double MaxCpuPercent { get; set; } = 30;
    public long MaxMemoryMB { get; set; } = 2048;
    
    // ═══════════════════════════════════════════════════════════
    // THROTTLING
    // ═══════════════════════════════════════════════════════════
    public TimeSpan MinDelayBetweenSites { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaxDelayBetweenSites { get; set; } = TimeSpan.FromMinutes(5);
    public double BackoffMultiplier { get; set; } = 1.5;
    
    // ═══════════════════════════════════════════════════════════
    // TIME AWARENESS
    // ═══════════════════════════════════════════════════════════
    public bool EnableTimeAwareThrottling { get; set; } = true;
    public int WorkHoursStart { get; set; } = 9;
    public int WorkHoursEnd { get; set; } = 18;
    public double WorkHoursMaxCpu { get; set; } = 15;
    
    // ═══════════════════════════════════════════════════════════
    // CRAWLING
    // ═══════════════════════════════════════════════════════════
    public int MaxPagesPerSite { get; set; } = 6;
    public TimeSpan CaptureTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AnalysisTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxConcurrentOperations { get; set; } = 1;
    
    // ═══════════════════════════════════════════════════════════
    // SEARCH
    // ═══════════════════════════════════════════════════════════
    public string[] SearchEngines { get; set; } = { "google", "bing" };
    public int MaxResultsPerQuery { get; set; } = 20;
    
    // ═══════════════════════════════════════════════════════════
    // LEARNING
    // ═══════════════════════════════════════════════════════════
    public int FeedbackCountForPromptEvolution { get; set; } = 10;
    public double SourceDemoteYieldThreshold { get; set; } = 0.1;
    public int MinDesignsBeforeSourceDemote { get; set; } = 50;
    
    // ═══════════════════════════════════════════════════════════
    // FRESHNESS
    // ═══════════════════════════════════════════════════════════
    public bool EnableFreshnessCheck { get; set; } = true;
    public TimeSpan FreshnessCheckAge { get; set; } = TimeSpan.FromDays(90);
    public TimeSpan FreshnessCheckInterval { get; set; } = TimeSpan.FromDays(7);
    
    // ═══════════════════════════════════════════════════════════
    // MODELS
    // ═══════════════════════════════════════════════════════════
    public string VisionModel { get; set; } = "llava:13b";
    public string ReasoningModel { get; set; } = "phi4:latest";
    public string CodeModel { get; set; } = "deepseek-coder-v2:16b";
}
```

### appsettings.json Section

```json
{
  "DesignHunter": {
    "LeaderboardSize": 100,
    "InitialThreshold": 7.0,
    "MaxCpuPercent": 30,
    "MaxMemoryMB": 2048,
    "MinDelayBetweenSites": "00:00:30",
    "MaxPagesPerSite": 6,
    "EnableTimeAwareThrottling": true,
    "WorkHoursStart": 9,
    "WorkHoursEnd": 18,
    "VisionModel": "llava:13b",
    "ReasoningModel": "phi4:latest"
  }
}
```

---

## Implementation Phases

### Phase 1: Foundation (Week 1-2)

**Goal**: Basic crawl and analysis working

- [ ] Create `DesignHunterService` (background service)
- [ ] Create `CaptureService` (Playwright screenshots)
- [ ] Create `LlmAnalysisService` (LLaVA integration)
- [ ] Create `SeenUrlsRepository` (deduplication)
- [ ] Create `LeaderboardService` (Top 100 management)
- [ ] Seed basic prompts (homepage analysis, link evaluation)
- [ ] Basic MCP tools (`design_leaderboard_status`)

**Deliverable**: Can capture and score a single site

### Phase 2: Multi-Page Crawling (Week 2-3)

**Goal**: Smart page selection and multi-page analysis

- [ ] Implement `design_link_evaluation` prompt
- [ ] Create page-type-specific analysis prompts
- [ ] Implement `design_site_synthesis` prompt
- [ ] DOM extraction (colors, fonts, spacing)
- [ ] Site-level DNA generation

**Deliverable**: Can crawl 6 pages per site with smart selection

### Phase 3: Dynamic Discovery (Week 3-4)

**Goal**: Autonomous source discovery

- [ ] Create `DiscoveryAgent` (search integration)
- [ ] Implement `design_source_discovery` prompt
- [ ] Implement `design_source_evaluation` prompt
- [ ] Source quality tracking
- [ ] Query evolution system

**Deliverable**: System finds its own sources

### Phase 4: Learning Systems + A2UI Templates (Week 4-5)

**Goal**: Self-improving intelligence + A2UI pattern library

- [ ] User preference tracking
- [ ] Pattern co-occurrence learning
- [ ] Score calibration
- [ ] Prompt evolution based on feedback
- [ ] Trend detection
- [ ] **Pattern → A2UI conversion** (convert learned patterns to A2UI templates)
- [ ] **A2UI template library** (store reusable A2UI patterns)

**Deliverable**: System learns and improves + A2UI pattern library created

### Phase 5: Detection Enhancements (Week 5-6)

**Goal**: Deep design understanding

- [ ] Design system detection
- [ ] Color harmony analysis
- [ ] Typography detection
- [ ] Component extraction
- [ ] Animation detection (optional, needs video capture)

**Deliverable**: Rich metadata for each design

### Phase 6: A2UI Generation & Polish (Week 6-7)

**Goal**: Production-ready with A2UI generation

- [ ] **A2UI Generator Service** (brand + pattern → A2UI JSON)
- [ ] **`design_generate` MCP tool** (primary interface for A2UI generation)
- [ ] **`design_generate_from_pattern`** (use specific patterns)
- [ ] **`design_generate_similar`** (inspire from Top 100)
- [ ] **`design_convert_to_code`** (optional: A2UI → framework code)
- [ ] Resource monitoring and throttling
- [ ] Time-of-day awareness
- [ ] Error recovery and resilience
- [ ] MCP tools for gallery access
- [ ] Documentation

**Deliverable**: Complete autonomous system with A2UI generation

---

## API/MCP Tools

### MCP Tools for Design Intelligence

| Tool | Description |
|------|-------------|
| `design_hunter_status` | Get status of the background hunter (running, paused, stats) |
| `design_leaderboard` | Get current Top 100 leaderboard |
| `design_get` | Get details of a specific design |
| `design_search` | Search designs by style, pattern, industry |
| `design_similar` | Find designs similar to a given one |
| `design_gallery` | Browse captured designs with optional filters |
| `design_rate` | Rate a design (👍/👎) and optionally name it |
| `design_trends` | Get current design trends |
| `design_patterns` | Get pattern library with stats |
| `design_refresh` | Force refresh a specific design |
| `design_hunter_control` | Start/pause/configure the hunter |

### Example MCP Calls

```json
// Get leaderboard
{
  "name": "design_leaderboard",
  "arguments": {
    "limit": 20,
    "style": "minimalist"
  }
}

// Search designs
{
  "name": "design_search",
  "arguments": {
    "query": "dark mode SaaS dashboard",
    "minScore": 8.0,
    "limit": 10
  }
}

// Rate a design
{
  "name": "design_rate",
  "arguments": {
    "designId": "uuid",
    "rating": 5,
    "name": "Love this one"
  }
}

// Get trends
{
  "name": "design_trends",
  "arguments": {
    "period": "last_30_days",
    "category": "homepage"
  }
}
```

---

## Next Steps

1. **Approve this plan** - Review and confirm the architecture
2. **Create prompt seed file** - All 23 prompts in JSON format (20 core + 3 A2UI)
3. **Start Phase 1** - Foundation implementation
4. **Phase 4+**: Add A2UI generation capabilities
5. **Iterate** - Build, test, learn, improve

### Key Milestones

| Milestone | Deliverable |
|-----------|-------------|
| Phase 1-3 | Design Intelligence running (Top 100 leaderboard) |
| Phase 4 | A2UI pattern library (learned patterns as A2UI templates) |
| Phase 5 | `design_generate` MCP tool (brand → A2UI output) |
| Phase 6 | Production-ready with full A2UI generation |

---

## Questions to Resolve

1. **Search API**: Which search API to use? (SerpAPI, Google Custom Search, Bing)
2. **Video capture**: Do we want animation detection now or later?
3. **Storage location**: Where to store screenshots? (Local, S3, etc.)
4. **Gallery UI**: Do we need a web UI to browse designs, or MCP tools sufficient?

---

*Document created: December 2024*
*Status: Planning*
*Updated: December 2024 - A2UI Integration*

---

## Appendix: A2UI MCP Tools

### Primary Use Case: A2UI Generation

| Tool | Purpose | Returns |
|------|---------|---------|
| `design_generate` | Generate A2UI component | Design tokens + A2UI JSON |
| `design_generate_from_pattern` | Use specific pattern | A2UI based on learned pattern |
| `design_generate_similar` | Inspire from Top 100 | A2UI similar to reference |
| `design_convert_to_code` | Framework conversion | Blazor/React/Vue/HTML code |

### design_generate (PRIMARY TOOL)

**Input**:
```json
{
  "brand": "fittrack",
  "component": "pricing",      // hero, pricing, features, dashboard, full-page
  "style": "three-tier"         // optional
}
```

**Output**:
```json
{
  "brand": "fittrack",
  "designTokens": {
    ":root": {
      "--color-primary": "#7C3AED",
      "--color-secondary": "#EC4899",
      "--spacing-4": "16px",
      "--spacing-6": "24px",
      "--font-family": "Inter, sans-serif",
      "--radius-lg": "12px"
    }
  },
  "a2ui": {
    "type": "section",
    "id": "pricing-section",
    "style": {
      "padding": "var(--spacing-20) var(--spacing-4)",
      "backgroundColor": "var(--color-surface)"
    },
    "children": [
      {
        "type": "container",
        "style": { "maxWidth": "1200px" },
        "children": [
          {
            "type": "text",
            "variant": "h2",
            "value": "Simple, Transparent Pricing"
          },
          {
            "type": "grid",
            "columns": 3,
            "gap": "var(--spacing-6)",
            "children": [
              {
                "type": "pricing-card",
                "tier": "Basic",
                "price": { "monthly": 9, "annual": 7 },
                "features": ["Feature 1", "Feature 2"]
              }
            ]
          }
        ]
      }
    ]
  }
}
```

**Claude/Cursor Can**:
- Render it as a preview
- Convert to Blazor/React/Vue code
- Let user tweak and customize
- Save as file

