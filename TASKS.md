# LinkedIn Learning AI Course Summarizer - Prioritized Development Tasks

## 🔴 HIGH PRIORITY - Critical Path Tasks (Must Complete First)

### Phase 1: Foundation (Weeks 1-2)

#### 🔴 1.1 Project Setup & Configuration (Week 1) - CRITICAL
- [x] **🔴 P1:** Create new .NET 6 Console Application project named `LinkedInLearningSummarizer`
- [x] **🔴 P1:** Install required NuGet packages:
  - `Microsoft.Playwright` (version 1.40.0+)
  - `OpenAI` (version 1.11.0+) 
  - `DotNetEnv` (version 3.0.0+)
  - `Markdig` (version 0.33.0+)
- [x] **🔴 P1:** Install Playwright browsers: `pwsh bin/Debug/net8.0/playwright.ps1 install`
- [x] **🔴 P1:** Create `Models/AppConfig.cs` with all configuration properties
- [x] **🔴 P1:** Create `Services/ConfigurationService.cs` for loading and validating config
- [x] **🔴 P1:** Implement environment variable mapping for core settings:
  - `OPENAI_API_KEY`, `OPENAI_MODEL`
  - `OUTPUT_TRANSCRIPT_DIR`, `SESSION_PROFILE`
  - `HEADLESS`, `KEEP_TIMESTAMPS`
- [x] **🔴 P1:** Add configuration validation with clear error messages
- [x] **🔴 P1:** Create `.env.example` template file
- [x] **🔴 P1:** Set up basic `Program.cs` with command line argument parsing

#### 🔴 1.2 LinkedIn Session Management (Week 2) - CRITICAL
- [x] **🔴 P1:** Create `Services/LinkedInScraper.cs` class
- [x] **🔴 P1:** Initialize Playwright browser instance with configuration
- [x] **🔴 P1:** Implement first-run detection logic (check if `SESSION_PROFILE` exists)
- [x] **🔴 P1:** Launch browser in headed mode for first run (ignore HEADLESS setting)
- [x] **🔴 P1:** Navigate to LinkedIn Learning login page
- [x] **🔴 P1:** Wait for user to complete manual login process
- [x] **🔴 P1:** Save browser context state to `SESSION_PROFILE`
- [x] **🔴 P1:** Implement session restoration on subsequent runs
- [x] **🔴 P1:** Add session validation functionality

### Phase 2: Core Extraction (Weeks 3-4)

#### 🔴 2.1 Basic Navigation & Course Discovery (Week 3) - CRITICAL
- [x] **🔴 P1:** Create `Models/Course.cs` and `Models/Lesson.cs` with essential properties
- [x] **🔴 P1:** Implement course URL validation and parsing
- [x] **🔴 P1:** Navigate to course main page using saved session
- [x] **🔴 P1:** Extract course title, instructor, and basic metadata
- [x] **🔴 P1:** Locate lesson list/navigation elements
- [x] **🔴 P1:** Extract all lesson URLs and build lesson order
- [x] **🔴 P1:** Handle different course layouts and structures

#### 🔴 2.2 Basic Transcript Extraction (Week 4) - CRITICAL
- [ ] **🔴 P1:** Navigate to individual lesson pages
- [ ] **🔴 P1:** Locate transcript section/tab elements
- [ ] **🔴 P1:** Click to expand or activate transcript display
- [ ] **🔴 P1:** Extract raw transcript text from DOM elements
- [ ] **🔴 P1:** Handle lessons without available transcripts
- [ ] **🔴 P1:** Implement basic retry logic for failed extractions
- [ ] **🔴 P1:** Test extraction with 5-10 different courses

---

## 🟡 MEDIUM PRIORITY - Essential Features (Complete After Red)

### Phase 2: Enhanced Extraction (Week 5)

#### 🟡 3.1 Advanced Transcript Processing (Week 5)
- [ ] **🟡 P2:** Implement dynamic content handling with scrolling
- [ ] **🟡 P2:** Use `MAX_SCROLL_ROUNDS` configuration for scroll limits
- [ ] **🟡 P2:** Implement `KEEP_TIMESTAMPS` functionality
- [ ] **🟡 P2:** Add `SINGLE_PASS_THRESHOLD` optimization
- [ ] **🟡 P2:** Clean and format extracted text consistently
- [ ] **🟡 P2:** Handle different transcript formats (speaker labels, time codes)
- [ ] **🟡 P2:** Develop robust element selection strategies with fallbacks

