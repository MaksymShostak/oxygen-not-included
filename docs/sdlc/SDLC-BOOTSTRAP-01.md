# ONI SDLC bootstrap implementation plan

**Goal:** Embed the accepted working SDLC into ONI, verify and merge it, then create
a separate converter worktree from updated main.
**Architecture:** Preserve the reference Python controls and native Codex/GitHub
boundaries. Adapt only setup, scope selection and ONI instructions; the existing
.NET pipeline retains product build, testing, installation and release ownership.
**Tech stack:** Node 24.20.0, npm 12.0.2, Python 3.14.7, .NET SDK from global.json.
**Spec and authority:** [Accepted handoff](../plans/2026-09-08-sdlc-bootstrap-handoff.md).
Execute inline under the user's no-subagents instruction and existing pre-approvals.

## Checkpoints

- [x] Recheck current worktree, source commit, root licence, hooks and GitHub state.
- [x] Import .sdlc, supporting scripts/tests and generic guidance with source hashes/licences.
- [x] Reproduce changed setup, empty-external-lock and ONI routing requirements before fixes.
- [x] Adapt setup and control selection; retain the imported passing preservation suite.
- [x] Qualify actual local setup and scoped verification, review the diff, and retain gaps.

## Delivery gates

The bootstrap PR and task completion record own these outcomes after this local
implementation checkpoint; do not predict successful publication in its input tree.

1. Create a signed bootstrap commit, verify its signature, publish only origin's
  chore/oni-sdlc-bootstrap, and merge its PR under actual protections after CI.
2. Fast-forward the clean main checkout; create the converter worktree from that
  exact main revision. Keep AGPL code behind an explicit subpackage boundary.

No synthetic pilot, distribution framework, global trust change, MCP service setup,
game installation or product publication is part of this plan.
