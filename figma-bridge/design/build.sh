#!/bin/bash
# Usage: ./build.sh <screens-file.js> [more.js...]  -> runs lib.js + files in the open Figma file
DIR="$(cd "$(dirname "$0")" && pwd)"
OUT="$DIR/.run.js"
cat "$DIR/lib.js" > "$OUT"
for f in "$@"; do echo "" >> "$OUT"; cat "$DIR/$f" >> "$OUT"; done
cat >> "$OUT" <<'JS'

await flush();
return CREATED.map(function (f) { return f.name + '  ' + Math.round(f.width) + 'x' + Math.round(f.height); });
JS
node "$DIR/../run.js" "$OUT"
