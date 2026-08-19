# AI Model Recommendation for Portfolio Manager Development — 2026-08-18

Evaluates current AI-assisted development workflow and recommends optimal model selection
per task type to maximise code quality and minimise token costs going forward.

---

## Current State

This project currently uses **Claude Sonnet 4.6** for all Copilot tasks — planning,
implementation, research, and documentation. Sonnet 4.6 is a capable model but it is not
cost-optimal for every task type in a large, well-structured codebase.

The project has an excellent foundation for AI-assisted development that directly reduces
token costs:

- `copilot-instructions.md` with project-wide patterns (loaded automatically into every prompt)
- Angular component instruction files in `.github/instructions/`
- Skill files for `new-angular-feature` and `new-backend-service` (encode the full two-service pattern)
- Repository memory files documenting codebase structure and key algorithms
- Session memory for in-progress work

These assets significantly reduce the context that must be re-explained in each prompt.
The biggest remaining opportunity is **task routing** — using the right model tier for the
right task rather than defaulting to Sonnet for everything.

---

## Task Types in This Project

| Task Category                      | Complexity | Examples                                                                  |
| ---------------------------------- | ---------- | ------------------------------------------------------------------------- |
| Architecture & planning            | High       | New feature design, algorithm design, system-level decisions              |
| Financial algorithm implementation | High       | TWRR, regression beta vs. TSX, ACB, Sharpe ratio                          |
| Full-stack feature work            | High       | New Angular screen + .NET endpoint following the two-service pattern      |
| Bug investigation                  | Medium     | Understanding existing code + root cause analysis across multiple files   |
| Boilerplate code generation        | Low        | New DTO, simple CRUD service, HTML template following an existing pattern |
| Simple UI changes                  | Low        | CSS fix, label change, add one column to an existing grid                 |
| Documentation                      | Low        | Inline comments, README updates                                           |

---

## Model Comparison ✅ Analysed

| Model                             | Strengths                                                        | Weaknesses                           | Relative Cost |
| --------------------------------- | ---------------------------------------------------------------- | ------------------------------------ | ------------- |
| **Claude Opus 4**                 | Best reasoning, deepest financial domain knowledge, 200K context | Expensive                            | ~15× Sonnet   |
| **Claude Sonnet 4.6** _(current)_ | Strong code quality, context retention, fast                     | Mid-range cost                       | 1× baseline   |
| **Claude Sonnet 4.5**             | Similar to 4.6, slightly different knowledge cutoff              | Marginally less capable than 4.6     | ~0.9×         |
| **Claude Haiku 3.5**              | Very fast, very cheap, excellent pattern-following               | Weaker reasoning, misses edge cases  | ~0.1×         |
| **GPT-4o**                        | Broad knowledge, solid code generation                           | Less elegant context window handling | ~1.2×         |
| **GPT-4o-mini**                   | Very cheap, fast                                                 | Weak at reasoning and judgment       | ~0.05×        |
| **Gemini 1.5 Pro**                | 1M token context window                                          | Less precise in code generation      | ~0.8×         |

---

## Recommended Model by Task ✅ Analysed

### Keep Claude Sonnet 4.6 (Current) For:

- **Full-stack feature implementation** — new screens following the `new-angular-feature` skill
- **Bug investigation and fixes** requiring understanding of multiple interconnected files
- **Code review** and architectural guidance
- **New backend services** following the `new-backend-service` skill
- **Integration work** — connecting existing services in non-obvious ways

**Why**: The project's existing skill files, instructions, and memory reduce per-prompt context significantly.
Sonnet 4.6 handles these tasks well without needing Opus. Switching to a cheaper model for complex
features risks subtle bugs in the financial logic or deviations from the established Angular signal pattern.

---

### Use Claude Haiku 3.5 For:

These tasks are purely mechanical and pattern-following. Haiku is ~10× cheaper and returns in
a fraction of the time for work where reasoning depth is not required.

**Good Haiku tasks**:

- Adding new record types / DTOs following an established signature
- Simple Angular component property additions (one `input()`, one `@if` block)
- CSS/SCSS styling changes scoped to a single component
- Adding a new database column + migration scaffolding
- Adding a new filter or sort column to an existing table following the existing column pattern

**How to prompt Haiku effectively** — be precise and reference a pattern:

> "Add a new `EarningsDate` column to `WatchlistItemDto`. Follow the exact pattern of
> `DividendYield` in the same file. Also add it as `earningsDate?: string` to the
> `WatchlistSummaryDto` interface in `portfolio.models.ts`."

Haiku struggles when asked to understand large amounts of context or make judgment calls.
Give it very precise, pattern-referencing prompts.

**Estimated savings**: 70–85% cost reduction vs. Sonnet for these tasks.

---

### Use Claude Opus 4 For:

- **Financial algorithm design** — designing TWRR, regression beta vs. TSX, ACB calculation engine from first principles
- **Critical architecture decisions** — e.g., "How should we restructure the portfolio data model to support multi-lot ACB tracking without breaking existing features?"
- **Security reviews** — evaluating whether a new authentication or authorization change is safe
- **Complex performance analysis** — understanding why a query is slow with many interacting factors

