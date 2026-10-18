// Sends a script to the Figma plugin and prints what it returns.
//   node run.js script.js            run a script file
//   node run.js script.js out.png    also save a returned Uint8Array (e.g. exportAsync) to out.png
//   node run.js --status             check whether the plugin is connected

const fs = require('fs');
const path = require('path');

const BASE = 'http://localhost:3055';

async function main() {
  const [arg, outFile] = process.argv.slice(2);
  if (!arg) throw new Error('usage: node run.js <script.js> [out.png] | --status');

  if (arg === '--status') {
    console.log(await (await fetch(BASE + '/status')).text());
    return;
  }

  const token = fs.readFileSync(path.join(__dirname, '.token'), 'utf8');
  const res = await fetch(BASE + '/run', {
    method: 'POST',
    headers: { 'x-bridge-token': token },
    body: fs.readFileSync(arg, 'utf8'),
  });
  const msg = await res.json();

  for (const line of msg.logs || []) console.log('[log]', line);
  if (!msg.ok) {
    console.error('ERROR:', msg.error);
    if (msg.stack) console.error(msg.stack);
    process.exit(1);
  }
  if (msg.result && msg.result.__bytes) {
    const file = outFile || 'out.png';
    fs.writeFileSync(file, Buffer.from(msg.result.__bytes, 'base64'));
    console.log('saved', file);
  } else {
    console.log(JSON.stringify(msg.result, null, 2));
  }
}

main().catch((e) => { console.error(e.message); process.exit(1); });
