# Token Optimization Report — Portfolio Manager AI Context

**Date:** 2026-07-14  
**Reported issue:** Context window at ~68% and growing with each session.

---

## Root Cause Analysis

### 1. `copilot-instructions.md` — Always-On Overload (FIXED)

|                    | Before | After           |
| ------------------ | ------ | --------------- |
| Lines              | 149    | 53              |
| Est. tokens        | ~1,500 | ~530            |
| Saving per request | —      | **~970 tokens** |

**Problem:** `applyTo: "**"` means this file is injected into the context window on **every single request**, regardless of what you're working on. At 149 lines with architecture diagrams, route tables, and code examples, this was burning ~1,500 tokens of persistent overhead.

**Fix:** Rewrote to 53 lines of only essential, non-discoverable facts (run commands, state pattern rules, backend pitfalls). Everything discoverable by reading code was removed.

---

### 2. `angular-best-practices.instructions.md` — Duplicate of User-Level Instructions (FIXED)

|                               | Before | After             |
| ----------------------------- | ------ | ----------------- |
| Lines                         | 217    | 44                |
| Est. tokens                   | ~1,500 | ~320              |
| Saving per `.ts`/`.html` edit | —      | **~1,180 tokens** |

**Problem:** This file loaded for every `.ts`, `.html`, `.scss`, `.css`, `angular.json`, and `tsconfig.json` edit. At 217 lines it contained full code examples for component structure, signals, templates, services, routing, forms, naming conventions, security, and performance — **all of which are already in the user-level `angular.instructions.md`** that loads for the same file patterns.

This caused double-loading of ~1,500 tokens of identical Angular content on every frontend interaction.

**Fix:** Replaced with a 44-line project-specific delta covering only what differs from standard Angular: component member ordering, state service readonly pattern, and the list of Material modules used in this project.

---

### 3. Missing Skills — Pattern Rediscovery Cost (FIXED)

**Problem:** Without skills, every time you ask to add a new feature or backend service, the agent must read 5-10 existing files to rediscover the patterns before generating code. That's typically 3,000–8,000 tokens of file-reading overhead per scaffolding task.

**Fix:** Created two on-demand skills loaded **only when needed**:

| Skill                  | Trigger                          | Template tokens loaded                       |
| ---------------------- | -------------------------------- | -------------------------------------------- |
| `/new-angular-feature` | "add a new feature/page"         | ~1,200 (vs ~5,000 of reading existing files) |
| `/new-backend-service` | "add a backend service/endpoint" | ~1,200 (vs ~5,000 of reading existing files) |

---

### 4. Missing Backend Instruction (FIXED)

**Problem:** No dedicated `.cs`-file instruction meant backend conventions (Yahoo Finance throttling, enum serialization, EF migration rules) had to be in the always-on `copilot-instructions.md` or rediscovered every session.

**Fix:** Created `dotnet-backend.instructions.md` with `applyTo: "**/*.cs"` — loads only when editing C# files (~360 tokens, targeted).

---

## Token Budget Summary

| Source                                                   | Before     | After  | Saving                                             |
| -------------------------------------------------------- | ---------- | ------ | -------------------------------------------------- |
| `copilot-instructions.md` (every request)                | ~1,500     | ~530   | **970/request**                                    |
| `angular-best-practices.instructions.md` (every FE edit) | ~1,500     | ~320   | **1,180/FE edit**                                  |
| Pattern rediscovery for scaffolding tasks                | ~5,000+    | ~1,200 | **3,800/task**                                     |
| `dotnet-backend.instructions.md` (CS edits only)         | 0 (inline) | ~360   | (net zero, extracted from copilot-instructions.md) |

**Estimated savings:** ~2,000–5,000 tokens per session depending on task type.

---

## Remaining Opportunities

### A. Large docs folder (not yet addressed)

The `docs/` folder contains 22 markdown files totalling ~250 KB. These are **not** auto-included in context but can be accidentally pulled in by semantic search or when the agent tries to understand features. **Recommendation:** Do not reference docs in instructions unless needed for a specific task. Use `/chronicle improve` to check if they're being over-read.

### B. User-level `angular.instructions.md`

Located at `%APPDATA%\Code\User\prompts\angular.instructions.md` — this is ~230 lines and loads for every Angular file in any workspace. It cannot be modified here (it's user-scoped) but is now the canonical Angular reference since the project-level file only holds project deltas.

### C. Session context accumulation

Long conversations accumulate context from earlier turns. For complex multi-step tasks, prefer starting a fresh chat session for each distinct task rather than one long session that carries everything forward.

### D. Avoid attaching large files as context

When using `#file` references in chat, avoid attaching entire model files (`portfolio.models.ts`) or large service files unless specifically needed. Request targeted code blocks instead.

---

## Files Changed

| File                                                          | Change                                                 |
| ------------------------------------------------------------- | ------------------------------------------------------ |
| `.github/copilot-instructions.md`                             | 149 → 53 lines, restructured for density               |
| `.github/instructions/angular-best-practices.instructions.md` | 217 → 44 lines, project delta only                     |
| `.github/instructions/dotnet-backend.instructions.md`         | **NEW** — C# backend conventions, `applyTo: "**/*.cs"` |
| `.github/skills/new-angular-feature/SKILL.md`                 | **NEW** — Feature scaffolding skill                    |
| `.github/skills/new-angular-feature/assets/templates.md`      | **NEW** — Copy-paste templates                         |
| `.github/skills/new-backend-service/SKILL.md`                 | **NEW** — Backend service scaffolding skill            |
| `.github/skills/new-backend-service/assets/templates.md`      | **NEW** — Copy-paste templates                         |
