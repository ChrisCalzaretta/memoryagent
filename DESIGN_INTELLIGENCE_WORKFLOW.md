# Design Intelligence System - Brand Improvement Workflow

## 🎯 What It Does

The Design Intelligence System autonomously learns from real-world designs to help improve your brand guidelines.

## 🔄 Autonomous Learning Loop (When Enabled)

```
┌─────────────────────────────────────────────────────────────┐
│                  AUTONOMOUS LEARNING CYCLE                   │
└─────────────────────────────────────────────────────────────┘

1. 🔍 DISCOVER
   ↓ Uses LLM to generate search queries
   ↓ Finds high-quality design websites
   ↓ Stores as "pending sources"

2. 📸 CRAWL
   ↓ Captures screenshots at multiple breakpoints (1920px, 1024px, 375px)
   ↓ Extracts DOM structure, CSS, fonts, colors
   ↓ Stores raw design data in Neo4j

3. 🧠 ANALYZE
   ↓ LLM scores each page (0-10) on:
   ↓   - Visual hierarchy
   ↓   - Color harmony
   ↓   - Typography quality
   ↓   - Layout composition
   ↓   - Responsive design
   ↓   - Accessibility
   ↓ Extracts patterns (color palettes, spacing, fonts)

4. 🏆 LEADERBOARD
   ↓ Maintains top 100 designs (score >= 7.0)
   ↓ Only best designs make it to leaderboard
   ↓ Floor score increases as better designs found

5. 📚 LEARN
   ↓ Analyzes what makes high-scoring designs great
   ↓ Evolves analysis prompts based on feedback
   ↓ Learns patterns from successful designs

6. 🔄 REPEAT
   └─ Continues discovering and learning forever
```

## 📊 Available API Endpoints

### Discovery & Learning
- `POST /api/design-intelligence/discover?targetCount=10` - Find new design sources
- `GET /api/design-intelligence/sources/pending?limit=10` - See pending sources
- `GET /api/design-intelligence/leaderboard?limit=100` - Top designs

### Manual Processing
- `POST /api/design-intelligence/crawl?url=https://example.com` - Crawl a specific site
- `POST /api/design-intelligence/analyze?designId=abc123` - Analyze captured design
- `POST /api/design-intelligence/process?url=https://example.com` - Full pipeline (crawl + analyze)

### Testing
- `POST /api/design-intelligence/test/generate-queries?count=5&category=saas` - Generate search queries

## 🚀 How to Use It to Improve Your Brand

### Option 1: Enable Autonomous Learning (Background)

**In appsettings.json:**
```json
"DesignIntelligence": {
  "EnableBackgroundLearning": true,
  "BackgroundIntervalSec": 3600,  // Run every hour
  "LeaderboardSize": 100,
  "InitialThreshold": 7.0,
  "SearchProvider": "google",
  "SearchApiKey": "YOUR_API_KEY"  // Optional: for Google Custom Search
}
```

**Then restart the container:**
```bash
docker-compose -f docker-compose-shared-Calzaretta.yml restart design-agent
```

It will automatically:
- Discover design websites every hour
- Crawl and analyze them
- Build a leaderboard of best designs
- Learn patterns from high-quality designs

### Option 2: Manual Learning (On-Demand)

Process specific websites you admire:

```bash
# Process a design you like
curl -X POST "http://localhost:5004/api/design-intelligence/process?url=https://stripe.com"

# Check the leaderboard
curl http://localhost:5004/api/design-intelligence/leaderboard

# Use insights to update your brand
curl -X PUT http://localhost:5004/api/design/brand/testbrand \
  -H "Content-Type: application/json" \
  -d '{ ... updated tokens based on learnings ... }'
```

## 💡 Planned Feature: "Improve My Brand" Endpoint

**What's missing (but would be awesome):**

```bash
# Future endpoint (not yet implemented)
POST /api/design/brand/testbrand/improve
{
  "inspirationUrls": ["https://stripe.com", "https://linear.app"],
  "aspectsToImprove": ["color palette", "typography"],
  "keepIdentity": true  // Maintain core brand personality
}

# Would return:
{
  "original": { ... },
  "improved": { ... },
  "changes": [
    "Updated primary color for better contrast (WCAG AAA)",
    "Refined typography scale based on Linear.app patterns",
    "Added semantic color tokens from Stripe"
  ],
  "inspirationAnalysis": { ... }
}
```

## 🛠️ Current Limitations

1. **No automatic brand improvement** - You must manually update brands
2. **No "learn from this site" → "apply to my brand"** workflow
3. **Search API not configured** - Discovery needs Google Custom Search API key
4. **Background learning disabled by default** - Requires explicit enablement

## 📝 Recommendation

**To improve your brand today:**

1. **Manual approach:**
   - Process websites you admire manually
   - Review their patterns in the leaderboard
   - Update your brand tokens based on insights

2. **Enable background learning:**
   - Set `EnableBackgroundLearning: true`
   - Configure search API (optional but recommended)
   - Let it build a quality design database
   - Use leaderboard insights to inform brand updates

3. **Future enhancement:**
   - Build an "improve brand" endpoint that:
     - Analyzes current brand
     - Compares to leaderboard designs
     - Suggests specific improvements
     - Optionally auto-applies changes
