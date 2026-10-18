// Shared drawing library for the EXE Team Matching mobile screens.
// Prepended to every screen script (see build.sh). Follows DESIGN.md.

var FONT = 'Be Vietnam Pro';
await Promise.all([
  figma.loadFontAsync({ family: FONT, style: 'Regular' }),
  figma.loadFontAsync({ family: FONT, style: 'Medium' }),
  figma.loadFontAsync({ family: FONT, style: 'SemiBold' }),
  figma.loadFontAsync({ family: 'Material Symbols Rounded', style: 'Regular' }),
  figma.loadFontAsync({ family: 'Material Icons Round', style: 'Regular' }),
  figma.loadFontAsync({ family: 'Inter', style: 'Regular' })
]);
await figma.loadAllPagesAsync();

// ---------- tokens ----------
var COLORS = {
  'primary': '#0277BD', 'primary-dark': '#01579B', 'sky': '#29B6F6', 'primary-border': '#B3E5FC',
  'primary-container': '#E1F5FE', 'on-primary': '#FFFFFF', 'surface': '#FFFFFF', 'background': '#F5FAFE',
  'border': '#D9E2EC', 'border-strong': '#728AA3', 'text-primary': '#102A43', 'text-secondary': '#486581',
  'text-disabled': '#9FB3C8', 'scrim': '#102A43', 'success': '#1B5E20', 'success-container': '#E8F5E9',
  'warning': '#8A5300', 'warning-container': '#FFF4E0', 'error': '#B3261E', 'error-container': '#FDECEA',
  'neutral-container': '#EEF2F6'
};
var TYPE = {
  'display': [24, 32, 'SemiBold'], 'title': [20, 28, 'SemiBold'], 'subtitle': [16, 24, 'SemiBold'],
  'body-large': [16, 24, 'Regular'], 'body': [14, 20, 'Regular'], 'label': [14, 20, 'Medium'],
  'caption': [12, 16, 'Regular'], 'caption-bold': [12, 16, 'SemiBold']
};
var SPACE = { 4: 'space/1', 8: 'space/2', 12: 'space/3', 16: 'space/4', 24: 'space/6', 32: 'space/8' };
var RADIUS = { 8: 'radius/sm', 12: 'radius/md', 16: 'radius/lg', 24: 'radius/xl', 999: 'radius/full' };

function rgb(h) {
  return { r: parseInt(h.substr(1, 2), 16) / 255, g: parseInt(h.substr(3, 2), 16) / 255, b: parseInt(h.substr(5, 2), 16) / 255 };
}

var V = {}, TS = {}, ES = {};
(await figma.variables.getLocalVariablesAsync()).forEach(function (v) { V[v.name] = v; });
var _cols = await figma.variables.getLocalVariableCollectionsAsync();
function _col(name) {
  var c = _cols.filter(function (x) { return x.name === name; })[0];
  if (!c) { c = figma.variables.createVariableCollection(name); _cols.push(c); }
  return c;
}
function _ensure(c, name, type, val) {
  if (V[name]) return;
  var v = figma.variables.createVariable(name, c, type);
  v.setValueForMode(c.modes[0].modeId, val);
  V[name] = v;
}
var _cc = _col('Color');
Object.keys(COLORS).forEach(function (k) {
  var c = rgb(COLORS[k]);
  _ensure(_cc, 'color/' + k, 'COLOR', { r: c.r, g: c.g, b: c.b, a: k === 'scrim' ? 0.4 : 1 });
});
var _sc = _col('Space');
Object.keys(SPACE).forEach(function (k) { _ensure(_sc, SPACE[k], 'FLOAT', Number(k)); });
Object.keys(RADIUS).forEach(function (k) { _ensure(_sc, RADIUS[k], 'FLOAT', Number(k)); });
var _mc = _col('Motion');
_ensure(_mc, 'motion/fast', 'FLOAT', 100); _ensure(_mc, 'motion/base', 'FLOAT', 200); _ensure(_mc, 'motion/slow', 'FLOAT', 300);

(await figma.getLocalTextStylesAsync()).forEach(function (s) { TS[s.name] = s; });
Object.keys(TYPE).forEach(function (k) {
  if (TS['type/' + k]) return;
  var s = figma.createTextStyle(), d = TYPE[k];
  s.name = 'type/' + k; s.fontName = { family: FONT, style: d[2] }; s.fontSize = d[0];
  s.lineHeight = { unit: 'PIXELS', value: d[1] };
  TS[s.name] = s;
});
(await figma.getLocalEffectStylesAsync()).forEach(function (s) { ES[s.name] = s; });
function _shadow(y, blur) {
  var c = rgb(COLORS['text-primary']);
  return { type: 'DROP_SHADOW', color: { r: c.r, g: c.g, b: c.b, a: 0.08 }, offset: { x: 0, y: y }, radius: blur, spread: 0, visible: true, blendMode: 'NORMAL' };
}
[['elevation/0', []], ['elevation/1', [_shadow(1, 4)]], ['elevation/2', [_shadow(4, 16)]]].forEach(function (e) {
  if (ES[e[0]]) return;
  var s = figma.createEffectStyle(); s.name = e[0]; s.effects = e[1]; ES[e[0]] = s;
});

var PAGE = figma.root.children.filter(function (p) { return p.name === 'Mobile'; })[0];
if (!PAGE) { PAGE = figma.createPage(); PAGE.name = 'Mobile'; }
await figma.setCurrentPageAsync(PAGE);

// ---------- primitives ----------
var Q = [];   // deferred async style bindings
var P = {};   // pending sizing, applied when a node is appended to its parent

function pk() { for (var i = 0; i < arguments.length; i++) if (arguments[i] != null) return arguments[i]; return 0; }
function fill(tok) {
  var paint = { type: 'SOLID', color: rgb(COLORS[tok]) };
  return [figma.variables.setBoundVariableForPaint(paint, 'color', V['color/' + tok])];
}
function num(node, field, val, map) {
  node[field] = val;
  if (map[val]) { try { node.setBoundVariable(field, V[map[val]]); } catch (e) {} }
}
function radius(node, r, only) {
  (only || ['topLeftRadius', 'topRightRadius', 'bottomLeftRadius', 'bottomRightRadius']).forEach(function (f) { num(node, f, r, RADIUS); });
}
function sides(f, w) {
  f.strokeTopWeight = w.t || 0; f.strokeBottomWeight = w.b || 0; f.strokeLeftWeight = w.l || 0; f.strokeRightWeight = w.r || 0;
}
function add(parent, k) {
  parent.appendChild(k);
  var s = P[k.id];
  if (!s) return;
  if (s.w === 'fill') { k.layoutSizingHorizontal = 'FILL'; if (k.type === 'TEXT') k.textAutoResize = 'HEIGHT'; }
  if (s.h === 'fill') k.layoutSizingVertical = 'FILL';
  if (s.lines && k.type === 'TEXT' && s.w === 'fill') { k.textTruncation = 'ENDING'; k.maxLines = s.lines; }
  if (s.abs) { k.layoutPositioning = 'ABSOLUTE'; k.x = s.abs[0]; k.y = s.abs[1]; }
}

