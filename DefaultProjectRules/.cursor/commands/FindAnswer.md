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
   - Call `find_similar_questions` with the question and context
   - If match found with high similarity, use that answer!

2. If no cached answer, search code:
   - Call `smartsearch` with the question
   - It auto-detects the best search strategy (semantic, graph, or pattern)

3. After finding answer:
   - Call `store_qa` to cache for future use

## Example

```
find_similar_questions(
  question: "How does authentication work?",
  context: "myproject",
  limit: 5
)
→ Returns similar past Q&A if found

smartsearch(
  query: "How does authentication work?",
  context: "myproject"
)
→ Searches code semantically

store_qa(
  question: "How does authentication work?",
  answer: "Authentication uses JWT tokens via AuthService...",
  relevantFiles: ["Services/AuthService.cs"],
  context: "myproject"
)
```

## Benefits
- Instant answers for repeated questions
- Consistent responses across sessions
- Builds institutional knowledge

