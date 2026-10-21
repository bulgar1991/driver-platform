# Security review (step 7, 21 Oct 2026)

Scope: the API, the Angular app, the deployment files. Method: code review against the OWASP Top 10
and ASVS level 1, dependency audits, and tests for each fix.

## Fixed in this step

| # | Finding | Risk | Fix |
| --- | --- | --- | --- |
| 1 | No way to add, remove or reset staff accounts except in the database | Shared passwords, ex-staff keep access | Admin → Staff: accounts with temporary passwords, roles, deactivation, reset |
| 2 | Staff tokens stayed valid 8 h after a password change or deactivation | Revocation impossible | Security stamp in the token, checked on every request (cached 30 s, cleared on change) |
| 3 | No password rules | Weak passwords | 12+ characters, not containing the email or common words; temporary passwords must be changed first, enforced by the API |
| 4 | `X-Forwarded-For` accepted from anyone | IP spoofing in the audit log, rate limits bypassed | Only proxies in `ForwardedHeaders:KnownNetworks` are trusted (the Docker network in production) |
| 5 | Development settings could reach production (fake payments, dev keys, emails to disk) | Bookings "paid" without payment, forgeable tokens | The API refuses to start in Production and lists the problems |
| 6 | No security headers | Clickjacking, MIME sniffing, XSS impact | API: nosniff, DENY framing, no-store, CSP `default-src 'none'`. Site (Caddy): HSTS, strict CSP (scripts from the site only), Permissions-Policy |
| 7 | Critical-CSS inlining added an inline `onload` handler | Would force `unsafe-inline` scripts | Disabled in the production build; checked that `index.html` has no inline script |
| 8 | The browser sent the staff token to every API call, including the public driver and review pages | Token exposure; audit entries credited to the wrong person | The token goes only to `/api/admin` and `/api/auth` |
| 9 | Logins, driver and partner actions were logged as "system" | Weak audit trail | Logged with the staff email, "driver …" or "partner …" |
| 10 | Unpaid bookings stayed open forever | Stale data, blocked references | Cancelled after 48 h or 1 h before pickup (hourly job) |

## Verified and in place

- Passwords: PBKDF2-SHA256, 210 000 iterations. Lockout after 5 failures for 15 min. Login rate limit 10/min/IP.
- Tokens and links: staff JWT (HS256, issuer/audience checked, 1 min skew); client and driver links are
  HMACs (no secret stored); review links and partner keys stored only as SHA-256; constant-time compares.
- Authorization: every admin endpoint has a policy (Staff/Admin); partner endpoints a separate scheme;
  partners only see their own bookings (tested); dispatchers cannot reach pricing, payments, partners, staff.
- Payments: card data never reaches us (Stripe Checkout); amounts read from our database and from
  Stripe, never from the client or the webhook body; Stripe signatures verified; webhooks idempotent.
- Input: data annotations plus business validation; EF Core parameterised queries only; CSV exports
  neutralise formulas; emails HTML-encoded.
- Personal data: driver links show client details only until 12 h after the shift; personal pages
  are `noindex` and disallowed in robots.txt; the database is not exposed outside Docker.
- Dependencies: `dotnet list package --vulnerable` reports none (a vulnerable transitive
  `System.Security.Cryptography.Pkcs` was pinned to 10.0.13 in step 3).

## Open

| Item | Notes |
| --- | --- |
| Frontend packages from rs-fec | `npm audit --omit=dev`: 5 high, all in Firebase / `@angular/fire`, which the app does not import, so they are not in the built site. Removing the unused rs-fec packages (Firebase, Sumsub, social login, Material…) would clear them and shrink installs. |
| Token in localStorage | Readable by any script on the site; mitigated by the strict CSP. An httpOnly cookie session would remove the risk; worth doing if the panel grows. |
| Two-factor sign-in for admins | Recommended before many staff accounts exist. |
| GDPR erasure requests | No "forget this client" action yet; data can be anonymised by hand. Retention periods must be decided (see the draft privacy policy). |
| Penetration test | An external test before or soon after launch. |
| Several API instances | Session revocation is immediate on the server that made the change, 30 s on others; migrations on start assume one instance. |