// box: auto-layout frame. o = {dir:'H'|'V', gap, p/px/py/pt/pb/pl/pr, fill, stroke, sw, r, w, h, minH,
// align (cross axis), justify (main axis), wrap, clip, elev, name, abs:[x,y], bt/bb (border top/bottom)}
function box(o, kids) {
  o = o || {};
  var f = figma.createFrame();
  f.name = o.name || 'Box';
  f.layoutMode = o.dir === 'H' ? 'HORIZONTAL' : 'VERTICAL';
  f.primaryAxisSizingMode = 'AUTO'; f.counterAxisSizingMode = 'AUTO';
  f.fills = o.fill ? fill(o.fill) : [];
  f.clipsContent = !!o.clip;
  if (o.gap != null) num(f, 'itemSpacing', o.gap, SPACE);
  num(f, 'paddingTop', pk(o.pt, o.py, o.p), SPACE); num(f, 'paddingBottom', pk(o.pb, o.py, o.p), SPACE);
  num(f, 'paddingLeft', pk(o.pl, o.px, o.p), SPACE); num(f, 'paddingRight', pk(o.pr, o.px, o.p), SPACE);
  if (o.r != null) radius(f, o.r, o.rOnly);
  if (o.stroke) { f.strokes = fill(o.stroke); f.strokeWeight = o.sw || 1; f.strokeAlign = 'INSIDE'; }
  if (o.bt || o.bb) { f.strokes = fill(o.bcolor || 'border'); f.strokeAlign = 'INSIDE'; sides(f, { t: o.bt, b: o.bb }); }
  if (o.align) f.counterAxisAlignItems = { start: 'MIN', center: 'CENTER', end: 'MAX' }[o.align];
  if (o.justify) f.primaryAxisAlignItems = { start: 'MIN', center: 'CENTER', end: 'MAX', between: 'SPACE_BETWEEN' }[o.justify];
  if (o.wrap) { f.layoutWrap = 'WRAP'; f.counterAxisSpacing = pk(o.gap, 8); }
  if (o.minH) f.minHeight = o.minH;
  if (o.maxW) f.maxWidth = o.maxW;
  if (o.elev) Q.push({ n: f, es: 'elevation/' + o.elev });
  (kids || []).forEach(function (k) { if (k) add(f, k); });
  if (typeof o.w === 'number') { f.layoutSizingHorizontal = 'FIXED'; f.resize(o.w, Math.max(1, f.height)); }
  if (typeof o.h === 'number') { f.layoutSizingVertical = 'FIXED'; f.resize(Math.max(1, f.width), o.h); }
  P[f.id] = { w: o.w, h: o.h, abs: o.abs };
  f.setPluginData('sh', typeof o.h === 'number' ? 'fixed' : (o.h === 'fill' ? 'fill' : ''));
  return f;
}
function H(o, kids) { o = o || {}; o.dir = 'H'; return box(o, kids); }
function Vb(o, kids) { o = o || {}; o.dir = 'V'; return box(o, kids); }
function sp() { return box({ name: 'Spacer', w: 'fill', h: 'fill' }); }
function gapBox(h) { return box({ name: 'Gap', h: h, w: 4 }); }

// T: text. o = {w:'fill'|number, align:'center'|'right', lines}
function T(str, style, color, o) {
  o = o || {};
  var t = figma.createText(), d = TYPE[style || 'body'];
  t.fontName = { family: FONT, style: d[2] }; t.fontSize = d[0]; t.lineHeight = { unit: 'PIXELS', value: d[1] };
  t.characters = String(str);
  t.fills = fill(color || 'text-primary');
  if (o.align) t.textAlignHorizontal = o.align.toUpperCase();
  if (typeof o.w === 'number') { t.textAutoResize = 'HEIGHT'; t.resize(o.w, t.height); }
  Q.push({ n: t, ts: 'type/' + (style || 'body') });
  P[t.id] = { w: o.w, lines: o.lines };
  return t;
}
// I: icon. Outlined = Material Symbols Rounded; filled = Material Icons Round.
function I(name, size, color, filled) {
  size = size || 24;
  var t = figma.createText();
  t.fontName = { family: filled ? 'Material Icons Round' : 'Material Symbols Rounded', style: 'Regular' };
  t.fontSize = size; t.lineHeight = { unit: 'PIXELS', value: size };
  t.characters = name; t.fills = fill(color || 'text-secondary'); t.name = 'icon/' + name;
  return t;
}
async function flush() {
  await Promise.all(Q.map(function (q) {
    return q.ts ? q.n.setTextStyleIdAsync(TS[q.ts].id) : q.n.setEffectStyleIdAsync(ES[q.es].id);
  }));
  Q = [];
}
// Children set to Fill while their parent was still hugging turn that parent Fixed at a stale
// size. settle() recomputes heights bottom-up once the root width is known. The height spec of
// each frame is kept in plugin data ('sh') so later runs (e.g. the @412 copies) can settle again.
function hSpec(n) { return n.getPluginData('sh'); }
function settle(n) {
  if (n.type !== 'FRAME' || n.layoutMode === 'NONE') return;
  var kids = n.children.filter(function (c) { return c.type === 'FRAME' && c.layoutPositioning !== 'ABSOLUTE'; });
  kids.forEach(settle);
  var fills = kids.filter(function (c) { return hSpec(c) === 'fill'; });
  if (hSpec(n) !== 'fixed') {
    if (n.layoutMode === 'VERTICAL' && fills.length) {
      fills.forEach(function (c) {
        c.layoutSizingVertical = 'FIXED';
        if (!c.children.length) c.resize(c.width, 1);
      });
    }
    n.layoutSizingVertical = 'HUG';
  }
  fills.forEach(function (c) { c.layoutSizingVertical = 'FILL'; });
}
function settleScreen(f) {
  settle(f);
  if (f.height < 800) { f.layoutSizingVertical = 'FIXED'; f.resize(f.width, 800); }
}