### Phase 3: File Generation (Week 6)

#### 🟡 3.2 Markdown Generation (Week 6)
- [ ] **🟡 P2:** Create `Services/MarkdownGenerator.cs`
- [ ] **🟡 P2:** Implement filename sanitization for cross-platform compatibility
- [ ] **🟡 P2:** Create course directory structure (course-name/lessons/)
- [ ] **🟡 P2:** Generate markdown file for each lesson with metadata
- [ ] **🟡 P2:** Create `README.md` with course overview
- [ ] **🟡 P2:** Generate `full-transcript.md` with complete content
- [ ] **🟡 P2:** Build table of contents with navigation links
- [ ] **🟡 P2:** Implement proper heading hierarchy (H1, H2, H3)

### Phase 4: AI Integration (Weeks 7-8)

#### 🟡 4.1 OpenAI Integration Setup (Week 7)
- [ ] **🟡 P2:** Create `Services/OpenAIService.cs`
- [ ] **🟡 P2:** Set up OpenAI client with API key from configuration
- [ ] **🟡 P2:** Load instruction file from `SUMMARY_INSTRUCTION_PATH`
- [ ] **🟡 P2:** Handle missing instruction files with default fallback
- [ ] **🟡 P2:** Implement single lesson summarization
- [ ] **🟡 P2:** Add basic API error handling and retry logic
- [ ] **🟡 P2:** Implement rate limiting to respect OpenAI limits

#### 🟡 4.2 Advanced AI Summarization (Week 8)
- [ ] **🟡 P2:** Implement transcript chunking using `MAP_CHUNK_SIZE` and `MAP_CHUNK_OVERLAP`
- [ ] **🟡 P2:** Create map-reduce pattern for long transcripts
- [ ] **🟡 P2:** Generate comprehensive course-level summaries
- [ ] **🟡 P2:** Integrate AI summaries into markdown generation
- [ ] **🟡 P2:** Implement token counting and cost calculation
- [ ] **🟡 P2:** Add cost estimation before processing

---

## 🟢 LOW PRIORITY - Polish & Enhancement (Complete After Yellow)

### Phase 5: Batch Processing (Week 9)

#### 🟢 5.1 Multi-Course Processing (Week 9)
- [ ] **🟢 P3:** Implement `urls.txt` file reading and parsing
- [ ] **🟢 P3:** Process courses sequentially from URL list
- [ ] **🟢 P3:** Add progress tracking across multiple courses
- [ ] **🟢 P3:** Implement processing queue and status tracking
- [ ] **🟢 P3:** Create resume functionality for interrupted processing
- [ ] **🟢 P3:** Generate batch completion reports
- [ ] **🟢 P3:** Add delays between courses to respect rate limits

### Phase 6: CLI Enhancement (Week 10)

#### 🟢 6.1 Command Line Interface (Week 10)
- [ ] **🟢 P3:** Implement `--check-config` validation command
- [ ] **🟢 P3:** Add `--reset-session` functionality
- [ ] **🟢 P3:** Create comprehensive help documentation
- [ ] **🟢 P3:** Add user-friendly progress indicators
- [ ] **🟢 P3:** Implement proper exit codes for different scenarios
- [ ] **🟢 P3:** Create confirmation prompts for destructive operations

### Phase 7: Robustness (Week 11)

#### 🟢 7.1 Error Handling & Logging (Week 11)
- [ ] **🟢 P3:** Install and configure Serilog logging framework
- [ ] **🟢 P3:** Set up structured logging with context information
- [ ] **🟢 P3:** Implement comprehensive error handling for all components
- [ ] **🟢 P3:** Add performance monitoring and metrics
- [ ] **🟢 P3:** Create detailed debug logging for troubleshooting
- [ ] **🟢 P3:** Handle partial failures in batch processing gracefully

### Phase 8: Testing & Distribution (Week 12)

