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

headers=$(curl -sI "$base/")
for h in strict-transport-security content-security-policy x-content-type-options; do
    if echo "$headers" | grep -qi "^$h:"; then echo "ok    header $h"; else echo "FAIL  header $h missing"; fail=1; fi
done
rm -f /tmp/smoke.$$
exit $fail
