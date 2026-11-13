# Future Brief: Branch-Aware Uniqueness and Deleted-Branch Sweeper

Status: Proposed (deferred during the branch-aware review persistence work)
Related: branch-aware code analysis and AI review persistence

## Context

Code analysis (`RepoFileAnalysis`) and AI review records (`Findings`,
`ReviewContexts`, `ReviewUsage`, `RepositoryScans`, `Queue`) are now branch-aware:
each row carries a `Branch` column (defaulting to `''` for legacy/detached HEAD
records). The immediate goal — recording the git branch alongside every analysis
and review — is complete and tested.

Two follow-up work items remain. This brief captures the decision space so they
can be picked up later.

## Work item 1: Make uniqueness constraints branch-aware

### Current state

Three tables have uniqueness/deduplication behaviour that is **not** yet branch-aware:

| Table | Current unique constraint | Problem |
|-------|----------------------------|---------|
| `RepoFileAnalysis` | `(RepoFileId, ReviewTypeId)` | A named-branch upsert can overwrite another branch's analysis record. |
| `Queue` | `(RepoFileId, ReviewTypeId)` | The same file/review-type across branches can collide in the transient queue. |
| `Findings` | `(RepositoryId, Fingerprint)` | Identical findings across branches dedupe to the first-seen row. |

### Why it was deferred

The system currently operates against one checked-out branch at a time
(sequential checkout). Under that model, cross-branch collisions are rare and the
additive `Branch` column preserves existing data without breaking changes.

### Decision needed

Extend the unique keys to include `Branch`:

- `RepoFileAnalysis`: `(RepoFileId, ReviewTypeId, Branch)`
- `Queue`: `(RepoFileId, ReviewTypeId, Branch)`
- `Findings`: `(RepositoryId, Fingerprint, Branch)`

**Considerations**

- Legacy `''` rows must still be handled (they represent "unknown" and must not be
  deleted by the sweeper).
- SQLite unique constraints treat `''` as a normal value, so the migration must not
  break existing dedupe expectations.
- `Findings` dedupe is intentionally cross-branch today (identical issues converge).
  Adding `Branch` to the fingerprint key changes that behaviour; decide whether that
  is desired or whether dedupe should remain repository-wide while `Branch` is just
  metadata.
- `Queue` is transient; branch-aware keys only matter if concurrent checkouts ever
  become a supported workflow.

## Work item 2: Deleted-branch sweeper

### Goal

Scan registered repositories for deleted git branches and automatically remove
rows belonging to those branches. Rationale: a review on a deleted branch either
merged (and will be re-reviewed on main/dev) or was abandoned (and is worthless).

### Scope

Sweep records whose `Branch` is not empty and no longer exists as a branch in the
repository.

Tables to sweep:

- `RepoFileAnalysis`
- `Findings`
- `ReviewContexts`
- `ReviewUsage`
- `RepositoryScans`
- `Queue` (pending items for deleted branches)

### Key decisions to make

1. **Branch existence source**
   - Read the local `.git` refs (e.g. `.git/refs/heads/**` and/or `packed-refs`),
	 consistent with the existing `GitBranchProvider` no-CLI approach; or
   - Introduce an opt-in git CLI dependency (not currently used anywhere).

2. **Safety rules**
   - Never delete `Branch = ''` rows (legacy/unknown/detached HEAD).
   - Only sweep branches that are provably deleted (avoid deleting on transient
	 ref read failures — fail safe, i.e. keep rows on uncertainty).

3. **Trigger & scheduling**
   - On-demand manual action vs. background worker vs. part of the existing
	 `RepositoryProcessor`/`ReviewWorkerService` cadence.

4. **Cascades & referential integrity**
   - Confirm deletion order (e.g. findings/context/usage before analysis).
   - Decide whether `Findings` referenced by other tables (e.g. resolution/outcome
	 linkage) require soft-delete instead of hard delete.

5. **Auditability**
   - Log a summary of what was removed per repository/branch (counts, timestamps).
   - Consider retaining a tombstone/audit record for accountability.

## Open questions

- Is concurrent checkout (multiple branches of the same repository processed
  simultaneously) a future requirement? If yes, Work item 1 becomes necessary and
  the sweeper must be branch-safe under concurrency.
- Should `Findings` deduplication remain repository-wide or become branch-scoped?
- Does the sweeper need to run per-repository-lock (it likely should reuse
  `RepositoryScanLockRegistry`)?

## Recommended sequencing

1. Decide the `Findings` dedupe semantics (branch-scoped vs repository-wide).
2. Implement branch-aware unique constraints and migration.
3. Implement the sweeper with fail-safe branch detection and `''` protection.
4. Add tests covering legacy rows, detached HEAD, deleted branch cleanup, and
   read-failure fallback.
