# Security and privacy

This repository is a sanitized portfolio snapshot. It does not include the shop database, production credentials, customer records, or the original private Git history. Source visibility is not a substitute for access control, and removing authentication code would make the application less safe.

Payments are manually verified transfers. The application does not collect card numbers or CVVs. Do not add card fields or put payment credentials in this repository. A future card integration needs a hosted payment provider and a separate security review.

## Controls and validation

- Owner-only administrative endpoints, password hashing, owner two-factor authentication, session revocation and login rate limits.
- Customer order isolation and protected, HttpOnly guest-order cookies.
- Antiforgery validation for modifying requests, secure cookies in production, HTTPS enforcement and no-store API responses.
- A 128 KiB default request body limit before model binding. Product image endpoints explicitly allow up to 16 MiB per request and validate individual file size/type.
- HTML responses block framing, plugins and cross-origin form submissions. This is a partial CSP, not a complete script injection defense.

Run the database-free HTTP boundary checks from `so.api`:

```sh
dotnet run --project ../tests/SecurityBoundaryChecks -c Release
```

These check normal and oversized JSON (including chunked requests), the explicit upload-size override, response headers, owner authorization and antiforgery rejection. The existing commerce and identity checks cover additional scenarios; see `tests/README.md`.

## Before hosting

Keep secrets in deployment configuration or a secret store, outside the web root and Git history. Configure production HTTPS, protected persistent key storage and restricted database access. Review supported runtime/dependency updates, backups and host logging; avoid logging passwords, reset links, payment details or customer request bodies. Test the actual deployed configuration. This source review is not a penetration test or a guarantee against compromise.

If a real credential is ever published, revoke or rotate it first; deleting the latest file alone does not remove copies from Git history.

## Reporting

Do not post credentials, customer data or exploit details in public issues. Use GitHub private vulnerability reporting if it is enabled for this repository, or arrange a private reporting channel with the owner first.
