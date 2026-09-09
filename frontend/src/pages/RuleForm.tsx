import { useEffect, useState, type FormEvent } from 'react'
import type { Rule, RuleInput, RuleOptions, RuleType, SourceSystem } from '../api/types'

interface Tier {
  threshold: number
  rate: number
}

interface Props {
  formId: string
  rule: Rule | null
  options: RuleOptions
  onSave: (input: RuleInput) => void
}

const today = new Date().toISOString().slice(0, 10)

function emptyInput(): RuleInput {
  return {
    name: '',
    ruleType: 'FixedPercentage',
    parametersJson: '{"percentage":5}',
    productCategory: null,
    productCode: null,
    sourceSystem: null,
    departmentId: null,
    priority: 100,
    validFrom: today,
    validTo: null,
    isActive: true,
  }
}

function toInput(rule: Rule): RuleInput {
  return {
    name: rule.name,
    ruleType: rule.ruleType,
    parametersJson: rule.parametersJson,
    productCategory: rule.productCategory,
    productCode: rule.productCode,
    sourceSystem: rule.sourceSystem,
    departmentId: rule.departmentId,
    priority: rule.priority,
    validFrom: rule.validFrom,
    validTo: rule.validTo,
    isActive: rule.isActive,
  }
}

function parseJson(json: string): Record<string, unknown> {
  try {
    const parsed = JSON.parse(json)
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

export function RuleForm({ formId, rule, options, onSave }: Props) {
  const [input, setInput] = useState<RuleInput>(() => (rule ? toInput(rule) : emptyInput()))
  const [percentage, setPercentage] = useState(5)
  const [amount, setAmount] = useState(50)
  const [mode, setMode] = useState<'Marginal' | 'Highest'>('Marginal')
  const [tiers, setTiers] = useState<Tier[]>([{ threshold: 0, rate: 2 }, { threshold: 30000, rate: 4 }])

  useEffect(() => {
    const next = rule ? toInput(rule) : emptyInput()
    setInput(next)
    const params = parseJson(next.parametersJson)
    if (typeof params.percentage === 'number') setPercentage(params.percentage)
    if (typeof params.amount === 'number') setAmount(params.amount)
    if (params.mode === 'Highest' || params.mode === 'Marginal') setMode(params.mode)
    if (Array.isArray(params.tiers) && params.tiers.length > 0) {
      setTiers(params.tiers.map((t: Tier) => ({ threshold: Number(t.threshold) || 0, rate: Number(t.rate) || 0 })))
    }
  }, [rule])

  function buildParameters(type: RuleType): string {
    switch (type) {
      case 'FixedPercentage':
        return JSON.stringify({ percentage })
      case 'FixedAmountPerTransaction':
        return JSON.stringify({ amount })
      case 'TieredRate':
        return JSON.stringify({ mode, tiers: [...tiers].sort((a, b) => a.threshold - b.threshold) })
    }
  }

  function set<K extends keyof RuleInput>(key: K, value: RuleInput[K]) {
    setInput((current) => ({ ...current, [key]: value }))
  }

  function submit(e: FormEvent) {
    e.preventDefault()
    onSave({ ...input, parametersJson: buildParameters(input.ruleType) })
  }

  function updateTier(index: number, patch: Partial<Tier>) {
    setTiers((current) => current.map((t, i) => (i === index ? { ...t, ...patch } : t)))
  }

  const preview = buildParameters(input.ruleType)

  return (
    <form id={formId} className="form" onSubmit={submit}>
      <div className="field">
        <label htmlFor="name">Kural adı</label>
        <input id="name" value={input.name} onChange={(e) => set('name', e.target.value)} required placeholder="örn. Golf dersi satışı %7" />
      </div>

      <div className="form-row">
        <div className="field">
          <label htmlFor="type">Kural tipi</label>
          <select id="type" value={input.ruleType} onChange={(e) => set('ruleType', e.target.value as RuleType)}>
            {options.types.map((t) => (
              <option key={t.type} value={t.type}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="priority">Öncelik</label>
          <input id="priority" type="number" value={input.priority} onChange={(e) => set('priority', Number(e.target.value))} />
          <span className="hint">Küçük değer önce uygulanır.</span>
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">Hesaplama parametreleri</div>

        {input.ruleType === 'FixedPercentage' && (
          <div className="field">
            <label htmlFor="percentage">Yüzde (%)</label>
            <input id="percentage" type="number" step="0.01" min="0.01" max="100" value={percentage} onChange={(e) => setPercentage(Number(e.target.value))} />
            <span className="hint">Her satış tutarının bu yüzdesi prim olarak hesaplanır; iadeler aynı oranla düşülür.</span>
          </div>
        )}

        {input.ruleType === 'FixedAmountPerTransaction' && (
          <div className="field">
            <label htmlFor="amount">İşlem başına tutar (TL)</label>
            <input id="amount" type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(Number(e.target.value))} />
            <span className="hint">Tutardan bağımsız olarak her işlem için sabit prim; iade işlemi aynı tutarı düşer.</span>
          </div>
        )}

        {input.ruleType === 'TieredRate' && (
          <>
            <div className="field">
              <label htmlFor="mode">Uygulama şekli</label>
              <select id="mode" value={mode} onChange={(e) => setMode(e.target.value as 'Marginal' | 'Highest')}>
                <option value="Marginal">Dilim dilim (her dilim kendi oranıyla)</option>
                <option value="Highest">Ulaşılan dilimin oranı tüm tutara</option>
              </select>
            </div>
            <div className="field">
              <label>Barem dilimleri</label>
              <div className="tiers">
                <div className="tier-row tier-head">
                  <span>Aylık net satış eşiği (TL)</span>
                  <span>Oran (%)</span>
                  <span />
                </div>
                {tiers.map((tier, i) => (
                  <div className="tier-row" key={i}>
                    <input type="number" min="0" step="1" value={tier.threshold} onChange={(e) => updateTier(i, { threshold: Number(e.target.value) })} aria-label="Eşik" />
                    <input type="number" min="0" max="100" step="0.01" value={tier.rate} onChange={(e) => updateTier(i, { rate: Number(e.target.value) })} aria-label="Oran" />
                    <button type="button" className="small ghost" disabled={tiers.length === 1} onClick={() => setTiers((c) => c.filter((_, j) => j !== i))}>
                      Kaldır
                    </button>
                  </div>
                ))}
              </div>
              <div>
                <button
                  type="button"
                  className="small"
                  onClick={() => setTiers((c) => [...c, { threshold: (c[c.length - 1]?.threshold ?? 0) + 10000, rate: (c[c.length - 1]?.rate ?? 0) + 1 }])}
                >
                  Dilim ekle
                </button>
              </div>
              <span className="hint">İlk dilim 0 eşiğinden başlamalıdır. Aylık net toplam (satış − iade) dilimlere göre hesaplanır.</span>
            </div>
          </>
        )}
      </div>

      <div className="form-section">
        <div className="form-section-title">Kapsam</div>
        <div className="form-row">
          <div className="field">
            <label htmlFor="category">Ürün kategorisi</label>
            <select id="category" value={input.productCategory ?? ''} onChange={(e) => set('productCategory', e.target.value || null)}>
              <option value="">Tümü</option>
              {options.categories.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="code">Ürün kodu</label>
            <input id="code" value={input.productCode ?? ''} onChange={(e) => set('productCode', e.target.value || null)} placeholder="Tümü (örn. GLF_LSN)" />
          </div>
        </div>
        <div className="form-row">
          <div className="field">
            <label htmlFor="source">Kaynak sistem</label>
            <select id="source" value={input.sourceSystem ?? ''} onChange={(e) => set('sourceSystem', (e.target.value || null) as SourceSystem | null)}>
              <option value="">Tümü</option>
              {options.sources.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="department">Departman</label>
            <select id="department" value={input.departmentId ?? ''} onChange={(e) => set('departmentId', e.target.value ? Number(e.target.value) : null)}>
              <option value="">Tümü</option>
              {options.departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <div className="form-section">
        <div className="form-section-title">Geçerlilik</div>
        <div className="form-row">
          <div className="field">
            <label htmlFor="validFrom">Başlangıç</label>
            <input id="validFrom" type="date" value={input.validFrom} onChange={(e) => set('validFrom', e.target.value)} required />
          </div>
          <div className="field">
            <label htmlFor="validTo">Bitiş</label>
            <input id="validTo" type="date" value={input.validTo ?? ''} onChange={(e) => set('validTo', e.target.value || null)} />
          </div>
        </div>
        <label className="checkbox">
          <input type="checkbox" checked={input.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          Kural aktif
        </label>
      </div>

      <details>
        <summary>Veritabanına yazılacak parametre JSON'u</summary>
        <pre className="json">{preview}</pre>
      </details>
    </form>
  )
}
