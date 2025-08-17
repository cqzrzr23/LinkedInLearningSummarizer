# LinkedIn Learning AI Course Summarizer - Development Instructions

## Project Overview

You are helping to build a **C# .NET Core Console Application** that:
1. Extracts transcripts from LinkedIn Learning courses automatically
2. Generates structured markdown files for each lesson and full course
3. Uses OpenAI API to create AI-powered course summaries
4. Processes multiple courses from a URL list file
5. Persists LinkedIn browser sessions to minimize login friction

## Technical Stack

- **Language:** C# 
- **Framework:** .NET 6.0 or higher
- **Project Type:** Console Application
- **Browser Automation:** Playwright for .NET
- **AI Integration:** OpenAI API (official .NET SDK)
- **Configuration:** Environment variables via .env file
- **File Processing:** Native C# file I/O and markdown generation

## Core Architecture

### Project Structure
```
linkedin-summarizer/
├── linkedin-summarizer.csproj
├── Program.cs                          # Entry point
├── Models/
│   ├── Course.cs                       # Course data model
│   ├── Lesson.cs                       # Lesson data model
│   └── AppConfig.cs                    # Configuration model
├── Services/
│   ├── LinkedInScraper.cs              # Playwright automation
│   ├── OpenAIService.cs                # AI summarization
│   ├── MarkdownGenerator.cs            # File generation
│   └── ConfigurationService.cs         # Config management
├── Utils/
│   └── FileHelper.cs                   # File operations
├── .env.example                        # Configuration template
├── urls.txt.example                    # Sample URL file
└── README.md                           # Setup instructions
```

### Required NuGet Packages
```xml
<PackageReference Include="Microsoft.Playwright" Version="1.40.0" />
<PackageReference Include="OpenAI" Version="1.11.0" />
<PackageReference Include="DotNetEnv" Version="3.0.0" />
<PackageReference Include="Markdig" Version="0.33.0" />
```

## Configuration Management

### Environment Variables (.env file)
```bash
# OpenAI Configuration
OPENAI_API_KEY=sk-your-api-key-here
OPENAI_MODEL=gpt-4o-mini

# File Paths
SUMMARY_INSTRUCTION_PATH=./prompts/summary.txt
OUTPUT_TRANSCRIPT_DIR=./output

# Browser Settings
HEADLESS=true
SESSION_PROFILE=linkedin_session

# Processing Settings
KEEP_TIMESTAMPS=false
MAX_SCROLL_ROUNDS=10
SINGLE_PASS_THRESHOLD=5000

# AI Processing
MAP_CHUNK_SIZE=4000
MAP_CHUNK_OVERLAP=200
```

### Configuration Loading
- Use `DotNetEnv.Env.Load()` to load .env file
- Validate all required environment variables on startup
- Fail fast with clear error messages for missing config
- No interactive prompts during processing

## Core Functionality Requirements

### 1. LinkedIn Session Management
**First Run Behavior:**
- Detect if SESSION_PROFILE exists
- If not found, launch browser in headed mode (ignore HEADLESS setting)
- Navigate to LinkedIn login page
- Wait for user to complete manual login (including 2FA)
- Save session cookies/state to SESSION_PROFILE file
- Display success message to user

**Subsequent Runs:**
- Load session from SESSION_PROFILE
- Use HEADLESS setting for browser mode
- Validate session is still active before processing
- If session expired, repeat first-run flow

### 2. Transcript Extraction Process
**Course Processing:**
- Navigate to each course URL from urls.txt
- Extract course metadata (title, instructor, total lessons)
- Iterate through all lessons in the course
- For each lesson:
  - Navigate to lesson page
  - Find and expand transcript section
  - Handle dynamic content loading (use MAX_SCROLL_ROUNDS)
  - Extract transcript text
  - Optionally preserve timestamps based on KEEP_TIMESTAMPS
  - Save lesson data to memory

**Error Handling:**
- Retry failed extractions with exponential backoff
- Log detailed error information
- Continue processing other lessons if one fails
- Generate partial results when possible

