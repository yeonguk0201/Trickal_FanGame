// Mechanical atlas export only: preserve the generated artwork and alpha; remove sheet gutters.
// Run with Node and sharp available via NODE_PATH.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'docs/art-prompts/pit-tiles-v1/pit-tile-atlas-v1.png');
const destination = path.join(root, 'game/Assets/Resources/PitTiles/Atlas.png');
const xs = [24, 328, 634, 936], ys = [30, 320, 610, 894];
const widths = [294, 302, 290, 300], heights = [271, 280, 271, 304];
async function main() {
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  const layers = [];
  for (let row = 0; row < 4; row++) for (let col = 0; col < 4; col++) {
    const input = await sharp(source).extract({ left: xs[col], top: ys[row],
      width: widths[col], height: heights[row] }).resize(256, 256, { fit: 'fill' }).png().toBuffer();
    layers.push({ input, left: col * 256, top: row * 256 });
  }
  // Keep corners at one scale and normalize open edges separately; original sheet gutters must not enter joins.
  const isolated = await sharp(layers[15].input).trim({ threshold: 8 }).resize(256, 256).png().toBuffer();
  const interior = await sharp(isolated).extract({ left: 112, top: 112, width: 32, height: 32 })
    .resize(256, 256).png().toBuffer();
  layers[0].input = interior;
  for (const index of [1, 2, 3, 4]) {
    const vertical = index === 2 || index === 4;
    const trimmed = await sharp(layers[index].input).trim({ threshold: 8 }).png().toBuffer();
    const bounds = await sharp(trimmed).metadata();
    const edge = await sharp(trimmed).extract({ left: vertical ? 0 : 12, top: vertical ? 12 : 0,
      width: bounds.width - (vertical ? 0 : 24), height: bounds.height - (vertical ? 24 : 0) })
      .resize(vertical ? 160 : 256, vertical ? 256 : 160, { fit: 'fill' }).png().toBuffer();
    const filler = await sharp(interior).resize(vertical ? 96 : 256, vertical ? 256 : 96, { fit: 'fill' })
      .png().toBuffer();
    layers[index].input = await sharp({ create: { width: 256, height: 256, channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 } } }).composite([
      { input: edge, left: index === 2 ? 96 : 0, top: index === 3 ? 96 : 0 },
      { input: filler, left: index === 4 ? 160 : 0, top: index === 1 ? 160 : 0 }
    ]).png().toBuffer();
  }
  for (const index of [5, 6, 7, 8, 15]) layers[index].input = isolated;
  for (let q = 0; q < 4; q++) {
    const corner = await sharp(layers[9 + q].input).trim({ threshold: 8 }).resize(128, 128).png().toBuffer();
    const right = q === 1 || q === 2, top = q < 2;
    layers[9 + q].input = await sharp({ create: { width: 256, height: 256, channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 } } }).composite([{ input: corner,
      left: right ? 128 : 0, top: top ? 0 : 128, blend: 'over' }]).png().toBuffer();
  }
  await sharp({ create: { width: 1024, height: 1024, channels: 4,
    background: { r: 0, g: 0, b: 0, alpha: 0 } } }).composite(layers).png().toFile(destination);
  console.log(destination);
  // Review the actual quarter-tile selection used by PitTileArtwork, rather than an AI layout concept.
  const shapes = [['#'], ['###', '..#', '..#'], ['###', '#..', '###'], ['###', '#.#', '###'],
    ['###', '..#', '###', '#..', '###'], ['#####'], ['#', '#', '#', '#', '#'], ['#..', '#..', '###', '#..', '#..']];
  const pieces = [];
  for (let i = 0; i < shapes.length; i++) {
    const shape = shapes[i];
    const occupied = (x, y) => shape[y]?.[x] === '#';
    for (let y = 0; y < shape.length; y++) for (let x = 0; x < shape[y].length; x++) {
      if (!occupied(x, y)) continue;
      for (let q = 0; q < 4; q++) {
        const right = q === 1 || q === 2, top = q < 2;
        const sx = right ? 1 : -1, sy = top ? -1 : 1;
        const h = occupied(x + sx, y), v = occupied(x, y + sy), d = occupied(x + sx, y + sy);
        const tile = !h && !v ? 5 + q : !v ? (top ? 1 : 3) : !h ? (right ? 2 : 4) : d ? 0 : 9 + q;
        const input = await sharp(destination).extract({ left: tile % 4 * 256 + (right ? 128 : 0),
          top: Math.floor(tile / 4) * 256 + (top ? 0 : 128), width: 128, height: 128 }).resize(40, 40).png().toBuffer();
        pieces.push({ input, left: i % 4 * 480 + Math.round((480 - shape[0].length * 80) / 2) + x * 80 + (right ? 40 : 0),
          top: Math.floor(i / 4) * 480 + Math.round((480 - shape.length * 80) / 2) + y * 80 + (top ? 0 : 40) });
      }
    }
  }
  await sharp({ create: { width: 1920, height: 960, channels: 4,
    background: { r: 184, g: 186, b: 128, alpha: 1 } } }).composite(pieces).png()
    .toFile(path.join(root, 'docs/art-prompts/pit-tiles-v1/pit-assembled-preview-v1.png'));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
