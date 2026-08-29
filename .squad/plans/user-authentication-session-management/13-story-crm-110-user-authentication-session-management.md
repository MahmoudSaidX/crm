# Story 13 — User Authentication & Session Management (CRM-110)

## Goal

Allow active staff users to sign in, refresh and revoke secure sessions, protect backend and Agent CRM routes, and record secret-free authentication events.

## Implementation

1. Add a staff-identity module owning users, refresh sessions, authentication events, its PostgreSQL schema and migrations.
2. Use hashed passwords, short-lived signed bearer tokens and rotated opaque refresh cookies; validate user/session activity on protected requests and revoke on logout or deactivation.
3. Add rate-limited login/refresh endpoints, HTTPS production enforcement, safe structured security events, and consistent 401 responses.
4. Add an Agent CRM PrimeNG sign-in screen, in-memory access credential handling, refresh-cookie recovery, an auth interceptor and a protected route guard using the existing theme/localization foundation.
5. Do not add user administration, roles, permissions, organization membership, customer identity or downstream application shell behavior.

## Verification

- Backend API and PostgreSQL integration tests cover login, hashing, rotation, expiry/revocation, inactive users, rate limiting and secret-free audit records.
- Frontend tests cover sign-in, guard/interceptor behavior, logout and error handling.
- Run architecture tests, format/lint/build gates and inspect the final diff for secrets, PII/credential logging and downstream scope.
