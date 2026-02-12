# Contributing

## Development workflow

- Work on **short-lived feature branches**. Do not commit directly to `main`.
- Use **conventional commit messages**:
  - `feat:` new feature
  - `fix:` bug fix
  - `refactor:` code change that neither fixes a bug nor adds a feature
  - `security:` security-related change
  - `chore:` build, tooling, or non-code change
  - `docs:` documentation only
- Open **pull requests** for coherent units of work. Keep PRs focused.
- **Merge only after CI passes.** The GitHub Actions build must succeed.
- **Delete the feature branch** after merging.

## Branch protection (main)

On the repository, configure branch protection for `main`:

- **Require a pull request** before merging.
- **Require status checks to pass** (e.g. the GitHub Actions build job).
- Do **not** require multiple reviewers or code owner reviews.
- Do **not** require linear history.
- Optionally allow administrators to bypass (useful for solo workflow).

## Local setup

See the main [README](../README.md) for prerequisites and run instructions (API, frontend, Docker).

## Pushing to GitHub

Before your first push:

1. Ensure no build artifacts or secrets are committed (see root `.gitignore`).
2. If the repo previously had large files (>100MB) in history and push was rejected, clean history (e.g. `git filter-repo` or BFG) and force-push, or start from a fresh commit that excludes those paths.
