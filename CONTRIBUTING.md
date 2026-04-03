# Contributing Guide

## Branching
- Default branch: `main`.
- Work in feature branches: `feature/<short-name>`, `fix/<short-name>`, `chore/<short-name>`.

## Commit standard
Use Conventional Commits:
- `feat: ...`
- `fix: ...`
- `docs: ...`
- `refactor: ...`
- `test: ...`
- `chore: ...`

Examples:
- `feat(cli): add range card export`
- `fix(ballistics): correct transonic drag interpolation`

## Change accounting (required)
Before coding:
- Create/update Issue with goal, scope, and acceptance criteria.
- Update `PROJECT_STATUS.md` in "Current Sprint" or "Planned" section.

Before merge:
- Update `CHANGELOG.md` under `Unreleased` or create a version section.
- Ensure PR checklist is complete.

## Pull requests
- One logical change per PR.
- Include reproducible steps and expected outputs.
- Keep PR focused and reasonably small.
