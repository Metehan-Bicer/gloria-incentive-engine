import { useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import type { Employee, Role } from '../api/types'
import { useSession } from '../auth/SessionContext'

const ROLES: { value: Role; label: string }[] = [
  { value: 'Admin', label: 'Admin' },
  { value: 'Muhasebe', label: 'Muhasebe' },
  { value: 'Personel', label: 'Personel' },
]

export function Layout() {
  const { session, update } = useSession()
  const [employees, setEmployees] = useState<Employee[]>([])

  useEffect(() => {
    let cancelled = false
    const headers = new Headers({ 'X-Role': 'Muhasebe' })
    fetch('/api/employees', { headers })
      .then((r) => (r.ok ? r.json() : []))
      .then((list: Employee[]) => {
        if (!cancelled) setEmployees(list)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [])

  const canManage = session.role !== 'Personel'

  return (
    <>
      <header className="topbar">
        <NavLink to="/" className="brand">
          <span className="brand-mark">G</span>
          Gloria Prim Sistemi
        </NavLink>
        <nav className="nav">
          <NavLink to="/primlerim">Prim Hesabı</NavLink>
          {canManage && <NavLink to="/kurallar">Prim Kuralları</NavLink>}
          {canManage && <NavLink to="/donemler">Dönemler</NavLink>}
          {canManage && <NavLink to="/aktarim">Veri Aktarımı</NavLink>}
        </nav>
        <div className="session">
          <label htmlFor="role">Rol</label>
          <select id="role" value={session.role} onChange={(e) => update({ role: e.target.value as Role })}>
            {ROLES.map((r) => (
              <option key={r.value} value={r.value}>
                {r.label}
              </option>
            ))}
          </select>
          <label htmlFor="employee">Personel</label>
          <select id="employee" value={session.employeeNo} onChange={(e) => update({ employeeNo: e.target.value })}>
            {employees.length === 0 && <option value={session.employeeNo}>{session.employeeNo}</option>}
            {employees.map((e) => (
              <option key={e.employeeNo} value={e.employeeNo}>
                {e.employeeNo} · {e.fullName}
              </option>
            ))}
          </select>
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
    </>
  )
}

