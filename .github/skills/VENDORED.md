# Vendored Angular skills

`angular-developer` and `angular-new-app` are copied unmodified from the Angular team's official
repository so Copilot can load them without a network call.

- Source: https://github.com/angular/skills
- Pinned commit: `d422d3c17e27c9860f3df91b6e617ad80970ba05` (fetched 2026-10-12)
- License: MIT, Copyright 2026 Google LLC

## Refreshing

```powershell
git clone --depth 1 https://github.com/angular/skills $env:TEMP\angular-skills
Copy-Item $env:TEMP\angular-skills\angular-developer .github\skills\ -Recurse -Force
Copy-Item $env:TEMP\angular-skills\angular-new-app .github\skills\ -Recurse -Force
```

Then update the pinned commit above. Do not hand-edit the vendored folders; put project-specific
guidance in `new-angular-feature` or `responsive-material-table` instead.

## Precedence

Upstream skills describe generic Angular. Where they differ from this repository, the project
instructions in `.github/copilot-instructions.md` and `.github/instructions/` win
(for example: signal stores instead of ad-hoc state services, `templateUrl`/`styleUrl` always).
