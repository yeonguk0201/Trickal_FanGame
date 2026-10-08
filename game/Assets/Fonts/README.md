# Frontend font

## Default font: ONE Mobile POP (2026-10-08)

- Source: `ONE Mobile POP.ttf` (ONE 모바일POP, © 2021 ONESTORE, designed by FONTRIX), supplied by the project owner and included unchanged.
- License: follow the ONE store font license (https://www.onestorecorp.com/). Per the license summary the project owner checked (2026-10-09), embedding in a program is allowed, while modifying, copying, redistributing or selling the font file itself is not. A game build is covered. This repository is public, so the TTF and its `.meta` are in `.gitignore` and are not committed: download the font from ONE store, put it at `Assets/Fonts/ONE Mobile POP.ttf`, and run the setup below once. Without the file the glyphs already in the TMP asset still render, but a Setup that needs a new character fails.
- `Trickal Fan Game > Fonts > Setup Default Font (ONE Mobile POP)` (`DefaultFontSetup`) makes it the source font of `Frontend Noto Sans KR.asset` and sets that asset as the TMP default font. The asset keeps its first file name and GUID so every Scene, Prefab and Setup reference stays valid; only its glyphs come from the new font.
- New UI text uses this TMP font asset (`Week13FrontendSetup.FontPath`). A character the font lacks makes a Setup's glyph step fail.

## Previous font: Noto Sans KR

- Source: [Noto Sans KR Regular, noto-cjk](https://github.com/notofonts/noto-cjk/blob/main/Sans/SubsetOTF/KR/NotoSansKR-Regular.otf)
- Downloaded: 2026-09-07. The original OTF is included unchanged.
- License: SIL Open Font License 1.1, included in `OFL.txt` from the upstream `Sans/LICENSE`.
- `Week13FrontendSetup` creates a dynamic, multi-atlas TMP asset and preloads the title's Korean and Latin glyphs. The Noto OTF stays included but is no longer the asset's source font.
- TMP Essential Resources are included from the installed Unity UI package; their own licenses remain with those resources. If removed, import them with `Window > TextMeshPro > Import TMP Essential Resources` before Setup.
