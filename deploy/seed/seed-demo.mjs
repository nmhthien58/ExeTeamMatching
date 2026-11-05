// BE-57 – Seed dữ liệu demo ẩn danh qua API thật (Gateway + Kafka), chạy một lần sau DB script.
// Đi qua API để sự kiện tới đủ Identity, Notification, Matching và Collaboration; chèn thẳng SQL sẽ làm các read model lệch nhau.
// Chạy: docker compose -f deploy/docker-compose.yml --profile seed run --rm seed. Chỉ dùng ở Development (cần /api/dev/token).
// Chạy lại trên DB đã seed thì dừng ngay, không tạo trùng; lần trước dừng giữa chừng thì báo lỗi. Đổi học kỳ hiện hành sang học kỳ demo.
import { readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { crc32 } from 'node:zlib'

const here = dirname(fileURLToPath(import.meta.url))
const GATEWAY = process.env.SEED_GATEWAY_URL ?? 'http://localhost:8080'
const OWN_STUDENT_EMAIL = process.env.SEED_STUDENT_EMAIL?.trim()
const HEALTH_URLS = (process.env.SEED_HEALTH_URLS ?? `${GATEWAY}/health`).split(',')
const SEMESTER_CODE = 'SP90'
const DAY = 86_400_000
const MEET_LINK = 'https://meet.example.com/exe-demo'

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const log = (message) => console.log(`[seed] ${message}`)
const dateOnly = (date) => date.toISOString().slice(0, 10)

class HttpError extends Error {
  constructor(method, path, status, body) {
    super(`${method} ${path} → ${status}: ${body.slice(0, 400)}`)
    this.status = status
    this.body = body
  }
}

async function call(method, path, { token, body, form } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const response = await fetch(`${GATEWAY}${path}`, {
    method,
    headers,
    body: form ?? (body === undefined ? undefined : JSON.stringify(body)),
  })
  const text = await response.text()
  if (!response.ok) throw new HttpError(method, path, response.status, text)
  return text ? JSON.parse(text) : null
}

/** Chờ dữ liệu đi qua Kafka: lặp tới khi `probe` trả giá trị khác null / false. */
async function until(label, probe, seconds = 60) {
  const deadline = Date.now() + seconds * 1000
  let last
  while (Date.now() < deadline) {
    try {
      const value = await probe()
      if (value) return value
    } catch (error) {
      last = error
    }
    await sleep(1000)
  }
  throw new Error(`Hết giờ chờ: ${label}${last ? ` (${last.message})` : ''}`)
}

async function devToken(person) {
  const { accessToken } = await call('POST', '/api/dev/token', { body: person })
  return accessToken
}

const asStudent = (s) => devToken({ role: 'Student', email: s.email, studentId: s.id })
const asLecturer = (l) => devToken({ role: 'Lecturer', email: l.email, lecturerId: l.id })
const asMentor = (m) => devToken({ role: 'Mentor', email: m.email, mentorId: m.id })

function monday(date) {
  const start = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()))
  return new Date(start.getTime() - ((start.getUTCDay() + 6) % 7) * DAY)
}

async function waitForServices() {
  for (const url of HEALTH_URLS) {
    await until(`health ${url}`, async () => (await fetch(url)).ok, 120)
    log(`health ok: ${url}`)
  }
}

async function importBuffer(admin, semesterId, courseCode, file, buffer) {
  const form = new FormData()
  form.set('file', new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), file)
  form.set('semesterId', semesterId)
  form.set('courseCode', courseCode)
  const preview = await call('POST', '/api/imports/students/preview', { token: admin, form })
  if (preview.errorRows.length > 0) throw new Error(`File ${file} có dòng lỗi: ${JSON.stringify(preview.errorRows[0])}`)
  await call('POST', `/api/imports/students/${preview.previewId}/confirm`, { token: admin })
}

async function importStudents(admin, semesterId, courseCode, file) {
  await importBuffer(admin, semesterId, courseCode, file, await readFile(join(here, file)))
}

