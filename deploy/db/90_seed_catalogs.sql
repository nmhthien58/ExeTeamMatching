-- BE-01 – Dữ liệu danh mục ban đầu. Chuyên ngành và mã lớp là dữ liệu tạm,
-- cần thay bằng danh mục thật của Bộ môn EXE (thiết kế DB, giả định 13).

\connect identity_db
SET ROLE identity_svc;
INSERT INTO identity.roles (id, code, name) VALUES
    (1, 'Student', 'Sinh viên'),
    (2, 'Lecturer', 'Giảng viên'),
    (3, 'Admin', 'Bộ môn EXE'),
    (4, 'Mentor', 'Mentor')
ON CONFLICT (id) DO NOTHING;
RESET ROLE;

\connect academic_db
SET ROLE academic_svc;

INSERT INTO academic.specializations (code, name) VALUES
    ('SE', 'Kỹ thuật phần mềm'),
    ('AI', 'Trí tuệ nhân tạo'),
    ('IA', 'An toàn thông tin'),
    ('IOT', 'Internet vạn vật'),
    ('GD', 'Thiết kế đồ họa'),
    ('MC', 'Truyền thông đa phương tiện'),
    ('DM', 'Digital Marketing'),
    ('IB', 'Kinh doanh quốc tế'),
    ('FIN', 'Tài chính'),
    ('LOG', 'Logistics và quản lý chuỗi cung ứng'),
    ('HM', 'Quản trị khách sạn'),
    ('LAW', 'Luật'),
    ('EN', 'Ngôn ngữ Anh'),
    ('JP', 'Ngôn ngữ Nhật'),
    ('KR', 'Ngôn ngữ Hàn'),
    ('CN', 'Ngôn ngữ Trung')
ON CONFLICT (code) DO NOTHING;

INSERT INTO academic.domains (code, name, description) VALUES
    ('D01', 'AI & Data Innovation', 'AI / IT'),
    ('D02', 'Robotics & Automation', 'Robotics'),
    ('D03', 'IoT & Smart Systems', 'IoT'),
    ('D04', 'Software / SaaS / Platform', 'Software Engineering / IT'),
    ('D05', 'Computer Foundation & Digital Literacy', 'Computer Foundation / IT'),
    ('D06', 'Semiconductor & Embedded Deep Tech', 'Vi mạch bán dẫn'),
    ('D07', 'Digital Automotive & Mobility', 'Ô tô số'),
    ('D08', 'Cybersecurity & Trust', 'An toàn thông tin'),
    ('D09', 'Media & Communication Innovation', 'Truyền thông'),
    ('D10', 'Graphic Design / UX / Brand', 'Thiết kế đồ họa'),
    ('D11', 'Language & Global Market', 'Ngôn ngữ Anh/Trung/Nhật'),
    ('D12', 'Marketing & Consumer Growth', 'Marketing'),
    ('D13', 'Logistics & Supply Chain Tech', 'Logistics – SCM'),
    ('D14', 'Law / LegalTech / Compliance', 'Luật'),
    ('D15', 'Finance / Fintech / Funding', 'Tài chính'),
    ('D16', 'Human Development & Soft Skills', 'Kỹ năng mềm'),
    ('D17', 'Culture & Vietnamese Identity', 'Âm nhạc dân tộc'),
    ('D18', 'Vovinam / Wellness / Discipline', 'Vovinam')
ON CONFLICT (code) DO NOTHING;

INSERT INTO academic.class_codes (code) VALUES
    ('EXE101SE01'), ('EXE101SE02'), ('EXE101GD01'), ('EXE101MKT01'), ('EXE101IB01'),
    ('EXE201SE01'), ('EXE201SE02'), ('EXE201GD01'), ('EXE201MKT01'), ('EXE201IB01')
ON CONFLICT (code) DO NOTHING;

INSERT INTO academic.semesters (code, name, start_date, end_date, is_current) VALUES
    ('FA26', 'Fall 2026', DATE '2026-09-07', DATE '2026-12-26', true)
ON CONFLICT (code) DO NOTHING;

INSERT INTO academic.rule_settings (id) VALUES (1) ON CONFLICT (id) DO NOTHING;

RESET ROLE;
