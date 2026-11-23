"""Build the offline loading-screen concept with existing project artwork."""
import base64
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
template = (HERE / "template.html").read_text(encoding="utf-8")
for token, path, mime in [
    ("__FONT__", ROOT / "game/Assets/Fonts/ONE Mobile POP.ttf", "font/ttf"),
    ("__ART__", ROOT / "game/Assets/Art/Characters/Player/Character_Erpin.png", "image/png"),
]:
    template = template.replace(token, f"data:{mime};base64," + base64.b64encode(path.read_bytes()).decode("ascii"))
(HERE / "loading-preview.html").write_text(template, encoding="utf-8")
print(HERE / "loading-preview.html")
