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
- **Require status checks to pass** — the **Build** workflow's job(s) only. Do
  **not** add the **Deploy** workflow (or any of its jobs) as a required
  check: it only runs on push to `main`, never on a PR, so requiring it
  deadlocks every PR waiting on a check that cannot run yet.
- Do **not** require multiple reviewers or code owner reviews — on a
  single-maintainer repo, nobody can approve their own PR, so this deadlocks
  every PR the maintainer opens.
- Do **not** require linear history.
- **Block force-push and branch deletion** on `main` — this is the actual
  protection; leave "include administrators" off so the maintainer keeps a
  direct-push escape hatch. There is no local pre-push hook in this repo
  enforcing checks before a push reaches GitHub — CI on the PR is the only
  gate today.

## Local setup

See the main [README](../README.md) for prerequisites and run instructions (API, frontend, Docker).

## Pushing to GitHub

Before your first push:

1. Ensure no build artifacts or secrets are committed (see root `.gitignore`).
2. If the repo previously had large files (>100MB) in history and push was rejected, clean history (e.g. `git filter-repo` or BFG) and force-push, or start from a fresh commit that excludes those paths.
