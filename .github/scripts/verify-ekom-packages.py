"""Read-only NuGet preflight. Never uploads packages or creates releases."""
import argparse
import json
from pathlib import Path
import re
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

PACKAGE_IDS = (
    "Ekom.AspNetCore", "Ekom.Common", "Ekom.Core", "Ekom.U10", "Ekom.U17",
    "Ekom.U18", "Ekom.U19", "Ekom.Web", "Ekom.Web.U17", "Ekom.Web.U18",
    "Ekom.Web.U19",
)
FEED = "https://api.nuget.org/v3-flatcontainer"


def verify_metadata(content, package_id, version, commit):
    metadata = ET.fromstring(content).find("{*}metadata")
    if metadata is None:
        raise ValueError(f"{package_id}: missing package metadata")
    if metadata.findtext("{*}id", "").lower() != package_id.lower():
        raise ValueError(f"{package_id}: package ID mismatch")
    if metadata.findtext("{*}version") != version:
        raise ValueError(f"{package_id}: package version mismatch")
    repository = metadata.find("{*}repository")
    actual = repository.get("commit", "") if repository is not None else ""
    if not re.fullmatch(r"[0-9a-fA-F]{40}", actual) or actual.lower() != commit.lower():
        raise ValueError(
            f"{package_id} {version}: source commit mismatch: "
            f"expected {commit}, found {actual or '(missing)'}. "
            "Do not reuse this version for different source code."
        )


def remote_metadata(package_id, version):
    # The flat-container endpoint includes unlisted versions; search results do not.
    name = package_id.lower()
    request = urllib.request.Request(
        f"{FEED}/{name}/{version}/{name}.nuspec",
        headers={"User-Agent": "Ekom-release-preflight", "Cache-Control": "no-cache"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        error.close()
        if error.code == 404:
            return None
        raise  # Authentication, rate limits and server errors are not availability.


def preflight(directory, version, commit, fetch=remote_metadata):
    if not re.fullmatch(r"(?:0\.2\.\d+|1\.\d+\.\d+)", version):
        raise ValueError("Only stable Ekom 0.2.x and 1.x.x versions are supported")
    if not re.fullmatch(r"[0-9a-fA-F]{40}", commit):
        raise ValueError("A full source commit SHA is required")
    directory = Path(directory)
    expected = {f"{name}.{version}.nupkg" for name in PACKAGE_IDS}
    actual = {path.name for path in directory.glob("*.nupkg")}
    if actual != expected:
        raise ValueError(f"Unexpected package set. Missing: {sorted(expected - actual)}; extra: {sorted(actual - expected)}")
    plan = []
    for name in PACKAGE_IDS:
        path = directory / f"{name}.{version}.nupkg"
        with zipfile.ZipFile(path) as package:
            manifests = [entry for entry in package.namelist() if entry.endswith(".nuspec")]
            if len(manifests) != 1:
                raise ValueError(f"{path.name}: expected exactly one nuspec")
            verify_metadata(package.read(manifests[0]), name, version, commit)
        existing = fetch(name, version)
        if existing is not None:
            verify_metadata(existing, name, version, commit)
        plan.append({"path": str(path.resolve()), "alreadyPublished": existing is not None})
    return plan


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output)
    # Never leave a stale successful plan after a failed validation.
    output.unlink(missing_ok=True)
    plan = preflight(args.directory, args.version, args.commit)
    output.write_text(json.dumps(plan, indent=2), encoding="utf-8")
    for package in plan:
        action = "Already published from this commit" if package["alreadyPublished"] else "Ready to publish"
        print(f"{action}: {Path(package['path']).name}")


if __name__ == "__main__":
    main()
