#!/usr/bin/env bash
set -euo pipefail

runtime="${1:-osx-arm64}"
configuration="${CONFIGURATION:-Release}"
self_contained="${SELF_CONTAINED:-false}"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
project="$repo_root/src/Prereqqer.App/Prereqqer.App.csproj"
publish_dir="$repo_root/artifacts/publish/$runtime"
bundle_dir="$repo_root/artifacts/package/$runtime/Prereqqer.app"

case "$runtime" in
  osx-*) ;;
  *)
    echo "Runtime must be an osx runtime identifier, for example osx-arm64 or osx-x64." >&2
    exit 2
    ;;
esac

dotnet publish "$project" \
  -c "$configuration" \
  -r "$runtime" \
  --self-contained "$self_contained" \
  -o "$publish_dir"

"$script_dir/create-macos-app-bundle.sh" "$publish_dir" "$bundle_dir"
