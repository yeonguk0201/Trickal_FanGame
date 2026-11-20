"""Builds the offline boss feet tuner HTML from output/boss-feet-review (BossFeetLayoutExporter.ExportBatch).

Usage: python scripts/boss-feet-tuner/build.py [export folder]
"""
import base64
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[2]
folder = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root / "output" / "boss-feet-review"
template = pathlib.Path(__file__).with_name("template.html")
manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))


def data_url(name):
    if not name:
        return ""
    return "data:image/png;base64," + base64.b64encode((folder / name).read_bytes()).decode("ascii")


images = {}
for key in [manifest["playerFile"], manifest["playerShadowFile"]]:
    images[key] = data_url(key)
for boss in manifest["bosses"]:
    for key in [boss["roomFile"], boss["roomForegroundFile"], boss["shadowFile"]] + [p["file"] for p in boss["poses"]]:
        images[key] = data_url(key)

html = (template.read_text(encoding="utf-8")
        .replace("/*MANIFEST*/null", json.dumps(manifest, ensure_ascii=False))
        .replace("/*IMAGES*/null", json.dumps(images)))
out = folder / "boss-feet-tuner.html"
out.write_text(html, encoding="utf-8")
print(out)
