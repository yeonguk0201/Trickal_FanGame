@AGENTS.md

## Game asset scale maintenance

Asset pixel dimensions, sprite frame-cell sizes, pixels-per-unit (PPU), and world-unit scale are intentionally undecided. Do not treat temporary values as permanent standards.

Whenever these values change, update both this file and `AGENTS.md` in the same change. Record the current pixel sizes, frame-cell and pivot conventions, Unity import settings (PPU, filtering, compression), and related world-scale assumptions for the camera, colliders, movement, tiles, and projectiles.
