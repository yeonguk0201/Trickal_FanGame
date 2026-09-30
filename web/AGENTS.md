<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->

## Game asset scale maintenance

Asset pixel dimensions, sprite frame-cell sizes, pixels-per-unit (PPU), and world-unit scale are intentionally undecided. Do not treat any temporary values as a permanent project standard.

When changing any of these values, update this section and `CLAUDE.md` in the same change so later work can use the current convention:

- character, enemy, boss, item, projectile, and UI icon pixel dimensions;
- sprite-sheet frame-cell dimensions and pivot conventions;
- Unity import settings, especially PPU, filter mode, and compression;
- world-unit scale and any related camera, collider, movement-speed, tile-size, or projectile-size assumptions.
