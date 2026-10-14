#!/usr/bin/env bash
# Usage: release.sh 0.9.0
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

new="${1:?Usage: release.sh <version>   e.g. release.sh 0.9.0}"
[[ "$new" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || { echo "Not SemVer: $new"; exit 1; }

git diff --quiet && git diff --cached --quiet || { echo "Commit or stash your changes first."; exit 1; }
git rev-parse -q --verify "refs/tags/v$new" > /dev/null && { echo "Tag v$new already exists."; exit 1; }

entries=$(awk '/^## \[Unreleased\]/{f=1; next} /^## \[/{f=0} f' CHANGELOG.md | grep -E '^[[:space:]]*[-*] ' || true)
[[ -n "$entries" ]] || { echo "CHANGELOG [Unreleased] is empty - nothing to release."; exit 1; }

old=$(sed -n 's/^[[:space:]]*"version":[[:space:]]*"\([^"]*\)".*/\1/p' package.json | head -n1)
today=$(date +%Y-%m-%d)
echo "Releasing $old -> $new"

sed -i "s/\"version\":[[:space:]]*\"$old\"/\"version\": \"$new\"/" package.json
sed -i "s/version-$old-/version-$new-/; s/#v$old/#v$new/g" README.md
sed -i "s/^## \[Unreleased\].*/## [Unreleased]\n\n## [v$new] - $today/" CHANGELOG.md

bash .github/scripts/check-release.sh "v$new"

git add package.json README.md CHANGELOG.md
git commit -m "Release v$new"
git tag -a "v$new" -m "v$new"

echo
echo "Created commit and tag v$new. Review with: git show"
echo "Publish with:                            git push --follow-tags"