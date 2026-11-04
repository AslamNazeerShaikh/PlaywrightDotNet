#!/usr/bin/env bash
# Fetch uBlock Origin Lite and prepare a max-filtering unpacked Chrome build.
set -euo pipefail

UBOL_TAG="2026.914.1325"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/vendor/ubol-chrome-max"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

need() { command -v "$1" >/dev/null 2>&1 || { echo "download-ubol: missing $1" >&2; exit 1; }; }
need curl
need unzip
need python3

curl -fsSL -o "$TMP/ubol.xpi" "https://github.com/uBlockOrigin/uBOL-home/releases/download/${UBOL_TAG}/uBOLite_${UBOL_TAG}.firefox.signed.xpi"

rm -rf "$OUT"
mkdir -p "$OUT"
unzip -q "$TMP/ubol.xpi" -d "$OUT"

python3 - "$OUT" <<'EOF'
import json
import os
import sys

out = sys.argv[1]
path = os.path.join(out, "manifest.json")
with open(path, encoding="utf-8") as f:
    manifest = json.load(f)

manifest["background"] = {"service_worker": "/js/background.js", "type": "module"}
manifest.pop("browser_specific_settings", None)

extra = {"adguard-spyware-url", "ublock-experimental", "ind-0"}
for resource in manifest["declarative_net_request"]["rule_resources"]:
    if resource["id"] in extra or resource["id"].startswith("annoyances-"):
        resource["enabled"] = True

missing = [
    r["path"]
    for r in manifest["declarative_net_request"]["rule_resources"]
    if r["enabled"] and not os.path.isfile(os.path.join(out, r["path"].lstrip("/")))
]
if missing:
    raise SystemExit(f"missing ruleset files: {', '.join(missing)}")

with open(path, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write("\n")

enabled = sorted(
    r["id"]
    for r in manifest["declarative_net_request"]["rule_resources"]
    if r["enabled"]
)
print(f"uBO Lite {manifest['version']}: {len(enabled)} rulesets enabled")
print(", ".join(enabled))
EOF
