# Design Agent - Fix Plan

## 🔍 Current Issues

1. ❌ **Background Learning Disabled** - `EnableBackgroundLearning: false`
2. ⚠️ **Missing LLM Prompts** - Design Intelligence prompts not seeded in database
3. ⚠️ **No Search API Key** - Can't discover new designs automatically

## ✅ What's Working

- ✅ Neo4j connection successful
- ✅ Brand creation/retrieval working
- ✅ Design validation working
- ✅ Manual design processing endpoints available

## 🛠️ Fix Options

### Option 1: Enable Background Learning (Recommended)

**Enable autonomous design discovery and learning:**

1. **Update appsettings.json:**
   ```json
   "EnableBackgroundLearning": true
   ```

2. **Restart the container:**
   ```bash
   docker-compose -f docker-compose-shared-Calzaretta.yml restart design-agent
   ```

**Benefits:**
- Automatically discovers quality design websites
- Builds a leaderboard of best designs
- Learns patterns from successful designs
- Evolves prompts based on feedback

**Limitations without Search API:**
- Won't discover NEW websites automatically
- Can still process manually submitted URLs
- Can still analyze and learn from curated sources

### Option 2: Keep Manual Mode (Current)

**Keep background learning disabled, use manual endpoints:**

**No changes needed!** Just use the API to process specific sites:

```bash
# Process a specific design
curl -X POST "http://localhost:5004/api/design-intelligence/process?url=https://stripe.com"

# Check results
curl http://localhost:5004/api/design-intelligence/leaderboard
```

### Option 3: Full Setup with Search API (Advanced)

**Enable everything including automatic discovery:**

1. **Get Google Custom Search API key:**
   - Go to https://console.cloud.google.com
   - Enable Custom Search API
   - Create API key

2. **Update appsettings.json:**
   ```json
   "DesignIntelligence": {
     "EnableBackgroundLearning": true,
     "SearchProvider": "google",
     "SearchApiKey": "YOUR_GOOGLE_API_KEY_HERE",
     "SearchQueriesPerRun": 5,
     "BackgroundIntervalSec": 3600
   }
   ```

3. **Restart container**

**Benefits:**
- Fully autonomous operation
- Discovers new designs automatically
- No manual intervention needed

## 🎯 Recommendation

**For immediate testing: Option 2 (Manual Mode)**
- Already working, no changes needed
- Process sites you admire manually
- Build leaderboard gradually

**For production use: Option 1 (Background Learning without Search)**
- Enable background processing
- Submit curated sources manually
- System processes and learns automatically

**For full autonomy: Option 3 (Full Setup)**
- Requires API key setup
- Fully hands-off operation
- Best for long-term use

## 📋 Step-by-Step: Enable Background Learning (Option 1)

1. **Edit appsettings.json:**
