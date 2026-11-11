import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import zipfile

spec = importlib.util.spec_from_file_location("guard", Path(__file__).with_name("verify-ekom-packages.py"))
guard = importlib.util.module_from_spec(spec)
spec.loader.exec_module(guard)
COMMIT = "a" * 40
VERSION = "1.0.1"


def metadata(name, version=VERSION, commit=COMMIT):
    repository = f'<repository type="git" commit="{commit}" />' if commit else ""
    return f'<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>{name}</id><version>{version}</version>{repository}</metadata></package>'.encode()


class PackageGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        for name in guard.PACKAGE_IDS:
            self.package(name)

    def package(self, name, commit=COMMIT):
        with zipfile.ZipFile(self.directory / f"{name}.{VERSION}.nupkg", "w") as package:
            package.writestr(f"{name}.nuspec", metadata(name, commit=commit))

    def test_available_version_prepares_all_nine_packages(self):
        plan = guard.preflight(self.directory, VERSION, COMMIT, lambda *_: None)
        self.assertEqual(9, len(plan))
        self.assertTrue(all(not entry["alreadyPublished"] for entry in plan))

    def test_retry_skips_only_matching_source_commits(self):
        plan = guard.preflight(self.directory, VERSION, COMMIT, lambda name, _: metadata(name))
        self.assertTrue(all(entry["alreadyPublished"] for entry in plan))

    def test_partial_publish_retry(self):
        plan = guard.preflight(self.directory, VERSION, COMMIT,
                               lambda name, _: metadata(name) if name == "Ekom.Core" else None)
        self.assertEqual(1, sum(entry["alreadyPublished"] for entry in plan))

    def test_different_remote_commit_blocks_entire_plan(self):
        with self.assertRaisesRegex(ValueError, "source commit mismatch"):
            guard.preflight(self.directory, VERSION, COMMIT,
                            lambda name, _: metadata(name, commit="b" * 40) if name == guard.PACKAGE_IDS[-1] else None)

    def test_missing_remote_commit_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "source commit mismatch"):
            guard.preflight(self.directory, VERSION, COMMIT, lambda name, _: metadata(name, commit=""))

    def test_local_package_must_match_release_commit(self):
        self.package("Ekom.Core", commit="b" * 40)
        with self.assertRaisesRegex(ValueError, "source commit mismatch"):
            guard.preflight(self.directory, VERSION, COMMIT, lambda *_: None)

    def test_missing_and_unexpected_packages_are_rejected(self):
        (self.directory / f"Ekom.Core.{VERSION}.nupkg").unlink()
        (self.directory / "unexpected.nupkg").touch()
        with self.assertRaisesRegex(ValueError, "Unexpected package set"):
            guard.preflight(self.directory, VERSION, COMMIT)

    def test_wrong_id_version_and_malformed_xml_are_rejected(self):
        for content in (metadata("Other"), metadata("Ekom.Core", version="1.0.0"), b"not xml"):
            with self.subTest(content=content), self.assertRaises((ValueError, guard.ET.ParseError)):
                guard.verify_metadata(content, "Ekom.Core", VERSION, COMMIT)

    def test_only_http_404_means_available(self):
        for code in (401, 403, 404, 429, 500, 503):
            with self.subTest(code=code), patch.object(guard.urllib.request, "urlopen",
                    side_effect=urllib.error.HTTPError("https://example.test", code, "error", {}, None)):
                if code == 404:
                    self.assertIsNone(guard.remote_metadata("Ekom.Core", VERSION))
                else:
                    with self.assertRaises(urllib.error.HTTPError):
                        guard.remote_metadata("Ekom.Core", VERSION)

    def test_network_failure_is_not_availability(self):
        with patch.object(guard.urllib.request, "urlopen", side_effect=TimeoutError):
            with self.assertRaises(TimeoutError):
                guard.remote_metadata("Ekom.Core", VERSION)

    def test_failed_preflight_removes_stale_publish_plan(self):
        output = self.directory / "plan.json"
        output.write_text("old plan")
        arguments = ["guard", "--directory", str(self.directory), "--version", VERSION,
                     "--commit", COMMIT, "--output", str(output)]
        with patch("sys.argv", arguments), patch.object(guard, "preflight", side_effect=ValueError("collision")):
            with self.assertRaisesRegex(ValueError, "collision"):
                guard.main()
        self.assertFalse(output.exists())

    def test_invalid_release_inputs_are_rejected(self):
        for version, commit in (("2.0.0", COMMIT), ("1.0.1-rc.1", COMMIT), (VERSION, "abc")):
            with self.subTest(version=version, commit=commit), self.assertRaises(ValueError):
                guard.preflight(self.directory, version, commit)


if __name__ == "__main__":
    unittest.main()
