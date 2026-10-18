// S1. Sinh viên – Trang chủ và hồ sơ

function homeHeader(name, course, unread) {
  return H({ name: 'Header', w: 'fill', fill: 'sky', pl: 16, pr: 4, pt: 8, pb: 16, gap: 8, align: 'center' }, [
    Vb({ w: 'fill' }, [T('Chào ' + name, 'display', null, { w: 'fill', lines: 1 }), T('Học kỳ FA26 · ' + course, 'body', 'text-primary')]),
    iconBtn('mail', 'text-primary', unread)
  ]);
}
function task(icon, title, sub, tone) {
  return H({ name: 'Task/' + title, w: 'fill', gap: 12, p: 16, r: 16, fill: 'surface', stroke: 'border', align: 'center', minH: 48 }, [
    H({ w: 40, h: 40, r: 999, fill: tone === 'warning' ? 'warning-container' : 'primary-container', align: 'center', justify: 'center' }, [I(icon, 24, tone === 'warning' ? 'warning' : 'primary')]),
    Vb({ w: 'fill' }, [T(title, 'subtitle', null, { w: 'fill' }), sub ? T(sub, 'body', 'text-secondary', { w: 'fill' }) : null]),
    I('chevron_right', 24)
  ]);
}
function myTeamCard(n, status, specOk, extra) {
  return card({ name: 'Card/Nhóm của tôi', gap: 12 }, [
    H({ w: 'fill', gap: 8 }, [
      Vb({ w: 'fill' }, [T('GreenBox', 'subtitle', null, { w: 'fill' }), T('GreenBox – Hộp cơm xanh cho sinh viên', 'body', 'text-secondary', { w: 'fill', lines: 1 })]),
      chip(status[0], status[1], status[2])
    ]),
    reqBar(n, true, specOk)
  ].concat(extra || []));
}

function home(state) {
  var leader = state === 'Là trưởng nhóm';
  var o = { p: 16, gap: 16, barBg: 'sky', tab: ['student', 0, leader ? 1 : 2] };
  o.top = homeHeader(leader ? 'An' : 'Huy', leader ? 'EXE201' : 'EXE101', state === 'Chưa có nhóm' ? 1 : 0);
  if (state === 'Chưa có nhóm') {
    o.body = [
      deadline('72'),
      card({ gap: 12 }, [
        H({ w: 'fill', gap: 8, align: 'center' }, [T('Bạn chưa có nhóm', 'subtitle', null, { w: 'fill' }), chip('Chưa có nhóm', 'warning', 'error')]),
        T('Sau hạn chót, sinh viên chưa có nhóm sẽ được Bộ môn ghép tự động. Hãy xin tham gia một nhóm đang cần chuyên ngành của bạn.', 'body', 'text-secondary', { w: 'fill' }),
        btn('Khám phá nhóm', 'primary', { w: 'fill' }),
        btn('Tạo nhóm mới', 'secondary', { w: 'fill' })
      ]),
      task('mail', 'Bạn có 1 lời mời', 'Nhóm EduSpark · còn 2 ngày 21 giờ'),
      head('Nhóm phù hợp với bạn', 'Xem tất cả'),
      teamCard({ name: 'GreenBox', project: 'GreenBox – Hộp cơm xanh cho sinh viên', domains: ['D04 Software / SaaS', 'D12 Marketing'], n: 4, reason: 'Đang thiếu chuyên ngành Thiết kế mỹ thuật số của bạn' })
    ];
  } else if (state === 'Là thành viên') {
    o.body = [
      deadline('open'),
      myTeamCard(5, ['Hoạt động', 'success'], true),
      head('Việc tiếp theo'),
      task('school', 'Giảng viên: chờ xác nhận', 'Trưởng nhóm đã gửi yêu cầu đến Hoàng Văn Nam'),
      task('groups', 'Xem thành viên nhóm', '5 thành viên · 4 chuyên ngành')
    ];
  } else if (leader) {
    o.body = [
      deadline('72'),
      myTeamCard(5, ['Hoạt động', 'success'], true, [T('Nhóm kế thừa từ EXE101 · bạn là trưởng nhóm', 'body', 'text-secondary', { w: 'fill' })]),
      head('Việc cần làm'),
      task('sync_alt', '2 thay đổi chờ gửi Bộ môn', '1 thành viên rời nhóm · 1 thành viên mới', 'warning'),
      task('person_add', '1 yêu cầu tham gia mới', 'Vũ Thanh Tâm · Marketing'),
      task('school', 'Chọn giảng viên hướng dẫn', 'Nhóm chưa gửi yêu cầu hướng dẫn')
    ];
  } else if (state === 'Đợt đã khóa') {
    o.body = [
      deadline('closed'),
      myTeamCard(5, ['Đã chốt', 'primary'], true, [T('Nhóm đã được hệ thống chốt khi đóng đợt. Không thể đổi thành viên.', 'body', 'text-secondary', { w: 'fill' })]),
      head('Việc tiếp theo'),
      task('school', 'Giảng viên chính thức: Hoàng Văn Nam', 'Do Bộ môn phân công'),
      task('history', 'Lịch sử hoạt động nhóm', null)
    ];
  } else if (state === 'Được ghép tự động') {
    o.body = [
      deadline('closed'),
      banner('info', 'auto_awesome', 'Bạn chưa có nhóm khi đóng đợt nên Bộ môn đã ghép bạn vào nhóm Team 214.'),
      card({ gap: 12 }, [
        H({ w: 'fill', gap: 8 }, [Vb({ w: 'fill' }, [T('Team 214', 'subtitle', null, { w: 'fill' }), T('Chưa có tên dự án', 'body', 'text-secondary', { w: 'fill' })]), chip('Cần xem lại', 'warning', 'error')]),
        reqBar(5, true, true),
        btn('Xem nhóm của bạn', 'primary', { w: 'fill' })
      ])
    ];
  } else {
    o.top = H({ name: 'Header', w: 'fill', fill: 'sky', px: 16, pt: 8, pb: 16 }, [Vb({ gap: 8 }, [sk(180, 32), sk(140, 20)])]);
    o.body = [sk(328, 44, 12), skCard(), skCard()];
    o.tab = ['student', 0, 0];
  }
  o.body.push(sp());
  return screen('S01 – Trang chủ / ' + state, o);
}
['Chưa có nhóm', 'Là thành viên', 'Là trưởng nhóm', 'Đợt đã khóa', 'Được ghép tự động', 'Đang tải'].forEach(home);

