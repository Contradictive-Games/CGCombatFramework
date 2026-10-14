#!/usr/bin/env bash
# Usage:
#   check-release.sh            structural checks (PRs)
#   check-release.sh v0.9.0     full release checks for that tag
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"

errors=0
pass() { echo "  ✓ $1"; }
fail() { echo "  ✗ $1"; errors=$((errors + 1)); }

json_field() { sed -n "s/^[[:space:]]*\"$1\":[[:space:]]*\"\([^\"]*\)\".*/\1/p" package.json | head -n1; }

version=$(json_field version)
name=$(json_field name)
tag="${1:-}"

echo "package.json: $name@$version"

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] \
    && pass "version is SemVer" || fail "version '$version' is not SemVer (no leading v)"

[[ "$name" == "${name,,}" ]] \
    && pass "package name is lowercase" || fail "package name '$name' must be lowercase"

tracked=$(git ls-files)
missing=()
while IFS= read -r file; do
    path="$file"
    while [[ "$path" != "." && "$path" != "Samples~" ]]; do
        grep -qxF "$path.meta" <<< "$tracked" || missing+=("$path.meta")
        path=$(dirname "$path")
    done
done < <(git ls-files -- Runtime Editor 'Samples~' Tests README.md CHANGELOG.md LICENSE.md package.json | grep -v '\.meta$')

if [[ ${#missing[@]} -eq 0 ]]; then
    pass "all shipped files have .meta files"
else
    fail "missing .meta files:"
    printf '      %s\n' "${missing[@]}" | sort -u
fi


if [[ -n "$tag" ]]; then
    [[ "$tag" == "v$version" ]] \
        && pass "tag matches package.json" || fail "tag $tag != v$version"

    grep -qF "## [v$version]" CHANGELOG.md \
        && pass "CHANGELOG has a v$version section" || fail "CHANGELOG has no '## [v$version]' heading"

    unreleased=$(awk '/^## \[Unreleased\]/{f=1; next} /^## \[/{f=0} f' CHANGELOG.md | grep -E '^[[:space:]]*[-*] ' || true)
    [[ -z "$unreleased" ]] \
        && pass "CHANGELOG [Unreleased] is empty" || fail "CHANGELOG [Unreleased] still has entries"

    repo=$(git remote get-url origin | sed -E 's#^(git@github\.com:|https://github\.com/)##; s#\.git$##')
    grep -qF "github.com/$repo.git#v$version" README.md \
        && pass "README install URL points at $repo#v$version" \
        || fail "README install URL should be https://github.com/$repo.git#v$version"

fi

echo
if [[ $errors -gt 0 ]]; then
    echo "$errors problem(s) found."
    exit 1
fi
echo "All checks passed."