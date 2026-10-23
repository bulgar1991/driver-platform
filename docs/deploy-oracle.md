# Deploying on Oracle Cloud (Always Free)

The whole platform (site, API, PostgreSQL, daily backups) on one free Oracle Cloud server, with the
same files as any other server: `deploy/docker-compose.yml`. This page covers what is particular to
Oracle; configuration, the first sign-in, backups and updates are in [deployment.md](deployment.md).

> Oracle renames its console menus from time to time: the paths below are a guide, not exact
> labels. Free-tier limits are as Oracle stated them in 2026; check the
> [Always Free page](https://www.oracle.com/cloud/free/) for the current ones.

## What you get for free

- **Ampere A1 (ARM) servers**: up to 4 CPUs (OCPUs) and 24 GB of memory in total, without a time
  limit. One server with 2 OCPUs and 12 GB is plenty for this platform.
- **Block storage**: 200 GB in total (the server's disk counts).
- **Object storage**: a few GB, enough for off-server copies of the backups (step 9).
- The free trial credits (30 days) are extra. When they end, only resources marked
  **Always Free-eligible** keep running: everything in this guide is.

Every image in the compose file (Caddy, .NET 10, Node, PostgreSQL 17) is published for ARM, and the
Dockerfiles don't pin a processor, so nothing changes for ARM.

## 1. Account and region

- The **home region**, chosen at sign-up, is permanent, and Always Free servers exist only there.
  Check it under *Profile → Tenancy*. An EU region (Milan, Frankfurt, Amsterdam, Paris…) keeps the
  clients' data in the EU.
- Optional, recommended: *Billing → Upgrade to Pay As You Go*. Always Free resources stay free, but
  Oracle no longer reclaims a free server it considers idle, and A1 capacity is easier to get.
  A card is needed for this. To be safe from surprises, set a budget alert (*Billing → Budgets*)
  of €1.

## 2. SSH key (on your Mac)

```bash
ssh-keygen -t ed25519 -f ~/.ssh/oracle_magnus -C "magnus-drive"
cat ~/.ssh/oracle_magnus.pub     # paste this when creating the server
```

## 3. Network

*Networking → Virtual cloud networks → Start VCN Wizard → Create VCN with Internet Connectivity*,
name `magnus-vcn`, defaults for the rest. This creates a public subnet with internet access.

Then open the web ports: *magnus-vcn → Security Lists → Default Security List → Add Ingress
Rules*, source CIDR `0.0.0.0/0`:

| Protocol | Destination port | For |
|---|---|---|
| TCP | 80 | HTTP, and Let's Encrypt's certificate check |
| TCP | 443 | HTTPS |
| UDP | 443 | HTTP/3 (optional) |

SSH (TCP 22) is already open. To limit it to your own IP, edit that rule's source.

## 4. Server

*Compute → Instances → Create instance*:

- **Name**: `magnus-drive`
- **Image**: *Canonical Ubuntu 24.04* (the aarch64 build is picked with the shape below)
- **Shape**: *Change shape → Ampere → VM.Standard.A1.Flex*, 2 OCPUs, 12 GB memory
  (must show *Always Free-eligible*)
- **Networking**: `magnus-vcn`, its public subnet, *Assign a public IPv4 address*
- **SSH keys**: *Paste public keys*, the line from step 2
- **Boot volume**: the default (about 47 GB) is enough; up to 200 GB stays free

*"Out of capacity for shape VM.Standard.A1.Flex"*: free ARM servers run out in busy regions. Try
another availability domain in the same form (some regions, Paris among them, have only one), try
1 OCPU / 6 GB (you can resize later), or try again later. Upgrading to Pay As You Go (step 1)
usually solves it.

Or let your computer keep trying: `deploy/oracle-launch.sh` asks Oracle for the server every
90 seconds (2 OCPU / 12 GB, then 1 / 6) until it gets one, and prints its public IP. It needs the
OCI CLI (`brew install oci-cli`) and an API signing key: *Profile → My profile → Tokens and keys →
API keys → Add API key → Generate API key pair*; save the private key as `~/.oci/oci_api_key.pem`
(`chmod 600`) and the shown configuration as `~/.oci/config`, with
`key_file=~/.oci/oci_api_key.pem`. Then, with the public subnet's OCID (on its page):

```bash
OCI_SUBNET_ID=ocid1.subnet.oc1… OCI_SSH_KEY=~/.ssh/oracle_magnus.pub \
  nohup deploy/oracle-launch.sh > oracle-launch.log 2>&1 &
tail -f oracle-launch.log
```

It never creates a second server: it stops if one called `magnus-drive` exists.

Note the **public IP** on the instance page. It stays the same across reboots, but not if the
instance is deleted. To keep it for good: *Networking → IP management → Reserved public IPs*, then
attach it to the instance's VNIC (one reserved IP is included in the free tier).

## 5. Domain

At the domain registrar (Namecheap: *Domain List → Manage → Advanced DNS*):

| Type | Host | Value |
|---|---|---|
| A | `@` | the server's public IP |

The site answers on the bare domain (`SITE_DOMAIN`), not on `www`. Wait until
`dig +short yourdomain.it` shows the IP before step 8, or Caddy can't get the certificate yet.

## 6. Prepare the server

```bash
ssh -i ~/.ssh/oracle_magnus ubuntu@SERVER_IP
```

Oracle's Ubuntu image has its own firewall rules that allow only SSH, on top of the security list.
Open 80 and 443 there too, and keep the rules after a reboot:

```bash
sudo iptables -I INPUT 6 -p tcp -m state --state NEW --dport 80 -j ACCEPT
sudo iptables -I INPUT 6 -p tcp -m state --state NEW --dport 443 -j ACCEPT
sudo iptables -I INPUT 6 -p udp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

Updates, Docker, and the project:

```bash
sudo apt update && sudo apt -y upgrade
sudo apt -y install unattended-upgrades git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu && exit        # sign in again for the group to apply
```

```bash
ssh -i ~/.ssh/oracle_magnus ubuntu@SERVER_IP
sudo mkdir -p /opt/driver-platform && sudo chown ubuntu: /opt/driver-platform
git clone https://github.com/bulgar1991/driver-platform.git /opt/driver-platform
cd /opt/driver-platform/deploy
```

## 7. Configuration

As in [deployment.md § 3](deployment.md#3-configuration):

```bash
cp .env.example .env && chmod 600 .env
openssl rand -base64 48   # three times: POSTGRES_PASSWORD, JWT_SIGNING_KEY, LINKS_SIGNING_KEY
nano .env
```

Oracle specifics:

- **Email**: Oracle blocks outgoing port 25, so use the provider's port **587** (`SMTP_PORT=587`,
  the default). Brevo, Postmark, Mailgun or Amazon SES all work.
- `SITE_DOMAIN` and `ACME_EMAIL`: your domain and an address for Let's Encrypt notices.
- The API refuses to start in Production while anything required is missing (keys, Stripe,
  email, company VAT number and address) and lists what is.

## 8. Start

```bash
docker compose --env-file .env up -d --build    # the first build takes 5–10 minutes
docker compose logs -f api                      # migrations, then "Application started"
./smoke-test.sh https://yourdomain.it
```

This is the first build of the images on a real server, and on ARM: if a step fails, the error is
in the output of the first command.

Then the first sign-in, staff, Stripe webhook and checks: [deployment.md § 4](deployment.md#4-start).

## 9. Backups off the server

The `backup` service keeps daily dumps in `deploy/backups` on the server (see
[deployment.md § 5](deployment.md#5-backups)). A copy elsewhere survives losing the server; Oracle
Object Storage works for that through its S3-compatible API:

1. *Storage → Buckets → Create bucket* `magnus-backups` (Standard tier, private).
2. *Profile → My profile → Customer secret keys → Generate secret key*: note the access key and
   the secret (shown once).
3. On the server, `sudo apt -y install rclone`, then `rclone config`: new remote `oci`, type
   *Amazon S3 compliant*, provider *Other*, the two keys, endpoint
   `https://NAMESPACE.compat.objectstorage.REGION.oraclecloud.com` (the namespace is on the
   tenancy page, the region code like `eu-milan-1` on the region menu).
4. Every night after the dump (`crontab -e`):

   ```
   30 4 * * * rclone copy /opt/driver-platform/deploy/backups oci:magnus-backups --max-age 48h
   ```

A copy in the same Oracle account is lost with the account: for a second, independent copy, the
same rclone command can also send the dumps to another provider.

## 10. Updates

```bash
cd /opt/driver-platform/deploy
docker compose exec backup sh /scripts/backup.sh      # a backup first
git pull && docker compose --env-file .env up -d --build
```

## If something doesn't answer

| Symptom | Check |
|---|---|
| The site doesn't load at all | Security list rules (step 3) and the server's iptables rules (step 6): `sudo iptables -L INPUT -n` must list 80 and 443 before the final REJECT |
| Certificate error, or Caddy logs `challenge failed` | The A record (step 5) and port 80 open; `docker compose logs web` |
| `api` keeps restarting | `docker compose logs api`: the list of missing settings |
| Emails don't arrive | `SMTP_*` in `.env`, port 587, and the sender domain verified at the email provider |
| The server stopped by itself | Oracle reclaimed an idle free server: start it again, and see step 1 (Pay As You Go) |
