#!/bin/sh
# Read-only checks after a deployment: nothing is booked or paid.
#   ./smoke-test.sh https://magnusdrive.it
set -eu
base="${1:?usage: smoke-test.sh <https://site>}"
fail=0

check() {
    name="$1"; url="$2"; expect="$3"
    code=$(curl -s -o /tmp/smoke.$$ -w '%{http_code}' "$url")
    if [ "$code" = "$expect" ]; then echo "ok    $name ($code)"; else echo "FAIL  $name: $code, expected $expect"; fail=1; fi
}

check "site"              "$base/"                                   200
check "spa route"         "$base/book"                               200
check "api health"        "$base/api/health"                         200
check "booking options"   "$base/api/booking-options"                200
check "partner docs"      "$base/api/partner/openapi/partner.json"   200
check "admin needs login" "$base/api/admin/bookings"                 401
check "partner needs key" "$base/api/partner/v1/bookings"            401
check "no dev payments"   "$base/api/dev/payments/x"                 404

check "sitemap"           "$base/sitemap.xml"                        200

# SEO: pages prerendered for this domain, private pages kept out of search results.
seo() {
    name="$1"; ok="$2"
    if [ "$ok" = yes ]; then echo "ok    $name"; else echo "FAIL  $name"; fail=1; fi
}
curl -s "$base/tariffs" | grep -q "<link rel=\"canonical\" href=\"$base/tariffs\">" && r=yes || r=no
seo "tariffs prerendered with canonical $base/tariffs" "$r"
curl -s "$base/robots.txt" | grep -q "^Sitemap: $base/sitemap.xml" && r=yes || r=no
seo "robots.txt points to the sitemap" "$r"
curl -sI "$base/admin" | grep -qi "^x-robots-tag: noindex" && r=yes || r=no
seo "panel marked noindex" "$r"
for lang in de fr en; do
    curl -s "$base/$lang/" | grep -q "<html lang=\"$lang\"" && r=yes || r=no
    seo "/$lang/ served in $lang" "$r"
done
curl -s "$base/" | grep -q '<html lang="it"' && r=yes || r=no
seo "/ served in Italian" "$r"

headers=$(curl -sI "$base/")
for h in strict-transport-security content-security-policy x-content-type-options; do
    if echo "$headers" | grep -qi "^$h:"; then echo "ok    header $h"; else echo "FAIL  header $h missing"; fail=1; fi
done
rm -f /tmp/smoke.$$
exit $fail
