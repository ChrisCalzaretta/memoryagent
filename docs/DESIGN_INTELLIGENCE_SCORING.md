# 🎯 Design Intelligence Scoring System

> Detailed documentation of how designs are evaluated, scored, and ranked in the Design Intelligence System.

---

## Table of Contents

1. [Overview](#overview)
2. [Multi-Dimensional Scoring](#multi-dimensional-scoring)
3. [Page-Type Specific Scoring](#page-type-specific-scoring)
4. [Site Aggregation](#site-aggregation)
5. [Quality Gate](#quality-gate)
6. [Score Calibration](#score-calibration)
7. [Human Feedback Integration](#human-feedback-integration)
8. [Scoring Examples](#scoring-examples)

---

## Overview

The Design Intelligence System uses a **multi-dimensional scoring approach** where different aspects of design are evaluated separately, then aggregated into an overall score.

### Core Principles

| Principle | Implementation |
|-----------|----------------|
| **Page-Type Specific** | Different criteria for homepage vs pricing vs dashboard |
| **Category Scoring** | Multiple dimensions (hero, nav, CTA, etc.) scored 0-10 |
| **Weighted Aggregation** | Important pages (homepage) weighted higher |
| **LLM-Driven** | All scoring done by LLaVA/phi4, not hardcoded rules |
| **Self-Calibrating** | Learns from human feedback to adjust scoring |
| **Trust-Aware** | Awwwards sites get lower threshold than random sites |

### Scoring Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SCORING PIPELINE                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. CAPTURE                                                                 │
│      └── Screenshot (desktop, tablet, mobile) + DOM extraction              │
│                                                                              │
│   2. PAGE ANALYSIS (LLaVA + page-specific prompt)                           │
│      ├── Homepage → Hero(8.5), Nav(9.0), Social(7.5), Comp(8.8)            │
│      ├── Pricing → Tiers(9.2), Price(8.8), Features(9.0), CTA(8.5)         │
│      ├── Features → Presentation(8.0), Rhythm(8.5), Visuals(9.0)            │
│      └── ... (other pages)                                                   │
│                 ↓                                                            │
│      Overall Page Score = weighted avg of category scores                    │
│                                                                              │
│   3. SITE AGGREGATION                                                        │
│      Homepage:  8.8 × 2.0 = 17.6                                            │
│      Pricing:   9.0 × 1.5 = 13.5                                            │
│      Features:  8.5 × 1.0 = 8.5                                             │
│      About:     7.5 × 0.5 = 3.75                                            │
│      ─────────────────────────                                              │
│      Sum: 43.35 / Total Weight: 5.0                                         │
│      SITE SCORE: 8.67                                                       │
│                                                                              │
│   4. QUALITY GATE                                                            │
│      Threshold: 7.0                                                          │
│      Trust Adjustment: Awwwards (-1.0) = 6.0 effective threshold            │
│      8.67 >= 6.0? ✅ YES → KEEP                                             │
│                                                                              │
│   5. LEADERBOARD RANKING                                                     │
│      Insert at rank based on score                                           │
│      Evict lowest if > 100 designs                                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Multi-Dimensional Scoring

Each page type has **multiple scoring dimensions** that are evaluated independently.

### Why Multi-Dimensional?

| Single Score | Multi-Dimensional |
|--------------|-------------------|
| ❌ "Site scores 8.5" | ✅ "Hero: 9.2, Nav: 7.5, CTA: 8.8" |
| ❌ No actionable feedback | ✅ "Improve navigation design" |
| ❌ Can't learn patterns | ✅ "Sites with hero 9+ score 8.5 avg" |
| ❌ Can't detect trade-offs | ✅ "Beautiful but low accessibility" |

### Scoring Scale (0-10)

| Range | Grade | Description | Action |
|-------|-------|-------------|--------|
| **9.0-10.0** | A | Exceptional, best-in-class | Study and emulate |
| **8.0-8.9** | B | Excellent, high quality | Keep as reference |
| **7.0-7.9** | C | Good, meets standards | Acceptable |
| **6.0-6.9** | D | Fair, has issues | Discard (unless high-trust source) |
| **0-5.9** | F | Poor, significant problems | Discard immediately |

---

## Page-Type Specific Scoring

### Homepage Scoring (design_analysis_homepage)

**Evaluated Categories:**

#### 1. Hero Section (0-10)
**Criteria:**
- **Headline Clarity** (impact, readability, benefit communication)
- **Visual Treatment** (image quality, gradient, video, illustration)
- **CTA Design** (button prominence, action clarity, placement)
- **Above-the-fold Effectiveness** (first impression, visual hierarchy)

**Example:**
```json
{
  "hero": {
    "score": 9.2,
    "strengths": [
      "Clear value proposition in headline",
      "Gradient background creates depth",
      "Primary CTA highly visible"
    ],
    "weaknesses": [
      "Secondary CTA could be more distinct"
    ]
  }
}
```

#### 2. Navigation (0-10)
**Criteria:**
- **Style** (sticky, transparent, colored, minimal)
- **Hierarchy** (primary/secondary items clear)
- **Organization** (logical grouping, item count)
- **Mobile Considerations** (hamburger menu, responsive behavior)

#### 3. Social Proof (0-10)
**Criteria:**
- **Logo Clouds** (quality of logos, arrangement)
- **Testimonials** (placement, design, credibility)
- **Statistics** (impressive numbers, visual presentation)
- **Placement** (above/below fold, integration)

#### 4. Overall Composition (0-10)
**Criteria:**
- **Visual Hierarchy** (eye flow, information priority)
- **Whitespace Usage** (breathing room, density)
- **Color Harmony** (palette coherence, contrast)
- **Typography System** (font choices, scale, readability)

**Overall Homepage Score:**
```
Score = (Hero × 0.35) + (Nav × 0.25) + (Social × 0.20) + (Composition × 0.20)
```

---

### Pricing Page Scoring (design_analysis_pricing)

**Evaluated Categories:**

#### 1. Tier Layout (0-10)
**Criteria:**
- **Number of Tiers** (2, 3, or 4 - 3 is optimal)
- **Visual Arrangement** (horizontal cards, vertical comparison)
- **Differentiation** (size, color, elevation for recommended tier)
- **Highlighted Tier** (middle tier highlighted = best practice)

#### 2. Price Presentation (0-10)
**Criteria:**
- **Typography** (price prominence, size hierarchy)
- **Billing Toggle** (monthly/annual switch presence and design)
- **Currency & Period** ($ vs USD, /month clarity)
- **Discount Communication** ("Save 20%" on annual)

#### 3. Feature Comparison (0-10)
**Criteria:**
- **List Clarity** (bullet points, checkmarks, clear text)
- **Scannability** (can user quickly compare?)
- **Comparison Table** (side-by-side table for detailed comparison)
- **Visual Indicators** (✓ included, ✗ not included, ~ limited)

#### 4. CTAs (0-10)
**Criteria:**
- **Button Design** (size, color, prominence)
- **Action Text** ("Start Free Trial" vs "Buy Now")
- **Hierarchy** (primary CTA vs secondary "Contact Sales")
- **Urgency/Trust** ("No credit card required", "14-day trial")

#### 5. Trust Elements (0-10)
**Criteria:**
- **Guarantees** ("30-day money-back guarantee")
- **Testimonials** (customer quotes on pricing page)
- **Security Badges** (SSL, payment logos)
- **FAQ Section** (common objections addressed)

**Overall Pricing Score:**
```
Score = (Tier × 0.25) + (Price × 0.20) + (Features × 0.25) + (CTA × 0.20) + (Trust × 0.10)
```

---

### Features Page Scoring (design_analysis_features)

**Evaluated Categories:**

#### 1. Feature Presentation (0-10)
**Criteria:**
- **Organization** (grouped logically, sections clear)
- **Benefit Framing** ("Save 10 hours/week" vs "Automation feature")
- **Visual Support** (icons, illustrations, screenshots per feature)
- **Depth** (right amount of detail, not overwhelming)

#### 2. Layout Rhythm (0-10)
**Criteria:**
- **Section Alternation** (image-left, text-right → text-left, image-right)
- **Visual Variety** (bento grid, alternating, stacked)
- **Scroll Experience** (pacing, reveal timing)
- **Pattern Consistency** (predictable structure)

#### 3. Visual Elements (0-10)
**Criteria:**
- **Icon Style** (consistent, modern, appropriate size)
- **Illustrations** (custom vs stock, quality, style match)
- **Screenshots** (actual product, high quality, annotated)
- **Integration** (visuals enhance, not distract)

#### 4. Information Hierarchy (0-10)
**Criteria:**
- **Headings** (H2, H3 structure clear)
- **Scannability** (can user skim and understand?)
- **Progressive Disclosure** ("Learn more" for details)
- **Visual Weight** (important features more prominent)

**Overall Features Score:**
```
Score = (Presentation × 0.30) + (Rhythm × 0.25) + (Visuals × 0.25) + (Hierarchy × 0.20)
```

---

### Dashboard/App UI Scoring (design_analysis_dashboard)

**Evaluated Categories:**

#### 1. Navigation (0-10)
**Criteria:**
- **Sidebar Design** (collapsible, fixed, floating)
- **Information Architecture** (logical grouping, nesting)
- **Active States** (current page clearly indicated)
- **Hierarchy** (primary/secondary nav distinction)

#### 2. Content Area (0-10)
**Criteria:**
- **Card/Panel Usage** (appropriate, not excessive)
- **Information Density** (balance of data vs whitespace)
- **Visual Balance** (left/right, top/bottom)
- **Responsive Behavior** (adapts to screen size)

#### 3. Data Visualization (0-10)
**Criteria:**
- **Chart Quality** (appropriate chart types, readable)
- **Metrics Presentation** (KPIs prominent, context clear)
- **Color Coding** (semantic colors, status indicators)
- **Accessibility** (color + shape/text, not color alone)

#### 4. Interaction Patterns (0-10)
**Criteria:**
- **Button Design** (clear, appropriate size)
- **Form Elements** (inputs, dropdowns, checkboxes visible)
- **Feedback States** (loading, success, error clear)
- **Micro-interactions** (hover, focus, active states)

**Overall Dashboard Score:**
```
Score = (Nav × 0.25) + (Content × 0.25) + (DataViz × 0.30) + (Interactions × 0.20)
```

---

### Blog/Article Page Scoring (design_analysis_blog)

**Evaluated Categories:**

#### 1. Typography (0-10)
- Font choices (readability, personality)
- Line height (comfortable reading)
- Font sizes (heading scale, body text)
- Contrast (text vs background)

#### 2. Content Width (0-10)
- Line length (45-75 characters optimal)
- Margins and gutters
- Responsive behavior
- Whitespace around text

#### 3. Visual Elements (0-10)
- Image handling (full-width, inline, captions)
- Code blocks (syntax highlighting, copy button)
- Callouts and quotes (design, prominence)
- Video embeds (responsive, styled)

#### 4. Navigation (0-10)
- Table of contents (presence, sticky)
- Progress indicator (scroll progress)
- Related content (suggestions, links)
- Breadcrumbs (site context)

**Overall Blog Score:**
```
Score = (Typography × 0.35) + (Width × 0.25) + (Visuals × 0.25) + (Nav × 0.15)
```

---

### Generic Page Scoring (design_analysis_generic)

**For pages that don't fit specific categories:**

#### Evaluated Categories:
1. **Visual Hierarchy** (0-10) - Clear information priority
2. **Typography** (0-10) - Font usage and readability
3. **Color Usage** (0-10) - Palette and contrast
4. **Spacing/Layout** (0-10) - Whitespace and structure
5. **Overall Aesthetic** (0-10) - General design quality

**Overall Generic Score:**
```
Score = Average of all 5 categories
```

---

## Site Aggregation

Multiple pages are combined into a **single site score** using weighted averaging.

### Page Weights

| Page Type | Weight | Rationale |
|-----------|--------|-----------|
| **Homepage** | 2.0x | Most important page, first impression |
| **Pricing** | 1.5x | High-effort page, critical for conversions |
| **Features** | 1.0x | Standard page, important but common |
| **Dashboard** | 1.0x | Standard page (if accessible) |
| **Blog/Article** | 0.5x | Lower priority, content-focused |
| **About** | 0.5x | Lower priority, static content |
| **Generic** | 0.5x | Lowest priority |

### Aggregation Formula

```
Site Score = Σ(PageScore × PageWeight) / Σ(PageWeight)
```

### Example Calculation

**Site: example.com**

| Page | Score | Weight | Contribution |
|------|-------|--------|--------------|
| Homepage | 9.2 | 2.0 | 18.4 |
| Pricing | 8.8 | 1.5 | 13.2 |
| Features | 8.5 | 1.0 | 8.5 |
| About | 7.2 | 0.5 | 3.6 |

```
Total: 43.7
Total Weight: 5.0
Site Score: 43.7 / 5.0 = 8.74
```

---

## Quality Gate

The **quality gate** determines which designs enter the leaderboard.

### Base Threshold

**Default: 7.0** (configurable via `InitialThreshold`)

Only designs scoring **≥ 7.0** are kept.

### Trust Score Adjustment

High-trust sources get a **threshold reduction**:

```csharp
EffectiveThreshold = BaseThreshold - (SourceTrustScore × 0.1)
```

**Examples:**

| Source | Trust Score | Base Threshold | Effective Threshold |
|--------|-------------|----------------|---------------------|
| Random site | 5 | 7.0 | 6.5 |
| Dribbble | 7 | 7.0 | 6.3 |
| SiteInspire | 9 | 7.0 | 6.1 |
| Awwwards | 10 | 7.0 | **6.0** |

**Rationale**: If Awwwards (human-judged) featured it, it's probably good even if LLM scores it 6.5.

### Quality Gate Logic

```csharp
public async Task<bool> PassesQualityGateAsync(
    double siteScore, 
    DesignSource source)
{
    var baseThreshold = _options.InitialThreshold; // 7.0
    var trustBonus = source.TrustScore * 0.1;
    var effectiveThreshold = baseThreshold - trustBonus;
    
    var passes = siteScore >= effectiveThreshold;
    
    if (!passes)
    {
        _logger.LogInformation(
            "❌ {Url} scored {Score:F1} < threshold {Threshold:F1}",
            source.Url, siteScore, effectiveThreshold);
    }
    
    return passes;
}
```

### Leaderboard Dynamics

**When leaderboard < 100:**
- Threshold: 7.0 (base)
- Any design ≥ 7.0 enters

**When leaderboard = 100:**
- Threshold: Score of #100 (current floor)
- New design must beat #100 to enter
- #100 gets evicted

**Floor Evolution:**
```
Day 1:     Floor = 7.0  (threshold)
Week 1:    Floor = 7.2  (weakest in Top 100)
Month 1:   Floor = 7.8  (rising)
Month 3:   Floor = 8.5  (high quality)
Year 1:    Floor = 9.0+ (elite only)
```

---

## Score Calibration

The system **learns** to score more accurately by comparing LLM scores to human feedback.

### Calibration Process

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SCORE CALIBRATION                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   1. LLM SCORES DESIGN                                                       │
│      LLaVA: "Homepage scores 7.5"                                           │
│                                                                              │
│   2. HUMAN PROVIDES FEEDBACK (optional)                                      │
│      User: 👍 "Love this one!" (rating = 9)                                 │
│                                                                              │
│   3. DETECT MISMATCH                                                         │
│      LLM Score: 7.5                                                          │
│      Human Score: 9.0                                                        │
│      Mismatch: +1.5 (human rated higher)                                    │
│                                                                              │
│   4. ANALYZE MISMATCH (design_feedback_analysis prompt)                      │
│      phi4: "LLM missed: subtle animations, premium feel, attention to       │
│             detail in micro-interactions. Blind spot: static screenshots    │
│             can't capture motion design."                                    │
│                                                                              │
│   5. UPDATE CALIBRATION MODEL                                                │
│      Store in ModelPerformance:                                              │
│      - Model: llava:13b                                                      │
│      - PageType: homepage                                                    │
│      - Bias: -0.3 (tends to underscore)                                     │
│      - Accuracy: 0.85 (correlation with human)                              │
│                                                                              │
│   6. EVOLVE PROMPT (after 10+ feedback items)                                │
│      New prompt includes: "Pay special attention to micro-interactions      │
│                            and premium feel indicators..."                   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Calibration Data Model

```csharp
public class ScoreCalibration
{
    public string Model { get; set; }              // "llava:13b"
    public string PageType { get; set; }           // "homepage", "pricing", etc.
    public double AverageBias { get; set; }        // LLM - Human (negative = underscores)
    public double StandardDeviation { get; set; }  // Consistency of bias
    public double Accuracy { get; set; }           // Correlation coefficient (0-1)
    public int SampleSize { get; set; }            // Number of comparisons
    public DateTime LastUpdated { get; set; }
}
```

### Bias Correction

After enough calibration data, apply bias correction:

```csharp
public double ApplyBiasCorrection(double rawScore, string pageType)
{
    var calibration = await _db.GetCalibrationAsync("llava:13b", pageType);
    
    if (calibration == null || calibration.SampleSize < 20)
    {
        return rawScore; // Not enough data yet
    }
    
    // Apply correction
    var correctedScore = rawScore - calibration.AverageBias;
    
    // Clamp to 0-10
    return Math.Clamp(correctedScore, 0, 10);
}

// Example:
// Raw score: 7.5
// Bias: -0.3 (underscores by 0.3)
// Corrected: 7.5 - (-0.3) = 7.8
```

---

## Human Feedback Integration

Users can optionally provide feedback on designs via MCP tool.

### Feedback Collection

```json
// MCP: design_rate
{
  "designId": "uuid",
  "rating": 5,              // 1 (👎) or 5 (👍)
  "name": "Love this one"   // optional custom name
}
```

### Rating Scale

| Human Rating | Numeric | Meaning |
|--------------|---------|---------|
| 👎 Thumbs Down | 1 → 4.0 | Dislike, overrated |
| 👍 Thumbs Up | 5 → 9.0 | Love it, high quality |

**Why 4.0 and 9.0?**
- Maps to our 0-10 scale
- 👍 = 9.0 (exceptional, not perfect 10)
- 👎 = 4.0 (poor, but not 0)

### Feedback Processing

```csharp
public async Task ProcessFeedbackAsync(
    string designId, 
    int rating, 
    string? customName)
{
    var design = await _storage.GetDesignAsync(designId);
    var llmScore = design.OverallScore;
    var humanScore = rating == 1 ? 4.0 : 9.0;
    var mismatch = Math.Abs(llmScore - humanScore);
    
    // Store feedback
    await _storage.StoreFeedbackAsync(new Feedback
    {
        DesignId = designId,
        LlmScore = llmScore,
        HumanRating = rating,
        HumanScore = humanScore,
        Mismatch = mismatch,
        CustomName = customName,
        Timestamp = DateTime.UtcNow
    });
    
    // If significant mismatch, analyze
    if (mismatch > 2.0)
    {
        await _learningService.AnalyzeMismatchAsync(design, humanScore);
    }
    
    // Trigger prompt evolution if enough feedback
    var recentFeedback = await _storage.GetRecentFeedbackAsync(20);
    if (recentFeedback.Count >= 10)
    {
        await _learningService.EvolvePromptAsync("design_analysis_homepage", recentFeedback);
    }
}
```

---

## Scoring Examples

### Example 1: High-Scoring Homepage (9.2)

**Site: linear.app**

```json
{
  "pageType": "homepage",
  "categories": {
    "hero": {
      "score": 9.5,
      "strengths": [
        "Clear, benefit-driven headline",
        "Stunning gradient animation",
        "CTA highly prominent with contrast",
        "Product screenshot shows actual UI"
      ],
      "weaknesses": []
    },
    "navigation": {
      "score": 9.0,
      "strengths": [
        "Minimal, doesn't compete with hero",
        "Sticky with transparency effect",
        "Logical item grouping"
      ],
      "weaknesses": [
        "Could indicate active section on scroll"
      ]
    },
    "socialProof": {
      "score": 9.0,
      "strengths": [
        "Recognizable company logos",
        "Subtle, doesn't dominate",
        "Well-integrated below hero"
      ],
      "weaknesses": []
    },
    "composition": {
      "score": 9.2,
      "strengths": [
        "Perfect visual hierarchy",
        "Generous whitespace",
        "Smooth gradient palette",
        "Typography scale is perfect"
      ],
      "weaknesses": []
    }
  },
  "overallScore": 9.2,
  "autoName": "Gradient Excellence"
}
```

**Why it scores high:**
- All categories score 9.0+
- No significant weaknesses
- Cohesive design system
- Modern, on-trend execution

---

### Example 2: Medium-Scoring Pricing (7.8)

**Site: example-saas.com**

```json
{
  "pageType": "pricing",
  "categories": {
    "tierLayout": {
      "score": 8.5,
      "strengths": [
        "Three tiers with middle highlighted",
        "Good size differentiation"
      ],
      "weaknesses": [
        "Could use more elevation/shadow"
      ]
    },
    "pricePresentation": {
      "score": 7.0,
      "strengths": [
        "Prices are prominent"
      ],
      "weaknesses": [
        "No annual/monthly toggle",
        "Billing period not clear"
      ]
    },
    "featureComparison": {
      "score": 8.0,
      "strengths": [
        "Clear checkmarks",
        "Good feature descriptions"
      ],
      "weaknesses": [
        "No comparison table view"
      ]
    },
    "ctas": {
      "score": 8.2,
      "strengths": [
        "Primary CTAs are prominent",
        "Clear action text"
      ],
      "weaknesses": []
    },
    "trustElements": {
      "score": 7.0,
      "strengths": [
        "Has testimonial section"
      ],
      "weaknesses": [
        "No money-back guarantee",
        "No security badges"
      ]
    }
  },
  "overallScore": 7.8,
  "autoName": "Standard SaaS Pricing"
}
```

**Why it scores medium:**
- Some categories weak (price presentation 7.0)
- Missing best practices (annual toggle, guarantee)
- Functional but not exceptional
- Would benefit from trust elements

---

### Example 3: Rejected Design (6.2)

**Site: poor-design.com**

```json
{
  "pageType": "homepage",
  "categories": {
    "hero": {
      "score": 5.5,
      "strengths": [],
      "weaknesses": [
        "Headline is vague, no clear benefit",
        "Stock photo looks generic",
        "CTA blends into background",
        "Too much text above fold"
      ]
    },
    "navigation": {
      "score": 6.0,
      "strengths": [
        "All items visible"
      ],
      "weaknesses": [
        "Too many items (9), overwhelming",
        "No visual hierarchy",
        "Competes with hero"
      ]
    },
    "socialProof": {
      "score": 7.0,
      "strengths": [
        "Has logos"
      ],
      "weaknesses": [
        "Logos are low resolution",
        "Placement is awkward"
      ]
    },
    "composition": {
      "score": 6.5,
      "strengths": [],
      "weaknesses": [
        "Weak visual hierarchy",
        "Inconsistent spacing",
        "Colors lack harmony",
        "Too many font sizes"
      ]
    }
  },
  "overallScore": 6.2,
  "discardReason": "Score 6.2 below threshold 7.0"
}
```

**Why it's rejected:**
- Multiple categories below 7.0
- Overall score 6.2 < 7.0 threshold
- Many weaknesses identified
- Would not improve the leaderboard

---

## Summary

### Key Takeaways

1. **Multi-dimensional scoring** provides rich, actionable feedback
2. **Page-type-specific prompts** evaluate appropriate criteria
3. **Weighted aggregation** prioritizes important pages
4. **Quality gate** (7.0 base, trust-adjusted) filters designs
5. **Score calibration** learns from human feedback
6. **Prompt evolution** improves accuracy over time

### Scoring Philosophy

> "We don't just ask 'Is this good?' - we ask 'What makes this good?' and 'How good is each part?'"

This approach enables:
- Better pattern learning (know which elements score well)
- Actionable insights (improve specific weaknesses)
- Trust in the system (transparent scoring breakdown)
- Continuous improvement (calibration and evolution)

---

*Document Version: 1.0*
*Last Updated: December 2024*

