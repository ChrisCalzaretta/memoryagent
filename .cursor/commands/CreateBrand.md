# Create Brand Guidelines

Create a complete brand system for this project.

## Steps

### 1. Check Existing Brands
First, check if a brand already exists:
```
design_list_brands
```

### 2. Get Questionnaire
If no brand exists, get the questionnaire:
```
design_questionnaire
```

### 3. Answer Questions
Review the questionnaire and gather these answers from the user:
- Brand name
- Description (1-2 sentences)
- Target audience
- Industry
- Personality traits (3-5)
- Voice archetype
- Theme preference (dark/light/both)
- Visual style
- Platforms (Web, iOS, Android, Desktop)
- Frameworks (Blazor, React, etc.)
- Accessibility level (AA/AAA)

### 4. Create Brand
```
design_create_brand(
  brand_name: "[name]",
  description: "[description]",
  industry: "[industry]",
  personality_traits: ["Professional", "Minimal", ...],
  brand_voice: "Trusted advisor",
  visual_style: "Minimal",
  theme_preference: "Dark mode",
  platforms: ["Web"],
  frameworks: ["Blazor"],
  accessibility_level: "AA"
)
```

### 5. Verify Brand
Get the created brand to verify:
```
design_get_brand(context: "[brand-context]")
```

## Output
- Design tokens (colors, fonts, spacing)
- Component specifications
- Theme definitions
- Voice guidelines
- Accessibility requirements

