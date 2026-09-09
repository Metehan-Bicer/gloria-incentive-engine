import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Period, PeriodSummary } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { Drawer } from '../components/Drawer'
import { Icons } from '../components/Icons'
import { formatDateTime, money, periodLabel } from '../format'

export function PeriodsPage() {
  const { session } = useSession()
  const [periods, setPeriods] = useState<Period[]>([])
  const [selected, setSelected] = useState<{ year: number; month: number } | null>(null)
  const [summary, setSummary] = useState<PeriodSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [drawerError, setDrawerError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const loadPeriods = useCallback(async () => {
    try {
      setError(null)
      setPeriods(await api.get<Period[]>('/api/periods'))
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Dönemler yüklenemedi')
    }
  }, [])

  const loadSummary = useCallback(async () => {
    if (!selected) {
      setSummary(null)
      return
    }
    try {
      setDrawerError(null)
      setSummary(await api.get<PeriodSummary>(`/api/calculations/${selected.year}/${selected.month}`))
    } catch (e) {
      setSummary(null)
      setDrawerError(e instanceof ApiError ? e.message : 'Özet yüklenemedi')
    }
  }, [selected])

  useEffect(() => {
    loadPeriods()
  }, [loadPeriods, session.role])

  useEffect(() => {
    loadSummary()
  }, [loadSummary])

  const closeDrawer = useCallback(() => {
    setSelected(null)
    setDrawerError(null)
  }, [])

  async function run() {
    if (!selected) return
    setBusy(true)
    setDrawerError(null)
    setMessage(null)
    try {
      const result = await api.post<PeriodSummary>(`/api/calculations/run?year=${selected.year}&month=${selected.month}`)
      setSummary(result)
      setMessage(`${periodLabel(selected.year, selected.month)} dönemi için ${result.employeeCount} personelin primi hesaplandı. Toplam: ${money(result.totalCommission)}`)
      await loadPeriods()
    } catch (e) {
      setDrawerError(e instanceof ApiError ? e.message : 'Hesaplama başarısız')
    } finally {
      setBusy(false)
    }
  }

  async function close() {
    if (!selected) return
    const label = periodLabel(selected.year, selected.month)
    if (!window.confirm(`${label} dönemi kapatılacak. Kapatıldıktan sonra bu döneme ait satış kayıtları ve hesaplamalar değiştirilemez. Devam edilsin mi?`)) return
    setBusy(true)
    setDrawerError(null)
    setMessage(null)
    try {
      const result = await api.post<PeriodSummary>(`/api/periods/${selected.year}/${selected.month}/close`)
      setSummary(result)
      setMessage(`${label} dönemi kapatıldı ve hesaplamalar dondurularak kesinleştirildi.`)
      await loadPeriods()
    } catch (e) {
      setDrawerError(e instanceof ApiError ? e.message : 'Dönem kapatılamadı')
    } finally {
      setBusy(false)
    }
  }

  const current = periods.find((p) => selected && p.year === selected.year && p.month === selected.month)
  const isClosed = summary?.status === 'Closed' || current?.status === 'Closed'
  const openCount = periods.filter((p) => p.status === 'Open').length
  const closedCount = periods.length - openCount

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Dönemler</h1>
          <p>Aylık hesaplamayı toplu çalıştırın, sonuçları inceleyin ve dönemi kapatarak kayıtları dondurun.</p>
        </div>
        <div className="toolbar">
          <button onClick={loadPeriods}>
            <Icons.refresh />
            Yenile
          </button>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {message && <Alert kind="success">{message}</Alert>}

      <div className="grid grid-stats">
        <div className="stat">
          <div className="stat-label">Açık dönem</div>
          <div className="stat-value">{openCount}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Kapalı dönem</div>
          <div className="stat-value">{closedCount}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Toplam satış kaydı</div>
          <div className="stat-value">{periods.reduce((s, p) => s + p.saleCount, 0)}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Hesaplanan prim</div>
          <div className="stat-value primary">{money(periods.reduce((s, p) => s + p.totalCommission, 0))}</div>
        </div>
      </div>

      <div className="card card-flush">
        <div className="card-header">
          <h2>Dönem listesi</h2>
          <span className="muted">Detay ve işlemler için satıra tıklayın</span>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Dönem</th>
                <th>Durum</th>
                <th className="num">Satış kaydı</th>
                <th className="num">Hesaplanan personel</th>
                <th className="num">Toplam prim</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {periods.map((p) => (
                <tr
                  key={`${p.year}-${p.month}`}
                  className={`clickable ${selected && selected.year === p.year && selected.month === p.month ? 'selected' : ''}`}
                  onClick={() => setSelected({ year: p.year, month: p.month })}
                >
                  <td>
                    <strong>{periodLabel(p.year, p.month)}</strong>
                  </td>
                  <td>
                    <span className={`badge ${p.status === 'Closed' ? 'badge-closed' : 'badge-open'}`}>
                      {p.status === 'Closed' && <Icons.lock width={12} height={12} />}
                      {p.status === 'Closed' ? 'Kapalı' : 'Açık'}
                    </span>
                    {p.closedAt && (
                      <span className="sub">
                        {formatDateTime(p.closedAt)} · {p.closedBy}
                      </span>
                    )}
                  </td>
                  <td className="num">{p.saleCount}</td>
                  <td className="num">{p.calculatedEmployees}</td>
                  <td className="num">{money(p.totalCommission)}</td>
                  <td className="num">
                    <Icons.chevronRight style={{ color: 'var(--text-muted)' }} />
                  </td>
                </tr>
              ))}
              {periods.length === 0 && (
                <tr>
                  <td colSpan={6} className="empty">
                    Henüz satış verisi yüklenmemiş.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <Drawer
        open={selected !== null}
        wide
        title={selected ? periodLabel(selected.year, selected.month) : ''}
        subtitle={isClosed ? 'Dönem kapalı; sonuçlar dondurulmuş ve kesinleştirilmiş.' : 'Dönem açık; hesaplama yeniden çalıştırılabilir.'}
        onClose={closeDrawer}
        footer={
          <>
            <button type="button" onClick={closeDrawer} disabled={busy}>
              Kapat
            </button>
            <button type="button" className="danger" onClick={close} disabled={busy || isClosed}>
              <Icons.lock />
              Dönemi kapat
            </button>
            <button type="button" className="primary" onClick={run} disabled={busy || isClosed}>
              <Icons.play />
              Tüm personel için hesapla
            </button>
          </>
        }
      >
        {drawerError && <Alert kind="error">{drawerError}</Alert>}
        {isClosed && (
          <Alert kind="info">Bu dönem kapatılmış. Satış kayıtları, import ve yeniden hesaplama bu dönem için engellenir; personel ekranı dondurulmuş sonucu gösterir.</Alert>
        )}

        {summary && (
          <>
            <div className="grid grid-stats">
              <div className="stat">
                <div className="stat-label">Hesaplanan personel</div>
                <div className="stat-value">{summary.employeeCount}</div>
              </div>
              <div className="stat">
                <div className="stat-label">Toplam prim</div>
                <div className="stat-value primary">{money(summary.totalCommission)}</div>
              </div>
            </div>

            <div className="card card-flush">
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Personel</th>
                      <th>Departman</th>
                      <th className="num">Brüt</th>
                      <th className="num">İade</th>
                      <th className="num">Prim</th>
                    </tr>
                  </thead>
                  <tbody>
                    {summary.employees.map((e) => (
                      <tr key={e.employeeNo}>
                        <td>
                          {e.fullName}
                          <span className="sub">{e.employeeNo}</span>
                        </td>
                        <td>{e.department}</td>
                        <td className="num">{money(e.grossSales)}</td>
                        <td className="num">{e.refundTotal ? money(e.refundTotal) : '—'}</td>
                        <td className="num">
                          <strong>{e.calculatedAt ? money(e.totalCommission) : '—'}</strong>
                          {e.isFinal && (
                            <span className="badge badge-muted" style={{ marginLeft: '0.4rem' }}>
                              kesin
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                    <tr className="row-total">
                      <td colSpan={4}>Toplam</td>
                      <td className="num">{money(summary.totalCommission)}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}
      </Drawer>
    </>
  )
}
