# Magnus Drive — driver platform

Booking and dispatch platform for private transfers in Italy, built from the business plan
("Business Plan – Platformă Taxi & Transfer Italia", Oct 2026).

| Part | Stack | Folder |
| --- | --- | --- |
| Client site, admin/dispatcher panel, driver link | Angular 21, Tailwind 3 (BEM + `@apply`), PrimeNG 21 | `frontend/` |
| REST API | ASP.NET Core (.NET 10), EF Core 10, PostgreSQL | `backend/` |

## Roadmap

The steps follow §8 of the business plan. Each step ends with something that can be tested.

| Step | Scope | Status |
| --- | --- | --- |
| 1. Foundation | Solution layout, full database schema, staff login with roles, frontend structure, theme | **Done** |
| 2. Booking & price | Booking form, map, price per km, day surcharges, no-car zones (Venice, ZTL) | **Done** |
| 3. Payments & invoices | Card/PayPal, PDF invoice, confirmation email, refunds with credit notes | **Done** |
| 4. Dispatcher panel | Bookings, shifts, drivers, vehicles, statuses | **Done** |
| 5. Driver link & messages | Personal driver link, WhatsApp, email, reviews | **Done** |
| 6. Partner API | API keys, bookings from other companies and Booking | **Done** |
| 7. Tests & launch | Real-ride tests, security review, backups | **Done** in code; real-ride tests and go-live in [docs/launch-checklist.md](docs/launch-checklist.md) |

## Run locally

Requirements: .NET 10 SDK, Node 24, PostgreSQL 17.

```bash
# API on http://localhost:5000 — migrates and seeds the database on start (Development only)
cd backend
dotnet tool restore
dotnet run --project src/DriverPlatform.Api

# Frontend on http://localhost:4200 — /api is proxied to :5000
cd frontend
npm install
npm start
```

The development connection string is in `backend/src/DriverPlatform.Api/appsettings.Development.json`
(`localhost:5432`, user/password `postgres`). Override it with
`ConnectionStrings__Database=...` if your database differs.

Development admin: `admin@localhost.test` / `change-me-now-123`. Five wrong passwords lock the
account for 15 minutes.

Tests:

```bash
cd backend && dotnet test
cd frontend && npm run test:unit && npm run lint
```

## Backend

```
backend/src/
  DriverPlatform.Domain          entities, enums, value objects — no dependencies
  DriverPlatform.Application     use cases and interfaces (IAppDbContext, ITokenService, …)
  DriverPlatform.Infrastructure  EF Core + Npgsql, migrations, JWT, password hashing, audit log, seed data
  DriverPlatform.Api             controllers, auth policies, rate limiting, health check
backend/tests/DriverPlatform.Tests
```

