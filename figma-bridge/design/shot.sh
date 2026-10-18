#!/bin/bash
# Usage: ./shot.sh <frame-name-prefix> <out.png> [scale]  -> contact sheet of matching frames
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$DIR/.shot.js"
cat > "$OUT" <<JS
await figma.loadAllPagesAsync();
var page = figma.root.children.filter(function (p) { return p.name === 'Mobile'; })[0];
var prefixes = '$1'.split('|');
var frames = page.findAll(function (n) { return n.type === 'FRAME' && n.parent.type !== 'FRAME' && prefixes.some(function (p) { return n.name.indexOf(p) === 0; }); });
frames.sort(function (a, b) { return a.y - b.y || a.x - b.x; });
var sheet = figma.createFrame(); sheet.layoutMode = 'HORIZONTAL'; sheet.itemSpacing = 32;
sheet.paddingLeft = sheet.paddingRight = sheet.paddingTop = sheet.paddingBottom = 24;
sheet.primaryAxisSizingMode = 'AUTO'; sheet.counterAxisSizingMode = 'AUTO';
sheet.fills = [{ type: 'SOLID', color: { r: 0.8, g: 0.8, b: 0.8 } }];
page.appendChild(sheet); sheet.x = 0; sheet.y = -9000;
frames.forEach(function (f) { sheet.appendChild(f.clone()); });
log(frames.map(function (f) { return f.name; }).join(' | '));
var bytes = await sheet.exportAsync({ format: 'PNG', constraint: { type: 'SCALE', value: ${3:-0.6} } });
sheet.remove();
return bytes;
JS
node "$DIR/../run.js" "$OUT" "$2"
