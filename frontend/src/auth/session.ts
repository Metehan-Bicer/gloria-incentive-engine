import type { Role } from '../api/types'

export interface Session {
  role: Role
  employeeNo: string
}

const STORAGE_KEY = 'gloria.session'

export const defaultSession: Session = { role: 'Admin', employeeNo: 'P1001' }

export function loadSession(): Session {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaultSession
    const parsed = JSON.parse(raw) as Partial<Session>
    if (parsed.role && parsed.employeeNo) return { role: parsed.role, employeeNo: parsed.employeeNo }
  } catch {
    // ignore corrupt storage
  }
  return defaultSession
}

export function saveSession(session: Session) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
}
