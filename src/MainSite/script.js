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
    allRepos = payload.items || [];
    render(allRepos);

    const generated = payload.generatedAtUtc ? new Date(payload.generatedAtUtc).toLocaleString() : "unknown";
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

    fragment.querySelector(".repo-name").textContent = repo.name;
    fragment.querySelector(".repo-stars").textContent = `★ ${repo.stars.toLocaleString()}`;
    fragment.querySelector(".repo-description").textContent = repo.description;
    fragment.querySelector(".repo-summary").textContent = repo.readmeSummary;
    fragment.querySelector(".repo-owner").textContent = `@${repo.owner}`;

    const link = fragment.querySelector(".repo-link");
    link.href = repo.htmlUrl;

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
    const combined = `${repo.name} ${repo.owner} ${repo.description} ${repo.readmeSummary}`.toLowerCase();
    return combined.includes(query);
  });

  render(filtered);
});

init();
