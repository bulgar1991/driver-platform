# Launch checklist

## A. Business and legal (owner, accountant, lawyer)

- [ ] Company data for invoices: name, address, P.IVA, C.F. (`COMPANY_*`).
- [ ] VAT rate on transfers confirmed (10% set by default) and **SdI e-invoicing**: intermediary chosen.
- [ ] Privacy policy and terms (`/privacy`, `/terms`, drafts in the app) completed and reviewed:
      cancellation policy, waiting time, retention periods, processors list.
- [ ] Data processing agreements with partners who send bookings.
- [ ] NCC licences and insurance for every driver and car entered in the panel.

## B. Accounts and providers

- [ ] Stripe live account, PayPal enabled, webhook created (see deployment.md).
- [ ] WhatsApp Business: number verified, the 4 templates approved in Italian, English, German and French (or shorten `WhatsApp:Languages` to the approved ones).
- [ ] SMTP provider with the domain authenticated (SPF, DKIM, DMARC) so emails are not spam.
- [ ] Maps provider (routing, address search, tiles) instead of the public OpenStreetMap servers.
- [ ] Server, DNS, off-site backup bucket, uptime monitor.

## C. Real-ride tests (staging first, then production with Stripe live and a real card)

Run each with a real driver and car, on the phone the driver will use. Tick when the result matches.

| # | Scenario | Check |
| --- | --- | --- |
| 1 | Hotel in Rome → Fiumicino, sedan, weekday | Price = base + km × rate; airport meeting point not used for drop-off; invoice PDF and email arrive |
| 2 | Fiumicino → Rome centre (ZTL), name sign, flight number | ZTL fee on the invoice; driver sees the name sign and flight; arrivals meeting point instructions |
| 3 | Venice hotel → Marco Polo, Saturday 23:00 | Pickup moved to Piazzale Roma; weekend + night surcharges added; client instructions in the email; driver sees the client's real address and the meeting point |
| 4 | Return trip with discount, 2 child seats | Two bookings, one payment, one invoice; seats on the driver link for both legs |
| 5 | Pay with PayPal | Booking confirmed, method "PayPal" in Payments |
| 6 | Close the Stripe page without paying, pay later from the booking link | Booking stays payable, then confirmed |
| 7 | Shift with two rides, driver confirms on the link | Client gets driver and car details; dispatcher sees "Confirmed" |
| 8 | Driver marks picked up and completed | Review link arrives; review appears under Reviews and on the driver |
| 9 | Dispatcher changes a confirmed shift | Driver gets the update and confirms again |
| 10 | Partial refund, then full refund with cancellation | Credit notes, emails, booking cancelled, Stripe shows both refunds |
| 11 | Partner books by API (test partner), then cancels | Webhooks received and signed; booking appears and disappears for dispatch |
| 12 | Driver's licence expiring in 10 days | Dashboard alert; shift warning; shift blocked after expiry |
| 13 | Phone with poor signal at the airport | Driver link usable; actions retried without duplicates |

## D. Technical go-live

- [ ] `./deploy/smoke-test.sh https://DOMAIN` passes.
- [ ] Google Search Console (and Bing Webmaster Tools): domain verified, `https://DOMAIN/sitemap.xml`
      submitted; the home page tested with the Rich Results Test; a link shared on WhatsApp shows
      the preview.
- [ ] Translations reviewed by native speakers (Italian, German, French:
      `frontend/src/locale/messages.*.json` and the emails in
      `backend/src/DriverPlatform.Application/Localization/`), and the legal pages in each language
      by the lawyer.
- [ ] Real logo: replace `frontend/src/assets/brand/icon.svg` and run `npm run images`.
- [ ] Bootstrap admin values removed from `.env`; admin password changed; dispatchers created.
- [ ] First backup present, copied off the server, **restore drill done**.
- [ ] Uptime monitor alerting the right phone.
- [ ] Stripe webhook deliveries green; a test refund done in live mode.
- [ ] `security-review.md` open items accepted or scheduled.
