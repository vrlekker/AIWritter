const resultsEl = document.getElementById("results");
const searchInput = document.getElementById("searchInput");
const metaInfo = document.getElementById("metaInfo");
const template = document.getElementById("repoCardTemplate");

let allRepos = [];

async function init() {
  try {
    const response = await fetch("./data/trending-repos.json", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Failed to load data: ${response.status}`);
    }

    const payload = await response.json();
    const items = payload.items || payload.Items || [];
    const generatedAtUtc = payload.generatedAtUtc || payload.GeneratedAtUtc;

    allRepos = items;
    render(allRepos);

    const generated = generatedAtUtc ? new Date(generatedAtUtc).toLocaleString() : "unknown";
    metaInfo.textContent = `Last refresh: ${generated} | Repositories: ${allRepos.length}`;
  } catch (error) {
    metaInfo.textContent = "Unable to load data. Run the engine workflow to generate trending-repos.json.";
    resultsEl.innerHTML = "";
  }
}

function render(repos) {
  resultsEl.innerHTML = "";

  if (!repos.length) {
    const empty = document.createElement("p");
    empty.className = "meta";
    empty.textContent = "No repositories match the current search.";
    resultsEl.appendChild(empty);
    return;
  }

  repos.forEach((repo, index) => {
    const fragment = template.content.cloneNode(true);
    const name = repo.name || repo.Name;
    const owner = repo.owner || repo.Owner;
    const description = repo.description || repo.Description;
    const stars = repo.stars || repo.Stars || 0;
    const readmeSummary = repo.readmeSummary || repo.ReadmeSummary;
    const htmlUrl = repo.htmlUrl || repo.HtmlUrl;

    fragment.querySelector(".repo-name").textContent = name;
    fragment.querySelector(".repo-stars").textContent = `★ ${stars.toLocaleString()}`;
    fragment.querySelector(".repo-description").textContent = description;
    fragment.querySelector(".repo-summary").textContent = readmeSummary;
    fragment.querySelector(".repo-owner").textContent = `@${owner}`;

    const link = fragment.querySelector(".repo-link");
    link.href = htmlUrl;

    const card = fragment.querySelector(".repo-card");
    card.style.animationDelay = `${index * 60}ms`;

    resultsEl.appendChild(fragment);
  });
}

searchInput.addEventListener("input", (event) => {
  const query = event.target.value.trim().toLowerCase();
  if (!query) {
    render(allRepos);
    return;
  }

  const filtered = allRepos.filter((repo) => {
    const combined = `${repo.name || repo.Name} ${repo.owner || repo.Owner} ${repo.description || repo.Description} ${repo.readmeSummary || repo.ReadmeSummary}`.toLowerCase();
    return combined.includes(query);
  });

  render(filtered);
});

init();