/** File zip không nén, đủ cho một file xlsx vài dòng; tránh thêm thư viện vào script chạy bằng node trần. */
function zipStored(entries) {
  const locals = []
  const centrals = []
  let offset = 0
  for (const [name, text] of Object.entries(entries)) {
    const path = Buffer.from(name)
    const data = Buffer.from(text)
    const fields = Buffer.alloc(16)
    fields.writeUInt32LE(crc32(data), 0)
    fields.writeUInt32LE(data.length, 4)
    fields.writeUInt32LE(data.length, 8)
    fields.writeUInt16LE(path.length, 12)
    // Bản 2.0, cờ UTF-8, không nén, thời gian 01/01/1980.
    const common = Buffer.from([20, 0, 0, 8, 0, 0, 0, 0, 0x21, 0])
    const local = Buffer.concat([Buffer.from([0x50, 0x4b, 3, 4]), common, fields, path, data])
    const tail = Buffer.alloc(14)
    tail.writeUInt32LE(offset, 10)
    centrals.push(Buffer.concat([Buffer.from([0x50, 0x4b, 1, 2, 20, 0]), common, fields, tail, path]))
    locals.push(local)
    offset += local.length
  }
  const central = Buffer.concat(centrals)
  const end = Buffer.alloc(22)
  end.writeUInt32LE(0x06054b50, 0)
  end.writeUInt16LE(centrals.length, 8)
  end.writeUInt16LE(centrals.length, 10)
  end.writeUInt32LE(central.length, 12)
  end.writeUInt32LE(offset, 16)
  return Buffer.concat([...locals, central, end])
}

const escapeXml = (text) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

/** File xlsx một sheet theo mẫu import W07. */
function studentSheet(rows) {
  const main = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
  const rels = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
  const pkg = 'http://schemas.openxmlformats.org/package/2006/relationships'
  const xml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
  const cell = (value, column, row) => `<c r="${String.fromCharCode(65 + column)}${row}" t="inlineStr"><is><t>${escapeXml(value)}</t></is></c>`
  const data = rows.map((values, i) => `<row r="${i + 1}">${values.map((value, column) => cell(value, column, i + 1)).join('')}</row>`).join('')
  return zipStored({
    '[Content_Types].xml': `${xml}<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>`,
    '_rels/.rels': `${xml}<Relationships xmlns="${pkg}"><Relationship Id="rId1" Type="${rels}/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
    'xl/workbook.xml': `${xml}<workbook xmlns="${main}" xmlns:r="${rels}"><sheets><sheet name="SinhVien" sheetId="1" r:id="rId1"/></sheets></workbook>`,
    'xl/_rels/workbook.xml.rels': `${xml}<Relationships xmlns="${pkg}"><Relationship Id="rId1" Type="${rels}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>`,
    'xl/worksheets/sheet1.xml': `${xml}<worksheet xmlns="${main}"><sheetData>${data}</sheetData></worksheet>`,
  })
}

/**
 * Sinh viên EXE101 chưa có nhóm cho email Google thật ở SEED_STUDENT_EMAIL, để đăng nhập thử.
 * Import riêng sau 30 sinh viên seed nên không làm lệch chỉ số nhóm bên dưới. Email chỉ nằm trong deploy/.env.
 */
async function importOwnStudent(admin, semesterId) {
  if (!OWN_STUDENT_EMAIL) return null
  const file = studentSheet([
    ['MSSV', 'Họ tên', 'Email', 'Chuyên ngành', 'Mã lớp'],
    ['DS189999', 'Sinh viên Demo', OWN_STUDENT_EMAIL, 'SE', 'EXE101SE01'],
  ])
  await importBuffer(admin, semesterId, 'EXE101', 'sinh-vien-rieng.xlsx', file)
  const students = await loadStudents(admin, semesterId, 'EXE101')
  return students.find((s) => s.studentCode === 'DS189999')
}

async function loadStudents(admin, semesterId, courseCode) {
  const page = await call('GET', `/api/students?semesterId=${semesterId}&courseCode=${courseCode}&sort=studentCode&pageSize=100`, { token: admin })
  return page.items
}

const SKILLS = ['TECHNOLOGY', 'UI_UX_DESIGN', 'MANAGEMENT', 'MARKETING', 'COMMUNICATION_PLANNING']

async function fillProfile(student, index, seeking) {
  student.token = await asStudent(student)
  await call('PUT', '/api/students/me', {
    token: student.token,
    body: {
      skills: SKILLS.map((skill, i) => ({ skill, level: ((index + i * 2) % 5) + 1 })),
      description: `Sinh viên ${student.specialization.name}, muốn tham gia dự án khởi nghiệp thực tế.`,
      isSeekingTeam: seeking,
    },
  })
}

