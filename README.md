# Automated Media Lab (AIWritter)

GitHub-native self-updating static portal for trending C# repositories.

## Architecture

- Frontend: static SPA in `src/MainSite` (served by GitHub Pages)
- Backend: .NET 8 engine in `engine` (executed by GitHub Actions)
- Data store: JSON files committed to the repository

## Workflows

- `engine.yml`: runs every 12 hours and on manual dispatch
- `deploy.yml`: deploys `src/MainSite` to GitHub Pages on pushes to `main`

## Secrets and Variables

Set these in repository Settings:

- Secret: `OPENAI_API_KEY` (optional)
- Variable: `OPENAI_MODEL` (optional, defaults to `gpt-4o-mini`)
- Variable: `SITE_BASE_URL` (optional, used for sitemap base URL)

## Local Run

```bash
dotnet run --project engine/GhostEngine.csproj
```

Output files:

- `src/MainSite/data/trending-repos.json`
- `src/MainSite/sitemap.xml`

## Notes

- If `OPENAI_API_KEY` is missing, the engine uses a deterministic fallback summary.
- GitHub Search API is used as a trending proxy (sorted by stars in recent repos).
