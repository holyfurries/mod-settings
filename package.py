import argparse
import json
import math
import shutil
import struct
import zlib
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

SLIDERS = [(86, 150), (128, 96), (170, 178)]


def icon_pixel(x: float, y: float) -> tuple[int, int, int]:
    corner_x = max(44.0, min(212.0, x))
    corner_y = max(44.0, min(212.0, y))
    panel_distance = math.hypot(x - corner_x, y - corner_y)
    if panel_distance > 32:
        return (10, 10, 10)
    if panel_distance > 26:
        return (240, 240, 240)
    for track_y, knob_x in SLIDERS:
        if math.hypot(x - knob_x, y - track_y) <= 14:
            return (61, 237, 97)
        if abs(y - track_y) <= 4 and 56 <= x <= 200:
            return (240, 240, 240) if x < knob_x else (90, 90, 90)
    return (33, 33, 33)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--prepare",
        action="store_true",
        help="Refresh the release DLL from the local Release build",
    )
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    manifest = json.loads((root / "package/manifest.json").read_text())
    assert len(manifest["description"]) <= 250
    assembly = root / "package/ModSettings.dll"
    if args.prepare:
        shutil.copyfile(root / "bin/Release/net6.0/ModSettings.dll", assembly)
    if not assembly.is_file():
        raise FileNotFoundError("Run a Release build, then python package.py --prepare")
    pixels = bytearray()
    for y in range(256):
        pixels.append(0)
        for x in range(256):
            pixels.extend(icon_pixel(x + 0.5, y + 0.5))
    icon = b"\x89PNG\r\n\x1a\n"
    for kind, data in (
        (b"IHDR", struct.pack(">2I5B", 256, 256, 8, 2, 0, 0, 0)),
        (b"IDAT", zlib.compress(bytes(pixels))),
        (b"IEND", b""),
    ):
        icon += (
            struct.pack(">I", len(data))
            + kind
            + data
            + struct.pack(">I", zlib.crc32(kind + data))
        )
    target = root / "dist" / f"ModSettings-{manifest['version_number']}.zip"
    target.parent.mkdir(exist_ok=True)
    with ZipFile(target, "w", ZIP_DEFLATED) as archive:
        archive.write(root / "package/manifest.json", "manifest.json")
        archive.write(root / "README.md", "README.md")
        archive.write(root / "LICENSE", "LICENSE")
        archive.writestr("icon.png", icon)
        archive.write(assembly, "Mods/ModSettings.dll")
    with ZipFile(target) as archive:
        assert archive.testzip() is None
        assert set(archive.namelist()) == {
            "manifest.json",
            "README.md",
            "LICENSE",
            "icon.png",
            "Mods/ModSettings.dll",
        }
    print(target)


if __name__ == "__main__":
    main()
