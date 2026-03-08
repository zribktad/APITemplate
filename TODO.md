# TODO

## Observability

- [x] Add observability stack and instrumentation for metrics, tracing, and alerting.
- [x] Add OpenTelemetry for traces, metrics, and correlation across database, HTTP, and cache operations.

## User Workflows

- [ ] Add user registration workflow.
- [ ] Add user lifecycle workflows such as activation, deactivation, and role management.

## Tenant Management

- [ ] Add tenant creation workflow.
- [ ] Add tenant removal workflow.

## Product Data

- [ ] Add workflow for attaching `ProductData` records to products.
- [ ] Support one-to-many relationship where a single product can have multiple `ProductData` entries.

## Notifications

- [ ] Add email notification for user registration.
- [ ] Add email notification for tenant invitation workflow.
- [ ] Add email notification for password reset workflow.
- [ ] Add email notification for user role changes.

## Search

- [ ] Add full-text search for products and categories.
- [ ] Add faceted filtering for search results.

## Background Jobs

- [ ] Add cleanup jobs for expired or orphaned data.
- [ ] Add reindex jobs for search data.
- [ ] Add retry jobs for failed notifications.
- [ ] Add periodic synchronization tasks for external integrations.

## Permissions

- [ ] Add a finer-grained permissions model beyond roles.
- [ ] Add policy-based access control per action and resource.

## File and Media Handling

- [ ] Add file upload support for `ProductData`.
- [ ] Add storage abstraction for local and S3-compatible backends.
- [ ] Add cleanup workflow for orphaned files.