**Why Opus over Sonnet for financial math**: Errors in Sharpe ratio formulas or ACB calculations can
mislead users into incorrect investment decisions. The higher cost is justified by the accuracy
requirement. Opus 4 has significantly deeper financial domain knowledge.

> **Note**: In VS Code Copilot, Opus 4 availability depends on your subscription tier. When available,
> reserve it for the algorithm design phase, not boilerplate implementation.

---

## Token Optimisation Strategies ✅ Analysed

### 1. Use Plan Mode for Architecture (Already Doing This)

Planning before implementation prevents the most expensive pattern: generating code, discovering it
is wrong, regenerating. The Plan mode workflow in VS Code Copilot is the right approach. Always plan
before implementing features that span both frontend and backend.

### 2. Reference Patterns Explicitly Instead of Describing Them

> ❌ "Create a state service with Angular signals for loading, error, and data. The service should call
> the API service and expose readonly computed signals using Angular's computed() function."

> ✅ "Create a new state service for `performance-analytics` following the exact pattern of
> `scanner-state.service.ts`."

The second prompt achieves the same result in ~80% fewer tokens.

### 3. Always Use the Skill Files for New Features

The `/new-angular-feature` and `/new-backend-service` skills encode the correct two-service pattern,
component structure, and route registration. Always invoke these for new feature scaffolding instead
of describing the pattern from scratch. This consistently saves 300–500 tokens per prompt.

### 4. Scope File Reads Aggressively

When asking for a bug fix, provide only the specific failing file and its direct dependency — not the
entire feature folder. The repository memory files already capture the high-level patterns; use them
as a starting point rather than re-reading entire services.

### 5. Batch Related Changes in One Prompt

> ❌ Three separate prompts for three related property additions.

> ✅ "Make these 3 changes: (1) add `EarningsDate` to `WatchlistItemDto`, (2) add it to
> `WatchlistSummaryDto`, (3) add it as an optional column in the watchlist grid. Follow existing patterns."

Batching saves the prompt startup cost (context loading) that applies to each individual call.

### 6. Keep `copilot-instructions.md` Lean

The current `copilot-instructions.md` is well-structured and concise — keep it that way. Avoid
adding verbose documentation there — it is loaded into every prompt and raises the baseline token cost.
Move detailed feature documentation and patterns to skill files instead, which are only loaded on demand.

### 7. Use the Explore Subagent for Research

The Explore subagent uses faster, cheaper model calls for file reading and searching. Use it to gather
codebase context before implementation rather than having the main agent do both research and generation
in the same turn. This is the pattern already used in Plan mode.

---

## Recommended Model by Development Phase ✅ Analysed

| Phase                             | Recommended Model             | Rationale                                                 |
| --------------------------------- | ----------------------------- | --------------------------------------------------------- |
| Planning & architecture           | Claude Sonnet 4.6 (Plan mode) | Good reasoning; Plan mode prevents wasted code generation |
| Financial algorithm design        | Claude Opus 4                 | Domain accuracy matters; errors can mislead investors     |
| Full-stack feature implementation | Claude Sonnet 4.6             | Complex enough to require Sonnet capability               |
| Pattern-based boilerplate         | Claude Haiku 3.5              | Purely mechanical; ~10× cost saving with no quality loss  |
| Complex bug investigation         | Claude Sonnet 4.6             | Context understanding required across multiple files      |
| Simple bug fix (typo, label, CSS) | Claude Haiku 3.5              | Pattern is obvious; no reasoning required                 |
| Code review                       | Claude Sonnet 4.6             | Judgment and reasoning required                           |
| Documentation                     | Claude Haiku 3.5              | No reasoning needed; pure text generation                 |

---

## Final Recommendation ✅ Analysed

**Primary model**: Keep **Claude Sonnet 4.6** for full-stack feature work, bug fixes, and code review.
It is the right capability/cost balance for Angular + .NET development with an established codebase.

**Introduce Claude Haiku 3.5** for a clearly defined category of low-complexity, pattern-following
tasks. Establishing a habit of routing these to Haiku can reduce total token spend by **30–40%** over
a development cycle without any quality impact on the features that matter.

**Use Claude Opus 4 selectively** for the financial algorithm improvements identified in the Financial
Calculation report (TWRR, regression beta, ACB engine). The accuracy requirement for financial
calculations justifies the premium. One Opus session for algorithm design followed by Sonnet for
implementation is the optimal split.

The project's existing skill infrastructure, instruction files, and memory files are already
well-optimised. The remaining opportunity is not in infrastructure but in consistent **task routing
discipline** — choosing the right tier for the right task every time.

---

## Quick Reference Card

```
New feature (full stack)          → Claude Sonnet 4.6  (use new-angular-feature skill)
Financial algorithm design        → Claude Opus 4
Bug fix (complex, multi-file)     → Claude Sonnet 4.6
Bug fix (simple, single change)   → Claude Haiku 3.5
Boilerplate DTO / CRUD service    → Claude Haiku 3.5
CSS / label / column change       → Claude Haiku 3.5
Code review                       → Claude Sonnet 4.6
Architecture planning             → Claude Sonnet 4.6  (Plan mode)
Documentation                     → Claude Haiku 3.5
```