// ---------- components ----------
function statusBar(bg) {
  return H({ name: 'Status bar', w: 'fill', h: 24, px: 16, fill: bg || 'surface', justify: 'between', align: 'center' }, [
    T('9:41', 'caption-bold'),
    H({ gap: 4, align: 'center' }, [I('signal_cellular_alt', 16, 'text-primary', true), I('wifi', 16, 'text-primary', true), I('battery_full', 16, 'text-primary', true)])
  ]);
}
function sysNav(bg) {
  return H({ name: 'System navigation', w: 'fill', h: 24, fill: bg || 'surface', justify: 'center', align: 'center' }, [
    box({ name: 'Handle', w: 108, h: 4, r: 999, fill: 'text-primary' })
  ]);
}
function iconBtn(name, color, badge) {
  var kids = [I(name, 24, color || 'text-primary')];
  if (badge) {
    kids.push(H({ name: 'Badge', fill: 'error', r: 999, px: 4, minH: 16, align: 'center', justify: 'center', abs: [26, 6] }, [T(String(badge), 'caption-bold', 'on-primary')]));
  }
  return H({ name: 'Icon button/' + name, w: 48, h: 48, align: 'center', justify: 'center' }, kids);
}
// appBar(title, {back, actions:[icon | [icon, badge]], bg})
function appBar(title, o) {
  o = o || {};
  var kids = [];
  if (o.back) kids.push(iconBtn(o.close ? 'close' : 'arrow_back'));
  kids.push(T(title, 'title', null, { w: 'fill', lines: 1 }));
  (o.actions || []).forEach(function (a) { kids.push(typeof a === 'string' ? iconBtn(a) : iconBtn(a[0], null, a[1])); });
  return H({ name: 'App bar', w: 'fill', h: 56, fill: o.bg || 'surface', pl: o.back ? 4 : 16, pr: 4, gap: 4, align: 'center', bb: o.flat ? 0 : 1 }, kids);
}
var TABS = {
  student: [['home', 'Trang chủ'], ['explore', 'Khám phá'], ['groups', 'Nhóm'], ['notifications', 'Thông báo'], ['person', 'Hồ sơ']],
  lecturer: [['home', 'Trang chủ'], ['inbox', 'Yêu cầu'], ['groups', 'Nhóm'], ['person', 'Hồ sơ']]
};
function tabBar(role, active, unread) {
  var items = TABS[role].map(function (t, i) {
    var on = i === active;
    var pill = H({ name: 'Indicator', w: 64, h: 32, r: 999, fill: on ? 'primary-container' : null, align: 'center', justify: 'center' }, [
      I(t[0], 24, on ? 'primary' : 'text-secondary', on),
      (unread && t[0] === 'notifications') ? H({ name: 'Badge', fill: 'error', r: 999, px: 4, minH: 16, align: 'center', justify: 'center', abs: [34, 0] }, [T(String(unread), 'caption-bold', 'on-primary')]) : null
    ]);
    return Vb({ name: 'Tab/' + t[1] + (on ? '/Selected' : ''), w: 'fill', h: 'fill', gap: 4, align: 'center', justify: 'center' }, [pill, T(t[1], 'caption-bold', on ? 'text-primary' : 'text-secondary')]);
  });
  return H({ name: 'Tab bar', w: 'fill', h: 80, fill: 'surface', bt: 1 }, items);
}
function spinner(color) {
  var e = figma.createEllipse(); e.name = 'Spinner'; e.resize(20, 20);
  e.fills = fill(color || 'on-primary'); e.arcData = { startingAngle: 0, endingAngle: 4.6, innerRadius: 0.76 };
  return e;
}
// btn(label, kind, {w:'fill', icon}) kind: primary|secondary|text|danger|dangerText|disabled|loading
function btn(label, kind, o) {
  o = o || {}; kind = kind || 'primary';
  var bg = { primary: 'primary', loading: 'primary', danger: 'error', disabled: 'border', secondary: 'surface' }[kind] || null;
  var fg = { primary: 'on-primary', danger: 'on-primary', disabled: 'text-disabled', secondary: 'primary', text: 'primary', dangerText: 'error' }[kind];
  var kids = kind === 'loading' ? [spinner()] : [o.icon ? I(o.icon, 20, fg) : null, T(label, 'label', fg)];
  var flat = kind === 'text' || kind === 'dangerText';
  return H({ name: 'Button/' + kind + '/' + label, w: o.w, minH: 48, px: flat ? 12 : 24, py: 14, gap: 8, r: 12, fill: bg, stroke: kind === 'secondary' ? 'primary' : null, align: 'center', justify: 'center' }, kids);
}
// input(label, value, {ph, helper, error, optional, minH, lead, trail, disabled, counter})
function input(label, value, o) {
  o = o || {};
  var field = H({ name: 'Field', w: 'fill', minH: o.minH || 56, px: 16, py: 16, gap: 8, r: 12, fill: o.disabled ? 'background' : 'surface',
    stroke: o.error ? 'error' : (o.disabled ? 'border' : 'border-strong'), sw: o.error ? 2 : 1, align: o.minH ? 'start' : 'center' }, [
    o.lead ? I(o.lead, 20) : null,
    T(value || o.ph || '', 'body-large', o.disabled ? 'text-disabled' : (value ? 'text-primary' : 'text-secondary'), { w: 'fill' }),
    o.error ? I('error', 20, 'error') : (o.trail ? I(o.trail, 20) : null)
  ]);
  var foot = null;
  if (o.error || o.helper || o.counter) {
    foot = H({ w: 'fill', gap: 8 }, [
      T(o.error || o.helper || '', 'body', o.error ? 'error' : 'text-secondary', { w: 'fill' }),
      o.counter ? T(o.counter, 'caption', 'text-secondary') : null
    ]);
  }
  return Vb({ name: 'Input/' + label, w: 'fill', gap: 8 }, [
    label ? T(label + (o.optional ? ' (không bắt buộc)' : ''), 'label') : null,
    Vb({ w: 'fill', gap: 4 }, [field, foot])
  ]);
}
function search(ph, value) {
  return H({ name: 'Search', w: 'fill', minH: 48, px: 12, py: 12, gap: 8, r: 12, fill: 'surface', stroke: 'border-strong', align: 'center' }, [
    I('search', 20), T(value || ph, 'body-large', value ? 'text-primary' : 'text-secondary', { w: 'fill', lines: 1 }), value ? I('close', 20) : null
  ]);
}
var STATUS = { warning: 'schedule', success: 'check_circle', error: 'cancel', primary: 'verified', neutral: 'remove_circle' };
// chip(label, kind, icon) kind: domain|skill|filter|filterOn|warning|success|error|primary|neutral
function chip(label, kind, icon) {
  kind = kind || 'skill';
  var st = STATUS[kind];
  var bg = { domain: 'primary-container', filterOn: 'primary', warning: 'warning-container', success: 'success-container', error: 'error-container', primary: 'primary-container', neutral: 'neutral-container' }[kind] || null;
  var fg = { domain: 'primary-dark', skill: 'text-secondary', filter: 'text-primary', filterOn: 'on-primary', warning: 'warning', success: 'success', error: 'error', primary: 'primary-dark', neutral: 'text-secondary' }[kind];
  var ic = icon || st || (kind === 'filterOn' ? 'check' : null);
  var icColor = kind === 'primary' ? 'primary' : fg;
  return H({ name: 'Chip/' + kind + '/' + label, h: 32, pl: ic ? 8 : 12, pr: 12, gap: 4, r: 999, fill: bg,
    stroke: kind === 'skill' ? 'border' : (kind === 'filter' ? 'border-strong' : null), align: 'center' }, [
    ic ? I(ic, 16, icColor) : null, T(label, 'label', fg)
  ]);
}
function chips(labels, kind, o) {
  return H({ name: 'Chips', w: 'fill', gap: 8, wrap: true }, labels.map(function (l) { return typeof l === 'string' ? chip(l, kind) : l; }));
}
// banner(kind, icon, text, action) kind: info|warning|error|neutral|success
function banner(kind, icon, text, action) {
  var bg = { info: 'primary-container', warning: 'warning-container', error: 'error-container', neutral: 'neutral-container', success: 'success-container' }[kind];
  var ic = { info: 'primary', warning: 'warning', error: 'error', neutral: 'text-secondary', success: 'success' }[kind];
  return H({ name: 'Banner/' + kind, w: 'fill', px: 16, py: 12, gap: 12, r: 12, fill: bg, align: 'center' }, [
    I(icon, 20, ic), T(text, 'body', kind === 'error' ? 'error' : 'text-primary', { w: 'fill' }), action ? T(action, 'label', 'primary') : null
  ]);
}
// deadline(level) level: open|72|24|closed
function deadline(level) {
  if (level === '72') return banner('warning', 'schedule', 'Còn 2 ngày 5 giờ · Hạn chót 18:00 · 17/09/2026');
  if (level === '24') return banner('error', 'warning', 'Còn 5 giờ · Hạn chót 18:00 · 17/09/2026');
  if (level === 'closed') return banner('neutral', 'lock', 'Đợt đăng ký đã đóng lúc 18:00 · 17/09/2026');
  return banner('info', 'event', 'Hạn chót 18:00 · 17/09/2026');
}
function dots(n) {
  var d = [];
  for (var i = 0; i < 6; i++) d.push(box({ name: i < n ? 'Dot/filled' : 'Dot/empty', w: 12, h: 12, r: 999, fill: i < n ? 'primary' : null, stroke: i < n ? null : 'border-strong' }));
  return H({ name: 'Dots', gap: 4, align: 'center' }, d);
}
function check(ok, text) {
  return H({ name: 'Check', w: 'fill', gap: 8, align: 'center' }, [I(ok ? 'check_circle' : 'error', 20, ok ? 'success' : 'warning'), T(text, 'body', ok ? 'success' : 'warning', { w: 'fill' })]);
}
// reqBar(n, full, specOk): team requirement bar
function reqBar(n, full, specOk) {
  var row = H({ name: 'Members', gap: 8, align: 'center' }, [dots(n), T(n + '/6 thành viên', 'body')]);
  if (!full) return row;
  return Vb({ name: 'Team requirement bar', w: 'fill', gap: 8 }, [
    row,
    check(n >= 4 && n <= 6, n >= 4 ? '4–6 thành viên' : '4–6 thành viên · cần thêm ' + (4 - n) + ' người'),
    check(specOk, specOk ? 'Ít nhất 2 chuyên ngành' : 'Ít nhất 2 chuyên ngành · nhóm mới có 1')
  ]);
}
function avatar(initials, size) {
  size = size || 40;
  return H({ name: 'Avatar', w: size, h: size, r: 999, fill: 'primary-border', align: 'center', justify: 'center' }, [T(initials, size >= 72 ? 'title' : 'label', 'primary-dark')]);
}
function initials(name) { var p = name.split(' '); return (p[0][0] + p[p.length - 1][0]).toUpperCase(); }
function card(o, kids) {
  o = o || {};
  return Vb({ name: o.name || 'Card', w: 'fill', p: pk(o.p, 16), gap: pk(o.gap, 8), r: 16, fill: o.fill || 'surface', stroke: o.stroke || 'border', sw: o.sw || 1 }, kids);
}
function divider() { return box({ name: 'Divider', w: 'fill', h: 1, fill: 'border' }); }
function head(text, action) {
  return H({ name: 'Section header', w: 'fill', gap: 8, align: 'center' }, [T(text, 'subtitle', null, { w: 'fill' }), action ? T(action, 'label', 'primary') : null]);
}
function kv(label, value) {
  return H({ name: 'Row/' + label, w: 'fill', gap: 16, py: 12 }, [T(label, 'body', 'text-secondary'), T(value, 'body', 'text-primary', { w: 'fill', align: 'right' })]);
}
// teamCard({name, project, domains, n, missing, reason, status:[label, kind]})
function teamCard(d) {
  return card({ name: 'Team card/' + d.name }, [
    H({ w: 'fill', gap: 8 }, [
      Vb({ w: 'fill' }, [T(d.name, 'subtitle', null, { w: 'fill', lines: 2 }), T(d.project, 'body', 'text-secondary', { w: 'fill', lines: 1 })]),
      d.status ? chip(d.status[0], d.status[1], d.status[2]) : null
    ]),
    d.domains ? chips(d.domains, 'domain') : null,
    reqBar(d.n),
    d.missing ? T('Đang thiếu: ' + d.missing, 'body', 'text-secondary', { w: 'fill' }) : null,
    d.reason ? H({ w: 'fill', gap: 8, align: 'center' }, [I('auto_awesome', 20, 'primary'), T(d.reason, 'body', 'primary-dark', { w: 'fill' })]) : null
  ]);
}
// person({name, id, spec, skills, tag:[label, kind], note, actions:[nodes], trail: node, sub})
function person(d, bare) {
  var kids = [
    H({ w: 'fill', gap: 12, align: 'center' }, [
      avatar(initials(d.name)),
      Vb({ w: 'fill' }, [T(d.name, 'subtitle', null, { w: 'fill', lines: 2 }), T(d.sub || (d.id + ' · ' + d.spec), 'body', 'text-secondary', { w: 'fill', lines: 1 })]),
      d.tag ? chip(d.tag[0], d.tag[1], d.tag[2]) : null, d.trail || null
    ]),
    d.skills ? chips(d.skills, 'skill') : null,
    d.note ? T(d.note, 'body', 'text-secondary', { w: 'fill' }) : null,
    d.reason ? H({ w: 'fill', gap: 8, align: 'center' }, [I('auto_awesome', 20, 'primary'), T(d.reason, 'body', 'primary-dark', { w: 'fill' })]) : null,
    d.time ? T(d.time, 'caption', 'text-secondary') : null,
    d.actions ? H({ w: 'fill', gap: 8, justify: 'end' }, d.actions) : null
  ];
  if (bare) return Vb({ name: 'Member/' + d.name, w: 'fill', gap: 8, py: 12 }, kids);
  return card({ name: 'Student card/' + d.name, gap: 12 }, kids);
}
function row(lead, title, sub, trail) {
  return H({ name: 'Row/' + title, w: 'fill', gap: 12, py: 12, minH: 48, align: 'center' }, [
    typeof lead === 'string' ? I(lead, 24) : lead,
    Vb({ w: 'fill' }, [T(title, 'body-large', null, { w: 'fill' }), sub ? T(sub, 'body', 'text-secondary', { w: 'fill' }) : null]),
    typeof trail === 'string' ? I(trail, 24) : (trail || null)
  ]);
}
function empty(icon, title, desc, action) {
  return Vb({ name: 'Empty state', w: 'fill', h: 'fill', gap: 8, py: 32, px: 16, align: 'center', justify: 'center' }, [
    I(icon, 48, 'text-secondary'), gapBox(8), T(title, 'subtitle', null, { w: 'fill', align: 'center' }),
    T(desc, 'body', 'text-secondary', { w: 'fill', align: 'center' }), action ? gapBox(8) : null, action || null
  ]);
}
function sk(w, h, r) { return box({ name: 'Skeleton', w: w, h: h, r: r == null ? 8 : r, fill: 'neutral-container' }); }
function skCard() {
  return card({ name: 'Card/Skeleton', gap: 12 }, [sk(180, 20), sk(260, 16), H({ gap: 8 }, [sk(96, 32, 999), sk(120, 32, 999)]), sk(140, 16)]);
}
function skRow() {
  return H({ name: 'Row/Skeleton', w: 'fill', gap: 12, py: 12, align: 'center' }, [sk(40, 40, 999), Vb({ gap: 8 }, [sk(200, 16), sk(140, 14)])]);
}
function toggle(on) {
  return H({ name: 'Switch/' + (on ? 'On' : 'Off'), w: 52, h: 32, r: 999, p: 4, fill: on ? 'primary' : 'surface', stroke: on ? null : 'border-strong', sw: 2, align: 'center', justify: on ? 'end' : 'start' }, [
    box({ w: on ? 24 : 16, h: on ? 24 : 16, r: 999, fill: on ? 'on-primary' : 'border-strong' }), null
  ]);
}
function tabs(labels, active) {
  return H({ name: 'Tabs', w: 'fill', fill: 'surface', bb: 1 }, labels.map(function (l, i) {
    var on = i === active;
    return H({ name: 'Tab/' + l, w: 'fill', minH: 48, py: 14, align: 'center', justify: 'center', bb: on ? 2 : 0, bcolor: 'primary' }, [T(l, 'label', on ? 'primary' : 'text-secondary')]);
  }));
}
// timeline([{state:'done'|'now'|'todo'|'fail', title, desc, time}])
function timeline(steps) {
  return Vb({ name: 'Timeline', w: 'fill' }, steps.map(function (s, i) {
    var ic = { done: ['check_circle', 'success'], now: ['schedule', 'warning'], todo: ['radio_button_unchecked', 'text-disabled'], fail: ['cancel', 'error'], final: ['verified', 'primary'] }[s.state];
    return H({ name: 'Step/' + s.title, w: 'fill', gap: 12 }, [
      Vb({ h: 'fill', align: 'center', gap: 4 }, [I(ic[0], 24, ic[1], s.state !== 'todo'), i < steps.length - 1 ? box({ w: 2, h: 'fill', fill: 'border' }) : null]),
      Vb({ w: 'fill', pb: 24, gap: 4 }, [
        T(s.title, 'subtitle', s.state === 'todo' ? 'text-secondary' : 'text-primary', { w: 'fill' }),
        s.desc ? T(s.desc, 'body', 'text-secondary', { w: 'fill' }) : null,
        s.time ? T(s.time, 'caption', 'text-secondary') : null
      ])
    ]);
  }));
}
function dialog(title, body, cancel, action, danger) {
  return Vb({ name: 'Dialog', w: 'fill', maxW: 400, p: 24, gap: 16, r: 24, fill: 'surface', elev: 2 }, [
    T(title, 'title', null, { w: 'fill' }),
    typeof body === 'string' ? T(body, 'body', 'text-secondary', { w: 'fill' }) : body,
    H({ w: 'fill', gap: 8, justify: 'end' }, [btn(cancel || 'Hủy', 'text'), btn(action, danger ? 'dangerText' : 'text')])
  ]);
}
function sheet(title, kids) {
  var body = Vb({ w: 'fill', px: 24, pb: 24, gap: 16 }, [
    H({ w: 'fill', align: 'center', gap: 8 }, [T(title, 'title', null, { w: 'fill' }), iconBtn('close')])
  ].concat(kids));
  return Vb({ name: 'Bottom sheet', w: 'fill', fill: 'surface', r: 24, rOnly: ['topLeftRadius', 'topRightRadius'], elev: 2 }, [
    H({ w: 'fill', py: 12, justify: 'center' }, [box({ name: 'Handle', w: 32, h: 4, r: 999, fill: 'border-strong' })]),
    body, sysNav()
  ]);
}
function snackbar(text) {
  return H({ name: 'Snackbar', w: 'fill', px: 16, py: 14, r: 8, fill: 'text-primary', elev: 2 }, [T(text, 'body', 'on-primary', { w: 'fill' })]);
}
function bottomBar(kids, note) {
  return Vb({ name: 'Bottom bar', w: 'fill', p: 16, gap: 8, fill: 'surface', bt: 1 }, kids.concat(note ? [T(note, 'body', 'text-secondary', { w: 'fill' })] : []));
}

