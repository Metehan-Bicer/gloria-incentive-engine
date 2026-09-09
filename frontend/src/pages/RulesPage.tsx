import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { AuditLog, Rule, RuleInput, RuleOptions } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { Drawer } from '../components/Drawer'
import { Icons } from '../components/Icons'
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

const actionLabel = { Create: 'Oluşturma', Update: 'Güncelleme', Delete: 'Silme' } as const

type DrawerState = { mode: 'closed' } | { mode: 'create' } | { mode: 'edit'; rule: Rule }

export function RulesPage() {
  const { session } = useSession()
  const [rules, setRules] = useState<Rule[]>([])
  const [options, setOptions] = useState<RuleOptions | null>(null)
  const [typeLabels, setTypeLabels] = useState<Record<string, string>>({})
  const [drawer, setDrawer] = useState<DrawerState>({ mode: 'closed' })
  const [history, setHistory] = useState<AuditLog[]>([])
  const [error, setError] = useState<string | null>(null)
  const [drawerError, setDrawerError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isAdmin = session.role === 'Admin'
  const editing = drawer.mode === 'edit' ? drawer.rule : null

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

  const closeDrawer = useCallback(() => {
    setDrawer({ mode: 'closed' })
    setDrawerError(null)
  }, [])

  async function save(input: RuleInput) {
    setSaving(true)
    setDrawerError(null)
    setMessage(null)
    try {
      if (editing) {
        await api.put<Rule>(`/api/rules/${editing.id}`, input)
        setMessage(`"${input.name}" kuralı güncellendi.`)
      } else {
        await api.post<Rule>('/api/rules', input)
        setMessage(`"${input.name}" kuralı oluşturuldu. Açık dönemlerdeki hesaplamalar bir sonraki görüntülemede bu kuralı içerecek.`)
      }
      closeDrawer()
      await load()
    } catch (e) {
      setDrawerError(e instanceof ApiError ? e.message : 'Kaydetme başarısız')
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
      if (editing?.id === rule.id) closeDrawer()
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

  const drawerOpen = drawer.mode !== 'closed'
  const drawerTitle = editing ? 'Kuralı düzenle' : 'Yeni prim kuralı'

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Prim Kuralları</h1>
          <p>Kurallar veritabanında tutulur; yeni kalem eklemek veya oran değiştirmek için kod değişikliği gerekmez.</p>
        </div>
        {isAdmin && (
          <div className="toolbar">
            <button className="primary" onClick={() => setDrawer({ mode: 'create' })}>
              <Icons.plus />
              Yeni kural
            </button>
          </div>
        )}
      </div>

      {!isAdmin && <Alert kind="info">Kural düzenleme yalnızca Admin rolüne açıktır. Bu ekranı salt okunur görüntülüyorsunuz.</Alert>}
      {error && <Alert kind="error">{error}</Alert>}
      {message && <Alert kind="success">{message}</Alert>}

      <div className="card card-flush">
        <div className="card-header">
          <h2>Tanımlı kurallar</h2>
          <span className="muted">{rules.length} kural</span>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="num">Öncelik</th>
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
                <tr key={rule.id} className={`clickable ${editing?.id === rule.id ? 'selected' : ''}`} onClick={() => setDrawer({ mode: 'edit', rule })}>
                  <td className="num">{rule.priority}</td>
                  <td>
                    <strong>{rule.name}</strong>
                    <span className="sub">#{rule.id}</span>
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
                      <div className="row-actions">
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

      <Drawer
        open={drawerOpen && options !== null}
        title={isAdmin ? drawerTitle : (editing?.name ?? 'Kural')}
        subtitle={editing ? `#${editing.id} · ${typeLabels[editing.ruleType] ?? editing.ruleType}` : 'Kural kaydedildiğinde açık dönemlerde hemen uygulanır.'}
        onClose={closeDrawer}
        footer={
          isAdmin ? (
            <>
              <button type="button" onClick={closeDrawer} disabled={saving}>
                Vazgeç
              </button>
              <button type="submit" form="rule-form" className="primary" disabled={saving}>
                {editing ? 'Değişiklikleri kaydet' : 'Kuralı oluştur'}
              </button>
            </>
          ) : (
            <button type="button" onClick={closeDrawer}>
              Kapat
            </button>
          )
        }
      >
        {drawerError && <Alert kind="error">{drawerError}</Alert>}

        {options && isAdmin && <RuleForm formId="rule-form" rule={editing} options={options} onSave={save} />}

        {options && !isAdmin && editing && (
          <dl className="kv">
            <dt>Tip</dt>
            <dd>{typeLabels[editing.ruleType]}</dd>
            <dt>Parametre</dt>
            <dd>{describeParameters(editing)}</dd>
            <dt>Kapsam</dt>
            <dd>{describeScope(editing)}</dd>
            <dt>Geçerlilik</dt>
            <dd>
              {formatDate(editing.validFrom)}
              {editing.validTo ? ` – ${formatDate(editing.validTo)}` : ' →'}
            </dd>
            <dt>Durum</dt>
            <dd>{editing.isActive ? 'Aktif' : 'Pasif'}</dd>
          </dl>
        )}

        {editing && (
          <div className="form-section" style={{ marginTop: '1.5rem' }}>
            <div className="form-section-title">Değişiklik geçmişi · {history.length} kayıt</div>
            {history.length === 0 && <div className="muted">Bu kural için audit kaydı bulunamadı.</div>}
            <div className="timeline">
              {history.map((log) => (
                <div key={log.id} className="timeline-item">
                  <div className="inline-list">
                    <span className={`badge ${log.action === 'Create' ? 'badge-open' : log.action === 'Delete' ? 'badge-closed' : 'badge-info'}`}>{actionLabel[log.action]}</span>
                    <span className="when">
                      {formatDateTime(log.changedAt)} · {log.changedBy}
                    </span>
                  </div>
                  {log.oldValuesJson && <pre className="json">Eski: {log.oldValuesJson}</pre>}
                  {log.newValuesJson && <pre className="json">Yeni: {log.newValuesJson}</pre>}
                </div>
              ))}
            </div>
          </div>
        )}
      </Drawer>
    </>
  )
}
