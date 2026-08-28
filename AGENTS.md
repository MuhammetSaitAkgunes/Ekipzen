# Repository Workflow

- Keep local work in small, reviewable commits and regularly compare it with the `ikpro` GitHub remote.
- After a development batch is complete and verified, request one explicit approval for the
  reported synchronization batch. That single approval may cover the named feature-branch
  push, merge into the named target branch, and target-branch push.
- Before requesting batch approval, report the exact source/target refs, commits, changed
  files, verification results, and all Git operations that the approval will authorize.
- Do not expand an approved batch to additional commits, branches, tags, or remotes.
- Never force-push or rewrite shared history unless the user explicitly approves that exact destructive operation.
