#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <source-directory> <bundle-path>" >&2
  exit 2
fi

source_dir="$1"
bundle_dir="$2"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
contents_dir="$bundle_dir/Contents"
macos_dir="$contents_dir/MacOS"
resources_dir="$contents_dir/Resources"
icon_source="$repo_root/src/Prereqqer.App/Assets/prereqqer-icon.icns"

brand_name="$(
SOURCE_DIR="$source_dir" \
python3 - <<'PY'
import json
import os
from pathlib import Path

def default_definition_path() -> Path:
    source_dir = Path(os.environ["SOURCE_DIR"])
    return source_dir / "conditions.json"

def read_brand(path: Path) -> str:
    with path.open("r", encoding="utf-8-sig") as handle:
        document = json.load(handle)
    value = document.get("branding", {}).get("applicationName", "")
    return value.strip() if isinstance(value, str) else ""

explicit = os.environ.get("PREREQQER_BRAND_NAME", "").strip()
if explicit:
    print(explicit)
    raise SystemExit

definition_path = Path(os.environ.get("PREREQQER_DEFINITION_PATH", "")).expanduser()
if not str(definition_path):
    definition_path = default_definition_path()

try:
    print(read_brand(definition_path) or "Prereqqer")
except Exception:
    print("Prereqqer")
PY
)"

rm -rf "$bundle_dir"
mkdir -p "$macos_dir" "$resources_dir"

cp "$icon_source" "$resources_dir/prereqqer-icon.icns"
cp -R "$source_dir"/. "$macos_dir"
rm -f "$macos_dir/Info.plist" "$macos_dir/prereqqer-icon.icns"
chmod +x "$macos_dir/Prereqqer.App"

if [ -f "$source_dir/conditions.json" ]; then
  cp "$source_dir/conditions.json" "$resources_dir/conditions.json"
fi

BRAND_NAME="$brand_name" python3 - "$contents_dir/Info.plist" <<'PY'
import os
import plistlib
import sys

brand_name = os.environ.get("BRAND_NAME", "").strip() or "Prereqqer"
plist_path = sys.argv[1]

plist = {
    "CFBundleExecutable": "Prereqqer.App",
    "CFBundleDisplayName": brand_name,
    "CFBundleIconFile": "prereqqer-icon.icns",
    "CFBundleIdentifier": "se.prereqqer.app",
    "CFBundleInfoDictionaryVersion": "6.0",
    "CFBundleName": brand_name,
    "CFBundlePackageType": "APPL",
    "CFBundleShortVersionString": "1.0.0",
    "CFBundleVersion": "1.0.0",
    "LSMinimumSystemVersion": "12.0",
    "NSHighResolutionCapable": True,
}

with open(plist_path, "wb") as handle:
    plistlib.dump(plist, handle, sort_keys=False)
PY

echo "Created $bundle_dir"
echo "Dock title: $brand_name"
