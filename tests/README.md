# SO regression checks

Run from the repository root: `node tests/run-commerce.cjs`. Requires .NET 8 and the configured local SQL Server. The runner builds Release, creates uniquely named disposable databases, and tests owner enrollment, two-factor sign-in, one-time recovery, lockout, customer isolation, order pricing and inventory. It never enrolls the real owner or creates orders in the shop database. The API used in tests has separate credentials and a separate database connection.

The three older JavaScript entry points delegate to this runner. Do not run the older backed-up scripts directly: they predate persistent owner authentication. Browser and accessibility checks are separate from this runner.