// ---------- screens ----------
var CREATED = [];
function removeOld(name) {
  PAGE.findAll(function (n) { return n.type === 'FRAME' && n.name === name && (n.parent.type === 'PAGE' || n.parent.type === 'SECTION'); })
    .forEach(function (n) { n.remove(); });
}
// screen(name, {bar, body:[...], p, gap, bg, tab:[role, index, unread], bottom, snack, barBg, tabsTop, top})
function screen(name, o) {
  removeOld(name);
  var body = Vb({ name: 'Body', w: 'fill', h: 'fill', p: pk(o.p, 16), gap: pk(o.gap, 16) }, o.body || []);
  var f = Vb({ name: name, w: 360, minH: 800, fill: o.bg || 'background', clip: true }, [
    statusBar(o.barBg), o.bar || null, o.top || null, body,
    o.snack ? Vb({ w: 'fill', px: 16, pb: 8 }, [snackbar(o.snack)]) : null,
    o.bottom || null, o.tab ? tabBar(o.tab[0], o.tab[1], o.tab[2]) : null, sysNav(o.navBg)
  ]);
  PAGE.appendChild(f);
  settleScreen(f);
  f.x = CREATED.length * 440; f.y = -2000;
  CREATED.push(f);
  return f;
}
// overlay(screenFrame, node, 'center'|'bottom'): scrim + dialog or bottom sheet on top of a screen
function overlay(scr, node, pos) {
  var ov = Vb({ name: 'Overlay', fill: 'scrim', align: 'center', justify: pos === 'bottom' ? 'end' : 'center', px: pos === 'bottom' ? 0 : 24 }, [node]);
  scr.appendChild(ov);
  ov.layoutPositioning = 'ABSOLUTE'; ov.x = 0; ov.y = 0;
  ov.layoutSizingHorizontal = 'FIXED'; ov.layoutSizingVertical = 'FIXED';
  ov.resize(scr.width, scr.height);
  ov.constraints = { horizontal: 'STRETCH', vertical: 'STRETCH' };
  ov.children.forEach(settle);
  return scr;
}

