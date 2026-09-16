# Repository Workflow

- Keep local work in small, reviewable commits and regularly compare it with the `ekipzen` GitHub remote.
- After a development batch is complete and verified, request one explicit approval for the
  reported synchronization batch. That single approval may cover the named feature-branch
  push, merge into the named target branch, and target-branch push.
- Before requesting batch approval, report the exact source/target refs, commits, changed
  files, verification results, and all Git operations that the approval will authorize.
- Do not expand an approved batch to additional commits, branches, tags, or remotes.
- Never force-push or rewrite shared history unless the user explicitly approves that exact destructive operation.

# Project Memory

## Identity

- MCP server: `msa_project_memory`
- Project key: `ekipzen` — always pass `project="ekipzen"`.
- The product is named **Ekipzen** in code, UI and docs (formerly **İK Pro**; renamed by
  explicit user decision). The Project Memory key `ekipzen` matches the product name.
- The Git remote is named `ekipzen` and points to `https://github.com/MuhammetSaitAkgunes/Ekipzen.git`.

## Local-task fast path

If the task is clearly limited to a single file, component or function, a small styling
change, a typo, or a local implementation detail, and it needs no architecture, business
rule, historical decision, security, tenancy, integration or cross-module context:

- Do not call Project Memory.
- Do not scan the repository broadly; inspect only the relevant files directly.
- If the task turns out to be broader during inspection, switch to the context-heavy flow.

## Context-heavy flow

Use this flow when the task involves any of: architecture, business rules, database,
API contracts, authentication/authorization, tenancy, security, integrations, deployment,
UI conventions, historical decisions, known issues, or cross-module behavior.

1. Start with a narrow `knowledge_search(project="ekipzen", query=..., limit=...)`.
   Keep the limit low (the default of 5 or less) and add `category` when it is obvious.
2. Call `knowledge_get(project="ekipzen", entry_id=...)` only for results that are
   actually relevant.
3. Never preload or bulk-read the whole memory.

## Source of truth

- Source code, migrations and tests are the implemented reality.
- Current plans, specs and explicit user decisions are the product intent.
- Git history is supporting historical evidence; it does not override current implemented
  reality or current product intent.
- Project Memory is durable engineering context that avoids expensive rediscovery;
  it is not an authority over the code.
- If memory conflicts with the source, verify the relevant source files and report the
  discrepancy.
- Documentation may be stale; determine whether a document represents current intent
  before relying on it.

## Memory health

- `knowledge_get` returns `memory_health`. When it is `possibly_stale` or unknown,
  do not treat the entry as wrong automatically: check its `source_files`, determine
  the current reality, and update the entry if needed.
- Normal `knowledge_search` returns only active entries. Archived entries are
  historical context, not current guidance; if one is seen directly, never treat it as
  current behavior.

## Writing and updating memory

Write only long-lived knowledge that is expensive to rediscover:

- architecture invariants
- important business rules
- lasting technical decisions
- API and database contracts
- integration constraints
- deployment invariants
- lasting UI conventions
- verified known issues

Do not write:

- trivial implementation details
- temporary TODOs
- build or test output, or logs
- secrets or credentials
- large code blocks
- simple facts that are easy to find in a single file

Do not write memory automatically at the end of every task. Consider
`knowledge_write` / `knowledge_update` only when:

- new durable engineering knowledge emerged,
- existing durable knowledge changed meaningfully, or
- an existing memory entry no longer matches current reality.

If the task only changed implementation details and no durable knowledge changed,
leave memory untouched.

Before writing, run `knowledge_search(project="ekipzen", ...)` for a similar entry. If the
same concept already exists, prefer `knowledge_update` over creating a duplicate. Include
accurate `source_files` so staleness can be detected.

## Worktree safety

### Canonical Project Memory state

- Project Memory active state and its fingerprint baseline represent the canonical checkout
  registered in `project_registry`.
- Project key: `ekipzen`
- Registered canonical checkout: `C:\Users\Lenovo\OneDrive\Masaüstü\Ekipzen UI\Ekipzen`
- Never move the registry to a temporary Claude/Codex worktree path.

### Worktree-aware reads

- When working inside a Git worktree, every `knowledge_get` call made during context-heavy
  retrieval must pass `workspace_root` set to the output of `git rev-parse --show-toplevel`.
- Never guess `workspace_root` by hand; determine the current repository root reliably.
- Workspace-aware `memory_health` compares the current workspace's source files against the
  entry's **stored fingerprint baseline**. It is **not** a direct comparison with the
  current canonical checkout.
- `possibly_stale` here means: the workspace source differs from the source state against
  which this memory entry was last fingerprinted. In that case, do not apply the memory
  guidance until the relevant `source_files` have been verified.

### Canonical reads

- When working in the canonical checkout, prefer passing `workspace_root` to
  `knowledge_get`; the server can verify that it is the canonical root.
- Calling without `workspace_root` still works for backward compatibility with older
  clients, but agents should send explicit workspace context whenever possible.

### Write/update safety

- Never call `knowledge_write` or `knowledge_update` from inside a Git worktree.
- Never change active Project Memory based on unmerged branch/worktree state.
- If durable knowledge changed:
  1. merge the code into the canonical checkout,
  2. verify the canonical state,
  3. then synchronize memory from the canonical checkout.
- `knowledge_write` / `knowledge_update` calls made in the canonical checkout must also pass
  `workspace_root` set to the canonical repository root.

### Rejection behavior

If the MCP returns `non_canonical_workspace` or `workspace_rejected`, do not bypass it. In
particular, do not:

- retry with the `workspace_root` parameter removed,
- move the registry to the worktree,
- use an unrelated path.

Report the situation to the user instead.

## Maintenance principle

Project Memory must not become a second codebase. Aim for high-value durable knowledge
at a low retrieval token cost: keep entries short, self-contained and non-overlapping.