### 3. Markdown Generation
**File Structure:**
```
output/
└── course-name-sanitized/
    ├── README.md                   # Course overview + AI summary
    ├── lessons/
    │   ├── 01-lesson-name.md
    │   ├── 02-lesson-name.md
    │   └── ...
    └── full-transcript.md          # Complete course transcript
```

**Markdown Content Requirements:**
- Use proper heading hierarchy (H1 for course, H2 for lessons)
- Include lesson metadata (duration, instructor, lesson number)
- Generate table of contents with navigation links
- Sanitize filenames for cross-platform compatibility
- Include course metadata at the top of each file

### 4. AI Summarization
**OpenAI Integration:**
- Use configured OPENAI_MODEL (default: gpt-4o-mini)
- Load custom prompt template from SUMMARY_INSTRUCTION_PATH
- Handle long transcripts with chunking (MAP_CHUNK_SIZE and MAP_CHUNK_OVERLAP)
- Implement map-reduce pattern for large courses
- Generate summaries according to user-defined instructions

**Custom AI Instructions:**
- Load instruction content from file specified in SUMMARY_INSTRUCTION_PATH
- If file doesn't exist, use default summarization prompt
- Support markdown format for instruction files
- Pass loaded instructions as system prompt to OpenAI API
- Allow users complete flexibility in defining summary format and style

**Example Instruction File (Instructions.md):**
```markdown
# Instructions for Course Summarization
You are an expert course summarizer. Your job is to generate a **clear, structured, and concise summary** of the ENTIRE course transcript. Follow these rules:
---
## 🎯 Purpose
- The summary is for busy professionals who want a **quick yet comprehensive overview** of the course.
- Emphasize **skills learned**, **tools covered**, and **actionable takeaways**.
---
## 📋 Format
Your output **must be in Markdown** with the following sections, in this exact order:
## Course Title
- The official course title (as stated in the transcript or metadata).
# Course Summary
- 8–12 concise bullets capturing the main learning outcomes.
- One short paragraph tying all topics together.
## Best Fit Learners
- Describe the type of learner or professional who would benefit most from this course.
- Include roles, experience level, or use-cases that make the course especially valuable.
## Skills & Tools
- List of tools, libraries, commands, or frameworks taught in the course.
## Key Terminology
- Bullet list of important terms with 1–2 sentence definitions (extracted directly from the course).
## Practical Checklist
- 6–10 steps or exercises a learner can perform after finishing the course.
## Common Pitfalls
- 5–8 mistakes or misconceptions learners should avoid.
---
## ✍️ Writing Style
- Use **clear, professional language**.
- Be **faithful to the transcript** — don't invent content not present in the course.
- Be concise: each bullet should be one short sentence.
- Use Markdown bullets (`- item`) for lists.
- Keep terminology exact (preserve function names, API calls, code syntax).
---
## ❌ Do Not
- Do not summarize lesson-by-lesson.
- Do not speculate or add outside knowledge.
- Do not exceed ~600 words total.
```

**Implementation Requirements:**
- Read instruction file content as string
- Use as system message in OpenAI API call
- Provide clear error handling if instruction file is missing or unreadable
- Include default fallback instructions for basic summarization
- Support any text-based instruction format (markdown, plain text, etc.)

**API Management:**
- Implement rate limiting to respect OpenAI limits
- Handle API errors gracefully with retries
- Provide cost estimation for processing
- Log API usage for monitoring

## Command Line Interface

### Primary Command
```bash
linkedin-summarizer urls.txt
```

### Additional Commands
```bash
linkedin-summarizer --check-config    # Validate configuration
linkedin-summarizer --reset-session   # Clear saved LinkedIn session
```

### Expected Behavior
- Read course URLs from specified file (one URL per line)
- Process all courses sequentially
- Display progress updates during processing
- Generate comprehensive completion report
- Exit with appropriate status codes

## Error Handling & Logging

