# Branching and Commit Conventions

## Trunk

`main` is protected. All work reaches `main` through a pull request; no
direct pushes.

## Branch names

| Kind | Pattern |
|------|---------|
| Feature | `feature/CRM-<id>-<slug>` |
| Fix | `fix/CRM-<id>-<slug>` |
| Chore | `chore/CRM-<id>-<slug>` |
| Docs | `docs/CRM-<id>-<slug>` |

Use lowercase, hyphen-separated slugs (e.g. `feature/CRM-107-repository-workflow`).

## Commit messages

Follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`,
`fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `build:`, `ci:`.

Reference the Linear id in the body or footer:

```
docs: document repository layout and developer workflow

Refs: CRM-107
```

Commit titles are ASCII English even though the product supports Arabic and
RTL. Localized or non-ASCII content, when needed, belongs in the commit body.

## Pull requests

- One Ready story per PR, per
  [implementation-workflow.md](implementation-workflow.md).
- The description links the Linear id, lists the Acceptance Criteria it
  satisfies, and confirms each
  [Definition of Done](definition-of-done.md) item (marking non-applicable
  items `N/A` with a justification).
- A story only moves to Done after the Definition of Done is met.

## Rebasing and merging

- Rebase the feature branch on `main` before requesting review.
- Squash-merge on completion, keeping the Conventional Commit title.
