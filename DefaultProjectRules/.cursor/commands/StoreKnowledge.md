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

3. Tag business domains:
   - Call `detect_domains` on main files to categorize by business domain

4. Example:
```json
{
  "question": "How does authentication work?",
  "answer": "Authentication uses JWT tokens via AuthService...",
  "relevantFiles": ["Services/AuthService.cs", "Controllers/AuthController.cs"],
  "context": "MyProject"
}
```

## Benefits
- Next time similar question is asked, instant recall!
- Builds project-specific knowledge base
- Reduces repeated explanations

