# CI Quality Gates

Every pull request to `main` must pass both jobs in `.github/workflows/tests.yml`:

- `backend-tests`: restore, format verification, build, unit/API/integration/architecture tests and pending EF model-change validation.
- `frontend-tests`: clean install, format verification, lint, production builds and unit/component tests.

GitHub branch protection should require these checks and disallow direct pushes to `main`. A required check must not be skipped or converted to an allowed failure.

## Emergency bypass

An administrator may bypass a required check only to restore a broken production or security-critical service when waiting for the normal gate would materially increase harm. The bypass must be recorded in the pull request with the failing check, reason, approver and rollback plan. A follow-up issue must restore the skipped verification immediately after the incident. Convenience, flaky tests and deadline pressure are not emergencies.

CRM-203 defines this policy and the executable repository gates. Repository-host branch protection is configured alongside publication; it is not represented by a speculative in-repository policy framework.
