As a Product Owner, the goal for this UI is **"High Signal, Low Noise."** Since this site is automated, the UI needs to look authoritative and "live." We want a developer-centric aesthetic (Dark Mode by default) that feels like a premium dashboard, not a generic blog.

### 1. The Visual Design Concept
* **Theme:** "Obsidian & Emerald." Deep charcoal backgrounds (#0D1117) with neon green accents (#2EA043) for "Live" status indicators.
* **Typography:** Monospace fonts for data (JetBrains Mono) and clean Sans-Serif (Inter) for readability.
* **Iconography (FontAwesome):**
    * `fa-robot`: For automated summary sections.
    * `fa-bolt`: For trending/high-velocity items.
    * `fa-shield-halved`: For the Cyber Security category.
    * `fa-code-branch`: For GitHub-related data.
    * `fa-clock`: To show "Last Updated" (crucial for trust).

---

### 2. The UI Layout (Wireframe Logic)
1.  **The "Pulse" Header:** A thin top bar showing: `[● LIVE] Last Engine Run: 42 minutes ago | 124 items processed`.
2.  **Category Navigation:** A horizontal "Pill" menu to filter between your 10 categories.
3.  **The Masonry Grid:** Cards for each news item. 
    * **Card Header:** Source Icon + Category Label.
    * **Card Body:** The **Rewritten** catchy title and the 2-sentence summary.
    * **Card Footer:** "View Original" link + "Share" button.

---

### 3. VS Code Copilot Instructions (The Design Prompt)

You can give this specific set of instructions to Copilot to generate the frontend code.

**Prompt for Copilot:**
> "Design a responsive React frontend for my Automated Media Lab. Use **Tailwind CSS** for styling and **FontAwesome** for icons.
> 
> **Layout Requirements:**
> 1. **Header:** Create a sticky header with a dark background. Include a 'Live Status' indicator that pulses green and displays the 'lastUpdated' timestamp from my `master-feed.json`.
> 2. **Navigation:** Create a horizontal scrolling category bar with icons (e.g., `fa-microchip` for AI, `fa-user-shield` for Security).
> 3. **Grid System:** Use a CSS Grid (1 col on mobile, 3 cols on desktop) to display 'News Cards'.
> 4. **Card Component Styling:**
>    - Background: `#161B22` with a subtle border `#30363D`.
>    - Hover State: Border changes to `#2EA043`.
>    - Title: Use a semi-bold font with the rewritten title.
>    - Summary: Use a slightly dimmed text color for the AI-generated summary.
>    - Source Badge: A small badge at the bottom showing the original source name.
> 
> **Logic:**
> - Fetch the data from `src/data/master-feed.json`.
> - Implement a search bar that filters the list by title or category.
> - Ensure the entire UI is 'Dark Mode' only to match a developer dashboard aesthetic."

---

### 4. Interactive "Ghost" Features
To make the site feel more "Premium," have Copilot add these small touches:

* **The "AI Writing" Animation:** When a card loads, give it a very slight fade-in effect to simulate content being "delivered."
* **Source Transparency:** A small "info" icon on each card that, when hovered, shows: *"Original source: [Link]. Rewritten by Gemini 1.5 Flash."* This builds massive credibility.
* **Direct "Copy to Clipboard":** A button on each card to copy the summary instantly (great for people who want to share your summaries on LinkedIn/Twitter).

### 5. Why FontAwesome is the Secret Sauce
Using FontAwesome makes the site look like a professional tool rather than a hobby project.
* **Instruction to Copilot:** *"Add FontAwesome via CDN in the index.html and use the `<i>` tags for all category icons. Ensure the icons have a fixed width so the text aligns perfectly."*

**Does this "Premium Dashboard" direction fit your vision, or do you want it to look more like a classic news site (like the New York Times)?**