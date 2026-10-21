# Deployment

One server with Docker runs everything: Caddy (HTTPS and the site), the API, PostgreSQL and a daily
backup job. Files: `deploy/docker-compose.yml`, `deploy/Caddyfile`, `deploy/.env.example`,
`backend/Dockerfile`, `frontend/Dockerfile`.

> The Docker images and the Caddyfile were written without Docker on the development machine:
> the first build on the server is their first real test. The API release build, the production
> configuration checks, the backup and the restore were tested locally.

## 1. Server

- A VPS in the EU, ideally Italy or nearby (Aruba, Hetzner, OVH, DigitalOcean…): 2 vCPU, 4 GB RAM,
  40 GB SSD is plenty to start. Ubuntu 24.04 LTS.
- Docker Engine with the compose plugin (`apt install docker.io docker-compose-v2`).
- Firewall: allow 22 (SSH, keys only), 80 and 443. The database is not exposed.
- Automatic security updates (`unattended-upgrades`).

## 2. DNS

Point the domain (e.g. `magnusdrive.it`) at the server with an A (and AAAA) record. Caddy gets the
certificate from Let's Encrypt on the first request.

## 3. Configuration

```bash
git clone … && cd driver-platform/deploy
cp .env.example .env && chmod 600 .env
openssl rand -base64 48   # run three times: POSTGRES_PASSWORD, JWT_SIGNING_KEY, LINKS_SIGNING_KEY
nano .env
```

The API refuses to start in Production if a key is missing or a development value, if payments are
not Stripe, if email is not configured, or if the company's VAT number and address (printed on
invoices) are empty. The error lists every problem.

## 4. Start

```bash
docker compose --env-file .env up -d --build
docker compose logs -f api        # migrations run, then "Application started"
./smoke-test.sh https://magnusdrive.it
```

Then:

1. Sign in at `/login` with `BOOTSTRAP_ADMIN_EMAIL` / `BOOTSTRAP_ADMIN_PASSWORD`, change the
   password (My account), then **remove both values from `.env`** and `docker compose up -d`.
2. Admin → Staff: create the dispatchers (each gets a temporary password to change).
3. Stripe Dashboard → Developers → Webhooks: endpoint `https://DOMAIN/api/payments/stripe/webhook`
   with `checkout.session.completed`, `checkout.session.async_payment_succeeded`,
   `checkout.session.async_payment_failed`, `checkout.session.expired`; put its `whsec_…` in `.env`.
4. Pricing, zones and company details: check them in the panel.

## 5. Backups

- The `backup` service dumps the database every day at `BACKUP_AT` (UTC) into `BACKUP_HOST_DIR`,
  checks each dump and keeps `BACKUP_KEEP_DAYS` days.
- **Copy the dumps off the server** (a backup on the same disk is lost with the server), e.g. with
  restic or rclone to an S3-compatible bucket in another provider, from a host cron job:
  `0 4 * * * restic -r s3:… backup /opt/driver-platform/deploy/backups`.
- Restore drill, monthly:

  ```bash
  docker compose exec -T backup sh /scripts/restore.sh /backups/<file>.dump restore_drill
  docker compose exec -T db dropdb -U driver_platform restore_drill
  ```

- Real restore: stop the API, restore into `driver_platform` with `--force`, start the API.

## 6. Updates

```bash
git pull && docker compose --env-file .env up -d --build
```

Migrations run when the API starts (one instance). A backup is taken first by hand:
`docker compose exec backup sh /scripts/backup.sh`.

## 7. Monitoring

- Uptime: an external monitor (UptimeRobot, Better Stack…) on `https://DOMAIN/api/health` every minute.
- Logs: `docker compose logs api` (structured, to stdout). Watch for `Webhook … failed`,
  `WhatsApp … failed`, `OSRM unavailable`, `Could not email`.
- Stripe: Dashboard → Webhooks shows failed deliveries.

## 8. Before real traffic

- Maps: the public OSRM and Nominatim servers are for development only. Use a provider or
  self-host, then set `OSRM_BASE_URL` / `NOMINATIM_BASE_URL`. Map tiles: same for
  tile.openstreetmap.org (`MAP_CONFIG` in the frontend; also allow the new host in the Caddyfile CSP).
- WhatsApp: set `WHATSAPP_PROVIDER=Meta` once the templates are approved.
