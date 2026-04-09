To run a website **entirely on GitHub** while utilizing your C# skills and the "Automated Media Lab" concept, you are essentially building a **"Self-Updating Static Site."** GitHub acts as the **Database** (Git commits/JSON files), the **Backend** (GitHub Actions), and the **Host** (GitHub Pages).

Below is the full implementation plan formatted for **VS Code Copilot**. You can paste these sections directly into a chat or save them as a `.github/copilot-instructions.md` file in your repository.

Assume the environment is a GitHub-hosted ubuntu-latest runner with the .NET 8 SDK pre-installed via actions/setup-dotnet."

---

### Part 1: Architecture Overview (For Copilot Context)
> "We are building a GitHub-native automated portal. The site is a static frontend hosted on GitHub Pages. The backend logic is handled by C# console applications running via GitHub Actions. The data 'database' consists of JSON files stored within the repository. Every time the C# engine runs, it fetches data from external APIs (like GitHub API), updates the JSON files, and commits them back to the repo, triggering a new site deployment."

---

### Part 2: Folder & File Structure
```text
.
├── .github/
│   ├── workflows/
│   │   ├── engine.yml           # Runs the C# data-fetcher on a schedule
│   │   └── deploy.yml           # Deploys the site to GitHub Pages
│   └── copilot-instructions.md  # Rules for AI code generation
├── src/
│   ├── MainSite/                # React or Vanilla JS Frontend
│   │   ├── index.html
│   │   └── data/                # Where the JSON "database" lives
│   │       └── trending-repos.json
├── engine/                      # The C# "Ghost Engine"
│   ├── GhostEngine.csproj
│   ├── Program.cs               # Logic for fetching & processing data
│   └── Models/                  # Data structures for GitHub API
├── .instructions.md             # Custom VS Code Copilot rules
└── README.md
```

---

### Part 3: Feature Requirements (The "What")
1.  **Scheduled Data Fetching:** The C# engine must run every 12 hours.
2.  **GitHub API Integration:** Fetch trending repositories (using `Octokit` or `HttpClient`).
3.  **Content Transformation:** Use an LLM API (e.g., OpenAI/Claude) to summarize the README of the fetched repos.
4.  **Auto-Commit:** The engine must save the summary into `src/MainSite/data/trending-repos.json` and push the change back to the `main` branch.
5.  **Dynamic Frontend:** The website must fetch the local `trending-repos.json` at runtime and render a clean, searchable UI.
6.  **SEO Optimization:** The engine must also update a `sitemap.xml` file dynamically based on the new content.

---

### Part 4: Technical Implementation Plan (The "How")

#### Step 1: The C# Engine (Logic)
**Copilot Prompt:**
> "Create a C# Console App (.NET 8) in the /engine folder. It should use HttpClient to fetch trending C# repositories from the GitHub API. Parse the JSON response, extract the repo name and description, and save the top 5 into a file named `/src/MainSite/data/trending-repos.json`. Use System.Text.Json for serialization."

#### Step 2: The GitHub Action (The "Server")
**Copilot Prompt:**
> "Generate a GitHub Actions workflow (.yml) that runs every 12 hours. It should: 
> 1. Checkout the repo. 
> 2. Setup .NET 8. 
> 3. Run the project in the /engine folder. 
> 4. If files in /src/MainSite/data change, commit them back to the repo using a 'system-bot' identity."

#### Step 3: The Frontend (The "UI")
**Copilot Prompt:**
> "Create a simple index.html and script.js in /src/MainSite. The script should fetch('./data/trending-repos.json') and display the results as a list of cards with the repository name, description, and a 'View on GitHub' link. Use modern CSS for a dark-themed developer aesthetic."

---

### Part 5: Custom .github/copilot-instructions.md
Add this to your repo to ensure Copilot always follows your "GitHub-Only" architecture:

```markdown
# Project Rules: Automated Media Lab
- All backend logic MUST be written in C# (.NET 8).
- Do not suggest external hosting (AWS/Azure). Everything must run on GitHub Actions.
- Data storage must be handled via JSON files in the repository.
- Use 'Octokit' for any complex GitHub API interactions.
- Ensure all GitHub Actions use the 'GITHUB_TOKEN' for authentication where possible.
- The frontend should be a lightweight Single Page Application (SPA).
```

### Why this works:
* **Cost:** $0 (GitHub Free tier covers Actions and Pages for public repos).
* **Maintenance:** Once the C# logic is solid, you never have to touch it. It updates itself.
* **Portability:** The entire business lives in a single folder that you can clone and duplicate for other niches (e.g., "Trending Python Repos," "Weekly Tech News").