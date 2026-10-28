---
name: pr-draft
description: Draft a pull request for the current branch from its commits and diff, show it for approval, then open it with gh pr create. Never commits or pushes.
disable-model-invocation: true
argument-hint: "[extra notes for the PR description]"
---

# Draft and open a pull request

## 1. Check the branch
```bash
git branch --show-current            # must not be main
git status --short                   # uncommitted changes? -> tell the user, stop
git log --oneline main..HEAD         # commits in this PR
git diff --stat main...HEAD
git rev-list --count @{u}..HEAD      # > 0 or no upstream -> ask the user to push, stop
gh pr list --head <branch>           # PR already exists -> show its URL, stop
```
Do not commit or push yourself - those are the user's steps.

## 2. Draft
Read the diff (`git diff main...HEAD`) and write, in English:
- **Title**: imperative, <= 70 chars, describing the whole change (not the last commit).
- **Body**, following `.github/pull_request_template.md`:
  - *Summary* - what and why, 2-4 bullets.
  - *Changes* - grouped by area (Contracts / API / Client / Tests / Tooling).
  - *How to test* - concrete commands and manual steps.
  - *AI assistance* - which Claude Code features were used (agents, skills, hooks, plan mode) and what was verified by hand.
  - Extra notes from the user: $ARGUMENTS

## 3. Approve
Show the title and body to the user and wait for an explicit "yes" (or edits).

## 4. Open
Write the body to a temp file in the scratchpad and run:
```bash
gh pr create --base main --head <branch> --title "<title>" --body-file <file>
```
Reply with the PR URL and suggest the next step: the automatic Claude review on GitHub, or the `code-reviewer` agent locally (`review PR #<n>`).
