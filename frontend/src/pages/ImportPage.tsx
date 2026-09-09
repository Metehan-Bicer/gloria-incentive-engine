import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { ImportBatch, ImportError, ImportResult, SourceSystem } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { formatDateTime } from '../format'

const SOURCES: { value: SourceSystem; label: string; hint: string }[] = [
  { value: 'PMS', label: 'PMS – Fidelio', hint: 'BelgeNo;IslemTarihi;OdaNo;MisafirAdi;UrunKodu;UrunAdi;Adet;Tutar;ParaBirimi;KasiyerNo;IslemTipi;Otel' },
  { value: 'POS', label: 'POS – Flyby', hint: 'FisNo;Tarih;Saat;OutletKodu;PLU;UrunAdi;Adet;BirimFiyat;ToplamTutar;SatisPersoneli;IadeMi;OdemeTipi;OdaNo' },
  { value: 'ERP', label: 'ERP – Oracle JDE', hint: 'DocNumber;DocType;GLDate;BusinessUnit;ObjectAccount;Amount;Currency;Reference;EmployeeNo;Description;Status' },
]

export function ImportPage() {
  const { session } = useSession()
  const [source, setSource] = useState<SourceSystem>('PMS')
  const [file, setFile] = useState<File | null>(null)
  const [batches, setBatches] = useState<ImportBatch[]>([])
  const [selectedBatch, setSelectedBatch] = useState<ImportBatch | null>(null)
  const [errors, setErrors] = useState<ImportError[]>([])
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const loadBatches = useCallback(async () => {
    try {
      setError(null)
      setBatches(await api.get<ImportBatch[]>('/api/import/batches'))
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Aktarım geçmişi yüklenemedi')
    }
  }, [])

  useEffect(() => {
    loadBatches()
  }, [loadBatches, session.role])

  useEffect(() => {
    if (!selectedBatch) {
      setErrors([])
      return
    }
    api
      .get<ImportError[]>(`/api/import/batches/${selectedBatch.id}/errors`)
      .then(setErrors)
      .catch(() => setErrors([]))
  }, [selectedBatch])

  async function upload(e: FormEvent) {
    e.preventDefault()
    if (!file) return
    setBusy(true)
    setError(null)
    setResult(null)
    try {
      const res = await api.upload<ImportResult>(`/api/import/${source.toLowerCase()}`, file)
      setResult(res)
      await loadBatches()
      setSelectedBatch(res.batch)
      setFile(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Yükleme başarısız')
    } finally {
      setBusy(false)
    }
  }

  const sourceInfo = SOURCES.find((s) => s.value === source)!

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Veri Aktarımı</h1>
          <p>Kaynak sistem raporlarını (CSV, noktalı virgül ayraçlı) ana satış tablosuna aktarın. Mükerrer ve hatalı satırlar ayrı loglanır.</p>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <div className="grid grid-2">
        <div>
          <div className="card">
            <div className="card-header">
              <h2>Dosya yükle</h2>
            </div>
            <form className="form" onSubmit={upload}>
              <div className="form-row">
                <div className="field">
                  <label htmlFor="source">Kaynak sistem</label>
                  <select id="source" value={source} onChange={(e) => setSource(e.target.value as SourceSystem)}>
                    {SOURCES.map((s) => (
                      <option key={s.value} value={s.value}>
                        {s.label}
                      </option>
                    ))}
                  </select>
                  <span className="hint mono">{sourceInfo.hint}</span>
                </div>
                <div className="field">
                  <label htmlFor="file">CSV dosyası</label>
                  <input id="file" type="file" accept=".csv,text/csv" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                </div>
              </div>
              <div className="toolbar">
                <button className="primary" type="submit" disabled={!file || busy}>
                  {busy ? 'Aktarılıyor…' : 'Aktar'}
                </button>
              </div>
            </form>

            {result && (
              <Alert kind={result.batch.errorRows > 0 ? 'info' : 'success'}>
                <strong>{result.batch.fileName}</strong>: {result.batch.totalRows} satır okundu, {result.batch.importedRows} aktarıldı, {result.batch.duplicateRows} mükerrer atlandı,{' '}
                {result.batch.errorRows} hatalı satır loglandı.
              </Alert>
            )}
          </div>

          <div className="card">
            <div className="card-header">
              <h2>Aktarım geçmişi</h2>
              <button className="small" onClick={loadBatches}>
                Yenile
              </button>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Kaynak</th>
                    <th>Dosya</th>
                    <th>Zaman</th>
                    <th className="num">Okunan</th>
                    <th className="num">Aktarılan</th>
                    <th className="num">Mükerrer</th>
                    <th className="num">Hatalı</th>
                  </tr>
                </thead>
                <tbody>
                  {batches.map((b) => (
                    <tr key={b.id} className={`clickable ${selectedBatch?.id === b.id ? 'selected' : ''}`} onClick={() => setSelectedBatch(b)}>
                      <td className="num">{b.id}</td>
                      <td>
                        <span className="badge badge-info">{b.sourceSystem}</span>
                      </td>
                      <td>
                        {b.fileName}
                        <div className="muted" style={{ fontSize: '0.78rem' }}>
                          {b.importedBy}
                        </div>
                      </td>
                      <td>{formatDateTime(b.startedAt)}</td>
                      <td className="num">{b.totalRows}</td>
                      <td className="num">{b.importedRows}</td>
                      <td className="num">{b.duplicateRows}</td>
                      <td className="num">{b.errorRows > 0 ? <span className="badge badge-warn">{b.errorRows}</span> : 0}</td>
                    </tr>
                  ))}
                  {batches.length === 0 && (
                    <tr>
                      <td colSpan={8} className="empty">
                        Henüz aktarım yapılmamış.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div>
          <div className="card">
            <div className="card-header">
              <h2>{selectedBatch ? `Aktarım #${selectedBatch.id} · reddedilen satırlar` : 'Reddedilen satırlar'}</h2>
              <span className="muted">{errors.length} kayıt</span>
            </div>
            {!selectedBatch && <div className="empty">Detay için listeden bir aktarım seçin.</div>}
            {selectedBatch && errors.length === 0 && <div className="empty">Bu aktarımda reddedilen satır yok.</div>}
            {errors.map((e) => (
              <div key={e.id} style={{ marginBottom: '0.8rem' }}>
                <div>
                  <span className={`badge ${e.isDuplicate ? 'badge-muted' : 'badge-closed'}`}>{e.isDuplicate ? 'Mükerrer' : 'Hata'}</span>{' '}
                  <span className="muted">satır {e.lineNumber}</span> · {e.reason}
                </div>
                <pre className="json">{e.rawLine}</pre>
              </div>
            ))}
          </div>
        </div>
      </div>
    </>
  )
}
