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
