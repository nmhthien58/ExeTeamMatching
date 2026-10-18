#!/bin/bash
# Usage: ./shot2.sh <page> <section> <prefix|prefix...> <out.png> [scale] [all]
# Contact sheet of frames under a section. Default: first frame per prefix; "all": every match.
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$DIR/.shot.js"
cat > "$OUT" <<JS
await figma.loadAllPagesAsync();
var page = figma.root.children.filter(function (p) { return p.name === '$1'; })[0];
var sec = page.findOne(function (n) { return n.type === 'SECTION' && n.name === '$2'; });
var prefixes = '$3'.split('|');
var all = sec.findAll(function (n) { return n.type === 'FRAME' && n.parent.type === 'SECTION'; });
var frames = [];
prefixes.forEach(function (p) {
  var m = all.filter(function (n) { return n.name.indexOf(p) === 0; });
  frames = frames.concat('$6' === 'all' ? m : m.slice(0, 1));
});
var sheet = figma.createFrame(); sheet.layoutMode = 'HORIZONTAL'; sheet.itemSpacing = 32;
sheet.paddingLeft = sheet.paddingRight = sheet.paddingTop = sheet.paddingBottom = 24;
sheet.primaryAxisSizingMode = 'AUTO'; sheet.counterAxisSizingMode = 'AUTO';
sheet.fills = [{ type: 'SOLID', color: { r: 0.8, g: 0.8, b: 0.8 } }];
figma.currentPage.appendChild(sheet); sheet.x = -20000; sheet.y = -20000;
frames.forEach(function (f) { sheet.appendChild(f.clone()); });
log(frames.map(function (f) { return f.name; }).join(' | '));
var bytes = await sheet.exportAsync({ format: 'PNG', constraint: { type: 'SCALE', value: ${5:-0.5} } });
sheet.remove();
return bytes;
JS
node "$DIR/../run.js" "$OUT" "$4"
