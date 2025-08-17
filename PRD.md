# Product Requirements Document: LinkedIn Learning AI Course Summarizer

## Overview

**Product Name:** LinkedIn Learning AI Course Summarizer  
**Version:** 1.0  
**Date:** August 2025  
**Product Manager:** [Your Name]

## Executive Summary

A C# .NET Core console application that extracts transcripts from LinkedIn Learning courses, converts them to structured markdown format, and generates AI-powered course summaries and insights. The tool aims to eliminate manual transcript extraction while providing intelligent course analysis for enhanced learning and reference.

## Problem Statement

Currently, users must manually copy transcripts from LinkedIn Learning lessons one by one, which is:
- Time-consuming for multi-lesson courses
- Error-prone and inconsistent in formatting
- Difficult to search and reference later
- Lacks course-level summarization and insights

## Target Users

**Primary Users:**
- Students and professionals taking LinkedIn Learning courses
- Corporate training coordinators managing multiple courses

**User Personas:**
1. **The Learner:** Takes 2-5 courses monthly, wants organized notes for future reference
2. **The Training Manager:** Processes 10+ courses for team training, needs summaries for executives

## Goals and Success Metrics

### Primary Goals
1. **Automation:** Reduce transcript extraction time from hours to minutes
2. **Intelligence:** Provide AI-powered course analysis, summaries and insights
3. **Organization:** Generate consistently formatted, searchable transcripts
4. **Scalability:** Support batch processing of multiple courses

### Success Metrics
- **Time Savings:** 90%+ reduction in manual extraction time
- **Accuracy:** 95%+ transcript accuracy compared to manual extraction
- **User Adoption:** 100+ active users within 3 months
- **Processing Volume:** Support 50+ course processing per user session

## Core Features

### 1. Automated Transcript Extraction
**Description:** Automatically navigate LinkedIn Learning courses and extract lesson transcripts

**Requirements:**
- Extract transcripts from individual lessons
- Handle various course structures (video-only, mixed content)
- Optionally preserve timestamp information based on KEEP_TIMESTAMPS setting
- Support courses with closed captions enabled
- Use MAX_SCROLL_ROUNDS for dynamic content loading
- Optimize extraction for courses under SINGLE_PASS_THRESHOLD

**Acceptance Criteria:**
- ✅ Successfully extract transcripts from 95% of LinkedIn Learning courses
- ✅ Maintain original text formatting and structure
- ✅ Handle network timeouts and retry failed extractions
- ✅ Log extraction progress and errors

### 2. Structured Markdown Generation
**Description:** Convert extracted transcripts into organized, readable markdown files

**Requirements:**
- Generate individual lesson markdown files
- Create comprehensive course-level markdown compilation
- Include metadata (course title, instructor, lesson duration)
- Implement consistent formatting standards

**File Structure:**
```
course-name/
├── README.md (course overview + summary)
├── lessons/
│   ├── 01-lesson-name.md
│   ├── 02-lesson-name.md
│   └── ...
└── full-transcript.md
```

**Acceptance Criteria:**
- ✅ Generate valid markdown with proper heading hierarchy
- ✅ Include lesson metadata and navigation links
- ✅ Create searchable content with consistent formatting
- ✅ Generate table of contents for course overview

### 3. AI-Powered Course Summarization
**Description:** Use OpenAI API to generate intelligent course summaries and key insights

**Requirements:**
- Integrate with OpenAI API using configured model
- Load custom prompts from SUMMARY_INSTRUCTION_PATH
- Generate executive summaries (200-500 words)
- Extract key learning objectives and takeaways
- Identify main topics and skill areas covered
- Handle chunking for long transcripts using MAP_CHUNK_SIZE and MAP_CHUNK_OVERLAP

**Summary Components:**
- Course overview and target audience
- Key concepts and learning objectives  
- Practical applications and examples
- Recommended follow-up resources

