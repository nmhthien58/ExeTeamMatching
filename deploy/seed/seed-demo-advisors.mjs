// Seed bổ sung cho demo ghép nhóm, ghép giảng viên, ghép mentor (chạy sau `demo-db.sh reset truoc-moc`, trước `snapshot`).
//   node deploy/seed/seed-demo-advisors.mjs
// Chạy lại được: chỉ điền chỗ còn thiếu, giảng viên / mentor tìm theo email trước khi tạo.
//  1. Học kỳ FA26: dời tuần bắt đầu mentor sang MENTOR_WEEK để job 07:00 hằng ngày không tự ghép mentor trước buổi demo
//     (demo dùng nút "Ghép sớm" ở W28).
//  2. Mọi sinh viên FA26 có chuyên ngành (thuộc đúng ngành) và đủ 5 kỹ năng. Không đụng nhóm: sinh viên chưa có nhóm giữ nguyên
//     để demo "Kết thúc sớm" → job ghép sinh viên lẻ.
//  3. Đủ giảng viên (khảo sát lần đầu xong: domain, số nhóm mong muốn) và mentor (domain, số nhóm mong muốn) để mọi nhóm ghép được.
//  4. Giảng viên quan tâm vài nhóm, trưởng nhóm đặt nguyện vọng giảng viên → đủ các bậc ghép: hai bên cùng chọn, nguyện vọng,
//     giảng viên quan tâm, trùng chuyên môn. Mentor chỉ quan tâm được sau khi đợt lập nhóm đã chốt nên không seed ở đây.
const GATEWAY = process.env.SEED_GATEWAY_URL ?? 'https://gateway-production-6183.up.railway.app'
const ADMIN_EMAIL = process.env.SEED_ADMIN_EMAIL ?? 'admin@example.com'
const SEMESTER_CODE = 'FA26'
const MENTOR_WEEK = Number(process.env.SEED_MENTOR_WEEK ?? 16)
const LECTURER_MAX_PER_COURSE = 6
const MENTOR_MAX = 8
const AVG_TEAM_SIZE = 5

const log = (message) => console.log(`[seed-advisors] ${message}`)

class HttpError extends Error {
  constructor(method, path, status, body) {
    super(`${method} ${path} → ${status}: ${body.slice(0, 300)}`)
    this.status = status
  }
}

