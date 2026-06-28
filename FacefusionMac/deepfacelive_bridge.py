from __future__ import annotations

import argparse
import platform
import subprocess
import sys
from pathlib import Path


REPO_URL = "https://github.com/iperov/DeepFaceLive.git"
ROOT = Path(__file__).resolve().parent
VENDOR_DIR = ROOT / "vendor" / "DeepFaceLive"


def run(command: list[str], cwd: Path | None = None) -> int:
    print("+ " + " ".join(command))
    completed = subprocess.run(command, cwd=str(cwd) if cwd else None, check=False)
    return int(completed.returncode)


def fetch() -> int:
    VENDOR_DIR.parent.mkdir(parents=True, exist_ok=True)
    if (VENDOR_DIR / ".git").exists():
        return run(["git", "pull", "--ff-only"], cwd=VENDOR_DIR)
    if VENDOR_DIR.exists() and any(VENDOR_DIR.iterdir()):
        print(f"{VENDOR_DIR} exists but is not a git checkout.")
        return 2
    return run(["git", "clone", "--depth", "1", REPO_URL, str(VENDOR_DIR)])


def status() -> int:
    system = platform.system()
    machine = platform.machine()
    print(f"Host: {system} {machine}")
    print(f"DeepFaceLive repo: {VENDOR_DIR}")
    print(f"Installed: {(VENDOR_DIR / '.git').exists()}")

    if system == "Darwin":
        print()
        print("DeepFaceLive official builds target Windows with DirectX 12.")
        print("On macOS, use FacefusionMac/app.py for the local realtime camera path.")
        print("Keep DeepFaceLive here as upstream reference or use it on a Windows machine.")
    elif system == "Windows":
        print()
        print("This host can use DeepFaceLive more directly.")
        print("After fetching, follow the upstream README/release instructions.")
    else:
        print()
        print("This host is not the official target for DeepFaceLive releases.")
    return 0


def open_upstream_readme() -> int:
    readme = VENDOR_DIR / "README.md"
    if not readme.exists():
        print("DeepFaceLive is not fetched yet. Run: python deepfacelive_bridge.py fetch")
        return 2
    print(readme.read_text(encoding="utf-8", errors="replace")[:4000])
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="DeepFaceLive upstream bridge for FacefusionMac.")
    parser.add_argument("command", choices=("status", "fetch", "readme"), help="Bridge command to run.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "status":
        return status()
    if args.command == "fetch":
        return fetch()
    if args.command == "readme":
        return open_upstream_readme()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
