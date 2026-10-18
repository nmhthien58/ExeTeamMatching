// Local bridge between Claude (run.js) and the Figma plugin (ui.html).
// Listens on 127.0.0.1 only. No dependencies: `node server.js`.

const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const PORT = 3055;
const POLL_HOLD_MS = 25000;
const RUN_TIMEOUT_MS = 180000;

// /run requires this token so that a random web page cannot push scripts
// into the open Figma file through localhost.
const token = crypto.randomBytes(24).toString('hex');
fs.writeFileSync(path.join(__dirname, '.token'), token);

let nextId = 1;
const queue = [];           // jobs waiting for the plugin: { id, code }
const waiting = new Map();  // id -> { res, timer } for /run callers
let parked = null;          // the plugin's long-poll response, if idle
let lastPollAt = 0;

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

function send(res, status, body) {
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Access-Control-Allow-Origin': '*',
  });
  res.end(body === undefined ? '' : JSON.stringify(body));
}

function dispatch() {
  if (!parked || queue.length === 0) return;
  const { res, timer } = parked;
  parked = null;
  clearTimeout(timer);
  send(res, 200, queue.shift());
}

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];

  if (req.method === 'GET' && url === '/poll') {
    lastPollAt = Date.now();
    if (parked) { clearTimeout(parked.timer); send(parked.res, 204); }
    const timer = setTimeout(() => { parked = null; send(res, 204); }, POLL_HOLD_MS);
    parked = { res, timer };
    req.on('close', () => { if (parked && parked.res === res) { clearTimeout(timer); parked = null; } });
    return dispatch();
  }

  if (req.method === 'POST' && url === '/result') {
    const msg = JSON.parse(await readBody(req));
    const caller = waiting.get(msg.id);
    if (caller) {
      clearTimeout(caller.timer);
      waiting.delete(msg.id);
      send(caller.res, 200, msg);
    }
    return send(res, 200, { ok: true });
  }

  if (req.method === 'GET' && url === '/status') {
    return send(res, 200, {
      pluginConnected: Date.now() - lastPollAt < POLL_HOLD_MS + 5000,
      queued: queue.length,
    });
  }

  if (req.method === 'POST' && url === '/run') {
    if (req.headers['x-bridge-token'] !== token) return send(res, 403, { ok: false, error: 'bad token' });
    const code = await readBody(req);
    const id = nextId++;
    const timer = setTimeout(() => {
      waiting.delete(id);
      const i = queue.findIndex((j) => j.id === id);
      if (i !== -1) queue.splice(i, 1);
      send(res, 504, { ok: false, error: 'Timed out. Is the plugin window open in Figma?' });
    }, RUN_TIMEOUT_MS);
    waiting.set(id, { res, timer });
    queue.push({ id, code });
    return dispatch();
  }

  send(res, 404, { ok: false, error: 'not found' });
});

server.listen(PORT, '127.0.0.1', () => {
  console.log('Figma bridge listening on http://localhost:' + PORT);
});
