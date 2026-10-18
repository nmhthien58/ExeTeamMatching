// A. Khởi động, đăng nhập và phần dùng chung

function logo(size) {
  return H({ name: 'Logo', w: size, h: size, r: size >= 96 ? 24 : 16, fill: 'primary', align: 'center', justify: 'center' }, [I('groups', Math.round(size * 0.58), 'on-primary', true)]);
}
function errState(icon, title, desc, label) {
  return empty(icon, title, desc, btn(label || 'Thử lại', 'primary'));
}
function stepBar(i, n) {
  var segs = [];
  for (var k = 0; k < n; k++) segs.push(box({ w: 'fill', h: 4, r: 999, fill: k < i ? 'primary' : 'primary-container' }));
  return Vb({ name: 'Steps', w: 'fill', gap: 8 }, [T('Bước ' + i + '/' + n, 'label', 'text-secondary'), H({ w: 'fill', gap: 4 }, segs)]);
}
function notif(icon, title, desc, time, unread) {
  return H({ name: 'Notification/' + (unread ? 'Unread' : 'Read'), w: 'fill', gap: 12, px: 16, py: 12, fill: unread ? 'primary-container' : 'surface', bb: 1 }, [
    H({ w: 40, h: 40, r: 999, fill: unread ? 'surface' : 'neutral-container', align: 'center', justify: 'center' }, [I(icon, 24, unread ? 'primary' : 'text-secondary')]),
    Vb({ w: 'fill', gap: 4 }, [
      T(title, unread ? 'subtitle' : 'body-large', null, { w: 'fill' }),
      T(desc, 'body', 'text-secondary', { w: 'fill' }),
      T(time, 'caption', 'text-secondary')
    ]),
    unread ? box({ name: 'Unread dot', w: 8, h: 8, r: 999, fill: 'primary' }) : null
  ]);
}
function dayLabel(t) { return H({ w: 'fill', px: 16, pt: 16, pb: 8 }, [T(t, 'label', 'text-secondary')]); }

// A01
screen('A01 – Splash / Mặc định', { bg: 'surface', body: [
  sp(),
  Vb({ w: 'fill', gap: 16, align: 'center' }, [logo(96), T('EXE Team Matching', 'display'), T('Lập nhóm EXE101 và EXE201', 'body', 'text-secondary')]),
  sp(),
  H({ w: 'fill', justify: 'center', pb: 32 }, [spinner('primary')])
] });

// A02
[['groups', 'Lập nhóm đúng quy định', 'Nhóm 4–6 người, ít nhất 2 chuyên ngành. Ứng dụng cho bạn biết nhóm đang thiếu gì.'],
 ['person_search', 'Tìm đúng thành viên', 'Xem nhóm đang tuyển và sinh viên phù hợp theo chuyên ngành, không cần nhắn hỏi từng người.'],
 ['event_upcoming', 'Theo dõi hạn chót', 'Mọi lời mời và yêu cầu đều có trạng thái. Bạn được nhắc 72 giờ và 24 giờ trước hạn chót.']
].forEach(function (s, i) {
  var pager = [];
  for (var k = 0; k < 3; k++) pager.push(box({ w: k === i ? 24 : 8, h: 8, r: 999, fill: k === i ? 'primary' : 'border-strong' }));
  screen('A02 – Giới thiệu / Slide ' + (i + 1), { bg: 'surface', body: [
    H({ w: 'fill', justify: 'end' }, [i < 2 ? btn('Bỏ qua', 'text') : gapBox(48)]),
    sp(),
    Vb({ w: 'fill', gap: 16, align: 'center' }, [
      H({ name: 'Illustration', w: 200, h: 200, r: 999, fill: 'primary-container', align: 'center', justify: 'center' }, [I(s[0], 96, 'primary')]),
      gapBox(8),
      T(s[1], 'display', null, { w: 'fill', align: 'center' }),
      T(s[2], 'body-large', 'text-secondary', { w: 'fill', align: 'center' })
    ]),
    sp(),
    H({ w: 'fill', justify: 'center', gap: 8 }, pager)
  ], bottom: Vb({ w: 'fill', p: 16, fill: 'surface' }, [btn(i < 2 ? 'Tiếp tục' : 'Bắt đầu', 'primary', { w: 'fill' })]) });
});