### Logging Requirements
- Use structured logging (consider Serilog)
- Log levels: Debug, Info, Warning, Error
- Include timestamps and context information
- Separate log files for different components
- Console output for user-facing progress updates

### Error Scenarios to Handle
- Missing or invalid .env configuration
- Network connectivity issues
- LinkedIn session expiration
- OpenAI API rate limiting or errors
- Invalid course URLs
- File system permissions
- Browser automation failures

## Quality Requirements

### Performance
- Process typical course (20 lessons) in under 10 minutes
- Memory usage should not exceed 500MB
- Support resuming interrupted processing
- Optimize for batch processing efficiency

### Reliability
- 95%+ success rate for transcript extraction
- Graceful degradation when individual lessons fail
- Robust session management across long processing sessions
- Comprehensive error recovery mechanisms

### Security
- Secure storage of OpenAI API keys (never log)
- Safe handling of LinkedIn session data
- Input validation for URLs and file paths
- No hardcoded credentials in source code

## Development Guidelines

### Code Style
- Follow C# naming conventions
- Use async/await for all I/O operations
- Implement proper disposal patterns for resources
- Use dependency injection for service management
- Include comprehensive XML documentation comments

### Testing Considerations
- Unit tests for core business logic
- Integration tests for OpenAI API interactions
- Mock Playwright for automated testing
- Configuration validation tests
- Error scenario coverage

### Documentation
- Clear README with setup instructions
- Code comments for complex business logic
- Configuration examples and explanations
- Troubleshooting guide for common issues

## Important Notes

### LinkedIn Compliance
- Respect robots.txt and terms of service
- Implement reasonable delays between requests
- Only extract publicly available transcript data
- Include appropriate disclaimers about usage

### OpenAI Usage
- Monitor token usage and costs
- Implement reasonable request limits
- Handle context window limitations properly
- Provide usage estimates to users

### Browser Automation Best Practices
- Use stable element selectors
- Implement wait strategies for dynamic content
- Handle different screen sizes and browser states
- Clean up browser resources properly

## Future Considerations

This console application is designed to potentially be converted to a web application later. Keep the following in mind:

- Separate business logic from console-specific code
- Use dependency injection to support different hosting models
- Design services to be stateless where possible
- Consider background job processing patterns
- Plan for multi-user scenarios in service design

## Important Instructions for Claude

Always follow these steps when working on this project:

1. **Start of Every Conversation**: Read PLANNING.md to understand the project vision, architecture, and current phase
2. **Before Starting Work**: Check TASKS.md to see what needs to be done and what's already completed, and check SESSION.md for important notes and context from previous sessions
3. **After Completing Work**: Mark completed tasks with [x] in TASKS.md immediately
4. **During Development**: Add any newly discovered tasks to TASKS.md under the appropriate milestone

This ensures continuity across conversations and maintains accurate project tracking.

## Task Progress Tracking

### After Completing Any Tasks
When you complete tasks from TASKS.md, you MUST update the progress summary at the end of the file, but ALWAYS ask for user permission first:

1. **Ask Permission**: "Should I update the TASKS.md progress summary with the latest completion counts?"
2. **Count Completed Tasks**: Use `grep -c "- \[x\]" TASKS.md` to count completed tasks
3. **Count Total Tasks**: Use `grep -c "- \[" TASKS.md` to count all tasks  
4. **Update Summary Section** at the end of TASKS.md (only after user approval):
   ```
   *Last Updated: [Current Date]*
   *Total Tasks: [Total Count]*
   *Completed: [Completed Count]*
   *In Progress: [Count of ߚ tasks]*
   *Blocked: [Count of ⚠️ tasks]*
   ```

### Task Status Conventions
- `[x]` = Completed task
- `[ ]` = Incomplete task  
- `ߚ` = In progress (mark in task content)
- `⚠️` = Blocked (mark in task content)

### When to Update
- After marking any tasks as complete
- At the end of each development session
- When major milestones are reached

**IMPORTANT**: Never update the progress summary without explicit user permission. Always ask first.

This ensures accurate project progress tracking across all conversations while respecting user control.