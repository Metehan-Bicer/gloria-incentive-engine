import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { AuditLog, Rule, RuleInput, RuleOptions } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { formatDate, formatDateTime } from '../format'
import { RuleForm } from './RuleForm'

function describeParameters(rule: Rule): string {
  try {
    const p = JSON.parse(rule.parametersJson)
    switch (rule.ruleType) {
      case 'FixedPercentage':
        return `%${p.percentage}`
      case 'FixedAmountPerTransaction':
        return `${p.amount} TL / işlem`
      case 'TieredRate': {
        const tiers = (p.tiers as { threshold: number; rate: number }[]) ?? []
        const text = tiers.map((t) => `${t.threshold.toLocaleString('tr-TR')}+ → %${t.rate}`).join(', ')
        return `${p.mode === 'Highest' ? 'Ulaşılan dilim' : 'Dilim dilim'}: ${text}`
      }
    }
  } catch {
    return rule.parametersJson
  }
  return rule.parametersJson
}

function describeScope(rule: Rule): string {
  const parts: string[] = []
  if (rule.productCategory) parts.push(`Kategori ${rule.productCategory}`)
  if (rule.productCode) parts.push(`Ürün ${rule.productCode}`)
  if (rule.sourceSystem) parts.push(`Kaynak ${rule.sourceSystem}`)
  if (rule.departmentName) parts.push(`Departman ${rule.departmentName}`)
  return parts.length ? parts.join(' · ') : 'Tüm satışlar'
}