screen('S01 – Trang chủ / Banner hạn chót 4 mức', { bar: appBar('Banner hạn chót'), body: [
  T('Còn hơn 72 giờ', 'label', 'text-secondary'), deadline('open'),
  T('Còn 72 giờ trở xuống', 'label', 'text-secondary'), deadline('72'),
  T('Còn 24 giờ trở xuống', 'label', 'text-secondary'), deadline('24'),
  T('Đã khóa', 'label', 'text-secondary'), deadline('closed'), sp()], gap: 8 });

// S02
function profileHead(name, sub) {
  return Vb({ name: 'Profile header', w: 'fill', gap: 8, align: 'center', py: 8 }, [
    avatar(initials(name), 72), T(name, 'title', null, { w: 'fill', align: 'center' }), T(sub, 'body', 'text-secondary', { w: 'fill', align: 'center' })
  ]);
}
function myProfile(state) {
  var has = state === 'Có nhóm';
  return screen('S02 – Hồ sơ của tôi / ' + state, { bar: appBar('Hồ sơ', { actions: ['edit', 'settings'] }), tab: ['student', 4, 0], body: [
    profileHead('Đỗ Gia Huy', 'SE190005 · Lớp SE1911'),
    card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [
      kv('Ngành', 'Thiết kế mỹ thuật số'), divider(), kv('Chuyên ngành', 'Thiết kế đồ họa và mỹ thuật số'), divider(),
      kv('Email', 'huydg@example.edu.vn'), divider(), kv('Số điện thoại', '0901234567'),
      H({ w: 'fill', gap: 8, pb: 12, align: 'center' }, [I('lock', 20), T('Chỉ thành viên cùng nhóm, giảng viên của nhóm và Bộ môn thấy số này.', 'body', 'text-secondary', { w: 'fill' })])
    ])]),
    card({ gap: 12 }, [T('Kỹ năng', 'subtitle'), chips(['UI/UX', 'Figma', 'Brand kit']), divider(), T('Giới thiệu', 'subtitle'),
      T('Mình thiết kế UI, brand kit và poster. Muốn tham gia nhóm làm sản phẩm số cho sinh viên.', 'body', null, { w: 'fill' })]),
    has
      ? card({ gap: 8 }, [T('Nhóm hiện tại', 'label', 'text-secondary'), H({ w: 'fill', gap: 8, align: 'center' }, [Vb({ w: 'fill' }, [T('GreenBox', 'subtitle'), T('Thành viên · EXE101', 'body', 'text-secondary')]), chip('Hoạt động', 'success'), I('chevron_right', 24)])])
      : card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [row('person_search', 'Tôi đang tìm nhóm', 'Hồ sơ của bạn hiện trong danh sách sinh viên tìm nhóm', toggle(true))])])
  ] });
}
['Có nhóm', 'Chưa có nhóm'].forEach(myProfile);

