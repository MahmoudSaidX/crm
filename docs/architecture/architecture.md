# Architecture

Squad CRM starts as a modular monolith with strong business boundaries,
explicit contracts and independent persistence ownership so modules can
be extracted later with less redesign.

ASP.NET Core hosts modules. PostgreSQL/EF Core uses schema-per-module.
Cross-module synchronous calls use explicit application contracts only
when immediate consistency is required; durable side effects favor
integration events + transactional outbox.

Angular provides Agent CRM and Customer Portal surfaces with reusable
shared libraries and business-capability boundaries. PrimeNG is the
primary UI library and is adapted to the Squad CRM design system.

ERP, AI, Email, WhatsApp, SMS and storage use ports/adapters. Reporting
uses dedicated read models rather than cross-module transactional joins.
Local development should avoid paid dependencies where possible.