// A03
function login(state) {
  var msg = null;
  if (state === 'Lỗi mạng') msg = banner('error', 'wifi_off', 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.', 'Thử lại');
  if (state === 'Hủy đăng nhập') msg = banner('neutral', 'info', 'Bạn chưa chọn tài khoản Google. Bấm đăng nhập để thử lại.');
  return screen('A03 – Đăng nhập Google / ' + state, { bg: 'surface', body: [
    sp(),
    Vb({ w: 'fill', gap: 16, align: 'center' }, [
      logo(72), T('EXE Team Matching', 'display', null, { w: 'fill', align: 'center' }),
      T('Lập nhóm, mời thành viên và theo dõi yêu cầu của bạn cho môn EXE101 và EXE201.', 'body-large', 'text-secondary', { w: 'fill', align: 'center' })
    ]),
    sp(),
    msg,
    Vb({ w: 'fill', gap: 12 }, [
      state === 'Đang xử lý' ? btn('', 'loading', { w: 'fill' }) : btn('Đăng nhập với Google', 'primary', { w: 'fill', icon: 'login' }),
      T(state === 'Đang xử lý' ? 'Đang kiểm tra tài khoản của bạn…' : 'Dùng email bạn đã đăng ký với Bộ môn EXE.', 'body', 'text-secondary', { w: 'fill', align: 'center' })
    ]),
    gapBox(16)
  ] });
}
['Mặc định', 'Đang xử lý', 'Lỗi mạng', 'Hủy đăng nhập'].forEach(login);

// A04
screen('A04 – Tài khoản chưa có trong danh sách / Mặc định', { bg: 'surface', bar: appBar('Đăng nhập', { back: true, flat: true }), body: [
  gapBox(24),
  Vb({ w: 'fill', gap: 8, align: 'center' }, [
    I('person_off', 48, 'text-secondary'), gapBox(8),
    T('Tài khoản chưa có trong danh sách', 'title', null, { w: 'fill', align: 'center' }),
    T('Email huydg@example.edu.vn không có trong danh sách học kỳ FA26 mà Bộ môn EXE đã import.', 'body-large', 'text-secondary', { w: 'fill', align: 'center' })
  ]),
  card({ gap: 12 }, [
    T('Bạn có thể làm gì?', 'subtitle'),
    H({ w: 'fill', gap: 12 }, [I('alternate_email', 24, 'primary'), T('Đăng nhập lại bằng email bạn đã đăng ký môn EXE trên FAP.', 'body', null, { w: 'fill' })]),
    H({ w: 'fill', gap: 12 }, [I('support_agent', 24, 'primary'), T('Nếu email đã đúng, liên hệ Bộ môn EXE qua exe.department@example.edu.vn để được thêm vào danh sách.', 'body', null, { w: 'fill' })])
  ]),
  sp()
], bottom: Vb({ w: 'fill', p: 16, fill: 'surface' }, [btn('Đăng nhập bằng tài khoản khác', 'primary', { w: 'fill' })]) });

// A05
function profileSetup(state) {
  var step2 = state !== 'Bước 1';
  var body;
  if (!step2) {
    body = [
      stepBar(1, 2),
      Vb({ w: 'fill', gap: 4 }, [T('Kiểm tra thông tin của bạn', 'title', null, { w: 'fill' }), T('Thông tin do Bộ môn EXE import từ danh sách học kỳ FA26.', 'body', 'text-secondary', { w: 'fill' })]),
      card({ gap: 0, p: 0 }, [
        Vb({ w: 'fill', px: 16 }, [kv('Họ tên', 'Đỗ Gia Huy'), divider(), kv('MSSV', 'SE190005'), divider(), kv('Ngành', 'Thiết kế mỹ thuật số'), divider(),
          kv('Chuyên ngành', 'Thiết kế đồ họa và mỹ thuật số'), divider(), kv('Mã lớp', 'SE1911'), divider(), kv('Email', 'huydg@example.edu.vn')])
      ]),
      banner('info', 'info', 'Thông tin sai? Liên hệ Bộ môn EXE để sửa. Bạn vẫn có thể tiếp tục.')
    ];
  } else {
    var bad = state === 'Lỗi nhập SĐT';
    body = [
      stepBar(2, 2),
      Vb({ w: 'fill', gap: 4 }, [T('Giới thiệu bản thân', 'title', null, { w: 'fill' }), T('Giúp các nhóm biết bạn có thể đóng góp gì.', 'body', 'text-secondary', { w: 'fill' })]),
      input('Số điện thoại / Zalo', bad ? '90123456' : '0901234567', { optional: true, lead: 'call',
        error: bad ? 'Số điện thoại gồm 10 chữ số và bắt đầu bằng 0. Ví dụ: 0901234567.' : null,
        helper: 'Chỉ thành viên cùng nhóm, giảng viên của nhóm và Bộ môn thấy số này.' }),
      Vb({ w: 'fill', gap: 8 }, [
        T('Kỹ năng', 'label'),
        chips([chip('UI/UX', 'filterOn'), chip('Figma', 'filterOn'), chip('Brand kit', 'filterOn'), chip('Poster', 'filter'), chip('Illustration', 'filter'), chip('Video', 'filter'), chip('Thuyết trình', 'filter'), chip('Nghiên cứu thị trường', 'filter')]),
        T('Đã chọn 3. Chọn tối đa 6 kỹ năng.', 'body', 'text-secondary', { w: 'fill' })
      ]),
      input('Giới thiệu ngắn', 'Mình thiết kế UI, brand kit và poster. Muốn tham gia nhóm làm sản phẩm số cho sinh viên.', { optional: true, minH: 120, counter: '86/200' })
    ];
  }
  return screen('A05 – Hoàn thiện hồ sơ lần đầu / ' + state, { bar: appBar('Hoàn thiện hồ sơ', { back: step2 }), body: body,
    bottom: bottomBar([state === 'Đang lưu' ? btn('', 'loading', { w: 'fill' }) : btn(step2 ? 'Lưu hồ sơ' : 'Tiếp tục', 'primary', { w: 'fill' })]) });
}
['Bước 1', 'Bước 2', 'Lỗi nhập SĐT', 'Đang lưu'].forEach(profileSetup);

// A06
function notifications(state, lecturer) {
  var o = { p: 0, gap: 0, bg: 'surface' };
  o.bar = appBar('Thông báo', { back: lecturer, actions: state === 'Có dữ liệu' || lecturer ? ['done_all'] : [] });
  if (!lecturer) o.tab = ['student', 3, state === 'Có dữ liệu' ? 2 : 0];
  if (lecturer) {
    o.body = [dayLabel('Hôm nay'),
      notif('assignment_ind', 'Nhóm GreenBox gửi yêu cầu hướng dẫn', 'Trưởng nhóm Nguyễn Minh An · EXE201 · 5/6 thành viên', '09:12', true),
      notif('verified', 'Bộ môn đã phân công nhóm EduSpark cho bạn', 'Bạn là giảng viên chính thức của nhóm EduSpark.', '08:30', true),
      dayLabel('Hôm qua'),
      notif('sync_alt', 'Nhóm PawPal đổi thành viên', 'Bộ môn đã duyệt: 1 thành viên rời nhóm, 1 thành viên mới.', '16:45 · 14/09/2026'),
      notif('event', 'Hạn chót đợt Lập nhóm EXE201', 'Đợt đăng ký đóng lúc 18:00 · 17/09/2026.', '08:00 · 14/09/2026'), sp()];
  } else if (state === 'Có dữ liệu') {
    o.body = [dayLabel('Hôm nay'),
      notif('check_circle', 'Nhóm GreenBox đã chấp nhận bạn', 'Bạn hiện là thành viên của nhóm GreenBox.', '10:05', true),
      notif('mail', 'Nhóm EduSpark mời bạn tham gia', 'Lời mời còn hiệu lực 2 ngày 21 giờ.', '09:40', true),
      notif('schedule', 'Còn 72 giờ trước hạn chót', 'Đợt Lập nhóm EXE101 đóng lúc 18:00 · 17/09/2026.', '08:00'),
      dayLabel('Hôm qua'),
      notif('cancel', 'Yêu cầu tham gia nhóm VietCraft không thành công', 'Trưởng nhóm đã từ chối. Xem các nhóm khác đang tuyển.', '17:20 · 13/09/2026'),
      notif('school', 'Giảng viên Hoàng Văn Nam đã nhận nhóm', 'Kết quả chính thức do Bộ môn phân công.', '11:02 · 13/09/2026'), sp()];
  } else if (state === 'Đã đọc tất cả') {
    o.snack = 'Đã đánh dấu đã đọc tất cả';
    o.body = [dayLabel('Hôm nay'),
      notif('check_circle', 'Nhóm GreenBox đã chấp nhận bạn', 'Bạn hiện là thành viên của nhóm GreenBox.', '10:05'),
      notif('mail', 'Nhóm EduSpark mời bạn tham gia', 'Lời mời còn hiệu lực 2 ngày 21 giờ.', '09:40'),
      notif('schedule', 'Còn 72 giờ trước hạn chót', 'Đợt Lập nhóm EXE101 đóng lúc 18:00 · 17/09/2026.', '08:00'), sp()];
  } else if (state === 'Trống') {
    o.body = [empty('notifications', 'Chưa có thông báo', 'Lời mời, kết quả duyệt và nhắc hạn chót sẽ hiện ở đây.', btn('Xem nhóm đang tuyển', 'secondary'))];
  } else if (state === 'Đang tải') {
    o.body = [Vb({ w: 'fill', px: 16, pt: 8 }, [skRow(), skRow(), skRow(), skRow(), skRow(), skRow()]), sp()];
  } else {
    o.body = [errState('cloud_off', 'Không tải được thông báo', 'Kiểm tra kết nối mạng rồi thử lại.')];
  }
  return screen('A06 – Thông báo / ' + (lecturer ? 'Giảng viên' : state), o);
}
['Có dữ liệu', 'Trống', 'Đang tải', 'Lỗi tải', 'Đã đọc tất cả'].forEach(function (s) { notifications(s); });
notifications('Có dữ liệu', true);

// A07
function settings(state) {
  var f = screen('A07 – Cài đặt và tài khoản / ' + state, { bar: appBar('Cài đặt', { back: true }), body: [
    card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [
      row('mail', 'Nhận email thông báo', 'Lời mời, kết quả duyệt, phân công giảng viên', toggle(true)), divider(),
      row('alarm', 'Nhắc hạn chót', '72 giờ và 24 giờ trước khi đóng đợt', toggle(true))
    ])]),
    card({ gap: 0, p: 0 }, [Vb({ w: 'fill', px: 16 }, [
      row('info', 'Phiên bản ứng dụng', '1.0.0'), divider(),
      row('support_agent', 'Liên hệ Bộ môn EXE', 'exe.department@example.edu.vn', 'chevron_right'), divider(),
      row('policy', 'Chính sách dữ liệu cá nhân', null, 'chevron_right')
    ])]),
    sp(),
    btn('Đăng xuất', 'secondary', { w: 'fill', icon: 'logout' })
  ] });
  if (state !== 'Mặc định') overlay(f, dialog('Đăng xuất?', 'Bạn cần đăng nhập lại bằng Google để xem nhóm và thông báo.', 'Hủy', 'Đăng xuất'), 'center');
  return f;
}
['Mặc định', 'Dialog xác nhận đăng xuất'].forEach(settings);