async function call(method, path, { token, body } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  for (let attempt = 1; ; attempt++) {
    const response = await fetch(`${GATEWAY}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
    const text = await response.text()
    if (response.ok) return text ? JSON.parse(text) : null
    // 429 / 5xx tạm thời: thử lại vài lần.
    if ((response.status === 429 || response.status >= 500) && attempt < 4) {
      await new Promise((r) => setTimeout(r, 1000 * attempt))
      continue
    }
    throw new HttpError(method, path, response.status, text)
  }
}

const devToken = async (person) => (await call('POST', '/api/dev/token', { body: person })).accessToken

/** Chạy fn cho từng phần tử, tối đa `size` việc cùng lúc. */
async function pool(items, size, fn) {
  let next = 0
  const workers = Array.from({ length: Math.min(size, items.length) }, async () => {
    while (next < items.length) {
      const index = next++
      await fn(items[index], index)
    }
  })
  await Promise.all(workers)
}

async function pages(path, token) {
  const all = []
  for (let page = 1; ; page++) {
    const sep = path.includes('?') ? '&' : '?'
    const result = await call('GET', `${path}${sep}page=${page}&pageSize=100`, { token })
    all.push(...result.items)
    if (all.length >= result.totalCount || result.items.length === 0) return all
  }
}

const SKILLS = ['TECHNOLOGY', 'UI_UX_DESIGN', 'MANAGEMENT', 'MARKETING', 'COMMUNICATION_PLANNING']
// Cùng sinh viên → cùng điểm kỹ năng, chạy lại không đổi.
const hash = (text) => [...text].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7)

const LECTURER_NAMES = [
  'Trần Thu Hà', 'Nguyễn Đức Khoa', 'Phạm Minh Tâm', 'Lê Hoàng Long', 'Võ Thị Lan', 'Đặng Quốc Bảo', 'Bùi Ngọc Anh', 'Hồ Thanh Sơn',
  'Ngô Mỹ Linh', 'Dương Văn Hùng', 'Lý Thu Trang', 'Trịnh Gia Huy', 'Mai Phương Thảo', 'Cao Minh Đức', 'Tạ Hồng Nhung', 'Phan Tuấn Kiệt',
  'Đinh Khánh Vy', 'Lưu Quang Hải', 'Châu Bích Ngọc', 'Kiều Văn Thành',
]
const MENTOR_NAMES = [
  'Đỗ Quang Vinh', 'Vũ Thanh Mai', 'Hoàng Gia Bảo', 'Nguyễn Thảo Nguyên', 'Lâm Đức Trí', 'Phùng Hải Yến', 'Tô Minh Khang',
  'Quách Ngọc Hân', 'La Thành Công', 'Âu Diệu Linh', 'Mạc Tiến Dũng', 'Thái Kim Chi', 'Giang Hữu Phước', 'Ông Bảo Trâm',
]
const ORGS = ['FPT Software', 'Startup Demo', 'VinaCapital Demo', 'Tiki Demo', 'Momo Demo', 'Base Demo', 'Got It Demo']

async function main() {
  log(`Gateway: ${GATEWAY}`)
  const admin = await devToken({ role: 'Admin', email: ADMIN_EMAIL })

  // 1. Học kỳ
  const semester = (await call('GET', '/api/semesters', { token: admin })).find((s) => s.code === SEMESTER_CODE)
  if (!semester) throw new Error(`Không thấy học kỳ ${SEMESTER_CODE}`)
  if (semester.mentoringStartWeek !== MENTOR_WEEK) {
    await call('PUT', `/api/semesters/${semester.id}`, {
      token: admin,
      body: {
        code: semester.code, name: semester.name, startDate: semester.startDate, endDate: semester.endDate, isCurrent: semester.isCurrent,
        mentoringStartWeek: MENTOR_WEEK,
        expectedMentorSessions: semester.expectedMentorSessions ?? 3,
        slotBookingDeadlineWeek: semester.slotBookingDeadlineWeek ?? 2,
        meetingSlotMinutes: semester.meetingSlotMinutes ?? 15,
      },
    })
  }
  log(`${SEMESTER_CODE}: tuần bắt đầu mentor = ${MENTOR_WEEK}`)

  // 2. Sinh viên
  const specs = (await call('GET', '/api/specializations', { token: admin })).filter((s) => s.isActive !== false)
  const domains = await call('GET', '/api/domains', { token: admin })
  const students = {}
  for (const course of ['EXE101', 'EXE201']) {
    students[course] = await pages(`/api/students?semesterId=${semester.id}&courseCode=${course}&sort=studentCode`, admin)
  }
  const everyone = [...students.EXE101, ...students.EXE201]
  let fixedSpec = 0
  let fixedSkills = 0
  let failed = 0
  await pool(everyone, 8, async (s) => {
    try {
      const detail = await call('GET', `/api/students/${s.id}`, { token: admin })
      const needSpec = !detail.specialization
      const needSkills = (detail.skills ?? []).length < SKILLS.length
      if (!needSpec && !needSkills) return
      const h = hash(detail.studentCode)
      const options = specs.filter((sp) => sp.majorId == null || sp.majorId === detail.major?.id)
      const body = { description: detail.description, isSeekingTeam: detail.isSeekingTeam }
      if (needSpec && options.length) body.specializationId = options[h % options.length].id
      // API bắt buộc gửi skills mỗi lần: giữ điểm đang có, thiếu thì tạo.
      body.skills = needSkills ? SKILLS.map((skill, i) => ({ skill, level: ((h >> (i * 3)) % 5) + 1 })) : detail.skills
      const token = await devToken({ role: 'Student', email: detail.email, studentId: s.id })
      await call('PUT', '/api/students/me', { token, body })
      if (body.specializationId) fixedSpec++
      if (needSkills) fixedSkills++
    } catch (error) {
      failed++
      log(`  bỏ qua ${s.studentCode}: ${error.message}`)
    }
  })
  log(`Sinh viên: ${everyone.length} người, điền chuyên ngành ${fixedSpec}, điền kỹ năng ${fixedSkills}, lỗi ${failed}`)

  // 3. Ước số nhóm mỗi môn = nhóm đang có + sinh viên chưa có nhóm / 5 → số giảng viên, mentor cần.
  const teams = {}
  const estimate = {}
  for (const course of ['EXE101', 'EXE201']) {
    teams[course] = (await pages(`/api/teams?semesterId=${semester.id}&courseCode=${course}`, admin)).filter((t) => t.status !== 'DISBANDED')
    const free = students[course].filter((s) => !s.hasTeam).length
    estimate[course] = teams[course].length + Math.ceil(free / AVG_TEAM_SIZE)
    log(`${course}: ${teams[course].length} nhóm, ${free} sinh viên chưa có nhóm → ước ${estimate[course]} nhóm sau ghép`)
  }
  const lecturerCount = Math.min(LECTURER_NAMES.length, Math.max(6, Math.ceil(Math.max(estimate.EXE101, estimate.EXE201) / LECTURER_MAX_PER_COURSE) + 2))
  const mentorCount = Math.min(MENTOR_NAMES.length, Math.ceil((estimate.EXE101 + estimate.EXE201) / MENTOR_MAX) + 2)

  const domainsFor = (i) => [domains[i % domains.length].id, domains[(i + 5) % domains.length].id, domains[(i + 11) % domains.length].id]

  const allLecturers = await pages('/api/lecturers', admin)
  const lecturers = []
  for (let i = 0; i < lecturerCount; i++) {
    const email = `gv${i + 1}.seed@example.com`
    let lecturer = allLecturers.find((l) => l.email.toLowerCase() === email)
    const body = {
      fullName: lecturer?.fullName ?? LECTURER_NAMES[i], email, domainIds: domainsFor(i),
      maxTeamsExe101: LECTURER_MAX_PER_COURSE, maxTeamsExe201: LECTURER_MAX_PER_COURSE, isActive: true,
    }
    lecturer = lecturer
      ? await call('PUT', `/api/lecturers/${lecturer.id}`, { token: admin, body })
      : await call('POST', '/api/lecturers', { token: admin, body })
    // Khảo sát lần đầu (L06): domain chuyên môn và số nhóm mong muốn.
    // Giảng viên đã gắn tài khoản thật (email khác) thì không lấy được dev token: admin đã đặt domain, số nhóm; bỏ qua khảo sát và quan tâm.
    try {
      lecturer.token = await devToken({ role: 'Lecturer', email, lecturerId: lecturer.id })
      await call('PUT', '/api/lecturers/me', {
        token: lecturer.token,
        body: { domainIds: domainsFor(i), desiredTeamsExe101: LECTURER_MAX_PER_COURSE - 2, desiredTeamsExe201: LECTURER_MAX_PER_COURSE - 2 },
      })
    } catch (error) {
      if (error.status !== 409) throw error
      log(`  ${email}: đã gắn tài khoản khác, bỏ qua khảo sát`)
    }
    lecturers.push(lecturer)
  }
  log(`Giảng viên: ${lecturers.length} người, tối đa ${LECTURER_MAX_PER_COURSE} nhóm mỗi môn`)

  const allMentors = await pages('/api/mentors', admin)
  let mentorsReady = 0
  for (let i = 0; i < mentorCount; i++) {
    const email = `mentor${i + 1}.seed@example.com`
    let mentor = allMentors.find((m) => m.email?.toLowerCase() === email)
    const body = {
      fullName: mentor?.fullName ?? MENTOR_NAMES[i], email, organization: mentor?.organization ?? ORGS[i % ORGS.length],
      isInternal: false, domainIds: domainsFor(i + 3), maxTeams: MENTOR_MAX, isActive: true,
    }
    mentor = mentor
      ? await call('PUT', `/api/mentors/${mentor.id}`, { token: admin, body })
      : await call('POST', '/api/mentors', { token: admin, body })
    try {
      const token = await devToken({ role: 'Mentor', email, mentorId: mentor.id })
      await call('PUT', '/api/mentors/me', {
        token,
        body: { preferredTeams: MENTOR_MAX - 3, description: `Mentor ${body.organization}, đồng hành cùng nhóm khởi nghiệp sinh viên.`, domainIds: body.domainIds },
      })
    } catch (error) {
      if (error.status !== 409) throw error
      log(`  ${email}: đã gắn tài khoản khác, bỏ qua hồ sơ mentor`)
    }
    mentorsReady++
  }
  log(`Mentor: ${mentorsReady} người seed (+ mentor có sẵn), tối đa ${MENTOR_MAX} nhóm, mong muốn ${MENTOR_MAX - 3}`)

  // 4. Quan tâm của giảng viên và nguyện vọng của nhóm (chỉ được khi đợt lập nhóm còn mở).
  const byId = new Map(everyone.map((s) => [s.id, s]))
  let interests = 0
  let preferences = 0
  for (const course of ['EXE101', 'EXE201']) {
    const list = teams[course].filter((t) => !t.lecturerName)
    for (const [index, team] of list.entries()) {
      const first = lecturers[index % lecturers.length]
      const second = lecturers[(index + 1) % lecturers.length]
      const third = lecturers[(index + 2) % lecturers.length]
      // Cứ 4 nhóm: nhóm 0 hai bên cùng chọn, nhóm 1 chỉ nguyện vọng, nhóm 2 chỉ giảng viên quan tâm, nhóm 3 để ghép theo chuyên môn.
      const kind = index % 4
      if ((kind === 0 || kind === 2) && first.token) {
        try {
          await call('POST', `/api/teams/${team.id}/topic-interests`, { token: first.token, body: { advisorKind: 'LECTURER' } })
          interests++
        } catch (error) {
          log(`  quan tâm ${team.name}: ${error.message}`)
        }
      }
      if ((kind === 0 || kind === 1) && team.leader) {
        const leader = byId.get(team.leader.id)
        if (!leader) continue
        try {
          const token = await devToken({ role: 'Student', email: leader.email, studentId: leader.id })
          await call('PUT', `/api/teams/${team.id}/lecturer-preferences`, { token, body: { lecturerIds: [first.id, second.id, third.id] } })
          preferences++
        } catch (error) {
          log(`  nguyện vọng ${team.name}: ${error.message}`)
        }
      }
    }
  }
  log(`Giảng viên quan tâm ${interests} nhóm, ${preferences} nhóm đặt nguyện vọng`)
  log('Xong. Chờ ~1 phút cho Matching nhận sự kiện rồi chạy: bash deploy/railway/demo-db.sh snapshot')
}

main().catch((error) => {
  console.error(error)
  process.exit(1)
})