#### 🟢 8.1 Testing & Documentation (Week 12)
- [ ] **🟢 P3:** Write unit tests for configuration and core logic
- [ ] **🟢 P3:** Create integration tests for API interactions
- [ ] **🟢 P3:** Test complete workflow with real LinkedIn courses
- [ ] **🟢 P3:** Write comprehensive README.md with setup instructions
- [ ] **🟢 P3:** Create troubleshooting guide for common issues
- [ ] **🟢 P3:** Create setup scripts (`setup.bat`, `setup.sh`)
- [ ] **🟢 P3:** Test installation process on clean systems

---

## 🔄 ITERATIVE TASKS (Throughout Development)

### Continuous Quality Assurance
- [ ] **🟡 Ongoing:** Test extraction with various course types and formats
- [ ] **🟡 Ongoing:** Monitor memory usage and performance during development
- [ ] **🟡 Ongoing:** Validate secure handling of API keys and credentials
- [ ] **🟡 Ongoing:** Ensure cross-platform compatibility (Windows, macOS, Linux)
- [ ] **🟡 Ongoing:** Follow C# naming conventions and code quality standards

---

## 🎯 MILESTONE CHECKPOINTS

### 🔴 Critical Milestone 1 (End of Week 2)
**Must Complete Before Proceeding:**
- Configuration system working
- LinkedIn session management functional
- Can authenticate and save sessions
- Basic browser automation operational

### 🔴 Critical Milestone 2 (End of Week 4) 
**Must Complete Before Proceeding:**
- Can extract transcripts from basic courses
- Session persistence working reliably
- Course and lesson discovery functional
- Basic error handling in place

### 🟡 Essential Milestone 3 (End of Week 6)
**Must Complete Before Adding AI:**
- Markdown generation working
- File organization structure complete
- Advanced transcript extraction reliable
- Can process complete courses end-to-end

### 🟡 Essential Milestone 4 (End of Week 8)
**Must Complete Before Batch Processing:**
- OpenAI integration functional
- Custom instruction loading working
- AI summarization producing quality output
- Cost estimation and rate limiting in place

---

## 🚨 DEPENDENCY WARNINGS

### Critical Dependencies (Cannot Proceed Without)
1. **🔴 LinkedIn Session Management** → All extraction depends on this
2. **🔴 Basic Transcript Extraction** → Core functionality foundation
3. **🟡 Markdown Generation** → Required before AI integration
4. **🟡 OpenAI Integration** → Needed before batch processing optimization

### Blocking Risks
- **LinkedIn UI Changes:** Could break extraction logic
- **Session Expiration:** Must handle gracefully to avoid re-work
- **OpenAI API Issues:** Need fallback strategies for AI failures

---

## 📊 PRIORITY RATIONALE

### 🔴 RED (High Priority) - Foundation First
These tasks establish the core functionality without which nothing else works. Focus on getting a minimal viable extraction pipeline working before adding features.

### 🟡 YELLOW (Medium Priority) - Essential Features
These tasks add the key value propositions (AI summarization, file organization) that differentiate this tool from manual extraction.

### 🟢 GREEN (Low Priority) - Polish & UX
These tasks improve user experience and robustness but aren't required for core functionality. Can be deferred if timeline is tight.

---

## 🎯 RECOMMENDED EXECUTION ORDER

1. **Start with ALL 🔴 RED tasks in order** - Don't move to yellow until red is complete
2. **Complete 🟡 YELLOW tasks before any green** - These provide the core value
3. **Add 🟢 GREEN tasks for polish** - Only after core functionality is solid
4. **Handle 🔄 ITERATIVE tasks continuously** - Don't defer quality and testing

This prioritization ensures you have a working product at each stage, with the highest-risk/highest-value work completed first.

---

## 📊 PROGRESS SUMMARY

*Last Updated: 2025-08-18*
*Total Tasks: 91*
*Completed: 25*
*In Progress: 0*
*Blocked: 0*

### Current Phase: Week 3 - Basic Navigation & Course Discovery ✅
- Project structure and configuration complete
- LinkedIn session management fully implemented
- Browser automation with Playwright working
- **Course URL validation and parsing complete**
- **Course navigation with retry logic implemented**
- **Metadata extraction with fallback selectors done**
- **Lesson discovery and enumeration working**
- **75 unit tests created and passing**
- **Test output cleaned with conditional logging**
- Ready for Week 4: Basic Transcript Extraction