- Tables follow §6 of the plan: `clients`, `bookings` (with client IP and source), `payments`,
  `invoices`, `drivers`, `driver_documents`, `vehicles`, `assignments` (shifts), `partners`,
  `zones`, `vehicle_rates` / `surcharge_rules` / `extra_fees` (the plan's `pricing_rules`),
  `reviews`, `audit_log`, plus `staff_users`. Names are snake_case and enums are stored as text.
- Roles: `Admin` and `Dispatcher` sign in with email and password (JWT). Drivers get a personal
  link (step 5) and partners an API key (step 6). Only SHA-256 hashes of those tokens are stored.
- Seeded starting tariffs from §4: sedan €10 + €1.60/km, estate €12 + €1.90/km, van €15 + €2.20/km;
  weekend +10%, night +20%, July–August +25% (every year), Italian public holidays +20%. Events
  (Carnival, Biennale) are added as date ranges in the panel.
- Seeded zones: Venice historic centre (no car access, meeting points Piazzale Roma, Tronchetto,
  cruise terminal, Mestre station), ZTL circles for Rome, Milan (Area C) and Florence, and the
  airports FCO, VCE, MXP, FLR and NAP with an arrivals meeting point. Shapes and fees are
  approximate; refine them on the map in **Admin → Zones**.

### Pricing rules (step 2)

1. Fare = base fee + km × rate of the vehicle category. Distance is the driving route from the
   map provider, rounded to 0.1 km.
2. Every active surcharge matching the pickup time (Italian local time) is **added together** and
   applies to the fare: a Saturday night in August is 10% + 20% + 25% = +55%.
3. A return trip booked together gets the return discount (Admin → Pricing → General) on the
   surcharged fare of the return leg.
4. Zone access fees (ZTL) are added once per zone the ride starts or ends in. Extras are fixed:
   child seat (each), each large bag beyond one per passenger, name sign.
5. All amounts include VAT (10% by default, to confirm with the accountant). The booking stores
   the price line by line (`price_lines`), and it never changes after booking.

The booking API recalculates the price itself; if it differs from the total the client saw it
answers 409 with the new quote. A pickup in a no-car zone moves to the nearest meeting point (or
the one the client picks); the route starts there, and the client's exact address is kept for the
driver.

### Payments, invoices and emails (step 3)

Flow: booking (`AwaitingPayment`) → Stripe Checkout (card or PayPal, 3-D Secure handled by
Stripe) → webhook → booking `New` → invoice PDF → confirmation email with the invoice and the
client's booking link. A return trip is paid and invoiced together with the outbound ride.

- **Client link** `/my-booking/{reference}/{token}`: status, pay again if the payment was not
  completed, download the invoice. The token is an HMAC of the booking id (`Links:SigningKey`),
  so nothing secret is stored and the same link works in every email.
- **Webhooks are idempotent**: a repeated event changes nothing. The amount and the paid status
  are read back from Stripe, never taken from the webhook body.
- **Invoices**: numbered without gaps per type and year (`MD-2026-000001`, credit notes
  `NC-2026-000001`), with the seller and buyer frozen at issue time. The PDF is stored, so the
  document never changes. Seller data comes from the `Company` settings.
- **Refunds** (Admin → Payments): full or partial, optionally cancelling the ride. Each refund
  goes through Stripe and gets a credit note, which is emailed.
- **Export**: Admin → Payments → "CSV for the accountant" (invoices and credit notes in a date
  range, semicolon-separated, Italian number format).
- **Emails**: SMTP through MailKit (`Email:Host/Port/Username/Password`). In development they
  are written as `.eml` files to `backend/src/DriverPlatform.Api/App_Data/mail/`.

Development runs with `Payments:Provider = "Fake"`: instead of Stripe, the client sees a test
page at `/dev/payment/…` with "Pay by card" / "Pay with PayPal". Nothing is charged, and the API
refuses those calls when Stripe is configured.

To use Stripe (test mode first):

```bash
cd backend/src/DriverPlatform.Api
dotnet user-secrets set "Payments:Provider" "Stripe"
dotnet user-secrets set "Payments:Stripe:SecretKey" "sk_test_…"
# Forward webhooks to the local API; it prints the whsec_… secret.
stripe listen --forward-to localhost:5000/api/payments/stripe/webhook
dotnet user-secrets set "Payments:Stripe:WebhookSecret" "whsec_…"
```

In production, create the webhook endpoint `https://<api>/api/payments/stripe/webhook` with the
events `checkout.session.completed`, `checkout.session.async_payment_succeeded`,
`checkout.session.async_payment_failed` and `checkout.session.expired`. Enable PayPal under
Settings → Payment methods; Checkout shows the methods enabled there. The API refuses to start
with Stripe selected and the keys missing.

### Dispatcher panel (step 4)

Admins and dispatchers (pricing, zones and payments stay admin-only):

- **Dashboard**: rides to assign in the next 48 h, rides today, on the road, shifts to confirm,
  unpaid bookings, and driver/vehicle documents expired or expiring within 30 days.
- **Bookings**: filters (dates, status, to assign, search by reference, name, phone or flight),
  details with the meeting point instructions, internal notes and status changes. Unpaid
  bookings are hidden unless asked for. Tick bookings to plan a shift with them.
- **Shifts**: one day at a time, rides still to assign on the left. A shift is a driver, a car
  and one or more bookings. On save the API checks:
  - blocks: unpaid or already assigned bookings, a car of a smaller category or with too few
    seats, bags or child seats, documents expired on the day, the driver marked unavailable, and
    another open shift of the same driver or car within 30 minutes;
  - warns: less than 30 minutes between two rides, missing documents, documents expiring soon.
  The editor lists free drivers first and the smallest car that fits.
- **Statuses**: booking `New → Assigned → PickedUp → Completed` or `Cancelled`; shift
  `Draft → Sent → Seen → Confirmed → Completed` or `Cancelled`. Until the driver link exists
  (step 5) the dispatcher records "confirmed by phone", "picked up" and "completed". A shift
  completes by itself once all its bookings are completed; cancelling a shift puts its bookings
  back in the queue; editing a confirmed shift sets it back to draft. Cancelling a booking does
  not refund it: refunds are made in Payments.
- **Drivers / vehicles**: deactivated, never deleted. Phones and plates are unique and
  normalised. Development seeds a demo fleet (`Bootstrap:DemoFleet`): 3 drivers, 3 cars.
- Pages refresh every 30 s.

### Driver link, messages and reviews (step 5)

The flow of business plan §5:

1. The dispatcher creates a shift (draft, invisible to the driver) and presses **Send to driver**.
   The driver gets WhatsApp (and email, if on file) with the **personal link** `/driver/{token}`.
2. The link lists every shift sent to the driver until 12 h after it ends: route on the map,
   meeting point with the driver instructions, the client's exact address, passengers, bags,
   child seats, name sign, flight, client name, phone (call / WhatsApp) and notes. Opening it
   marks the shift **seen**.
3. The driver **confirms** the shift → each client gets the driver's name, phone and car
   (WhatsApp + email, once per driver/car).
4. **Picked up** (accepted from 3 h before the pickup time) and **completed**. The shift completes
   with its last ride.
5. After "completed" the client gets a **one-time review link** `/review/{token}` (email, or
   WhatsApp without email), valid 30 days. Ratings show per driver (Admin → Drivers, Reviews).

Editing a shift the driver already has sends the update at once and asks for a new confirmation;
cancelling it tells the driver. Every message is logged (`notification_logs`) and the shift card
shows what was delivered; a failed message never blocks the dispatcher. The driver link is an HMAC
of the driver id and a version: it never changes unless the dispatcher presses "Make a new link"
(lost phone, driver leaves), which invalidates the old one.

**WhatsApp** uses the WhatsApp Business Cloud API (`WhatsApp:Provider = "Meta"`,
`WhatsApp:PhoneNumberId`, `WhatsApp:AccessToken`). Until the account is approved, development
uses `"Log"`: messages are written to `backend/src/DriverPlatform.Api/App_Data/whatsapp/`.
Messages we start need templates approved by Meta, in every language listed in
`WhatsApp:Languages` (en, it by default; other clients get `DefaultLanguage`):

| Template | Body parameters |
| --- | --- |
| `driver_shift` | {{1}} driver first name, {{2}} shift summary, {{3}} personal link |
| `driver_shift_changed` | {{1}} driver first name, {{2}} what changed, {{3}} personal link |
| `client_driver_details` | {{1}} client first name, {{2}} booking reference, {{3}} pickup time, {{4}} meeting place, {{5}} driver name, phone and car |
| `client_review_request` | {{1}} client first name, {{2}} booking reference, {{3}} review link |

Suggested text for `driver_shift`: "Hello {{1}}, {{2}} Your shifts: {{3}}".

### Partner API (step 6)

For agencies, hotels, cruise companies and Booking (business plan §6). Admin → Partners:

- **API key** `md_{prefix}_{secret}`, shown once; only the prefix and its SHA-256 are stored.
  "Replace key" and "Revoke" take effect at once. Requests send it in `X-Api-Key`.
- **Agreed rate** (e.g. −10 % on the public price) and **commission** for the monthly statement
  (CSV per month: rides, price, commission, net; cancelled rides at zero).
- **Monthly invoicing**: partner bookings skip online payment and arrive as `New` for dispatch.
- **Passenger messages** on or off (some partners talk to their own passengers).
- **Webhook URL** (https): status changes are POSTed with
  `MagnusDrive-Signature: t={unix},v1={hex HMAC-SHA256(secret, "{t}.{body}")}`, plus
  `MagnusDrive-Event` (`booking.created` / `booking.updated`). The body is thin (reference,
  externalReference, status); fetch the booking for details. Rows are written in the same
  transaction as the change (outbox) and retried after 1, 2, 4 … 128 minutes.

Endpoints (`/api/partner/v1`, 120 requests/min per key), described in OpenAPI at
`/api/partner/openapi/partner.json` (public in every environment, partner endpoints only):

| Endpoint | |
| --- | --- |
| `POST /quotes` | prices with the agreed rate; nothing saved |
| `POST /bookings` | `externalReference` is idempotent: 201 created, 200 already existed, 409 price changed |
| `GET /bookings/{reference}` | by our `MD-…` or their `externalReference`; driver name, phone and car once confirmed |
| `GET /bookings?updatedSince=` | changes for polling, 100 at a time with `nextCursor` |
| `POST /bookings/{reference}/cancel` | until pickup; a driver who has the shift is told at once |

**Booking.com**: a partner of type "Booking.com" uses this API, and its bookings get source
`Booking`. Booking.com's own supplier integration follows their specification and approval
process; once they provide it, an adapter translates their messages to these same calls.

### Home page

Order: banner, **the booking form** (from / to → map with the route → approximate price of each
car, recalculated automatically as the form changes → options and contact once a car is chosen.
From and To can be typed (a city name is enough: Enter or leaving the field takes the best match)
or set by clicking the map ("Set From" / "Set To"); the map has + / − buttons but does not drag;
the same component as `/book`), welcome, four services, tariffs (live from the API), "For
drivers" with an application form, testimonials swiper, partners and clients, footer. Sections
are components in `frontend/src/app/features/home/components/`; each also has its own page
(`/services`, `/tariffs`, `/drivers`, `/reviews`, `/partners`), which the header and footer link
to (no in-page `#anchors`). The header is fixed at the top. **Texts, contacts, partner logos and social links are in
`features/home/home.content.ts`.**

- Testimonials show only real reviews whose authors ticked "you may publish my review" on the
  review page (first name and surname initial). With none yet, an invitation is shown instead.
- Partner logos appear only once added to `PARTNER_LOGOS` (with each partner's permission);
  until then the strip shows the kinds of partners the service is for.
- Driver applications are saved (Admin → Applications) and emailed to `Company:Email`.
- Animations use transform/opacity only (`styles/_motion.scss`, `RevealDirective`,
  `CountUpDirective`) and stop for users who ask for reduced motion. Measured while scrolling
  the whole page: 60 fps, no long tasks, no layout shift, on desktop and phone width.

### Launch (step 7)

- **Deployment**: Docker Compose on one server: Caddy (HTTPS, site, `/api` proxy), API, PostgreSQL,
  daily backups. See [docs/deployment.md](docs/deployment.md).
- **Security review**: findings, fixes and open items in [docs/security-review.md](docs/security-review.md).
- **Staff accounts** (Admin → Staff): temporary passwords shown once and changed at first sign-in;
  12+ character passwords; changing a role, deactivating or a new password ends open sessions.
  Everyone changes their own password under My account. **Audit log** under Admin → Audit log.
- **Production checks**: the API refuses to start in Production with development settings.
- **Housekeeping**: bookings never paid are cancelled after 48 h or 1 h before pickup.
- **Legal pages**: `/privacy` and `/terms` are drafts to complete with a lawyer.
- **Go-live**: real-ride test plan and checklist in [docs/launch-checklist.md](docs/launch-checklist.md).

### Maps

| Purpose | Development default | Setting |
| --- | --- | --- |
| Driving distance | public OSRM demo server | `Maps:OsrmBaseUrl` |
| Address search | public Nominatim (max 1 request/s) | `Maps:NominatimBaseUrl`, `Maps:UserAgent` |
| Map tiles | tile.openstreetmap.org, darkened in CSS | `MAP_CONFIG` token in the frontend |

**Countries served: Italy, France, Switzerland and Germany** (`Maps:Countries`). Address search
and map clicks only return places there; the price check refuses anything outside rough boxes
around the four (`GeoMath.IsInServiceArea`).

Addresses are shortened from OpenStreetMap's parts (`AddressFormatter`): "Via del Corso 12, 00187
Rome", or "San Marco 1, 30124 Venice". A searched place keeps its name ("Hotel Danieli, Riva degli
Schiavoni 4196…"), since the driver reads it; a point clicked on the map does not (it would be
whatever shop is there).

The public servers are fine for development but not for production traffic: before launch,
use a paid provider or self-host OSRM/Nominatim. If OSRM is unreachable, the quote uses a
straight-line estimate × 1.3 and is marked as estimated. On macOS, .NET needs
`DOTNET_SYSTEM_NET_SECURITY_USENETWORKFRAMEWORK=1` (set in `launchSettings.json`) to reach
TLS 1.3-only servers.
- New migration: `dotnet ef migrations add <Name> -p src/DriverPlatform.Infrastructure -s src/DriverPlatform.Api -o Persistence/Migrations`

Endpoints:

| Endpoint | Access |
| --- | --- |
| `POST /api/auth/login`, `GET /api/auth/me` | public / staff |
| `GET /api/geo/search?q=`, `GET /api/geo/reverse?lat=&lng=` | public, 60/min per IP |
| `GET /api/booking-options`, `POST /api/quotes` | public, 60/min per IP |
| `POST /api/bookings` | public, 10/min per IP |
| `GET /api/admin/pricing`, `PUT …/rates/{category}`, `PUT …/extras/{type}`, `PUT …/settings`, `POST/PUT/DELETE …/surcharges` | Admin |
| `GET/POST/PUT/DELETE /api/admin/zones` | Admin |
| `GET /api/admin/dashboard`, `GET /api/admin/bookings`, `GET …/{id}`, `PUT …/{id}/status`, `PUT …/{id}/notes` | staff |
| `GET/POST /api/admin/shifts`, `GET/PUT …/{id}`, `PUT …/{id}/status`, `GET …/availability?bookingIds=` | staff |
| `GET/POST /api/admin/drivers`, `GET/PUT …/{id}`, `GET/POST /api/admin/vehicles`, `PUT …/{id}` | staff |
| `GET /api/my-booking/{reference}/{token}`, `POST …/checkout`, `GET …/invoices/{number}` | signed client link |
| `POST /api/payments/stripe/webhook` | Stripe (signature checked) |
| `GET /api/admin/payments`, `POST …/{id}/refunds`, `GET /api/admin/invoices/{id}/pdf`, `POST …/{id}/resend`, `GET /api/admin/invoices/export` | Admin |
| `GET/POST /api/dev/payments/{session}` | development only (fake provider) |
| `GET /api/driver/{token}`, `POST …/shifts/{id}/confirm`, `POST …/bookings/{id}/picked-up`, `POST …/bookings/{id}/completed` | driver link |
| `GET/POST /api/reviews/{token}` | review link |
| `GET/POST /api/admin/drivers/{id}/link`, `GET /api/admin/reviews` | staff |
| `GET/POST /api/admin/partners`, `PUT …/{id}`, `POST/DELETE …/{id}/api-key`, `GET/POST …/{id}/webhook-secret`, `GET …/{id}/webhooks`, `GET …/{id}/statement` | Admin |
| `/api/partner/v1/…` (see above) | partner API key |
| `POST /api/auth/change-password` | staff |
| `GET/POST /api/admin/staff`, `PUT …/{id}`, `POST …/{id}/reset-password`, `GET /api/admin/audit` | Admin |
| `GET /api/health`, `GET /openapi/v1.json` (Development only) | public |

## Frontend

```
frontend/src/
  theme/colors.ts      the palette, shared by tailwind.config.ts and the PrimeNG preset (app/theme.ts)
  styles/              global BEM blocks: base, typography, form, page
  app/
    core/              models (interfaces), services, guards, interceptors, tokens
    shared/components/ button, icon, logo, section-heading, service-card, form-field, counter,
                       map (Leaflet), coming-soon
    shared/pipes/      money, duration
    layout/            public layout (header, footer), admin layout (sidebar filtered by role)
    features/
      booking/         /book and the home page: from/to → map → prices → details (BookingStore, typed forms)
      admin/           dashboard, bookings, shifts, drivers, vehicles, pricing, zones (map editor),
                       payments (refunds, invoices, export)
      my-booking/      the client's page from the signed link (pay, status, invoice)
      driver-portal/   /driver/:token, the driver's shifts on the phone
      review/          /review/:token, the one-time rating page
      dev-payment/     stand-in for Stripe's page in development
      home, auth/login, not-found
```

Conventions:

- **BEM + `@apply`.** Templates use BEM class names only (`.service-card__title`,
  `.button--outline`). Tailwind utilities appear only inside `@apply` in the `.scss` files.
  Each component styles its own block. Shared blocks such as `.input` and `.heading` live in
  `src/styles/`.
- **Colors** come from `src/theme/colors.ts` (`ink`, `gold`, `ivory`, `success`, `warning`,
  `danger`, `info`). Change them there, and Tailwind and PrimeNG both pick up the change.
- **Guards:** `authGuard` (signed-in staff; others go to `/login?returnUrl=…`),
  `roleGuard('Admin')`, `guestGuard` (keeps signed-in users off the login page).
- **Interceptors:** `authInterceptor` adds the bearer token to `/api` calls;
  `errorInterceptor` signs out when the API answers 401.
- Components are standalone with `OnPush`, use signal inputs, and load lazily from `app.routes.ts`.
- **Icons** are an icon font built with fantasticon. To add or change one, put a 24×24 stroked SVG
  in `src/assets/icons/src/` and run `npm run generate-fonts`: it outlines the strokes
  (`oslllo-svg-fixer` → `src/assets/icons/font-icons/`), builds `src/assets/fonts/icons.woff` +
  `icons.css` and the `IconName` type (`core/models/icon-names.generated.ts`). The generated files
  are committed, so builds don't run it. Use `<app-icon name="car" />`; size it with the host's
  width/height. Only woff is produced (this machine's ttf2woff2 cannot convert the font), and it
  is embedded in `icons.css`, so icons never show as empty boxes while loading.
- **Tariff cards** show the illustrations in `src/assets/images/cars/` (sedan, SUV, van); replace
  them with photos of the real cars when there are some (`CAR_IMAGE` in the tariffs component).
- **Form errors** float under the field (absolute), so showing one never moves the layout; forms
  leave at least 16px between rows for them.
- **Images** go through `<app-image [image]="IMAGES.hero" alt="…" />`: a tiny blurred preview (or
  a shimmer) fills the box at once and the photo fades in when loaded; the box keeps the photo's
  proportions, so nothing moves. `fit="cover"` fills the parent; `priority` for the banner (loaded
  first, preloaded in the prerendered page). To add a photo, put it in `src/assets/images/src/` and
  run `npm run images`: WebP sizes (480/960/1600), the preview and `core/images.generated.ts`. The
  same script makes the link-preview image `og.jpg` (1200×630) and the app icons / favicon from
  `src/assets/brand/icon.svg` (a placeholder "M" monogram until there is a real logo).

### Languages

The public site and the client/driver pages are in **Italian (default, at `/`), English (`/en`),
German (`/de`) and French (`/fr`)**, with a switcher in the header, the mobile menu and the footer.
The admin panel stays in English.

- Angular's built-in i18n: one prerendered build per language (`angular.json` → `i18n`). Texts are
  marked with `i18n` attributes in templates and `$localize` in code; translations are in
  `frontend/src/locale/messages.{it,de,fr}.json` (English is the source).
- **After changing or adding a text:** `npm run i18n` extracts the texts and lists what each
  language lacks; add those to the three files. The production build fails if a translation is
  missing, so nothing ships half-translated.
- `ng serve` serves the Italian build; the other languages are checked with a production build.
- Prices and dates follow the language ("58,00 €" in Italian, German, French; "€58.00" in English);
  address search returns place names in it ("Mailand" on the German site).
- Client links (booking page, payment return, review) open in the language the client booked in
  (`SiteLanguages` in the backend); other languages (Spanish, Russian…) get English.
- **Still in English, from the backend:** the confirmation emails and WhatsApp texts, the price
  breakdown lines ("Fare 13.5 km", "… access"), surcharge names typed in the panel, and some
  validation messages.

### SEO

- **Prerendered pages.** `/`, `/book`, `/services`, `/tariffs`, `/drivers`, `/reviews`,
  `/partners`, `/privacy` and `/terms`, in each language (36 pages), are rendered to HTML at build time (`app.routes.server.ts`),
  so search engines and link previews (WhatsApp, Facebook, Telegram) read them without running
  scripts; the browser then takes over the page (hydration). Live data (prices, reviews) is
  loaded by the browser: while prerendering, API calls return nothing (`serverApiInterceptor`).
  Other routes render in the browser only, and Caddy sends `X-Robots-Tag: noindex` for them.
- **Head tags** per page from the route data `seo(description)` in `app.routes.ts`
  (`SeoService`): description, robots, canonical, Open Graph and Twitter card; the home page also
  gets schema.org data (`TaxiService`, `Organization`, `WebSite`; contact details only once they
  are filled in `home.content.ts`). Routes without `seo` are `noindex`. Each page links its other
  language versions (`hreflang`, Italian as `x-default`); the sitemap lists all of them.
- **Site address.** `npm run build:site` with `SITE_URL=https://domain` puts it in canonical links
  and previews and writes `sitemap.xml` and the `Sitemap:` line of `robots.txt`. The Docker build
  does this from `SITE_DOMAIN`.
- Code that runs while prerendering must not touch `window`/`document` directly: browser-only
  work goes in `afterNextRender` (see the map, which loads Leaflet there).

## Open questions

- Surcharges are added together (decided 2026-10-21).
- **Map providers for production** (routing, address search, tiles): pick a paid provider or
  self-host before launch (see Maps above).
- **The VAT rate** (10% by default) and **Italian e-invoicing (SdI / FatturaPA)**: the PDF is
  the client's copy; transmitting the electronic invoice to SdI needs an intermediary (e.g.
  Aruba, Fatture in Cloud, or the accountant's software). Confirm both with the accountant.
- **Company data** for invoices (name, address, P.IVA, C.F.) goes in the `Company` settings.
- **Cancellation policy**: refunds are decided by the admin; a written policy (deadlines,
  amounts) is needed for the terms and could later be automated.
- **Document scans** (licences, insurance) are not uploaded yet: only numbers and expiry dates.
  File storage (where, how long) is to decide.
- **Automatic assignment** and **reassignment when a driver does not confirm in time** (§7):
  the data is there (sent/seen/confirmed times); the rule (how long to wait, who next) is to
  decide. **Flight tracking** needs a provider (e.g. AeroDataBox, FlightAware).
- **SMS**: clients without WhatsApp get nothing when they also have no email (partner
  bookings). An SMS provider can be added behind the same notification service.
- **Booking.com supplier integration**: request their specification and approval.
- **Partner invoices**: the monthly statement is a CSV; issuing a monthly invoice per partner
  (with SdI) is to set up with the accountant.
- **QuestPDF licence**: the Community licence is free for companies under USD 1M yearly revenue.
- **External accounts:** the WhatsApp Business account and the Booking connection take weeks to
  approve. The plan says to request both at the start of the project.