async function invite(team, leader, student, message) {
  await call('POST', `/api/teams/${team.id}/invitations`, { token: leader.token, body: { studentId: student.id, message } })
}

async function inviteAndAccept(team, leader, student) {
  await invite(team, leader, student, 'Mời bạn vào nhóm')
  const invitation = await until(`lời mời của ${student.studentCode}`, async () => {
    const items = await call('GET', '/api/invitations/me', { token: student.token })
    return items.find((i) => i.team.id === team.id && i.status === 'PENDING')
  })
  await call('POST', `/api/invitations/${invitation.id}/accept`, { token: student.token })
}

async function createTeam(leader, spec, domains) {
  const domainIds = spec.domains.map((i) => domains[i].id)
  return call('POST', '/api/teams', {
    token: leader.token,
    body: { name: spec.name, projectName: spec.project, ideaDescription: spec.idea, domainIds, primaryDomainId: domainIds[0] },
  })
}

async function runMatching(admin, kind, courseCode) {
  const run = await call('POST', `/api/advisor-matching/${kind}/preview`, { token: admin, body: { courseCode } })
  const applied = await call('POST', `/api/advisor-matching/runs/${run.id}/confirm`, { token: admin })
  log(`ghép ${kind} ${courseCode}: ${applied.matchedCount} nhóm được ghép, ${applied.unmatchedCount} chưa ghép`)
}

async function workspaceOf(leader, teamId) {
  return until(`workspace nhóm ${teamId}`, async () => {
    const items = await call('GET', '/api/workspaces/me', { token: leader.token })
    return items.find((w) => (w.teamId ?? w.team?.id) === teamId)
  })
}

async function detailWithAdvisor(token, workspaceId, role) {
  return until(`${role} vào workspace ${workspaceId}`, async () => {
    const workspace = await call('GET', `/api/workspaces/${workspaceId}`, { token })
    return workspace.people.some((p) => p.role === role) ? workspace : null
  })
}

async function channels(token, workspaceId) {
  const items = await call('GET', `/api/workspaces/${workspaceId}/channels`, { token })
  return { team: items.find((c) => c.kind === 'TEAM'), advisors: items.find((c) => c.kind === 'ADVISORS') }
}

const post = (channel, token, body, isAdvisorQuestion = false, parentMessageId) =>
  call('POST', `/api/channels/${channel.id}/messages`, {
    token,
    body: { clientMessageId: crypto.randomUUID(), body, isAdvisorQuestion, parentMessageId },
  })

