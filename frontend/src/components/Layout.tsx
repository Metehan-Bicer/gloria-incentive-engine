import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import type { Employee, Role } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Icons } from './Icons'

const ROLES: { value: Role; label: string; description: string }[] = [
  { value: 'Admin', label: 'Admin', description: 'Kuralları yönetir, tüm verilere erişir.' },
  { value: 'Muhasebe', label: 'Muhasebe', description: 'Tüm personelin primini görür, dönemi kapatır.' },
  { value: 'Personel', label: 'Personel', description: 'Yalnızca kendi prim hesabını görür.' },
]

function Brand() {
  return (
    <NavLink to="/" className="sidebar-brand">
      <span className="brand-mark">G</span>
      <span>
        <div className="brand-name">Gloria Hotels &amp; Resorts</div>
        <div className="brand-sub">Personel Prim Sistemi</div>
      </span>
    </NavLink>
  )
}

export function Layout() {
  const { session, update } = useSession()
  const [employees, setEmployees] = useState<Employee[]>([])
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()

  useEffect(() => {
    let cancelled = false
    fetch('/api/employees', { headers: { 'X-Role': 'Muhasebe' } })
      .then((r) => (r.ok ? r.json() : []))
      .then((list: Employee[]) => {
        if (!cancelled) setEmployees(list)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    setMenuOpen(false)
  }, [location.pathname])

  const canManage = session.role !== 'Personel'
  const role = ROLES.find((r) => r.value === session.role)

  return (
    <div className="shell">
      <header className="mobile-header">
        <Brand />
        <button type="button" className="icon-btn" onClick={() => setMenuOpen((v) => !v)} aria-label="Menü">
          {menuOpen ? <Icons.close /> : <Icons.menu />}
        </button>
      </header>

      {menuOpen && <div className="sidebar-backdrop" onClick={() => setMenuOpen(false)} />}

      <aside className={`sidebar ${menuOpen ? 'open' : ''}`}>
        <Brand />
        <nav className="sidebar-nav">
          <div className="sidebar-section">Prim</div>
          <NavLink to="/primlerim">
            <Icons.wallet />
            Prim Hesabı
          </NavLink>
          {canManage && (
            <>
              <NavLink to="/kurallar">
                <Icons.rules />
                Prim Kuralları
              </NavLink>
              <div className="sidebar-section">Yönetim</div>
              <NavLink to="/donemler">
                <Icons.calendar />
                Dönemler
              </NavLink>
              <NavLink to="/aktarim">
                <Icons.upload />
                Veri Aktarımı
              </NavLink>
            </>
          )}
        </nav>
        <div className="sidebar-session">
          <div>
            <label htmlFor="role">Oturum rolü</label>
            <select id="role" value={session.role} onChange={(e) => update({ role: e.target.value as Role })}>
              {ROLES.map((r) => (
                <option key={r.value} value={r.value}>
                  {r.label}
                </option>
              ))}
            </select>
          </div>
          <div>
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
          {role && <div className="hint">{role.description}</div>}
        </div>
      </aside>

      <div className="content">
        <main className="page">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
