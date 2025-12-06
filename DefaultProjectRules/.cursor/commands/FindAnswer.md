---
name: Find Answer
---
# Find Answer - Check Knowledge Base First

Before answering any question, check if Agent Lightning has a cached answer.

## When to Use
- BEFORE answering ANY code question
- When asked "how does X work?"
- When asked "where is Y implemented?"
- When troubleshooting issues

## Steps

1. Check for cached answers:
   - Call `find_similar_questions` with the question
   - If match found with high similarity, use that answer!

2. If no cached answer, search code:
   - Call `smartsearch` with the question
   - Call `query` for semantic search
   - Call `search_patterns` for implementation patterns

3. After finding answer:
   - Call `store_qa` to cache for future use

## Benefits
- Instant answers for repeated questions
- Consistent responses across sessions
- Builds institutional knowledge

