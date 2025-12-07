using DesignAgent.Server.Models.Questionnaire;
using System.Text;
using System.Text.Json;

namespace DesignAgent.Server.Services;

public class QuestionnaireService : IQuestionnaireService
{
    public BrandQuestionnaire GetQuestionnaire()
    {
        return new BrandQuestionnaire
        {
            Version = "1.0",
            Sections = new List<QuestionSection>
            {
                // Section 1: Brand Identity
                new QuestionSection
                {
                    Id = "identity",
                    Title = "🏷️ Brand Identity",
                    Icon = "tag",
                    Questions = new List<Question>
                    {
                        new Question { Id = "brand_name", Text = "What is the name of your brand/product?", Type = QuestionType.Text, Required = true },
                        new Question { Id = "tagline", Text = "Do you have a tagline?", Type = QuestionType.Text, Required = false, Example = "Your progress, visualized" },
                        new Question { Id = "description", Text = "Describe your product in 1-2 sentences:", Type = QuestionType.TextArea, Required = true, Example = "A fitness tracking app for busy professionals" }
                    }
                },
                
                // Section 2: Target Audience
                new QuestionSection
                {
                    Id = "audience",
                    Title = "👥 Target Audience",
                    Icon = "users",
                    Questions = new List<Question>
                    {
                        new Question { Id = "target_audience", Text = "Who is your target audience?", Type = QuestionType.TextArea, Required = true, Example = "Busy professionals, 25-45, want to stay healthy but short on time" },
                        new Question
                        {
                            Id = "industry",
                            Text = "What industry/category?",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "SaaS / Software",
                                "E-commerce",
                                "Finance / Fintech",
                                "Health / Fitness",
                                "Education",
                                "Entertainment",
                                "Enterprise / B2B",
                                "Consumer / B2C",
                                "Other"
                            }
                        }
                    }
                },
                
                // Section 3: Brand Personality
                new QuestionSection
                {
                    Id = "personality",
                    Title = "✨ Brand Personality",
                    Icon = "sparkles",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "personality_traits",
                            Text = "Pick 3-5 words that describe how your brand should FEEL:",
                            Type = QuestionType.MultiSelect,
                            Required = true,
                            MinSelect = 3,
                            MaxSelect = 5,
                            Options = new List<string>
                            {
                                "Professional", "Playful", "Trustworthy", "Bold", "Minimal",
                                "Luxurious", "Friendly", "Technical", "Energetic", "Calm",
                                "Innovative", "Traditional", "Warm", "Cool", "Serious", "Fun"
                            }
                        },
                        new Question
                        {
                            Id = "brand_voice",
                            Text = "If your brand was a person, how would they speak?",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Encouraging coach - supportive, motivating, not pushy",
                                "Trusted advisor - professional, knowledgeable, reliable",
                                "Friendly helper - casual, warm, approachable",
                                "Expert authority - confident, precise, authoritative",
                                "Playful friend - fun, witty, light-hearted",
                                "Calm guide - peaceful, patient, reassuring"
                            }
                        }
                    }
                },
                
                // Section 4: Visual Preferences
                new QuestionSection
                {
                    Id = "visual",
                    Title = "🎨 Visual Preferences",
                    Icon = "palette",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "theme_preference",
                            Text = "Primary theme mode:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Dark mode (dark backgrounds, light text)",
                                "Light mode (light backgrounds, dark text)",
                                "Both (user can switch)"
                            }
                        },
                        new Question { Id = "color_preferences", Text = "Any colors you LOVE? (will be considered for primary/accent)", Type = QuestionType.Text, Required = false, Example = "Green (represents growth), Blue (trust)" },
                        new Question { Id = "color_avoid", Text = "Any colors to AVOID?", Type = QuestionType.Text, Required = false, Example = "Purple, Pink" },
                        new Question
                        {
                            Id = "visual_style",
                            Text = "Visual style preference:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Minimal - lots of whitespace, simple, clean",
                                "Rich - detailed, layered, immersive",
                                "Bold - strong colors, high contrast, impactful",
                                "Soft - muted colors, gentle, approachable",
                                "Technical - data-dense, functional, precise"
                            }
                        },
                        new Question
                        {
                            Id = "corner_style",
                            Text = "Corner/radius preference:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Sharp (0-2px) - professional, serious",
                                "Slightly rounded (4-6px) - balanced",
                                "Rounded (8-12px) - friendly, modern",
                                "Very rounded (16px+) - playful, soft",
                                "Pill/Full (9999px) - trendy, distinctive"
                            }
                        }
                    }
                },
                
                // Section 5: Typography
                new QuestionSection
                {
                    Id = "typography",
                    Title = "🔤 Typography",
                    Icon = "type",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "font_preference",
                            Text = "Font style preference:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Sans-serif (clean, modern) - e.g., Inter, DM Sans",
                                "Serif (traditional, trustworthy) - e.g., Merriweather",
                                "Geometric (technical, precise) - e.g., Space Grotesk",
                                "Humanist (friendly, warm) - e.g., Source Sans",
                                "Monospace accent (technical) - for numbers/code",
                                "Let AI choose based on personality"
                            }
                        },
                        new Question { Id = "specific_fonts", Text = "Any specific fonts you want to use?", Type = QuestionType.Text, Required = false, Example = "DM Sans for headings, Inter for body" }
                    }
                },
                
                // Section 6: Platforms & Frameworks
                new QuestionSection
                {
                    Id = "platforms",
                    Title = "📱 Platforms & Frameworks",
                    Icon = "smartphone",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "platforms",
                            Text = "What platforms will you build for?",
                            Type = QuestionType.MultiSelect,
                            Required = true,
                            Options = new List<string> { "Web", "iOS", "Android", "Desktop (Windows/Mac)", "All of the above" }
                        },
                        new Question
                        {
                            Id = "frameworks",
                            Text = "What frameworks/technologies?",
                            Type = QuestionType.MultiSelect,
                            Required = true,
                            Options = new List<string>
                            {
                                "Blazor", "React", "Vue", "Angular", "SwiftUI",
                                "Kotlin/Compose", "Flutter", "React Native", ".NET MAUI", "Plain HTML/CSS"
                            }
                        },
                        new Question
                        {
                            Id = "css_framework",
                            Text = "CSS framework preference:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Tailwind CSS",
                                "Plain CSS with variables",
                                "SCSS/Sass",
                                "CSS-in-JS",
                                "Whatever works best"
                            }
                        }
                    }
                },
                
                // Section 7: Component Needs
                new QuestionSection
                {
                    Id = "components",
                    Title = "🧩 Component Needs",
                    Icon = "layout",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "component_types",
                            Text = "What types of UI will you be building?",
                            Type = QuestionType.MultiSelect,
                            Required = true,
                            Options = new List<string>
                            {
                                "Dashboards / Analytics",
                                "Forms / Data Entry",
                                "Lists / Tables",
                                "Cards / Tiles",
                                "Navigation (sidebar, tabs)",
                                "Modals / Dialogs",
                                "Authentication (login, signup)",
                                "Settings / Preferences",
                                "Landing / Marketing pages",
                                "E-commerce (products, cart)",
                                "Chat / Messaging",
                                "Data Visualization"
                            }
                        }
                    }
                },
                
                // Section 8: Motion & Animation
                new QuestionSection
                {
                    Id = "motion",
                    Title = "🎬 Motion & Animation",
                    Icon = "zap",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "motion_preference",
                            Text = "Animation preference:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "Minimal - subtle transitions only, professional",
                                "Moderate - meaningful animations for feedback",
                                "Rich - expressive animations, delightful micro-interactions",
                                "None - prefer no animations"
                            }
                        }
                    }
                },
                
                // Section 9: Inspiration
                new QuestionSection
                {
                    Id = "inspiration",
                    Title = "💡 Inspiration",
                    Icon = "lightbulb",
                    Questions = new List<Question>
                    {
                        new Question { Id = "inspiration_sites", Text = "Any websites/apps whose design you admire?", Type = QuestionType.TextArea, Required = false, Example = "Linear.app, Stripe.com, Notion.so" },
                        new Question { Id = "avoid_styles", Text = "Any styles you want to AVOID?", Type = QuestionType.TextArea, Required = false, Example = "Generic Bootstrap look, overly corporate" }
                    }
                },
                
                // Section 10: Accessibility
                new QuestionSection
                {
                    Id = "accessibility",
                    Title = "♿ Accessibility",
                    Icon = "accessibility",
                    Questions = new List<Question>
                    {
                        new Question
                        {
                            Id = "accessibility_level",
                            Text = "Accessibility compliance level:",
                            Type = QuestionType.Select,
                            Required = true,
                            Options = new List<string>
                            {
                                "WCAG AA (standard, recommended)",
                                "WCAG AAA (highest, strictest)",
                                "Basic (minimum requirements)"
                            },
                            DefaultValue = "WCAG AA (standard, recommended)"
                        }
                    }
                }
            }
        };
    }

    public ParsedBrandInput ParseAnswers(QuestionnaireAnswers answers)
    {
        var input = new ParsedBrandInput();
        
        foreach (var (key, value) in answers.Answers)
        {
            var strValue = value?.ToString() ?? string.Empty;
            var arrayValue = value is JsonElement je && je.ValueKind == JsonValueKind.Array
                ? je.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : (value as string[]) ?? Array.Empty<string>();
            
            switch (key)
            {
                case "brand_name": input.BrandName = strValue; break;
                case "tagline": input.Tagline = strValue; break;
                case "description": input.Description = strValue; break;
                case "target_audience": input.TargetAudience = strValue; break;
                case "industry": input.Industry = strValue; break;
                case "personality_traits": input.PersonalityTraits = arrayValue; break;
                case "brand_voice": input.VoiceArchetype = strValue; break;
                case "theme_preference": input.ThemePreference = ParseThemePreference(strValue); break;
                case "color_preferences": input.PreferredColors = strValue; break;
                case "color_avoid": input.AvoidColors = strValue; break;
                case "visual_style": input.VisualStyle = strValue; break;
                case "corner_style": input.CornerStyle = ParseCornerStyle(strValue); break;
                case "font_preference": input.FontPreference = strValue; break;
                case "specific_fonts": input.SpecificFonts = strValue; break;
                case "platforms": input.Platforms = arrayValue; break;
                case "frameworks": input.Frameworks = arrayValue; break;
                case "css_framework": input.CssFramework = strValue; break;
                case "component_types": input.ComponentTypes = arrayValue; break;
                case "motion_preference": input.MotionPreference = ParseMotionPreference(strValue); break;
                case "inspiration_sites": input.InspirationSites = strValue; break;
                case "avoid_styles": input.AvoidStyles = strValue; break;
                case "accessibility_level": input.AccessibilityLevel = ParseAccessibilityLevel(strValue); break;
            }
        }
        
        return input;
    }

    public string GetQuestionnaireMarkdown()
    {
        var questionnaire = GetQuestionnaire();
        var sb = new StringBuilder();
        
        sb.AppendLine("# 🎨 Brand Style Guide Builder\n");
        sb.AppendLine("Answer these questions to create your complete brand system.\n");
        sb.AppendLine("---\n");
        
        int questionNum = 1;
        foreach (var section in questionnaire.Sections)
        {
            sb.AppendLine($"## {section.Title}\n");
            
            foreach (var q in section.Questions)
            {
                sb.AppendLine($"**{questionNum}. {q.Text}**");
                if (!q.Required) sb.AppendLine("*(optional)*");
                
                if (q.Options != null && q.Options.Count > 0)
                {
                    foreach (var opt in q.Options)
                    {
                        sb.AppendLine($"- [ ] {opt}");
                    }
                }
                else
                {
                    sb.AppendLine($"> [Your answer{(q.Example != null ? $" - e.g., \"{q.Example}\"" : "")}]");
                }
                
                sb.AppendLine();
                questionNum++;
            }
            
            sb.AppendLine("---\n");
        }
        
        sb.AppendLine("**When you've answered all questions, send your responses and I'll create your complete brand system!**");
        
        return sb.ToString();
    }
    
    private static string ParseThemePreference(string value)
    {
        if (value.Contains("Dark", StringComparison.OrdinalIgnoreCase)) return "dark";
        if (value.Contains("Light", StringComparison.OrdinalIgnoreCase)) return "light";
        return "both";
    }
    
    private static string ParseCornerStyle(string value)
    {
        if (value.Contains("Sharp", StringComparison.OrdinalIgnoreCase)) return "sharp";
        if (value.Contains("Slightly", StringComparison.OrdinalIgnoreCase)) return "slight";
        if (value.Contains("Very", StringComparison.OrdinalIgnoreCase)) return "very-rounded";
        if (value.Contains("Pill", StringComparison.OrdinalIgnoreCase)) return "pill";
        return "rounded";
    }
    
    private static string ParseMotionPreference(string value)
    {
        if (value.Contains("Minimal", StringComparison.OrdinalIgnoreCase)) return "minimal";
        if (value.Contains("Rich", StringComparison.OrdinalIgnoreCase)) return "rich";
        if (value.Contains("None", StringComparison.OrdinalIgnoreCase)) return "none";
        return "moderate";
    }
    
    private static string ParseAccessibilityLevel(string value)
    {
        if (value.Contains("AAA", StringComparison.OrdinalIgnoreCase)) return "AAA";
        if (value.Contains("Basic", StringComparison.OrdinalIgnoreCase)) return "A";
        return "AA";
    }
}

