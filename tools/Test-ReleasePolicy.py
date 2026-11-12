#!/usr/bin/env python3
"""Run the workflow's actual release block with a fake gh; never contact GitHub."""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import textwrap

ROOT = Path(__file__).resolve().parents[1]


def find_bash() -> str:
    if os.name == "nt":
        result = subprocess.run(["git", "--exec-path"], capture_output=True, text=True, check=True)
        for parent in Path(result.stdout.strip()).parents:
            candidate = parent / "bin/bash.exe"
            if candidate.is_file():
                return str(candidate)
    value = shutil.which("bash")
    if not value:
        raise RuntimeError("Bash is required for offline workflow branch tests.")
    return value


def main() -> None:
    workflow = (ROOT / ".github/workflows/portable.yml").read_text(encoding="utf-8")
    block = workflow.split("      - name: Verify checksum and publish release\n", 1)[1]
    script = textwrap.dedent(block.split("        run: |\n", 1)[1])
    output = ROOT / "artifacts/release-policy" / os.urandom(8).hex()
    output.mkdir(parents=True)
    release_script = output / "release-step.sh"
    release_script.write_text(script, encoding="utf-8", newline="\n")
    bash = find_bash()
    subprocess.run([bash, "-n", release_script.as_posix()], check=True, timeout=30)
    mock = r'''
function gh() {
  printf '%s\n' "$*" >> commands.log
  case "$1 $2" in
    'release view')
      case "$TEST_MODE" in
        draft) echo true ;;
        published) echo false ;;
        *) return 1 ;;
      esac ;;
    'release create'|'release edit') return 0 ;;
    'release upload') [[ "$TEST_MODE" != 'upload-failure' ]] ;;
    *) echo 'Unexpected gh invocation in offline test' >&2; return 99 ;;
  esac
}
source "$RELEASE_SCRIPT"
'''
    cases = [(tag, "new", pre) for tag, pre in [
        ("v0.1.0", False), ("v0.1.0-rc.3", True), ("v0.1.0-rc.10", True), ("v0.1", False)]]
    cases += [(tag, mode, pre) for mode in ["draft", "published", "bad-checksum", "upload-failure"]
              for tag, pre in [("v0.1.0", False), ("v0.1.0-rc.3", True)]]
    for tag, mode, pre in cases:
        case = output / (tag + "-" + mode)
        files = case / "release-files"
        files.mkdir(parents=True)
        name = f"NyangShinobi-{tag}-win-x64.zip"
        payload = b"portable fixture for offline release flags"
        (files / name).write_bytes(payload)
        digest = "0" * 64 if mode == "bad-checksum" else hashlib.sha256(payload).hexdigest().upper()
        (files / (name + ".sha256")).write_text(digest + "\r\n", encoding="ascii")
        env = dict(os.environ, RELEASE_SCRIPT=release_script.as_posix(), TEST_MODE=mode,
                   RELEASE_TAG=tag, SOURCE_SHA="offline-validation", GH_TOKEN="offline-test-only", GH_REPO="example/repo")
        result = subprocess.run([bash, "--noprofile", "--norc", "-c", mock], cwd=case, env=env,
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
        (case / "output.txt").write_text(result.stdout + result.stderr, encoding="utf-8")
        expected_success = mode in ["new", "draft"]
        if (result.returncode == 0) != expected_success:
            raise AssertionError(f"{tag}/{mode}: unexpected exit {result.returncode}: {result.stdout} {result.stderr}")
        log = case / "commands.log"
        commands = log.read_text().splitlines() if log.exists() else []
        operations = [line.split()[1] for line in commands]
        expected = {"new": ["view", "create", "upload", "edit"], "draft": ["view", "upload", "edit"],
                    "published": ["view"], "bad-checksum": [], "upload-failure": ["view", "create", "upload"]}[mode]
        assert operations == expected, (tag, mode, operations)
        flag = "--prerelease=" + str(pre).lower()
        for line in commands:
            if line.startswith(("release create", "release edit")):
                assert flag in line and "--latest=false" in line, (tag, mode, line)
                assert "Pre-release" in line if pre else "Stable" in line, (tag, mode, line)
        if expected_success:
            assert "--draft=false" in commands[-1]
            notes = (case / "release-notes.md").read_text(encoding="utf-8")
            assert ("출시 채널: Pre-release" if pre else "출시 채널: Stable") in notes
        print(f"PASS: {tag} / {mode} / prerelease={str(pre).lower()}")
    print(f"PASS: {len(cases)} offline workflow cases; no GitHub requests. Reports: {output}")


if __name__ == "__main__":
    main()