**Acceptance Criteria:**
- ✅ Generate coherent, accurate course summaries using OpenAI API
- ✅ Complete summarization within 2 minutes for typical courses  
- ✅ Load custom prompt templates from SUMMARY_INSTRUCTION_PATH
- ✅ Handle rate limiting and API error scenarios
- ✅ Process long transcripts using chunking strategy

### 4. Configuration Management
**Description:** Load all configuration from `.env` file for seamless automation

**Configuration Requirements:**
- Single `.env` file contains all settings
- No user interaction required during processing
- Load course URLs from `urls.txt` parameter
- Validate configuration on startup

**Environment Variables:**
```
OPENAI_API_KEY=your_api_key_here
OPENAI_MODEL=gpt-4o-mini
SUMMARY_INSTRUCTION_PATH=./prompts/summary.txt
OUTPUT_TRANSCRIPT_DIR=./output
HEADLESS=true
KEEP_TIMESTAMPS=false
MAX_SCROLL_ROUNDS=10
SESSION_PROFILE=linkedin_session
SINGLE_PASS_THRESHOLD=5000
MAP_CHUNK_SIZE=4000
MAP_CHUNK_OVERLAP=200
```

**Configuration Behavior:**
- Load all settings from `.env` on startup
- Use `urls.txt` as input parameter containing course URLs
- Run fully automated without user prompts
- Validate all required environment variables

**Acceptance Criteria:**
- ✅ Load complete configuration from `.env` file only
- ✅ Process all courses from `urls.txt` automatically
- ✅ Validate configuration and fail fast on missing values
- ✅ No interactive prompts during processing

### 5. Persistent Browser Sessions
**Description:** Maintain LinkedIn login sessions with initial manual authentication

**Authentication Flow:**
- **First Run:** Launch browser (headed mode) for manual LinkedIn login
- **Subsequent Runs:** Use saved session from SESSION_PROFILE
- **Session Expiry:** Automatically detect and prompt for re-authentication

**Requirements:**
- Use SESSION_PROFILE for persistent browser sessions
- Launch headed browser on first run or when session expires
- Support headless mode via HEADLESS setting for subsequent runs
- Handle session expiration gracefully with user notification
- Respect LinkedIn's terms of service and rate limits

**First-Time Setup:**
1. Program detects no existing session profile
2. Launches browser in headed mode (ignores HEADLESS setting)
3. Navigates to LinkedIn login page
4. User manually enters credentials and completes any 2FA
5. Program saves session data to SESSION_PROFILE
6. Future runs use headless mode with saved session

**Session Management:**
- Store session cookies and authentication state
- Detect expired sessions during processing
- Gracefully pause and request re-authentication when needed
- Secure session storage with appropriate permissions

**Acceptance Criteria:**
- ✅ Launch headed browser for initial authentication
- ✅ Maintain sessions for 7+ days without re-login
- ✅ Gracefully handle expired sessions with user notification
- ✅ Secure session storage with proper file permissions
- ✅ Allow manual session reset when needed

### 6. Batch Processing
**Description:** Process multiple courses from a URL list file

**Requirements:**
- Read course URLs from `urls.txt` file automatically
- Process courses sequentially with progress tracking
- Output to configured OUTPUT_TRANSCRIPT_DIR
- Generate consolidated reports across courses
- Handle partial failures in batch operations

**Batch Features:**
- Resume interrupted batch processing
- Generate batch summary reports
- Configurable delays between course processing
- Parallel processing options for power users

**Acceptance Criteria:**
- ✅ Successfully process 20+ courses in single batch
- ✅ Provide real-time progress updates
- ✅ Generate batch completion reports
- ✅ Resume from last processed course on restart

## Technical Requirements

### System Requirements
- **Platform:** Cross-platform (Windows, macOS, Linux)
- **Runtime:** .NET 6.0 or higher
- **Browser:** Chrome/Chromium for automation
- **Storage:** 100MB+ free space per course

### External Dependencies
- **Browser Automation:** Playwright
- **LLM Integration:** OpenAI API
- **File Processing:** Markdown parsing and generation libraries

