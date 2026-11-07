import type { StudentRef } from '@/features/suggestions/api'
import { DEFAULT_TEAM_RULES, type TeamRules } from '@/shared/lib/teamRules'
import type { MemberMove } from './api'

export interface DraftTeam {
  id: string
  name: string
  isNew: boolean
  members: StudentRef[]
}

export interface Composition {
  isValid: boolean
  tooMany: boolean
  problems: string[]
}

/** Kiểm tra tại chỗ khi Admin chuyển sinh viên (W21) theo quy tắc W05, giống ValidateComposition của Matching. */
export function checkComposition(members: StudentRef[], rules: TeamRules = DEFAULT_TEAM_RULES): Composition {
  const count = members.length
  const specializations = new Set(members.map((m) => m.specialization?.id).filter((id) => id !== undefined)).size
  const problems: string[] = []
  if (count < rules.minSize) problems.push(`Thiếu ${rules.minSize - count} người`)
  if (count > rules.maxSize) problems.push(`Quá ${count - rules.maxSize} người`)
  if (specializations < rules.minSpecializations) problems.push(`Cần ít nhất ${rules.minSpecializations} chuyên ngành`)
  return { isValid: problems.length === 0, tooMany: count > rules.maxSize, problems }
}

/**
 * Áp các điều chỉnh chưa lưu lên phương án: sinh viên rời nhóm cũ (hoặc danh sách chưa ghép) và vào nhóm mới.
 * Trả bản sao, không đổi dữ liệu gốc.
 */
export function applyMoves(teams: DraftTeam[], unassigned: StudentRef[], moves: MemberMove[]) {
  const nextTeams = teams.map((t) => ({ ...t, members: [...t.members] }))
  let nextUnassigned = [...unassigned]
  const all = new Map([...teams.flatMap((t) => t.members), ...unassigned].map((s) => [s.id, s]))

  for (const move of moves) {
    const student = all.get(move.studentId)
    const target = nextTeams.find((t) => t.id === move.toTeamId)
    if (!student || !target) continue
    for (const team of nextTeams) team.members = team.members.filter((m) => m.id !== move.studentId)
    nextUnassigned = nextUnassigned.filter((s) => s.id !== move.studentId)
    target.members.push(student)
  }
  return { teams: nextTeams, unassigned: nextUnassigned }
}

/**
 * Thêm một điều chỉnh, gộp với điều chỉnh trước của cùng sinh viên (A → B rồi B → C thành A → C;
 * chuyển về chỗ cũ thì bỏ điều chỉnh).
 */
export function addMove(moves: MemberMove[], move: MemberMove): MemberMove[] {
  const previous = moves.find((m) => m.studentId === move.studentId)
  const rest = moves.filter((m) => m.studentId !== move.studentId)
  const origin = previous ? previous.fromTeamId : move.fromTeamId
  return origin === move.toTeamId ? rest : [...rest, { ...move, fromTeamId: origin }]
}
