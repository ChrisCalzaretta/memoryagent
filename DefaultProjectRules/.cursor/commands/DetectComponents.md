---
name: Detect Reusable Components
---
Follow this workflow:
1. detect_reusable_components on project directory (minOccurrences=2, minSimilarity=0.7)
2. Show me all detected candidates sorted by priority:
   - CRITICAL: 5+ occurrences, >0.9 similarity
   - HIGH: 3-4 occurrences, >0.8 similarity
   - MEDIUM: 2 occurrences, >0.7 similarity
3. For CRITICAL/HIGH priority: Recommend immediate extraction
4. For each candidate show:
   - Component name suggestion
   - Occurrences count
   - Similarity score
   - Proposed interface (parameters, events)
5. Ask which components to extract




