async function seedTeamWorkspace(team, members, instructor, now) {
  const [leader, second] = members
  const workspace = await workspaceOf(leader, team.id)
  const detail = await detailWithAdvisor(leader.token, workspace.id, 'INSTRUCTOR')
  const lecturerId = detail.people.find((p) => p.role === 'INSTRUCTOR').personId
  const lecturer = instructor(lecturerId)
  const lecturerToken = await asLecturer(lecturer)
  const { team: teamChannel, advisors } = await channels(leader.token, workspace.id)

  await post(teamChannel, leader.token, 'Chào cả nhóm, tuần này mình chốt phạm vi MVP nhé.')
  await post(teamChannel, second.token, 'Mình nhận phần giao diện, tối nay gửi bản nháp.')
  const question = await post(advisors, leader.token, 'Thưa cô, nhóm nên phỏng vấn bao nhiêu khách hàng ở tuần này ạ?', true)
  await post(advisors, lecturerToken, 'Tối thiểu 5 người, ưu tiên đúng phân khúc mục tiêu.', false, question.id)

  const assignee = [second.id]
  const task = await call('POST', `/api/workspaces/${workspace.id}/tasks`, {
    token: leader.token,
    body: { title: 'Vẽ wireframe màn đăng ký', description: 'Bản nháp cho buổi họp nhóm', kind: 'TASK', assigneeStudentIds: assignee, dueAt: new Date(now + 3 * DAY).toISOString() },
  })
  await call('POST', `/api/tasks/${task.id}/move`, { token: second.token, body: { status: 'DOING' } })
  await call('POST', `/api/workspaces/${workspace.id}/tasks`, {
    token: second.token,
    body: { title: 'Lập danh sách 5 khách hàng phỏng vấn', kind: 'TASK', assigneeStudentIds: [leader.id] },
  })

  const pdf = await readFile(join(here, 'bao-cao-demo.pdf'))
  const form = new FormData()
  form.set('file', new Blob([pdf], { type: 'application/pdf' }), 'bao-cao-tuan-3.pdf')
  const { file } = await call('POST', `/api/workspaces/${workspace.id}/files`, { token: second.token, form })

  const deliverable = await call('POST', `/api/workspaces/${workspace.id}/tasks`, {
    token: lecturerToken,
    body: { title: 'Báo cáo tuần 3', description: 'Nộp bản PDF', kind: 'DELIVERABLE', dueAt: new Date(now + 5 * DAY).toISOString() },
  })
  const fileDetail = await call('GET', `/api/files/${file.id}`, { token: second.token })
  const submission = await call('POST', `/api/tasks/${deliverable.id}/submissions`, { token: leader.token, body: { fileVersionId: fileDetail.versions[0].id } })
  await call('POST', `/api/submissions/${submission.id}/review`, { token: lecturerToken, body: { status: 'NEEDS_REVISION', comment: 'Thêm phần số liệu khảo sát.' } })

  const meeting = await call('POST', `/api/workspaces/${workspace.id}/meetings`, {
    token: leader.token,
    body: {
      title: 'Chốt phạm vi MVP',
      agenda: 'Chọn 3 tính năng cho buổi trình bày',
      location: MEET_LINK,
      slots: [
        { startsAt: new Date(now + 2 * DAY).toISOString(), durationMinutes: 30 },
        { startsAt: new Date(now + 3 * DAY).toISOString(), durationMinutes: 30 },
      ],
    },
  })
  await call('PUT', `/api/meetings/${meeting.id}/responses`, {
    token: second.token,
    body: { answers: [{ slotId: meeting.slots[0].id, answer: 'YES' }, { slotId: meeting.slots[1].id, answer: 'NO' }] },
  })
  const decided = await call('POST', `/api/workspaces/${workspace.id}/meetings`, {
    token: leader.token,
    body: { title: 'Họp nhóm đầu tuần', location: MEET_LINK, slots: [{ startsAt: new Date(now + DAY).toISOString(), durationMinutes: 30 }] },
  })
  await call('PUT', `/api/meetings/${decided.id}/minutes`, {
    token: leader.token,
    body: { content: 'Chốt 3 tính năng cho bản mẫu, phân công người làm từng phần.' },
  })
  return { workspace, lecturer, lecturerToken }
}

