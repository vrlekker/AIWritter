const resultsEl = document.getElementById("results");
const searchInput = document.getElementById("searchInput");
const metaInfo = document.getElementById("metaInfo");
const feedTabsEl = document.getElementById("feedTabs");
const feedDescriptionEl = document.getElementById("feedDescription");
const heroStatsEl = document.getElementById("heroStats");
const template = document.getElementById("itemCardTemplate");

const state = {
  feeds: [],
  activeFeedId: "",
  generatedAtUtc: "",
  totalItemCount: 0,
};

async function init() {
  try {
    const response = await fetch("./data/trending-repos.json", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`Failed to load data: ${response.status}`);
    }

    const payload = await response.json();
    const normalized = normalizePayload(payload);

    state.feeds = normalized.feeds;
    state.generatedAtUtc = normalized.generatedAtUtc;
    state.totalItemCount = normalized.totalItemCount;
    state.activeFeedId = resolveInitialFeed(normalized.featuredFeedId, normalized.feeds);

    renderHeroStats();
    renderTabs();
    renderActiveFeed();
  } catch (error) {
    metaInfo.textContent = "Unable to load data. Run the engine workflow to generate trending-repos.json.";
    resultsEl.innerHTML = "";
  }
}

function normalizePayload(payload) {
  const feeds = payload.feeds || payload.Feeds;
  const generatedAtUtc = payload.generatedAtUtc || payload.GeneratedAtUtc || "";
  const featuredFeedId = payload.featuredFeedId || payload.FeaturedFeedId || "";

  if (Array.isArray(feeds) && feeds.length > 0) {
    const normalizedFeeds = feeds.map(normalizeFeed);
    return {
      feeds: normalizedFeeds,
      generatedAtUtc,
      featuredFeedId,
      totalItemCount: normalizedFeeds.reduce((sum, feed) => sum + feed.items.length, 0),
    };
  }

  const legacyItems = payload.items || payload.Items || [];
  const fallbackFeed = {
    id: "repositories",
    title: "Repositories",
    description: "Repository feed",
    actionLabel: "View Repository",
    items: legacyItems.map(normalizeLegacyRepo),
  };

  return {
    feeds: [fallbackFeed],
    generatedAtUtc,
    featuredFeedId: fallbackFeed.id,
    totalItemCount: fallbackFeed.items.length,
  };
}

function normalizeFeed(feed) {
  const items = feed.items || feed.Items || [];
  return {
    id: feed.id || feed.Id,
    title: feed.title || feed.Title,
    description: feed.description || feed.Description,
    actionLabel: feed.actionLabel || feed.ActionLabel || "Open",
    items: items.map(normalizeItem),
  };
}

function normalizeItem(item) {
  return {
    title: item.title || item.Title || "Untitled",
    subtitle: item.subtitle || item.Subtitle || "",
    summary: item.summary || item.Summary || "",
    url: item.url || item.Url || "#",
    category: item.category || item.Category || "Update",
    metric: item.metric || item.Metric || "",
    publishedAtUtc: item.publishedAtUtc || item.PublishedAtUtc || "",
  };
}

function normalizeLegacyRepo(item) {
  const normalized = normalizeItem({
    title: item.name || item.Name,
    subtitle: `@${item.owner || item.Owner || "unknown"}`,
    summary: item.readmeSummary || item.ReadmeSummary || item.description || item.Description,
    url: item.htmlUrl || item.HtmlUrl,
    category: item.language || item.Language || "Repository",
    metric: `${(item.stars || item.Stars || 0).toLocaleString()} stars`,
    publishedAtUtc: item.lastUpdatedUtc || item.LastUpdatedUtc,
  });

  return normalized;
}

function resolveInitialFeed(featuredFeedId, feeds) {
  const params = new URLSearchParams(window.location.search);
  const requestedFeed = params.get("feed");

  if (requestedFeed && feeds.some((feed) => feed.id === requestedFeed)) {
    return requestedFeed;
  }

  if (featuredFeedId && feeds.some((feed) => feed.id === featuredFeedId)) {
    return featuredFeedId;
  }

  return feeds[0]?.id || "";
}

function renderHeroStats() {
  const generated = state.generatedAtUtc ? new Date(state.generatedAtUtc).toLocaleString() : "unknown";
  const stats = [
    `${state.feeds.length} feeds`,
    `${state.totalItemCount} live items`,
    `Updated ${generated}`,
  ];

  heroStatsEl.innerHTML = "";
  stats.forEach((label) => {
    const chip = document.createElement("span");
    chip.className = "stat-chip";
    chip.textContent = label;
    heroStatsEl.appendChild(chip);
  });
}

function renderTabs() {
  feedTabsEl.innerHTML = "";

  state.feeds.forEach((feed) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = feed.id === state.activeFeedId ? "feed-tab is-active" : "feed-tab";
    button.textContent = `${feed.title} (${feed.items.length})`;
    button.addEventListener("click", () => {
      state.activeFeedId = feed.id;
      searchInput.value = "";
      renderTabs();
      renderActiveFeed();
      syncUrl(feed.id);
    });
    feedTabsEl.appendChild(button);
  });
}

function renderActiveFeed() {
  const feed = getActiveFeed();
  if (!feed) {
    return;
  }

  const generated = state.generatedAtUtc ? new Date(state.generatedAtUtc).toLocaleString() : "unknown";
  metaInfo.textContent = `Updated ${generated} • ${feed.items.length} items in ${feed.title}`;
  feedDescriptionEl.textContent = feed.description;

  const query = searchInput.value.trim().toLowerCase();
  const items = query ? filterItems(feed.items, query) : feed.items;
  renderCards(items, feed.actionLabel, query);
}

function renderCards(items, actionLabel, query) {
  resultsEl.innerHTML = "";

  if (!items.length) {
    const empty = document.createElement("p");
    empty.className = "empty-state";
    empty.textContent = query
      ? "No items match the current search in this feed."
      : "No items are available for this feed yet.";
    resultsEl.appendChild(empty);
    return;
  }

  items.forEach((item, index) => {
    const fragment = template.content.cloneNode(true);
    fragment.querySelector(".item-badge").textContent = item.category;
    fragment.querySelector(".item-metric").textContent = item.metric;
    fragment.querySelector(".item-title").textContent = item.title;
    fragment.querySelector(".item-summary").textContent = item.summary;
    fragment.querySelector(".item-subtitle").textContent = item.subtitle;

    const link = fragment.querySelector(".item-link");
    link.href = item.url;
    link.textContent = actionLabel;

    const card = fragment.querySelector(".item-card");
    card.style.animationDelay = `${index * 50}ms`;

    resultsEl.appendChild(fragment);
  });
}

function filterItems(items, query) {
  return items.filter((item) => {
    const combined = `${item.title} ${item.subtitle} ${item.summary} ${item.category} ${item.metric}`.toLowerCase();
    return combined.includes(query);
  });
}

function getActiveFeed() {
  return state.feeds.find((feed) => feed.id === state.activeFeedId);
}

function syncUrl(feedId) {
  const url = new URL(window.location.href);
  url.searchParams.set("feed", feedId);
  history.replaceState({}, "", url);
}

searchInput.addEventListener("input", () => {
  renderActiveFeed();
});

init();
