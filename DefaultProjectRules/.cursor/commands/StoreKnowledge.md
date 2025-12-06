---
name: Store Knowledge
---
# Store Knowledge - Save Q&A for Future Recall

Store useful question-answer pairs so Agent Lightning can instantly recall them later.

## When to Use
- After answering any useful question
- After explaining how something works
- After solving a problem
- After providing code examples

## Steps

1. Check for similar existing Q&A:
   - Call `find_similar_questions` with the question
   - If match found, consider updating instead of creating new

2. Store the Q&A pair:
   - Call `store_qa` with:
     - `question`: The question that was asked
     - `answer`: Your helpful response
     - `relevantFiles`: Array of file paths that were involved
     - `context`: Project context name

3. Tag business domains (optional):
   - Call `get_insights` with category='domains' to categorize by business domain

## Example

```
find_similar_questions(
  question: "How does authentication work?",
  context: "myproject"
)
→ Check if already stored

store_qa(
  question: "How does authentication work?",
  answer: "Authentication uses JWT tokens via AuthService. The flow is: 1) User submits credentials to /auth/login, 2) AuthService validates against database, 3) JWT token generated with claims, 4) Token returned to client for subsequent requests.",
  relevantFiles: ["Services/AuthService.cs", "Controllers/AuthController.cs"],
  context: "myproject"
)

get_insights(
  category: "domains",
  filePath: "Services/AuthService.cs",
  context: "myproject"
)
→ Tags file with "Authentication" domain
```

## Benefits
- Next time similar question is asked, instant recall!
- Builds project-specific knowledge base
- Reduces repeated explanations

