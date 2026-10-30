# Revathryn

Revathryn is a local-first, incremental code-review orchestration platform. It discovers local repositories, fingerprints source files, runs the retained C# analyser only when needed, and prepares a priority-ordered review queue for later AI review execution phases.

## Solution layout

| Project | Responsibility |
| --- | --- |
| `Revathryn.Contracts` | Public models, interfaces, and enums. All interfaces live here. |
| `Revathryn.Data` | SQLite persistence, schema, and repository implementations. |
| `Revathryn.Engine` | Orchestration: scanning, fingerprinting, scheduling, queue, repository registration, DI wiring. |
| `Revathryn.Review` | Existing C# analyser integration (`ICSharpAnalysisEngine`) plus review-engine stubs. |
| `Revathryn.Resources` | User-facing strings. |
| `Revathryn` | ASP.NET Core host. |
| `*.Tests` | MSTest test projects (all test classes carry `[ExcludeFromCodeCoverage]`). |

## Phase 3 — repository discovery and incremental analysis

### Repository registration

`IRepositoryService` registers local folder repositories only. Registration:

- validates the folder exists and is accessible,
- normalises the path (absolute, trailing separators removed),
- rejects a duplicate physical path case-insensitively,
- persists metadata in SQLite,
- creates the default review definitions (`C# Code Review` and `General Review`) with a computed `ReviewDefinitionVersion`.

Repositories can be enabled or disabled via `SetEnabledAsync`. GitHub and remote repositories are intentionally out of scope.

### Scanning and fingerprinting

`IRepositoryScanner` enumerates files under a repository root and returns a `RepositoryScanResult`:

- configurable excluded directories (defaults: `.git`, `.vs`, `bin`, `obj`, `node_modules`, `packages`, `build`, `dist`),
- configurable included extensions (defaults: `cs`, `json`, `xml`, `config`, `csproj`, `props`, `targets`, `md`, `yml`, `yaml`),
- `/`-normalised repository-relative paths as the persistent file identity alongside `RepositoryId`,
- SHA256 as the authoritative content fingerprint,
- graceful handling of files that become inaccessible during the scan (`ScanFailure`).

`RepoFile` identity is `(RepositoryId, RelativePath)`; absolute paths are never persisted.

### Incremental C# analysis

`RepositoryProcessor` invokes the existing `ICSharpAnalysisEngine` only for discovered `.cs` files whose manifest says they need (re)analysis. Unchanged files are skipped, and a failed analysis is never recorded as successful, so the next scan retries it.

### Review scheduling

`IReviewScheduler` determines which review types require work by comparing the current file `Sha256`, the review definition's `ReviewDefinitionVersion`, and the analysis engine version against the completed `RepoFileAnalysis` record. This implements scenarios A–H from the phase specification: new files, changed files, unchanged files, new review types, edited review definitions, disabled/removed review types, per-file exclusions, and re-enabled review types.

`RepoFileAnalysis` represents only completed review work. Queueing never creates a completed analysis record.

### Review queue

`IReviewQueue` (backed by `Queue`/`IQueueRepository`) is a transient priority queue:

- higher `Priority` processed first, then oldest `CreatedDate`,
- `UNIQUE(RepoFileId, ReviewTypeId)` with `INSERT OR IGNORE` makes enqueueing idempotent,
- priority respects `ReviewLinks.PriorityOverride` and falls back to the review type default,
- no queue status field; entries stay pending until a later phase processes them.

### Orchestration and concurrency

`IRepositoryProcessor` coordinates scan, file reconciliation, removal detection, incremental C# analysis, scheduling, and queue population, returning a `RepositoryScanSummary`. `ProcessAllEnabledAsync` scans every enabled repository.

`RepositoryScanLockRegistry` provides a per-repository `SemaphoreSlim(1,1)`, so a single repository is never scanned concurrently. File removal relies on SQLite foreign-key cascades: deleting a `RepoFile` removes its `ReviewLinks`, `RepoFileAnalysis`, and pending `Queue` rows, while `Findings` survive via `ON DELETE SET NULL`.

## Build and test

```powershell
dotnet build Revathryn.slnx
dotnet test Revathryn.slnx
```

All timestamps are UTC. The codebase uses explicit types (never `var`), Allman braces, one public class per file, and C# 13 features.