// S2. Sinh viên – Khám phá và ghép nhóm

var TEAMS = [
  { name: 'GreenBox', project: 'GreenBox – Hộp cơm xanh cho sinh viên', domains: ['D04 Software / SaaS', 'D12 Marketing'], n: 4, missing: 'Thiết kế mỹ thuật số' },
  { name: 'EduSpark', project: 'EduSpark – Gia sư AI cho học sinh cấp 3', domains: ['D01 AI & Data'], n: 5, missing: 'Marketing' },
  { name: 'VietCraft', project: 'VietCraft – Sàn thủ công mỹ nghệ Việt', domains: ['D17 Culture', 'D12 Marketing'], n: 5, missing: 'Thiết kế mỹ thuật số' },
  { name: 'PawPal', project: 'PawPal – Chăm sóc thú cưng theo yêu cầu', domains: ['D04 Software / SaaS'], n: 3, missing: 'Kinh doanh quốc tế, Marketing' }
];
var MEMBERS = [
  { name: 'Nguyễn Minh An', id: 'SE190001', spec: 'Kỹ thuật phần mềm', tag: ['Trưởng nhóm', 'primary', 'star'] },
  { name: 'Trần Thu Hà', id: 'SS190002', spec: 'Marketing' },
  { name: 'Lê Quốc Bảo', id: 'SE190003', spec: 'Trí tuệ nhân tạo' },
  { name: 'Phạm Ngọc Linh', id: 'SA190004', spec: 'Kinh doanh quốc tế' }
];
function segment(labels, active) {
  return H({ name: 'Segmented', w: 'fill', p: 4, gap: 4, r: 12, fill: 'neutral-container' }, labels.map(function (l, i) {
    return H({ w: 'fill', minH: 40, r: 8, fill: i === active ? 'surface' : null, align: 'center', justify: 'center' }, [T(l, 'label', i === active ? 'primary-dark' : 'text-secondary')]);
  }));
}
function filterRow(on) {
  return H({ name: 'Filters', w: 'fill', gap: 8, wrap: true }, [
    chip('Bộ lọc' + (on ? ' · 2' : ''), 'filter', 'tune')
  ].concat(on ? [chip('D04 Software / SaaS', 'filterOn'), chip('Thiếu: Thiết kế mỹ thuật số', 'filterOn')] : []));
}
function memberList(list) {
  var kids = [];
  list.forEach(function (m, i) { if (i) kids.push(divider()); kids.push(person(m, true)); });
  return card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, kids)]);
}

