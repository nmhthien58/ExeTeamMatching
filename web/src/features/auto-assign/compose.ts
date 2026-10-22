import type { StudentRef } from '@/features/suggestions/api'
import type { MemberMove } from './api'

/** Quy tắc 1, 2 – giống ValidateComposition của Matching. */
export const MIN_SIZE = 4
export const MAX_SIZE = 6
export const MIN_SPECIALIZATIONS = 2

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

/** Kiểm tra tại chỗ khi Admin chuyển sinh viên (W21): sĩ số 4–6 và ít nhất 2 chuyên ngành. */
export function checkComposition(members: StudentRef[]): Composition {
  const count = members.length
  const specializations = new Set(members.map((m) => m.specialization?.id).filter((id) => id !== undefined)).size
  const problems: string[] = []
  if (count < MIN_SIZE) problems.push(`Thiếu ${MIN_SIZE - count} người`)
  if (count > MAX_SIZE) problems.push(`Quá ${count - MAX_SIZE} người`)
  if (specializations < MIN_SPECIALIZATIONS) problems.push('Cần thêm chuyên ngành thứ hai')
  return { isValid: problems.length === 0, tooMany: count > MAX_SIZE, problems }
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