### Performance Requirements
- **Extraction Speed:** 1-2 minutes per lesson
- **Memory Usage:** <500MB during normal operation
- **Concurrent Processing:** Support 1-3 parallel extractions
- **Error Recovery:** Automatic retry for transient failures

## User Experience Requirements

### Command Line Interface
```bash
# Process all courses from urls.txt (will prompt for login on first run)
linkedin-summarizer urls.txt

# Verify configuration
linkedin-summarizer --check-config

# Reset saved session (force new login)
linkedin-summarizer --reset-session
```

### Authentication Experience
- **First Run:** Browser opens automatically for LinkedIn login
- **User Action Required:** Manual login with credentials and 2FA if enabled
- **Session Saved:** Automatic session persistence for future runs
- **Subsequent Runs:** Fully automated processing in headless mode
- **Session Expiry:** Clear notification and automatic browser launch for re-auth

### Output Experience
- Clear progress indicators during extraction
- Structured directory output with intuitive naming
- Comprehensive logs for troubleshooting
- Success/failure summaries with actionable feedback

## Security and Compliance

### Data Privacy
- Store user credentials securely with encryption
- Never log or transmit LinkedIn passwords
- Respect user content and course access rights
- Implement secure session management

### LinkedIn Compliance
- Respect robots.txt and terms of service
- Implement reasonable rate limiting
- Use only publicly accessible transcript data
- Include disclaimer about educational/personal use

### API Security
- Secure storage of LLM API keys
- Implement API rate limiting and error handling

## Launch Strategy

### Phase 1: Core MVP (Weeks 1-4)
- Basic transcript extraction for single courses
- Simple markdown generation
- Manual session management
- Command-line interface

### Phase 2: Enhanced Features (Weeks 5-8)
- AI summarization integration
- Persistent session management
- Configuration file support
- Error handling improvements

### Phase 3: Advanced Features (Weeks 9-12)
- Batch processing from URL files
- Advanced markdown formatting
- Performance optimizations
- Comprehensive documentation

## Risk Assessment

### Technical Risks
- **LinkedIn UI Changes:** Medium risk - may break extraction logic
- **Rate Limiting:** Low risk - implement conservative delays
- **LLM API Costs:** Medium risk - provide usage controls and estimates

### Mitigation Strategies
- Implement robust element selection strategies
- Monitor LinkedIn for UI changes
- Provide cost estimation and usage tracking
- Offer local LLM alternatives

## Distribution Strategy

### Source Code Distribution
Share the source code for coworkers to build locally:

```
linkedin-summarizer/
├── src/
│   ├── linkedin-summarizer.csproj
│   ├── Program.cs
│   └── Services/...
├── .env.example
├── urls.txt.example
├── README.md                    # Build instructions
└── setup.bat / setup.sh        # Automated setup script
```

**Coworker Setup:**
```bash
git clone [your-repo]
cd linkedin-summarizer
dotnet restore
dotnet build -c Release
dotnet run urls.txt
```

## Future Enhancements

### Potential Features
- Web-based dashboard interface
- Integration with note-taking apps (Notion, Obsidian)
- Course comparison and analysis tools
- Team collaboration features
- Custom export formats (PDF, EPUB)

### Scalability Considerations
- Cloud deployment options
- Distributed processing capabilities
- Enterprise team management
- Advanced analytics and reporting

## Success Criteria

### Technical Success
- ✅ 95%+ successful transcript extraction rate
- ✅ Sub-5-minute processing time for typical courses
- ✅ Zero critical security vulnerabilities
- ✅ Cross-platform compatibility

### User Success
- ✅ 90%+ user satisfaction score
- ✅ 80%+ user retention after 30 days
- ✅ 50%+ of users process multiple courses
- ✅ Positive community feedback and contributions

## Conclusion

This LinkedIn Learning AI Course Summarizer addresses a clear market need for efficient course content extraction and organization. By focusing on automation, intelligent summarization, and user-friendly configuration, we can deliver significant value to learners and training professionals while maintaining compliance and security standards.