// S05
function explore(state) {
  var o = { bar: appBar('Khám phá', { flat: true }), tab: ['student', 1, 2], gap: 12,
    top: Vb({ w: 'fill', px: 16, pb: 12, gap: 12, fill: 'surface', bb: 1 }, [segment(['Nhóm đang tuyển', 'Sinh viên tìm nhóm'], 0), search('Tìm theo tên nhóm hoặc dự án', state === 'Không tìm thấy' ? 'robot' : null)]) };
  if (state === 'Có dữ liệu') {
    o.body = [filterRow(false), head('Gợi ý cho bạn', 'Xem tất cả'),
      teamCard({ name: 'GreenBox', project: TEAMS[0].project, domains: TEAMS[0].domains, n: 4, reason: 'Đang thiếu chuyên ngành Thiết kế mỹ thuật số của bạn' }),
      head('Tất cả nhóm đang tuyển · 17'), teamCard(TEAMS[1]), teamCard(TEAMS[2]), teamCard(TEAMS[3])];
  } else if (state === 'Đã lọc') {
    o.body = [filterRow(true), T('2 nhóm phù hợp', 'body', 'text-secondary'), teamCard(TEAMS[0]), teamCard(TEAMS[3]), sp()];
  } else if (state === 'Không tìm thấy') {
    o.body = [filterRow(true), empty('search_off', 'Không tìm thấy nhóm phù hợp', 'Không có nhóm đang tuyển khớp với "robot" và 2 bộ lọc đang áp dụng.', btn('Xóa bộ lọc', 'secondary'))];
  } else if (state === 'Đang tải') {
    o.body = [H({ gap: 8 }, [sk(96, 32, 999)]), skCard(), skCard(), skCard(), sp()];
  } else {
    o.body = [empty('cloud_off', 'Không tải được danh sách nhóm', 'Kiểm tra kết nối mạng rồi thử lại.', btn('Thử lại', 'primary'))];
  }
  return screen('S05 – Khám phá – Nhóm đang tuyển / ' + state, o);
}
['Có dữ liệu', 'Đã lọc', 'Không tìm thấy', 'Đang tải', 'Lỗi tải'].forEach(explore);

// S06
function filterSheet(state) {
  var on = state === 'Đã chọn';
  var f = explore_base('S06 – Bộ lọc và sắp xếp / ' + state);
  function group(title, items) { return Vb({ w: 'fill', gap: 8 }, [T(title, 'label'), chips(items)]); }
  overlay(f, sheet('Bộ lọc và sắp xếp', [
    group('Domain', [chip('D01 AI & Data', 'filter'), chip('D04 Software / SaaS', on ? 'filterOn' : 'filter'), chip('D10 Graphic Design', 'filter'), chip('D12 Marketing', 'filter'), chip('+14 domain', 'filter', 'expand_more')]),
    group('Chuyên ngành nhóm đang thiếu', [chip('Thiết kế mỹ thuật số', on ? 'filterOn' : 'filter'), chip('Marketing', 'filter'), chip('Kỹ thuật phần mềm', 'filter'), chip('Kinh doanh quốc tế', 'filter')]),
    group('Số chỗ trống', [chip('Từ 1', on ? 'filter' : 'filterOn'), chip('Từ 2', 'filter'), chip('Từ 3', 'filter')]),
    group('Sắp xếp', [chip('Độ phù hợp', 'filterOn'), chip('Mới nhất', 'filter'), chip('Nhiều chỗ trống', 'filter')]),
    H({ w: 'fill', gap: 8 }, [btn('Xóa bộ lọc', 'text'), btn(on ? 'Xem 2 nhóm' : 'Xem 17 nhóm', 'primary', { w: 'fill' })])
  ]), 'bottom');
  return f;
}
function explore_base(name) {
  return screen(name, { bar: appBar('Khám phá', { flat: true }), tab: ['student', 1, 2], gap: 12,
    top: Vb({ w: 'fill', px: 16, pb: 12, gap: 12, fill: 'surface', bb: 1 }, [segment(['Nhóm đang tuyển', 'Sinh viên tìm nhóm'], 0), search('Tìm theo tên nhóm hoặc dự án')]),
    body: [filterRow(false), teamCard(TEAMS[0]), teamCard(TEAMS[1]), sp()] });
}
var _s06a = filterSheet('Chưa chọn'), _s06b = filterSheet('Đã chọn');

// S07
screen('S07 – Nhóm gợi ý cho bạn / Có gợi ý', { bar: appBar('Nhóm gợi ý cho bạn', { back: true }), gap: 12, body: [
  T('Dựa trên chuyên ngành và kỹ năng trong hồ sơ của bạn.', 'body', 'text-secondary', { w: 'fill' }),
  teamCard({ name: 'GreenBox', project: TEAMS[0].project, domains: TEAMS[0].domains, n: 4, reason: 'Đang thiếu chuyên ngành Thiết kế mỹ thuật số của bạn' }),
  teamCard({ name: 'VietCraft', project: TEAMS[2].project, domains: TEAMS[2].domains, n: 5, reason: 'Cần kỹ năng Brand kit bạn đã chọn' }),
  teamCard({ name: 'PawPal', project: TEAMS[3].project, domains: TEAMS[3].domains, n: 3, reason: 'Nhóm mới có 1 chuyên ngành, bạn bổ sung chuyên ngành thứ hai' }), sp()] });