export function RulesPage() {
  const { session } = useSession()
  const [rules, setRules] = useState<Rule[]>([])
  const [options, setOptions] = useState<RuleOptions | null>(null)
  const [typeLabels, setTypeLabels] = useState<Record<string, string>>({})
  const [editing, setEditing] = useState<Rule | null>(null)
  const [creating, setCreating] = useState(false)
  const [history, setHistory] = useState<AuditLog[]>([])
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isAdmin = session.role === 'Admin'

  const load = useCallback(async () => {
    try {
      setError(null)
      const [list, opts] = await Promise.all([api.get<Rule[]>('/api/rules'), api.get<RuleOptions>('/api/rules/options')])
      setRules(list)
      setOptions(opts)
      setTypeLabels(Object.fromEntries(opts.types.map((t) => [t.type, t.label])))
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Kurallar yüklenemedi')
    }
  }, [])

  useEffect(() => {
    load()
  }, [load, session.role])

  useEffect(() => {
    if (!editing) {
      setHistory([])
      return
    }
    api
      .get<AuditLog[]>(`/api/audit-logs?entity=CommissionRule&entityId=${editing.id}`)
      .then(setHistory)
      .catch(() => setHistory([]))
  }, [editing])

  async function save(input: RuleInput) {
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      if (editing) {
        await api.put<Rule>(`/api/rules/${editing.id}`, input)
        setMessage(`"${input.name}" kuralı güncellendi.`)
      } else {
        await api.post<Rule>('/api/rules', input)
        setMessage(`"${input.name}" kuralı oluşturuldu. Açık dönemlerdeki hesaplamalar bir sonraki görüntülemede bu kuralı içerecek.`)
      }
      setEditing(null)
      setCreating(false)
      await load()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Kaydetme başarısız')
    } finally {
      setSaving(false)
    }
  }

  async function remove(rule: Rule) {
    if (!window.confirm(`"${rule.name}" kuralı silinsin mi?`)) return
    setError(null)
    try {
      await api.delete(`/api/rules/${rule.id}`)
      setMessage(`"${rule.name}" kuralı silindi.`)
      if (editing?.id === rule.id) setEditing(null)
      await load()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Silme başarısız')
    }
  }

  async function toggleActive(rule: Rule) {
    setError(null)
    try {
      await api.put<Rule>(`/api/rules/${rule.id}`, { ...rule, isActive: !rule.isActive })
      await load()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Güncelleme başarısız')
    }
  }

  const showForm = creating || editing !== null

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Prim Kuralları</h1>
          <p>Kurallar veritabanında tutulur; yeni kalem eklemek veya oran değiştirmek için kod değişikliği gerekmez.</p>
        </div>
        {isAdmin && (
          <button
            className="primary"
            onClick={() => {
              setEditing(null)
              setCreating(true)
              setMessage(null)
            }}
          >
            Yeni kural
          </button>
        )}
      </div>

      {!isAdmin && <Alert kind="info">Kural düzenleme yalnızca Admin rolüne açıktır. Bu ekranı salt okunur görüntülüyorsunuz.</Alert>}
      {error && <Alert kind="error">{error}</Alert>}
      {message && <Alert kind="success">{message}</Alert>}

      <div className={showForm ? 'grid grid-2' : ''}>
        <div className="card">
          <div className="card-header">
            <h2>Tanımlı kurallar</h2>
            <span className="muted">{rules.length} kural</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Öncelik</th>
                  <th>Kural</th>
                  <th>Tip</th>
                  <th>Parametre</th>
                  <th>Kapsam</th>
                  <th>Geçerlilik</th>
                  <th>Durum</th>
                  {isAdmin && <th />}
                </tr>
              </thead>
              <tbody>
                {rules.map((rule) => (
                  <tr
                    key={rule.id}
                    className={`clickable ${editing?.id === rule.id ? 'selected' : ''}`}
                    onClick={() => {
                      setCreating(false)
                      setEditing(rule)
                      setMessage(null)
                    }}
                  >
                    <td className="num">{rule.priority}</td>
                    <td>
                      <strong>{rule.name}</strong>
                      <div className="muted mono">#{rule.id}</div>
                    </td>
                    <td>{typeLabels[rule.ruleType] ?? rule.ruleType}</td>
                    <td>{describeParameters(rule)}</td>
                    <td>{describeScope(rule)}</td>
                    <td className="nowrap">
                      {formatDate(rule.validFrom)}
                      {rule.validTo ? ` – ${formatDate(rule.validTo)}` : ' →'}
                    </td>
                    <td>
                      <span className={`badge ${rule.isActive ? 'badge-open' : 'badge-muted'}`}>{rule.isActive ? 'Aktif' : 'Pasif'}</span>
                    </td>
                    {isAdmin && (
                      <td onClick={(e) => e.stopPropagation()}>
                        <div className="inline-list">
                          <button className="small" onClick={() => toggleActive(rule)}>
                            {rule.isActive ? 'Pasife al' : 'Aktifleştir'}
                          </button>
                          <button className="small danger" onClick={() => remove(rule)}>
                            Sil
                          </button>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
                {rules.length === 0 && (
                  <tr>
                    <td colSpan={8} className="empty">
                      Henüz kural tanımlanmamış.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {showForm && options && (
          <div>
            <div className="card">
              <div className="card-header">
                <h2>{editing ? `Kuralı düzenle: ${editing.name}` : 'Yeni kural'}</h2>
              </div>
              {isAdmin ? (
                <RuleForm
                  rule={editing}
                  options={options}
                  saving={saving}
                  onSave={save}
                  onCancel={() => {
                    setEditing(null)
                    setCreating(false)
                  }}
                />
              ) : (
                <pre className="json">{JSON.stringify(editing, null, 2)}</pre>
              )}
            </div>

            {editing && (
              <div className="card">
                <div className="card-header">
                  <h2>Değişiklik geçmişi</h2>
                  <span className="muted">{history.length} kayıt</span>
                </div>
                {history.length === 0 && <div className="muted">Bu kural için audit kaydı bulunamadı.</div>}
                {history.map((log) => (
                  <div key={log.id} style={{ marginBottom: '0.9rem' }}>
                    <div>
                      <span className={`badge ${log.action === 'Create' ? 'badge-open' : log.action === 'Delete' ? 'badge-closed' : 'badge-info'}`}>
                        {log.action === 'Create' ? 'Oluşturma' : log.action === 'Delete' ? 'Silme' : 'Güncelleme'}
                      </span>{' '}
                      <span className="muted">
                        {formatDateTime(log.changedAt)} · {log.changedBy}
                      </span>
                    </div>
                    {log.oldValuesJson && (
                      <pre className="json">
                        Eski: {log.oldValuesJson}
                      </pre>
                    )}
                    {log.newValuesJson && (
                      <pre className="json">
                        Yeni: {log.newValuesJson}
                      </pre>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </>
  )
}
