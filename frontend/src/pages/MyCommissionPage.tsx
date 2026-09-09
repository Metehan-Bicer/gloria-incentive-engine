import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { CalculationLine, CommissionResult, Employee } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { MONTHS, formatDate, formatDateTime, money, percent, periodLabel } from '../format'

const DEFAULT_YEAR = 2026
const DEFAULT_MONTH = 8

function rowClass(line: CalculationLine) {
  switch (line.lineType) {
    case 'Refund':
      return 'row-refund'
    case 'Excluded':
      return 'row-excluded'
    case 'RuleSubtotal':
      return 'row-subtotal'
    case 'Total':
      return 'row-total'
    case 'Tier':
      return 'row-tier'
    default:
      return ''
  }
}

function lineTypeLabel(type: CalculationLine['lineType']) {
  switch (type) {
    case 'Sale':
      return 'Satış'
    case 'Refund':
      return 'İade'
    case 'Excluded':
      return 'Hariç'
    case 'RuleSubtotal':
      return 'Ara toplam'
    case 'Total':
      return 'Toplam'
    case 'Tier':
      return 'Dilim'
  }
}

export function MyCommissionPage() {
  const { session } = useSession()
  const isEmployee = session.role === 'Personel'

  const [employees, setEmployees] = useState<Employee[]>([])
  const [employeeNo, setEmployeeNo] = useState(session.employeeNo)
  const [year, setYear] = useState(DEFAULT_YEAR)
  const [month, setMonth] = useState(DEFAULT_MONTH)
  const [result, setResult] = useState<CommissionResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (isEmployee) {
      setEmployeeNo(session.employeeNo)
      return
    }
    api
      .get<Employee[]>('/api/employees')
      .then((list) => {
        setEmployees(list)
        if (!list.some((e) => e.employeeNo === employeeNo) && list.length > 0) setEmployeeNo(list[0].employeeNo)
      })
      .catch(() => setEmployees([]))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEmployee, session.employeeNo, session.role])

  const load = useCallback(async () => {
    if (!employeeNo) return
    setLoading(true)
    setError(null)
    try {
      setResult(await api.get<CommissionResult>(`/api/employees/${employeeNo}/commissions/${year}/${month}`))
    } catch (e) {
      setResult(null)
      setError(e instanceof ApiError ? e.message : 'Hesaplama yüklenemedi')
    } finally {
      setLoading(false)
    }
  }, [employeeNo, year, month])

  useEffect(() => {
    load()
  }, [load, session.role])

  const grouped = useMemo(() => {
    if (!result) return []
    const groups: { key: string; title: string; lines: CalculationLine[] }[] = []
    const byRule = new Map<string, CalculationLine[]>()
    const excluded: CalculationLine[] = []
    let total: CalculationLine | null = null

    for (const line of result.lines) {
      if (line.lineType === 'Total') {
        total = line
      } else if (line.lineType === 'Excluded') {
        excluded.push(line)
      } else {
        const key = String(line.ruleId ?? 'other')
        if (!byRule.has(key)) byRule.set(key, [])
        byRule.get(key)!.push(line)
      }
    }

    for (const [key, lines] of byRule) {
      groups.push({ key, title: lines[0].ruleName ?? 'Kural', lines })
    }
    if (excluded.length) groups.push({ key: 'excluded', title: 'Hesaba dahil edilmeyen kayıtlar', lines: excluded })
    if (total) groups.push({ key: 'total', title: 'Genel toplam', lines: [total] })
    return groups
  }, [result])

  const years = [DEFAULT_YEAR - 1, DEFAULT_YEAR, DEFAULT_YEAR + 1]

  return (
    <>
      <div className="page-header">
        <div>
          <h1>{isEmployee ? 'Prim Hesabım' : 'Personel Prim Hesabı'}</h1>
          <p>Seçilen ay için satış, iade ve kural bazında hesaplama adımları.</p>
        </div>
        <div className="toolbar">
          {!isEmployee && (
            <select value={employeeNo} onChange={(e) => setEmployeeNo(e.target.value)} className="mono" style={{ padding: '0.5rem' }}>
              {employees.map((e) => (
                <option key={e.employeeNo} value={e.employeeNo}>
                  {e.employeeNo} · {e.fullName} ({e.department})
                </option>
              ))}
            </select>
          )}
          <select value={month} onChange={(e) => setMonth(Number(e.target.value))} style={{ padding: '0.5rem' }}>
            {MONTHS.map((m, i) => (
              <option key={m} value={i + 1}>
                {m}
              </option>
            ))}
          </select>
          <select value={year} onChange={(e) => setYear(Number(e.target.value))} style={{ padding: '0.5rem' }}>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          <button onClick={load} disabled={loading}>
            Yenile
          </button>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {result && (
        <>
          <div className="card" style={{ padding: '0.9rem 1.25rem' }}>
            <div className="toolbar" style={{ justifyContent: 'space-between' }}>
              <div>
                <strong>{result.fullName}</strong> <span className="muted">· {result.employeeNo} · {result.department} · {result.hotelCode}</span>
              </div>
              <div className="inline-list">
                <span className="badge badge-info">{periodLabel(result.year, result.month)}</span>
                {result.periodClosed ? (
                  <span className="badge badge-closed">Dönem kapalı · sonuç dondurulmuş</span>
                ) : (
                  <span className="badge badge-open">Dönem açık · her görüntülemede güncel hesap</span>
                )}
                <span className="muted" style={{ fontSize: '0.85rem' }}>
                  Hesaplama: {formatDateTime(result.calculatedAt)} ({result.calculatedBy})
                </span>
              </div>
            </div>
          </div>

          <div className="grid grid-stats" style={{ marginBottom: '1.25rem' }}>
            <div className="stat">
              <div className="stat-label">Brüt satış</div>
              <div className="stat-value">{money(result.grossSales)}</div>
            </div>
            <div className="stat">
              <div className="stat-label">İadeler</div>
              <div className={`stat-value ${result.refundTotal > 0 ? 'danger' : ''}`}>{result.refundTotal > 0 ? `− ${money(result.refundTotal)}` : money(0)}</div>
            </div>
            <div className="stat">
              <div className="stat-label">Net satış</div>
              <div className="stat-value">{money(result.netSales)}</div>
            </div>
            <div className="stat">
              <div className="stat-label">Hak edilen prim</div>
              <div className="stat-value primary">{money(result.totalCommission)}</div>
            </div>
          </div>

          <div className="card">
            <div className="card-header">
              <h2>Kural bazında özet</h2>
            </div>
            {result.ruleSummaries.length === 0 ? (
              <div className="empty">Bu dönemde prim kuralıyla eşleşen satış bulunmuyor.</div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Kural</th>
                      <th className="num">İşlem</th>
                      <th className="num">Net tutar</th>
                      <th className="num">Prim</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.ruleSummaries.map((s) => (
                      <tr key={s.ruleId}>
                        <td>{s.ruleName}</td>
                        <td className="num">{s.transactionCount}</td>
                        <td className="num">{money(s.baseAmount)}</td>
                        <td className="num">
                          <strong>{money(s.amount)}</strong>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {grouped.map((group) => (
            <div className="card" key={group.key}>
              <div className="card-header">
                <h2>{group.title}</h2>
                <span className="muted">{group.lines.filter((l) => l.sale).length > 0 ? `${group.lines.filter((l) => l.sale).length} kayıt` : ''}</span>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Tür</th>
                      <th>Tarih</th>
                      <th>Kaynak / Belge</th>
                      <th>Ürün</th>
                      <th>Açıklama</th>
                      <th className="num">Baz tutar</th>
                      <th className="num">Oran</th>
                      <th className="num">Prim</th>
                    </tr>
                  </thead>
                  <tbody>
                    {group.lines.map((line) => (
                      <tr key={line.sequence} className={rowClass(line)}>
                        <td className="num">{line.sequence}</td>
                        <td>{lineTypeLabel(line.lineType)}</td>
                        <td>{line.sale ? formatDate(line.sale.transactionDate) : ''}</td>
                        <td className="mono">{line.sale ? `${line.sale.sourceSystem}-${line.sale.externalDocumentNo}` : ''}</td>
                        <td>
                          {line.sale ? line.sale.productName : ''}
                          {line.sale && <div className="muted mono">{line.sale.productCode}</div>}
                        </td>
                        <td>{line.description}</td>
                        <td className="num">{line.lineType === 'Total' || line.lineType === 'RuleSubtotal' || line.lineType === 'Tier' || line.sale ? money(line.baseAmount) : ''}</td>
                        <td className="num">{percent(line.rate)}</td>
                        <td className="num">{line.lineType === 'Excluded' ? '' : money(line.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </>
      )}

      {!result && !error && <div className="card empty">{loading ? 'Hesaplanıyor…' : 'Personel ve dönem seçin.'}</div>}
    </>
  )
}
