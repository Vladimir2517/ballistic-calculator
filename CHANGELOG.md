# Changelog

All notable changes to this project are documented in this file.
Format follows Keep a Changelog and Semantic Versioning.

## [Unreleased]
### Added
- Governance files for process visibility (`CONTRIBUTING.md`, `PROJECT_STATUS.md`).
- GitHub templates for PR and Issues.
- CI workflow with syntax and CLI smoke checks.
- GitHub Pages documentation pipeline with MkDocs (`docs-pages.yml`).
- Documentation site config (`mkdocs.yml`) and docs dependencies (`docs-requirements.txt`).

## [0.1.0] - 2026-04-03
### Added
- Initial ballistic calculator CLI.
- Point-mass trajectory model with drag (G1/G7), RK4 integration.
- Atmospheric model (temperature, pressure, humidity).
- Wind drift and angular corrections (mrad/MOA/clicks).
- Ammunition presets and custom bullet mode.
- GitHub repository setup and default branch migration to `main`.
