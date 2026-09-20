#!/usr/bin/env python3
"""Reject Mac payloads which need non-system libraries outside the bundle."""

import argparse
from pathlib import Path
import re
import subprocess
import sys

MACHO_MAGICS = {bytes.fromhex(value) for value in (
    "feedface", "cefaedfe", "feedfacf", "cffaedfe",
    "cafebabe", "bebafeca", "cafebabf", "bfbafeca")}


def load_commands(path, arch):
    """Read the selected architecture's commands; LC_ID_DYLIB is not a dependency."""
    output = subprocess.check_output(
        ["/usr/bin/otool", "-arch", arch, "-l", str(path)], text=True)
    return re.split(r"Load command \d+\n", output)[1:]


def field(command, name):
    """Return a load-command field without otool's trailing offset annotation."""
    match = re.search(r"^\s*" + name + r"\s+(.+?)(?:\s+\(offset \d+\))?$",
                      command, re.MULTILINE)
    return match.group(1) if match else ""


def system_path(path):
    """Apple's system dylibs can live in the dyld cache, not on disk."""
    return path.startswith(("/usr/lib/", "/System/Library/"))


def expand_path(value, binary, root):
    """Resolve dyld's local tokens without consulting the developer's environment."""
    return value.replace("@loader_path", str(binary.parent)).replace(
        "@executable_path", str(root))


def version(value):
    """Compare deployment targets independently of omitted patch components."""
    parts = tuple(int(part) for part in value.split("."))
    return parts + (0,) * (3 - len(parts))


def audit(root, arch, minimum):
    """Check every Mach-O dependency, architecture and declared minimum OS."""
    errors = []
    count = 0
    for binary in sorted(root.rglob("*")):
        if not binary.is_file():
            continue
        with binary.open("rb") as stream:
            if stream.read(4) not in MACHO_MAGICS:
                continue
        count += 1
        architectures = subprocess.check_output(
            ["/usr/bin/lipo", "-archs", str(binary)], text=True).split()
        if arch not in architectures:
            errors.append(f"{binary.name}: missing {arch} architecture")
            continue
        commands = load_commands(binary, arch)
        rpaths = [expand_path(field(command, "path"), binary, root)
                  for command in commands if field(command, "cmd") == "LC_RPATH"]
        for command in commands:
            kind = field(command, "cmd")
            if kind in ("LC_BUILD_VERSION", "LC_VERSION_MIN_MACOSX"):
                target = field(command, "minos" if kind == "LC_BUILD_VERSION" else "version")
                if version(target) > version(minimum):
                    errors.append(f"{binary.name}: requires macOS {target}, bundle promises {minimum}")
            if kind not in ("LC_LOAD_DYLIB", "LC_LOAD_WEAK_DYLIB",
                            "LC_REEXPORT_DYLIB", "LC_LOAD_UPWARD_DYLIB"):
                continue
            dependency = field(command, "name")
            if system_path(dependency):
                continue
            candidates = ([path + dependency[len("@rpath"): ] for path in rpaths]
                          if dependency.startswith("@rpath/") else
                          [expand_path(dependency, binary, root)])
            resolved = False
            for candidate in candidates:
                path = Path(candidate)
                if system_path(candidate):
                    resolved = True
                elif path.is_absolute() and path.is_file() and root in path.resolve().parents:
                    resolved = True
            if not resolved:
                errors.append(f"{binary.name}: unbundled dependency {dependency}")
    if count == 0:
        errors.append("No Mach-O files found; supply the staged payload or Contents/MacOS.")
    for error in errors:
        print(error, file=sys.stderr)
    print(f"Native dependency audit: {count} binaries, {len(errors)} errors")
    return not errors


def main():
    """Audit one payload without changing its files or signatures."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("payload", type=Path)
    parser.add_argument("--arch", default="arm64")
    parser.add_argument("--min-macos", default="13.0")
    args = parser.parse_args()
    return 0 if audit(args.payload.resolve(), args.arch, args.min_macos) else 1


if __name__ == "__main__":
    sys.exit(main())
