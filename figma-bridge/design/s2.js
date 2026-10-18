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