async function main() {
  await waitForServices()
  const admin = await devToken({ role: 'Admin', email: process.env.SEED_ADMIN_EMAIL ?? 'admin.demo@example.com' })
  const semesters = await call('GET', '/api/semesters', { token: admin })
  const seeded = semesters.find((s) => s.code === SEMESTER_CODE)
  if (seeded) {
    // Đợt EXE201 đóng là bước cuối của seed; chưa đóng nghĩa là lần trước dừng giữa chừng.
    const rounds = await call('GET', `/api/registration-rounds?semesterId=${seeded.id}`, { token: admin })
    if (!rounds.some((r) => r.courseCode === 'EXE201' && r.status === 'CLOSED')) {
      throw new Error(`Học kỳ ${SEMESTER_CODE} đã có nhưng lần seed trước chưa xong. Seed chỉ chạy trên DB trống: xem mục "Seed lại từ đầu" trong deploy/seed/README.md.`)
    }
    log(`Đã có học kỳ ${SEMESTER_CODE}: dữ liệu demo đã seed, không làm gì thêm.`)
    return
  }

  const now = Date.now()
  // Học kỳ theo ngày chạy, đang ở tuần 2: còn hạn chọn slot (tuần 3), chưa tới tuần mentor (tuần 8).
  const start = new Date(monday(new Date(now)).getTime() - 7 * DAY)
  const semester = await call('POST', '/api/semesters', {
    token: admin,
    body: {
      code: SEMESTER_CODE,
      name: 'Học kỳ demo',
      startDate: dateOnly(start),
      endDate: dateOnly(new Date(start.getTime() + 15 * 7 * DAY - DAY)),
      isCurrent: true,
      mentoringStartWeek: 8,
      expectedMentorSessions: 3,
      slotBookingDeadlineWeek: 3,
      meetingSlotMinutes: 15,
    },
  })
  log(`Học kỳ ${semester.code}: ${semester.startDate} → ${semester.endDate}`)

  const domains = await call('GET', '/api/domains', { token: admin })
  const lecturers = []
  for (const [index, name] of ['Trần Thu Hà', 'Nguyễn Đức Khoa', 'Phạm Minh Tâm', 'Lê Hoàng Long'].entries()) {
    lecturers.push(await call('POST', '/api/lecturers', {
      token: admin,
      body: {
        fullName: name,
        email: `gv${index + 1}.seed@example.com`,
        domainIds: [domains[index].id, domains[index + 4].id],
        maxTeamsExe101: 3,
        maxTeamsExe201: 3,
      },
    }))
  }
  const mentors = [
    await call('POST', '/api/mentors', { token: admin, body: { fullName: 'Đỗ Quang Vinh', email: 'mentor1.seed@example.com', organization: 'Startup Demo', domainIds: [domains[0].id, domains[1].id], maxTeams: 3 } }),
    await call('POST', '/api/mentors', { token: admin, body: { fullName: 'Vũ Thanh Mai', email: 'mentor2.seed@example.com', organization: 'Công ty Demo', domainIds: [domains[2].id, domains[3].id], maxTeams: 2 } }),
    await call('POST', '/api/mentors', { token: admin, body: { fullName: lecturers[3].fullName, isInternal: true, lecturerId: lecturers[3].id, domainIds: [domains[3].id], maxTeams: 2 } }),
  ]
  log(`${lecturers.length} giảng viên, ${mentors.length} mentor (1 giảng viên kiêm mentor)`)

  const round = await call('POST', '/api/registration-rounds', {
    token: admin,
    body: {
      semesterId: semester.id,
      courseCode: 'EXE101',
      roundType: 'TEAM_FORMATION',
      name: 'Lập nhóm EXE101 (demo)',
      opensAt: new Date(now + 60_000).toISOString(),
      deadlineAt: new Date(now + 21 * DAY).toISOString(),
    },
  })
  await call('POST', `/api/registration-rounds/${round.id}/open`, { token: admin })

  await importStudents(admin, semester.id, 'EXE101', 'sinh-vien-exe101-seed.xlsx')
  await importStudents(admin, semester.id, 'EXE201', 'sinh-vien-exe201-seed.xlsx')
  const exe101 = await loadStudents(admin, semester.id, 'EXE101')
  const exe201 = await loadStudents(admin, semester.id, 'EXE201')
  log(`Đã import ${exe101.length} sinh viên EXE101, ${exe201.length} sinh viên EXE201`)
  const ownStudent = await importOwnStudent(admin, semester.id)

  for (const [index, student] of exe101.entries()) await fillProfile(student, index, index >= 17)
  for (const [index, student] of exe201.entries()) await fillProfile(student, index, false)
  if (ownStudent) await fillProfile(ownStudent, 0, true)
  log('Đã hoàn thiện hồ sơ 5 kỹ năng')
  // Matching dựng read model từ StudentProfileUpdated; chờ một nhịp trước khi tính gợi ý.
  await sleep(3000)

  const specs = [
    { name: 'Nhóm Bình Minh', project: 'EduSpark – nền tảng học nhóm', idea: 'Ghép bạn học theo môn và lịch rảnh', domains: [0, 4], members: [0, 1, 2, 3, 4] },
    { name: 'Nhóm Sao Mai', project: 'GreenLoop – đổi đồ cũ trong ký túc xá', idea: 'Sàn trao đổi đồ dùng cho sinh viên', domains: [1], members: [5, 6, 7, 8] },
    { name: 'Nhóm Hải Đăng', project: 'CareNest – đặt lịch khám cho người cao tuổi', idea: 'Con cái đặt lịch hộ bố mẹ', domains: [2], members: [9, 10, 11, 12, 13, 14] },
    { name: 'Nhóm Khởi Hành', project: 'FoodMap – bản đồ quán ăn sinh viên', idea: 'Đánh giá quán ăn quanh trường', domains: [3], members: [15, 16] },
  ]
  const teams = []
  for (const spec of specs) {
    const [leaderIndex, ...others] = spec.members
    const leader = exe101[leaderIndex]
    const team = await createTeam(leader, spec, domains)
    for (const index of others) await inviteAndAccept(team, leader, exe101[index])
    teams.push({ ...team, spec, leader, members: spec.members.map((i) => exe101[i]) })
  }
  const [dawn, star, lighthouse, departure] = teams
  log(`Đã tạo ${teams.length} nhóm EXE101`)

  // Dữ liệu đang chờ cho màn Khám phá, Lời mời, Yêu cầu.
  await call('PUT', `/api/teams/${departure.id}/recruitment-post`, {
    token: departure.leader.token,
    body: { title: 'Cần 2 bạn thiết kế và marketing', description: 'Nhóm đang làm bản mẫu, cần thêm người.', specializationIds: [exe101[1].specialization.id] },
  })
  await invite(departure, departure.leader, exe101[17], 'Nhóm cần bạn làm thiết kế')
  if (ownStudent) await invite(departure, departure.leader, ownStudent, 'Nhóm cần thêm bạn làm kỹ thuật')
  await call('POST', `/api/teams/${star.id}/join-requests`, {
    token: exe101[18].token,
    body: { message: 'Mình muốn vào nhóm', ideas: [{ title: 'Điểm thưởng đổi đồ', description: 'Tặng điểm khi đổi đồ thành công' }], hasNoIdea: false },
  })
  for (const index of [19, 20]) {
    await call('PUT', '/api/seeking-posts/me', {
      token: exe101[index].token,
      body: { title: 'Tìm nhóm làm sản phẩm giáo dục', description: 'Có kinh nghiệm làm khảo sát người dùng.', domainIds: [domains[0].id] },
    })
  }

  const baseline = [
    'Nhom,De tai,Truong nhom,Thanh vien',
    `Nhóm Ánh Dương,SunRoute – chia sẻ chuyến xe sinh viên,${exe201[0].studentCode},${exe201.slice(1, 5).map((s) => s.studentCode).join(' ')}`,
    `Nhóm Tiên Phong,MedKit – nhắc lịch uống thuốc,${exe201[5].studentCode},${exe201.slice(6, 9).map((s) => s.studentCode).join(' ')}`,
  ].join('\n')
  const form = new FormData()
  form.set('file', new Blob([baseline], { type: 'text/csv' }), 'moc-exe101-seed.csv')
  form.set('semesterId', semester.id)
  form.set('dryRun', 'false')
  await call('POST', '/api/teams/baseline-import', { token: admin, form })
  const sunTeam = await until('nhóm EXE201 kế thừa', () => call('GET', '/api/teams/me', { token: exe201[0].token }))
  await until('nhóm EXE201 thứ hai', () => call('GET', '/api/teams/me', { token: exe201[5].token }))
  log('Đã import mốc 2 nhóm EXE201 kế thừa')

  // Quan tâm và nguyện vọng rồi Ghép sớm, để có lý do ghép đủ loại ở W19, W28.
  const tokens = new Map()
  for (const lecturer of lecturers) tokens.set(lecturer.id, await asLecturer(lecturer))
  const interest = (team, lecturer) =>
    call('POST', `/api/teams/${team.id}/topic-interests`, { token: tokens.get(lecturer.id), body: { advisorKind: 'LECTURER' } })
  await interest(dawn, lecturers[0])
  await interest(dawn, lecturers[1])
  await interest(star, lecturers[1])
  await interest(lighthouse, lecturers[2])
  const prefer = (team, ...picked) =>
    call('PUT', `/api/teams/${team.id}/lecturer-preferences`, { token: team.leader.token, body: { lecturerIds: picked.map((l) => l.id) } })
  await prefer(dawn, lecturers[0], lecturers[1])
  await prefer(star, lecturers[1], lecturers[2])
  await prefer(lighthouse, lecturers[2], lecturers[0])

  await call('POST', `/api/teams/${sunTeam.id}/topic-interests`, { token: await asMentor(mentors[0]), body: { advisorKind: 'MENTOR' } })
  await runMatching(admin, 'LECTURER', 'EXE101')
  await runMatching(admin, 'LECTURER', 'EXE201')
  await runMatching(admin, 'MENTOR', 'EXE201')

  const byId = new Map(lecturers.map((l) => [l.id, l]))
  const { workspace: dawnSpace, lecturerToken: dawnLecturerToken } = await seedTeamWorkspace(dawn, dawn.members, (id) => byId.get(id), now)
  await seedTeamWorkspace(star, star.members, (id) => byId.get(id), now)
  await call('PUT', `/api/workspaces/${dawnSpace.id}/checkpoints/1`, {
    token: dawnLecturerToken,
    body: { presentedOn: dateOnly(new Date(now)), comment: 'Nhóm trình bày rõ vấn đề, cần thêm số liệu thị trường.' },
  })
  log('Đã tạo tin nhắn, việc, file, bài nộp, họp, checkpoint cho 2 nhóm EXE101')

  // EXE201: lịch họp hằng tuần, một nhóm đã chọn slot, một nhóm chưa (C09).
  const sunLeader = exe201[0]
  const sunSpace = await workspaceOf(sunLeader, sunTeam.id)
  const sunDetail = await detailWithAdvisor(sunLeader.token, sunSpace.id, 'INSTRUCTOR')
  const weeklyLecturer = byId.get(sunDetail.people.find((p) => p.role === 'INSTRUCTOR').personId)
  const weeklyToken = await asLecturer(weeklyLecturer)
  await call('PUT', '/api/meeting-schedules/me', {
    token: weeklyToken,
    body: { windows: [{ dayOfWeek: 2, startTime: '09:00', endTime: '10:00' }, { dayOfWeek: 4, startTime: '14:00', endTime: '15:00' }], slotMinutes: 15, location: MEET_LINK },
  })
  const schedule = await call('POST', '/api/meeting-schedules/me/publish', { token: weeklyToken })
  await call('PUT', `/api/workspaces/${sunSpace.id}/slot-booking`, { token: sunLeader.token, body: { slotId: schedule.slots[1].id } })
  log(`Đã đăng lịch họp hằng tuần ${schedule.slots.length} slot, nhóm Ánh Dương đã chọn slot`)

  // Buổi mentor: mentor đề xuất họp với nhóm được ghép (rule 25).
  const mentorDetail = await detailWithAdvisor(sunLeader.token, sunSpace.id, 'MENTOR')
  const mentor = mentors.find((m) => m.id === mentorDetail.people.find((p) => p.role === 'MENTOR').personId)
  await call('POST', `/api/workspaces/${sunSpace.id}/meetings`, {
    token: await asMentor(mentor),
    body: {
      title: 'Buổi mentor 1',
      agenda: 'Góp ý mô hình kinh doanh',
      location: MEET_LINK,
      slots: [{ startsAt: new Date(now + 4 * DAY).toISOString(), durationMinutes: 60 }, { startsAt: new Date(now + 5 * DAY).toISOString(), durationMinutes: 60 }],
    },
  })

  // Rule 18: môn chỉ sang giai đoạn học kỳ khi có đợt lập nhóm đã đóng, kể cả EXE201 kế thừa nhóm.
  // Mở đợt ngắn để RoundAutoCloseJob chạy thật: khóa đợt, chốt nhóm, xếp sinh viên còn lại.
  const exe201Round = await call('POST', '/api/registration-rounds', {
    token: admin,
    body: {
      semesterId: semester.id,
      courseCode: 'EXE201',
      roundType: 'TEAM_FORMATION',
      name: 'Lập nhóm EXE201 (demo)',
      opensAt: new Date(Date.now() + 5_000).toISOString(),
      deadlineAt: new Date(Date.now() + 30_000).toISOString(),
    },
  })
  await call('POST', `/api/registration-rounds/${exe201Round.id}/open`, { token: admin })
  log('Chờ RoundAutoCloseJob khóa đợt EXE201 và chốt nhóm…')
  await until('EXE201 sang giai đoạn học kỳ', async () => {
    const phases = await call('GET', '/api/semesters/current/phase', { token: admin })
    return phases.find((p) => p.courseCode === 'EXE201')?.phase === 'SEMESTER_RUNNING'
  }, 180)
  await until('nhóm EXE201 được chốt', async () => (await call('GET', '/api/teams/me', { token: exe201[0].token })).status === 'FINALIZED', 180)

  log('Xong. Tài khoản demo (lấy token bằng POST /api/dev/token):')
  log(`  Trưởng nhóm EXE101: ${exe101[0].email}, mã ${exe101[0].id}`)
  log(`  Trưởng nhóm EXE201: ${exe201[0].email}, mã ${exe201[0].id}`)
  log(`  Giảng viên: ${lecturers[0].email}, mã ${lecturers[0].id}`)
  log(`  Mentor: ${mentors[0].email}, mã ${mentors[0].id}`)
  if (ownStudent) log(`  Sinh viên chưa có nhóm (đăng nhập Google): ${ownStudent.email}, mã ${ownStudent.id}`)
}

main().catch((error) => {
  console.error(`[seed] LỖI: ${error.message}`)
  process.exitCode = 1
})