screen('S07 – Nhóm gợi ý cho bạn / Chưa đủ dữ liệu hồ sơ', { bar: appBar('Nhóm gợi ý cho bạn', { back: true }), body: [
  empty('auto_awesome', 'Chưa đủ thông tin để gợi ý', 'Thêm kỹ năng vào hồ sơ để ứng dụng tìm nhóm đang cần bạn.', btn('Bổ sung hồ sơ', 'primary'))] });
screen('S07 – Nhóm gợi ý cho bạn / Đang tải', { bar: appBar('Nhóm gợi ý cho bạn', { back: true }), gap: 12, body: [sk(280, 16), skCard(), skCard(), skCard(), sp()] });

// S08
function teamDetail(state) {
  var n = state === 'Nhóm đã đủ' ? 6 : 4, bottom, top = null;
  var members = MEMBERS.slice();
  if (n === 6) members = members.concat([{ name: 'Hoàng Mai Chi', id: 'DE180004', spec: 'Thiết kế đồ họa và mỹ thuật số' }, { name: 'Ngô Đức Trí', id: 'SE190009', spec: 'An toàn thông tin' }]);
  if (state === 'Mặc định') bottom = bottomBar([btn('Xin tham gia', 'primary', { w: 'fill' })]);
  if (state === 'Đã gửi yêu cầu') { top = banner('warning', 'schedule', 'Yêu cầu tham gia đang chờ trưởng nhóm. Còn 2 ngày 23 giờ.'); bottom = bottomBar([btn('Hủy yêu cầu', 'secondary', { w: 'fill' })], 'Đã gửi lúc 10:12 · 14/09/2026. Yêu cầu hết hạn sau 72 giờ.'); }
  if (state === 'Nhóm đã đủ') bottom = bottomBar([btn('Xin tham gia', 'disabled', { w: 'fill' })], 'Nhóm đã đủ 6 thành viên. Xem các nhóm khác đang tuyển.');
  if (state === 'Đợt đã khóa') { top = deadline('closed'); bottom = bottomBar([btn('Xin tham gia', 'disabled', { w: 'fill' })], 'Đợt đăng ký đã đóng lúc 18:00 · 17/09/2026. Bộ môn sẽ ghép nhóm cho sinh viên chưa có nhóm.'); }
  if (state === 'Bạn đã có nhóm') bottom = bottomBar([btn('Xin tham gia', 'disabled', { w: 'fill' })], 'Bạn đang là thành viên nhóm EduSpark. Mỗi sinh viên chỉ thuộc một nhóm.');
  if (state === 'Xem từ lời mời') { top = banner('info', 'mail', 'Nguyễn Minh An mời bạn tham gia. Lời mời còn 2 ngày 21 giờ.'); bottom = Vb({ name: 'Bottom bar', w: 'fill', p: 16, fill: 'surface', bt: 1 }, [H({ w: 'fill', gap: 8 }, [btn('Từ chối', 'secondary', { w: 'fill' }), btn('Chấp nhận', 'primary', { w: 'fill' })])]); }
  return screen('S08 – Chi tiết nhóm / ' + state, { bar: appBar('Chi tiết nhóm', { back: true }), bottom: bottom, snack: state === 'Đã gửi yêu cầu' ? 'Đã gửi yêu cầu tham gia' : null, body: [
    top,
    Vb({ w: 'fill', gap: 8 }, [
      H({ w: 'fill', gap: 8, align: 'center' }, [T('GreenBox', 'display', null, { w: 'fill' }), chip(n === 6 ? 'Đủ điều kiện' : 'Đang tuyển', n === 6 ? 'success' : 'primary')]),
      T('GreenBox – Hộp cơm xanh cho sinh viên', 'body-large', 'text-secondary', { w: 'fill' }),
      chips(['D04 Software / SaaS', 'D12 Marketing & Consumer Growth'], 'domain')
    ]),
    card({ gap: 12 }, [T('Điều kiện nhóm', 'subtitle'), reqBar(n, true, true), n < 6 ? T('Đang thiếu: Thiết kế mỹ thuật số', 'body', 'text-secondary', { w: 'fill' }) : null]),
    card({ gap: 8 }, [T('Ý tưởng', 'subtitle'), T('Ứng dụng đặt cơm hộp tái sử dụng trong ký túc xá: sinh viên đặt suất ăn, nhận hộp và trả hộp tại điểm thu, tích điểm đổi ưu đãi.', 'body-large', null, { w: 'fill' })]),
    n < 6 ? card({ gap: 8 }, [T('Nhóm đang tìm', 'subtitle'), T('1–2 bạn thiết kế UI và nhận diện thương hiệu, làm được Figma.', 'body-large', null, { w: 'fill' }), chips(['UI/UX', 'Figma', 'Brand kit'])]) : null,
    head('Thành viên · ' + n + '/6'), memberList(members)
  ] });
}
['Mặc định', 'Đã gửi yêu cầu', 'Nhóm đã đủ', 'Đợt đã khóa', 'Bạn đã có nhóm', 'Xem từ lời mời'].forEach(teamDetail);

// S09
function joinSheet(state) {
  var f = screen('S09 – Gửi yêu cầu tham gia / ' + state, { bar: appBar('Chi tiết nhóm', { back: true }), bottom: bottomBar([btn('Xin tham gia', 'primary', { w: 'fill' })]), body: [
    Vb({ w: 'fill', gap: 8 }, [T('GreenBox', 'display'), T('GreenBox – Hộp cơm xanh cho sinh viên', 'body-large', 'text-secondary', { w: 'fill' }), chips(['D04 Software / SaaS', 'D12 Marketing & Consumer Growth'], 'domain')]),
    card({ gap: 12 }, [T('Điều kiện nhóm', 'subtitle'), reqBar(4, true, true)]), sp()] });
  if (state === 'Gửi thành công') {
    return overlay(f, sheet('Đã gửi yêu cầu tham gia', [
      H({ w: 'fill', gap: 12, align: 'center' }, [I('check_circle', 48, 'success', true), T('Trưởng nhóm GreenBox sẽ phản hồi trong 72 giờ. Bạn sẽ nhận thông báo khi có kết quả.', 'body-large', null, { w: 'fill' })]),
      btn('Xem yêu cầu của tôi', 'primary', { w: 'fill' })
    ]), 'bottom');
  }
  return overlay(f, sheet('Xin tham gia GreenBox', [
    state === 'Lỗi gửi' ? banner('error', 'error', 'Chưa gửi được yêu cầu. Lời nhắn của bạn vẫn được giữ.', 'Thử lại') : null,
    input('Lời nhắn cho trưởng nhóm', 'Chào An, mình học Thiết kế mỹ thuật số, làm được UI và brand kit trên Figma. Mình muốn tham gia GreenBox.', { optional: true, minH: 120, counter: '109/300' }),
    banner('info', 'schedule', 'Yêu cầu có hiệu lực 72 giờ. Mỗi sinh viên chỉ thuộc một nhóm.'),
    state === 'Đang gửi' ? btn('', 'loading', { w: 'fill' }) : btn('Xin tham gia', 'primary', { w: 'fill' })
  ]), 'bottom');
}
['Mặc định', 'Đang gửi', 'Gửi thành công', 'Lỗi gửi'].forEach(joinSheet);

