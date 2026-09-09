import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { Period, PeriodSummary } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { formatDateTime, money, periodLabel } from '../format'

export function PeriodsPage() {
  const { session } = useSession()
  const [periods, setPeriods] = useState<Period[]>([])
  const [selected, setSelected] = useState<{ year: number; month: number } | null>(null)
  const [summary, setSummary] = useState<PeriodSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const loadPeriods = useCallback(async () => {
    try {
      setError(null)
      const list = await api.get<Period[]>('/api/periods')
      setPeriods(list)
      setSelected((current) => {
        if (current) return current
        const busiest = [...list].sort((a, b) => b.saleCount - a.saleCount)[0]
        return busiest ? { year: busiest.year, month: busiest.month } : null
      })
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Dönemler yüklenemedi')
    }
  }, [])

  const loadSummary = useCallback(async () => {
    if (!selected) return
    try {
      setSummary(await api.get<PeriodSummary>(`/api/calculations/${selected.year}/${selected.month}`))
    } catch (e) {
      setSummary(null)
      setError(e instanceof ApiError ? e.message : 'Özet yüklenemedi')
    }
  }, [selected])

  useEffect(() => {
    loadPeriods()
  }, [loadPeriods, session.role])

  useEffect(() => {
    loadSummary()
  }, [loadSummary])

  async function run() {
    if (!selected) return
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      const result = await api.post<PeriodSummary>(`/api/calculations/run?year=${selected.year}&month=${selected.month}`)
      setSummary(result)
      setMessage(`${periodLabel(selected.year, selected.month)} dönemi için ${result.employeeCount} personelin primi hesaplandı. Toplam: ${money(result.totalCommission)}`)
      await loadPeriods()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Hesaplama başarısız')
    } finally {
      setBusy(false)
    }
  }

  async function close() {
    if (!selected) return
    const label = periodLabel(selected.year, selected.month)
    if (!window.confirm(`${label} dönemi kapatılacak. Kapatıldıktan sonra bu döneme ait satış kayıtları ve hesaplamalar değiştirilemez. Devam edilsin mi?`)) return
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      const result = await api.post<PeriodSummary>(`/api/periods/${selected.year}/${selected.month}/close`)
      setSummary(result)
      setMessage(`${label} dönemi kapatıldı ve hesaplamalar dondurularak kesinleştirildi.`)
      await loadPeriods()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Dönem kapatılamadı')
    } finally {
      setBusy(false)
    }
  }

  const current = periods.find((p) => selected && p.year === selected.year && p.month === selected.month)
  const isClosed = summary?.status === 'Closed' || current?.status === 'Closed'

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Dönemler</h1>
          <p>Aylık hesaplamayı toplu çalıştırın, sonuçları inceleyin ve dönemi kapatarak kayıtları dondurun.</p>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {message && <Alert kind="success">{message}</Alert>}

      <div className="grid grid-2">
        <div>
          <div className="card">
            <div className="card-header">
              <h2>Dönem listesi</h2>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Dönem</th>
                    <th>Durum</th>
                    <th className="num">Satış kaydı</th>
                    <th className="num">Hesaplanan</th>
                    <th className="num">Toplam prim</th>
                  </tr>
                </thead>
                <tbody>
                  {periods.map((p) => (
                    <tr
                      key={`${p.year}-${p.month}`}
                      className={`clickable ${selected && selected.year === p.year && selected.month === p.month ? 'selected' : ''}`}
                      onClick={() => setSelected({ year: p.year, month: p.month })}
                    >
                      <td>{periodLabel(p.year, p.month)}</td>
                      <td>
                        <span className={`badge ${p.status === 'Closed' ? 'badge-closed' : 'badge-open'}`}>{p.status === 'Closed' ? 'Kapalı' : 'Açık'}</span>
                        {p.closedAt && (
                          <div className="muted" style={{ fontSize: '0.78rem' }}>
                            {formatDateTime(p.closedAt)} · {p.closedBy}
                          </div>
                        )}
                      </td>
                      <td className="num">{p.saleCount}</td>
                      <td className="num">{p.calculatedEmployees}</td>
                      <td className="num">{money(p.totalCommission)}</td>
                    </tr>
                  ))}
                  {periods.length === 0 && (
                    <tr>
                      <td colSpan={5} className="empty">
                        Henüz satış verisi yüklenmemiş.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div>
          {selected && (
            <div className="card">
              <div className="card-header">
                <h2>{periodLabel(selected.year, selected.month)}</h2>
                {isClosed ? <span className="badge badge-closed">Kapalı</span> : <span className="badge badge-open">Açık</span>}
              </div>
              <div className="toolbar" style={{ marginBottom: '1rem' }}>
                <button className="primary" onClick={run} disabled={busy || isClosed}>
                  Tüm personel için hesapla
                </button>
                <button className="danger" onClick={close} disabled={busy || isClosed}>
                  Dönemi kapat
                </button>
              </div>
              {isClosed && <Alert kind="info">Bu dönem kapatılmış. Satış kayıtları, import ve yeniden hesaplama bu dönem için engellenir; personel ekranı dondurulmuş sonucu gösterir.</Alert>}

              {summary && (
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
                            <div className="muted mono">{e.employeeNo}</div>
                          </td>
                          <td>{e.department}</td>
                          <td className="num">{money(e.grossSales)}</td>
                          <td className="num">{e.refundTotal ? money(e.refundTotal) : ''}</td>
                          <td className="num">
                            <strong>{e.calculatedAt ? money(e.totalCommission) : '—'}</strong>
                            {e.isFinal && <span className="badge badge-muted" style={{ marginLeft: '0.4rem' }}>kesin</span>}
                          </td>
                        </tr>
                      ))}
                      <tr className="row-total">
                        <td colSpan={4}>Toplam ({summary.employeeCount} personel hesaplandı)</td>
                        <td className="num">{money(summary.totalCommission)}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </>
  )
}