// A08
screen('A08 – Trạng thái chung / Đang tải danh sách', { bar: appBar('Tiêu đề màn hình'), body: [skCard(), skCard(), skCard(), sp()], gap: 12 });
screen('A08 – Trạng thái chung / Đang tải chi tiết', { bar: appBar('Tiêu đề màn hình', { back: true }), body: [
  sk(220, 28), sk(300, 16), H({ gap: 8 }, [sk(96, 32, 999), sk(120, 32, 999)]),
  card({ gap: 12 }, [sk(160, 20), sk(280, 16), sk(240, 16), sk(200, 16)]),
  card({ gap: 0 }, [skRow(), skRow(), skRow()]), sp()] });
screen('A08 – Trạng thái chung / Danh sách trống', { bar: appBar('Tiêu đề màn hình'), body: [empty('inbox', 'Chưa có dữ liệu', 'Nói rõ vì sao danh sách trống và việc nên làm tiếp theo.', btn('Hành động gợi ý', 'secondary'))] });
screen('A08 – Trạng thái chung / Lỗi tải', { bar: appBar('Tiêu đề màn hình'), body: [errState('error', 'Không tải được dữ liệu', 'Máy chủ đang bận. Thử lại sau ít phút.')] });
screen('A08 – Trạng thái chung / Mất mạng', { bar: appBar('Tiêu đề màn hình'), body: [errState('wifi_off', 'Không có kết nối mạng', 'Kiểm tra Wi-Fi hoặc dữ liệu di động rồi thử lại.')] });
screen('A08 – Trạng thái chung / Không có quyền', { bar: appBar('Tiêu đề màn hình', { back: true }), body: [empty('lock', 'Bạn không có quyền xem nội dung này', 'Nội dung chỉ dành cho thành viên của nhóm hoặc trưởng nhóm.', btn('Về trang chủ', 'primary'))] });
