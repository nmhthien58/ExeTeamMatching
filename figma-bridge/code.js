// Runs in Figma's plugin sandbox. Receives scripts from ui.html (which polls
// the local bridge server), executes them against the Plugin API and sends
// the return value back.

figma.showUI(__html__, { width: 280, height: 150, themeColors: true });

function serialize(value, depth) {
  depth = depth || 0;
  if (value === undefined) return null;
  if (value === null || typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') return value;
  if (value instanceof Uint8Array) return { __bytes: figma.base64Encode(value) };
  if (typeof value === 'symbol') return 'figma.mixed';
  if (typeof value === 'function') return '[function]';
  if (depth > 8) return '[max depth]';
  if (Array.isArray(value)) return value.map(function (v) { return serialize(v, depth + 1); });
  // Scene nodes: return a compact summary instead of walking the whole node.
  if (typeof value.id === 'string' && typeof value.type === 'string' && 'parent' in value) {
    var summary = { id: value.id, type: value.type, name: value.name };
    if ('width' in value) {
      summary.x = value.x; summary.y = value.y;
      summary.width = value.width; summary.height = value.height;
    }
    return summary;
  }
  var out = {};
  for (var key in value) {
    try { out[key] = serialize(value[key], depth + 1); } catch (e) { out[key] = '[unreadable]'; }
  }
  return out;
}

figma.ui.onmessage = async function (msg) {
  if (!msg || msg.type !== 'run') return;
  var logs = [];
  var log = function () {
    logs.push(Array.prototype.map.call(arguments, function (a) {
      return typeof a === 'string' ? a : JSON.stringify(serialize(a));
    }).join(' '));
  };
  var reply = { type: 'result', id: msg.id, ok: true, result: null, logs: logs };
  try {
    // The script body may use `await`, `return`, `figma` and `log(...)`.
    var run = eval('(async function (figma, log) {' + msg.code + '\n})');
    reply.result = serialize(await run(figma, log));
  } catch (e) {
    reply.ok = false;
    reply.error = (e && e.message) ? e.message : String(e);
    reply.stack = e && e.stack ? String(e.stack) : undefined;
  }
  figma.ui.postMessage(reply);
};