// S03
function editProfile(state) {
  var bad = state === 'Lỗi nhập liệu';
  return screen('S03 – Chỉnh sửa hồ sơ / ' + state, { bar: appBar('Chỉnh sửa hồ sơ', { back: true }), snack: state === 'Lưu thành công' ? 'Đã lưu hồ sơ' : null, body: [
    input('Số điện thoại / Zalo', bad ? '0901 23' : '0901234567', { optional: true, lead: 'call',
      error: bad ? 'Số điện thoại gồm 10 chữ số và bắt đầu bằng 0. Ví dụ: 0901234567.' : null, helper: 'Chỉ thành viên cùng nhóm, giảng viên của nhóm và Bộ môn thấy số này.' }),
    Vb({ w: 'fill', gap: 8 }, [T('Kỹ năng', 'label'),
      chips([chip('UI/UX', 'filterOn'), chip('Figma', 'filterOn'), chip('Brand kit', 'filterOn'), chip('Poster', 'filter'), chip('Illustration', 'filter'), chip('Video', 'filter'), chip('Thuyết trình', 'filter')]),
      T('Đã chọn 3. Chọn tối đa 6 kỹ năng.', 'body', 'text-secondary', { w: 'fill' })]),
    input('Giới thiệu ngắn', 'Mình thiết kế UI, brand kit và poster. Muốn tham gia nhóm làm sản phẩm số cho sinh viên.', { optional: true, minH: 120, counter: '86/200' }),
    card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [row('person_search', 'Tôi đang tìm nhóm', 'Cho phép trưởng nhóm thấy và mời bạn', toggle(true))])]),
    sp()
  ], bottom: bottomBar([state === 'Đang lưu' ? btn('', 'loading', { w: 'fill' }) : btn('Lưu thay đổi', 'primary', { w: 'fill' })]) });
}
['Mặc định', 'Lỗi nhập liệu', 'Đang lưu', 'Lưu thành công'].forEach(editProfile);

// S04
function otherProfile(state) {
  var bottom = null, tag = null;
  if (state === 'Trưởng nhóm xem') bottom = bottomBar([btn('Gửi lời mời', 'primary', { w: 'fill' })]);
  if (state === 'Đã mời') { bottom = bottomBar([btn('Gửi lời mời', 'disabled', { w: 'fill' })], 'Bạn đã gửi lời mời lúc 09:40 · 14/09/2026. Lời mời còn hiệu lực 2 ngày 21 giờ.'); tag = chip('Đang chờ', 'warning'); }
  if (state === 'Sinh viên đã có nhóm') { bottom = bottomBar([btn('Gửi lời mời', 'disabled', { w: 'fill' })], 'Sinh viên này đã thuộc nhóm EduSpark. Mỗi sinh viên chỉ thuộc một nhóm trong học kỳ.'); tag = chip('Đã có nhóm', 'neutral'); }
  return screen('S04 – Hồ sơ sinh viên khác / ' + state, { bar: appBar('Hồ sơ sinh viên', { back: true }), bottom: bottom, snack: state === 'Đã mời' ? 'Đã gửi lời mời' : null, body: [
    profileHead('Đỗ Gia Huy', 'SE190005 · Lớp SE1911'),
    tag ? H({ w: 'fill', justify: 'center' }, [tag]) : null,
    card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [
      kv('Ngành', 'Thiết kế mỹ thuật số'), divider(), kv('Chuyên ngành', 'Thiết kế đồ họa và mỹ thuật số'), divider(),
      H({ w: 'fill', gap: 8, py: 12, align: 'center' }, [I('lock', 20), T('Số điện thoại chỉ hiện với thành viên cùng nhóm.', 'body', 'text-secondary', { w: 'fill' })])
    ])]),
    card({ gap: 12 }, [T('Kỹ năng', 'subtitle'), chips(['UI/UX', 'Figma', 'Brand kit']), divider(), T('Có thể đóng góp', 'subtitle'),
      T('Thiết kế UI, brand kit, poster. Đã làm 2 dự án giao diện ứng dụng cho câu lạc bộ.', 'body', null, { w: 'fill' })]),
    state === 'Trưởng nhóm xem' ? banner('success', 'auto_awesome', 'Bổ sung chuyên ngành nhóm bạn đang thiếu: Thiết kế mỹ thuật số.') : null,
    sp()
  ] });
}
['Trưởng nhóm xem', 'Người khác xem', 'Đã mời', 'Sinh viên đã có nhóm'].forEach(otherProfile);
