"""Run a Blender Python job without requiring an MCP server or paid add-on."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[2]


def find_blender(explicit):
    if explicit:
        candidate = Path(explicit).expanduser().resolve()
        if not candidate.is_file():
            raise SystemExit(f"Blender executable does not exist: {candidate}")
        return candidate
    if os.environ.get("BLENDER_EXE"):
        return find_blender(os.environ["BLENDER_EXE"])
    if shutil.which("blender"):
        return Path(shutil.which("blender"))
    candidates = list((ROOT / "tmp/blender").glob("*/blender.exe"))
    for base in (os.environ.get("ProgramFiles"), os.environ.get("LOCALAPPDATA")):
        if base:
            candidates.extend((Path(base) / "Blender Foundation").glob("Blender */blender.exe"))
    if not candidates:
        raise SystemExit("Blender not found. Use --blender <path> or BLENDER_EXE.")
    return sorted(candidates, key=lambda path: path.stat().st_mtime, reverse=True)[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blender", help="Path to blender.exe")
    parser.add_argument("--script", type=Path, default=Path(__file__).with_name("smoke_preview.py"))
    parser.add_argument("--output", type=Path, default=ROOT / "output/blender-smoke")
    args = parser.parse_args()
    script = args.script.resolve()
    if not script.is_file():
        parser.error(f"Script not found: {script}")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    command = [str(find_blender(args.blender)), "--background", "--factory-startup",
               "--python-exit-code", "1", "--python", str(script), "--", "--output", str(output)]
    print(f"Blender: {command[0]}", flush=True)
    print(f"Output: {output}", flush=True)
    # Keep every job's log as UTF-8, including failures.
    with (output / "blender.log").open("w", encoding="utf-8") as log:
        result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
    if result.returncode:
        raise SystemExit(f"Blender failed ({result.returncode}). See {output / 'blender.log'}")
    print(f"Completed. Log: {output / 'blender.log'}")


if __name__ == "__main__":
    main()
