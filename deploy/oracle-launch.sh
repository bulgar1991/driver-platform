#!/bin/sh
# Creates the free Oracle Cloud server (Ampere A1) and, while Oracle answers "Out of capacity",
# tries again every few minutes until it gets one. Runs on your own computer, with the OCI CLI
# configured (~/.oci/config). See docs/deploy-oracle.md, step 4.
#
#   OCI_SUBNET_ID=ocid1.subnet… OCI_SSH_KEY=~/.ssh/oracle_magnus.pub ./oracle-launch.sh
#
# Optional: OCI_COMPARTMENT_ID (default: the tenancy in ~/.oci/config), OCI_NAME (magnus-drive),
# OCI_SHAPES ("2:12 1:6": OCPUs:GB tried in turn, each round), OCI_RETRY_SECONDS (90).
set -u

subnet="${OCI_SUBNET_ID:?set OCI_SUBNET_ID to the OCID of the public subnet}"
ssh_key="${OCI_SSH_KEY:?set OCI_SSH_KEY to your public key file (.pub)}"
name="${OCI_NAME:-magnus-drive}"
shapes="${OCI_SHAPES:-2:12 1:6}"
wait="${OCI_RETRY_SECONDS:-90}"
compartment="${OCI_COMPARTMENT_ID:-$(sed -n 's/^tenancy=//p' ~/.oci/config | head -1)}"

log() { echo "$(date '+%Y-%m-%d %H:%M:%S')  $*"; }

# Never a second server: stop if one with this name exists already (also checked before every
# attempt, since an attempt that timed out may still have created it).
exists() {
    states=$(oci compute instance list --compartment-id "$compartment" --display-name "$name" \
        --query 'data[*]."lifecycle-state"' --raw-output 2>/dev/null)
    echo "$states" | grep -q 'RUNNING\|PROVISIONING\|STARTING\|STOPPED\|STOPPING'
}
if exists; then
    log "$name exists already: see Compute → Instances"
    exit 0
fi

ad=$(oci iam availability-domain list --compartment-id "$compartment" --query 'data[0].name' --raw-output) || exit 1
image=$(oci compute image list --compartment-id "$compartment" --operating-system "Canonical Ubuntu" \
    --operating-system-version "24.04" --shape VM.Standard.A1.Flex --sort-by TIMECREATED \
    --query 'data[0].id' --raw-output) || exit 1
log "availability domain $ad, image $image"

attempt=0
while :; do
    for shape in $shapes; do
        if exists; then
            log "$name exists now: see Compute → Instances for its public IP"
            exit 0
        fi
        attempt=$((attempt + 1))
        ocpus=${shape%%:*}
        memory=${shape#*:}
        out=$(oci compute instance launch --compartment-id "$compartment" --availability-domain "$ad" \
            --display-name "$name" --shape VM.Standard.A1.Flex \
            --shape-config "{\"ocpus\": $ocpus, \"memoryInGBs\": $memory}" \
            --image-id "$image" --subnet-id "$subnet" --assign-public-ip true \
            --ssh-authorized-keys-file "$ssh_key" --wait-for-state RUNNING 2>&1)
        if echo "$out" | grep -q '"lifecycle-state": "RUNNING"'; then
            id=$(echo "$out" | sed -n 's/.*"id": "\(ocid1\.instance[^"]*\)".*/\1/p' | head -1)
            ip=$(oci compute instance list-vnics --instance-id "$id" --query 'data[0]."public-ip"' --raw-output)
            log "created $name ($ocpus OCPU, $memory GB) after $attempt attempts: public IP $ip"
            exit 0
        fi
        if echo "$out" | grep -qi 'out of host capacity\|out of capacity'; then
            log "attempt $attempt ($ocpus OCPU, $memory GB): no capacity yet"
        elif echo "$out" | grep -q 'NotAuthenticated\|NotAuthorized\|InvalidParameter\|LimitExceeded\|QuotaExceeded\|CannotParseRequest'; then
            # Waiting won't fix these: a key, permission, quota or setting to correct.
            log "attempt $attempt failed, stopping:"
            echo "$out"
            exit 1
        else
            # Network timeouts, rate limits, Oracle's temporary errors: try again.
            log "attempt $attempt ($ocpus OCPU, $memory GB): $(echo "$out" | sed -n 's/.*"message": "\(.*\)",*$/\1/p' | head -1), retrying"
        fi
    done
    sleep "$wait"
done