// S10
function seekers(state) {
  var o = { bar: appBar('Khám phá', { flat: true }), tab: ['student', 1, 2], gap: 12,
    top: Vb({ w: 'fill', px: 16, pb: 12, gap: 12, fill: 'surface', bb: 1 }, [segment(['Nhóm đang tuyển', 'Sinh viên tìm nhóm'], 1), search('Tìm theo tên hoặc kỹ năng')]) };
  if (state === 'Có dữ liệu') o.body = [filterRow(false), T('10 sinh viên đang tìm nhóm', 'body', 'text-secondary'),
    person({ name: 'Đỗ Gia Huy', id: 'SE190005', spec: 'Thiết kế mỹ thuật số', skills: ['UI/UX', 'Figma', 'Brand kit'], note: 'Thiết kế UI, brand kit, poster.' }),
    person({ name: 'Vũ Thanh Tâm', id: 'SS190006', spec: 'Marketing', skills: ['Content', 'Nghiên cứu thị trường'], note: 'Lập kế hoạch truyền thông, chạy khảo sát người dùng.' }),
    person({ name: 'Bùi Anh Khoa', id: 'SE190007', spec: 'Trí tuệ nhân tạo', skills: ['Python', 'Data'], note: 'Xây mô hình gợi ý, xử lý dữ liệu.' })];
  else if (state === 'Trống') o.body = [empty('person_search', 'Chưa có sinh viên nào đang tìm nhóm', 'Đăng bài tuyển để sinh viên phù hợp thấy nhóm của bạn.', btn('Đăng bài tuyển thành viên', 'secondary'))];
  else o.body = [sk(200, 16), skCard(), skCard(), skCard(), sp()];
  return screen('S10 – Khám phá – Sinh viên đang tìm nhóm / ' + state, o);
}
['Có dữ liệu', 'Trống', 'Đang tải'].forEach(seekers);

// S11
function seekPost(state) {
  var bad = state === 'Lỗi nhập liệu', edit = state === 'Chỉnh sửa';
  return screen('S11 – Bài "Tôi đang tìm nhóm" / ' + state, { bar: appBar(edit ? 'Sửa bài tìm nhóm' : 'Đăng bài tìm nhóm', { back: true, actions: edit ? ['delete'] : [] }), body: [
    input('Vai trò mong muốn', 'Thiết kế UI và nhận diện thương hiệu'),
    Vb({ w: 'fill', gap: 8 }, [T('Kỹ năng', 'label'), chips([chip('UI/UX', 'filterOn'), chip('Figma', 'filterOn'), chip('Brand kit', 'filterOn'), chip('Poster', 'filter'), chip('Video', 'filter')])]),
    input('Bạn có thể đóng góp gì?', bad ? '' : 'Thiết kế UI, brand kit, poster. Đã làm 2 dự án giao diện ứng dụng cho câu lạc bộ.', { minH: 120, ph: 'Ví dụ: thiết kế giao diện, dựng prototype…', counter: bad ? '0/300' : '78/300', error: bad ? 'Viết ít nhất một câu để nhóm biết bạn làm được gì.' : null }),
    Vb({ w: 'fill', gap: 8 }, [T('Domain quan tâm', 'label'), chips([chip('D04 Software / SaaS', 'filterOn'), chip('D10 Graphic Design', 'filterOn'), chip('Thêm domain', 'filter', 'add')])]),
    sp()
  ], bottom: bottomBar([btn(edit ? 'Lưu thay đổi' : 'Đăng bài', 'primary', { w: 'fill' })]) });
}
['Tạo mới', 'Chỉnh sửa', 'Lỗi nhập liệu'].forEach(seekPost);

// S12
function inviteCard(team, by, left, status) {
  return card({ gap: 12 }, [
    H({ w: 'fill', gap: 8 }, [Vb({ w: 'fill' }, [T(team.name, 'subtitle', null, { w: 'fill' }), T(team.project, 'body', 'text-secondary', { w: 'fill', lines: 1 })]), status ? chip(status[0], status[1]) : null]),
    reqBar(team.n),
    T(by, 'body', 'text-secondary', { w: 'fill' }),
    left ? H({ gap: 4, align: 'center' }, [I('schedule', 16, 'warning'), T(left, 'caption', 'warning')]) : null,
    status ? null : H({ w: 'fill', gap: 8 }, [btn('Từ chối', 'secondary', { w: 'fill' }), btn('Chấp nhận', 'secondary', { w: 'fill' })])
  ]);
}
function sentCard(team, time, status, action) {
  return card({ gap: 8 }, [
    H({ w: 'fill', gap: 8 }, [Vb({ w: 'fill' }, [T(team.name, 'subtitle', null, { w: 'fill' }), T(team.project, 'body', 'text-secondary', { w: 'fill', lines: 1 })]), chip(status[0], status[1])]),
    T(time, 'caption', 'text-secondary'),
    action ? H({ w: 'fill', justify: 'end' }, [btn(action, 'text')]) : null
  ]);
}
function myRequests(state) {
  var tabIdx = state === 'Yêu cầu đã gửi' ? 1 : 0;
  var o = { bar: appBar('Lời mời và yêu cầu', { back: true, flat: true }), top: tabs(['Lời mời nhận được', 'Yêu cầu đã gửi'], tabIdx), gap: 12 };
  if (state === 'Trống') o.body = [empty('mail', 'Chưa có lời mời nào', 'Xem các nhóm đang tuyển để xin tham gia.', btn('Khám phá nhóm', 'primary'))];
  else if (tabIdx === 1) o.body = [
    sentCard(TEAMS[0], 'Gửi lúc 10:12 · 14/09/2026 · còn 2 ngày 23 giờ', ['Đang chờ', 'warning'], 'Hủy yêu cầu'),
    sentCard(TEAMS[2], 'Từ chối lúc 17:20 · 13/09/2026', ['Đã từ chối', 'error']),
    sentCard(TEAMS[3], 'Hết hạn lúc 09:00 · 12/09/2026', ['Hết hạn', 'neutral']), sp()];
  else o.body = [
    inviteCard(TEAMS[1], 'Trần Đức Long (trưởng nhóm) mời bạn · 09:40 · 14/09/2026', 'Còn 2 ngày 21 giờ'),
    inviteCard(TEAMS[3], 'Lời mời đã hết hạn lúc 09:00 · 12/09/2026', null, ['Hết hạn', 'neutral']), sp()];
  var f = screen('S12 – Lời mời và yêu cầu của tôi / ' + state, o);
  if (state === 'Dialog xác nhận vào nhóm') overlay(f, dialog('Chấp nhận lời mời của EduSpark?', 'Mỗi sinh viên chỉ thuộc một nhóm. Khi bạn vào nhóm EduSpark, các lời mời và yêu cầu tham gia khác của bạn sẽ bị hủy.', 'Hủy', 'Chấp nhận'), 'center');
  return f;
}
['Lời mời nhận được', 'Yêu cầu đã gửi', 'Trống', 'Dialog xác nhận vào nhóm'].forEach(myRequests);

await flush();
return CREATED.map(function (f) { return f.name + '  ' + Math.round(f.width) + 'x' + Math.round(f.height); });
