# LinkedIn Learning AI Course Summarizer

An automated tool that extracts transcripts from LinkedIn Learning courses and generates AI-powered summaries using OpenAI's GPT models.

## Features

- 🔐 **Persistent Session Management** - Login once, process multiple courses
- 📝 **Transcript Extraction** - Automatically extracts lesson transcripts
- 🤖 **AI-Powered Summaries** - Generates comprehensive course summaries using OpenAI
- 📁 **Organized Output** - Creates structured markdown files for each course
- ⚡ **Batch Processing** - Process multiple courses from a URL list

## Prerequisites

- .NET 6.0 SDK or higher
- OpenAI API key
- LinkedIn Learning account (with access to courses you want to summarize)

## Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/LinkedInLearningSummarizer.git
   cd LinkedInLearningSummarizer
   ```

2. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

3. **Install Playwright browsers**
   ```bash
   pwsh bin/Debug/net6.0/playwright.ps1 install
   ```
   
   On macOS/Linux:
   ```bash
   ./bin/Debug/net6.0/playwright.sh install
   ```

4. **Configure environment**
   
   Copy `.env.example` to `.env` and update with your settings:
   ```bash
   cp .env.example .env
   ```
   
   Edit `.env` file:
   ```env
   # Required: Your OpenAI API key
   OPENAI_API_KEY=sk-your-actual-api-key-here
   
   # Optional: Customize other settings as needed
   OPENAI_MODEL=gpt-4o-mini
   OUTPUT_TRANSCRIPT_DIR=./output
   ```

## Usage

### Basic Usage

1. **Create a URL list file** (e.g., `urls.txt`)
   ```
   https://www.linkedin.com/learning/course-name-1
   https://www.linkedin.com/learning/course-name-2
   # Comments are supported
   ```

2. **Run the summarizer**
   ```bash
   dotnet run urls.txt
   ```

3. **First-time authentication**
   - On first run, a browser window will open
   - Log into LinkedIn Learning manually
   - The session will be saved for future use

### Commands

```bash
# Process courses from URL file
dotnet run urls.txt

# Check configuration
dotnet run -- --check-config

# Reset LinkedIn session
dotnet run -- --reset-session

# Show help
dotnet run -- --help
```

## Configuration Options

All configuration options can be set via environment variables or `.env` file:

| Variable | Default | Description |
|----------|---------|-------------|
| `OPENAI_API_KEY` | (required) | Your OpenAI API key |
| `OPENAI_MODEL` | `gpt-4o-mini` | OpenAI model to use |
| `OUTPUT_TRANSCRIPT_DIR` | `./output` | Directory for output files |
| `SESSION_PROFILE` | `linkedin_session` | Browser session profile name |
| `HEADLESS` | `true` | Run browser in headless mode |
| `KEEP_TIMESTAMPS` | `false` | Preserve timestamps in transcripts |
| `MAX_SCROLL_ROUNDS` | `10` | Max scroll attempts for loading content |
| `SINGLE_PASS_THRESHOLD` | `5000` | Character threshold for single-pass processing |
| `MAP_CHUNK_SIZE` | `4000` | Token chunk size for AI processing |
| `MAP_CHUNK_OVERLAP` | `200` | Overlap between chunks |
| `SUMMARY_INSTRUCTION_PATH` | `./prompts/summary.txt` | Custom AI instruction file |

## Output Structure

```
output/
└── course-name/
    ├── README.md              # Course overview with AI summary
    ├── lessons/
    │   ├── 01-lesson-name.md
    │   ├── 02-lesson-name.md
    │   └── ...
    └── full-transcript.md     # Complete course transcript
```

## Custom AI Instructions

To customize how summaries are generated, create a file at `./prompts/summary.txt` with your instructions. The AI will use these instructions when generating summaries.

## Security Notes

- **Never commit your `.env` file** - It contains sensitive API keys
- API keys are masked in console output for security
- Browser sessions are stored locally and should not be shared

## Troubleshooting

### "OPENAI_API_KEY is required" error
- Ensure your `.env` file exists and contains a valid OpenAI API key
- Check that the key starts with `sk-`

### Browser doesn't open for login
- Ensure Playwright browsers are installed: `pwsh bin/Debug/net6.0/playwright.ps1 install`
- Try resetting the session: `dotnet run -- --reset-session`

### Transcript extraction fails
- Verify you have access to the course on LinkedIn Learning
- Check that your session is still valid
- Some courses may not have transcripts available

## Development

### Building the project
```bash
dotnet build
```

### Running tests
```bash
dotnet test
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This tool is for personal educational use only. Please respect LinkedIn Learning's terms of service and only process courses you have legitimate access to. The authors are not responsible for any misuse of this tool.