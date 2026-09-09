import { useCallback, useEffect, useState, type DragEvent, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { ImportBatch, ImportError, ImportResult, SourceSystem } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { Alert } from '../components/Alert'
import { Drawer } from '../components/Drawer'
import { Icons } from '../components/Icons'
import { formatDateTime } from '../format'

const SOURCES: { value: SourceSystem; label: string; hint: string }[] = [
  { value: 'PMS', label: 'PMS – Fidelio', hint: 'BelgeNo;IslemTarihi;OdaNo;MisafirAdi;UrunKodu;UrunAdi;Adet;Tutar;ParaBirimi;KasiyerNo;IslemTipi;Otel' },
  { value: 'POS', label: 'POS – Flyby', hint: 'FisNo;Tarih;Saat;OutletKodu;PLU;UrunAdi;Adet;BirimFiyat;ToplamTutar;SatisPersoneli;IadeMi;OdemeTipi;OdaNo' },
  { value: 'ERP', label: 'ERP – Oracle JDE', hint: 'DocNumber;DocType;GLDate;BusinessUnit;ObjectAccount;Amount;Currency;Reference;EmployeeNo;Description;Status' },
]

type DrawerState = { mode: 'closed' } | { mode: 'upload' } | { mode: 'batch'; batch: ImportBatch }

export function ImportPage() {
  const { session } = useSession()
  const [source, setSource] = useState<SourceSystem>('PMS')
  const [file, setFile] = useState<File | null>(null)
  const [dragging, setDragging] = useState(false)
  const [batches, setBatches] = useState<ImportBatch[]>([])
  const [drawer, setDrawer] = useState<DrawerState>({ mode: 'closed' })
  const [errors, setErrors] = useState<ImportError[]>([])
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [drawerError, setDrawerError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const selectedBatch = drawer.mode === 'batch' ? drawer.batch : null

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

  const closeDrawer = useCallback(() => {
    setDrawer({ mode: 'closed' })
    setDrawerError(null)
    setDragging(false)
  }, [])

  async function upload(e: FormEvent) {
    e.preventDefault()
    if (!file) return
    setBusy(true)
    setDrawerError(null)
    setResult(null)
    try {
      const res = await api.upload<ImportResult>(`/api/import/${source.toLowerCase()}`, file)
      setResult(res)
      setFile(null)
      await loadBatches()
      setDrawer({ mode: 'batch', batch: res.batch })
    } catch (err) {
      setDrawerError(err instanceof ApiError ? err.message : 'Yükleme başarısız')
    } finally {
      setBusy(false)
    }
  }

  function onDrop(e: DragEvent<HTMLLabelElement>) {
    e.preventDefault()
    setDragging(false)
    const dropped = e.dataTransfer.files?.[0]
    if (dropped) setFile(dropped)
  }

  const sourceInfo = SOURCES.find((s) => s.value === source)!
  const totalImported = batches.reduce((sum, b) => sum + b.importedRows, 0)
  const totalRejected = batches.reduce((sum, b) => sum + b.errorRows, 0)
  const totalDuplicates = batches.reduce((sum, b) => sum + b.duplicateRows, 0)

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Veri Aktarımı</h1>
          <p>Kaynak sistem raporlarını (CSV, noktalı virgül ayraçlı) ana satış tablosuna aktarın. Mükerrer ve hatalı satırlar ayrı loglanır.</p>
        </div>
        <div className="toolbar">
          <button onClick={loadBatches}>
            <Icons.refresh />
            Yenile
          </button>
          <button className="primary" onClick={() => setDrawer({ mode: 'upload' })}>
            <Icons.upload />
            Dosya yükle
          </button>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {result && (
        <Alert kind={result.batch.errorRows > 0 ? 'warning' : 'success'}>
          <strong>{result.batch.fileName}</strong>: {result.batch.totalRows} satır okundu, {result.batch.importedRows} aktarıldı, {result.batch.duplicateRows} mükerrer atlandı,{' '}
          {result.batch.errorRows} hatalı satır loglandı.
        </Alert>
      )}

      <div className="grid grid-stats">
        <div className="stat">
          <div className="stat-label">Aktarım</div>
          <div className="stat-value">{batches.length}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Aktarılan satır</div>
          <div className="stat-value primary">{totalImported}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Mükerrer</div>
          <div className="stat-value">{totalDuplicates}</div>
        </div>
        <div className="stat">
          <div className="stat-label">Reddedilen</div>
          <div className={`stat-value ${totalRejected > 0 ? 'danger' : ''}`}>{totalRejected}</div>
        </div>
      </div>

      <div className="card card-flush">
        <div className="card-header">
          <h2>Aktarım geçmişi</h2>
          <span className="muted">Detay için satıra tıklayın</span>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th className="num">#</th>
                <th>Kaynak</th>
                <th>Dosya</th>
                <th>Zaman</th>
                <th className="num">Okunan</th>
                <th className="num">Aktarılan</th>
                <th className="num">Mükerrer</th>
                <th className="num">Hatalı</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {batches.map((b) => (
                <tr key={b.id} className={`clickable ${selectedBatch?.id === b.id ? 'selected' : ''}`} onClick={() => setDrawer({ mode: 'batch', batch: b })}>
                  <td className="num">{b.id}</td>
                  <td>
                    <span className="badge badge-info">{b.sourceSystem}</span>
                  </td>
                  <td>
                    {b.fileName}
                    <span className="sub">{b.importedBy}</span>
                  </td>
                  <td className="nowrap">{formatDateTime(b.startedAt)}</td>
                  <td className="num">{b.totalRows}</td>
                  <td className="num">{b.importedRows}</td>
                  <td className="num">{b.duplicateRows}</td>
                  <td className="num">{b.errorRows > 0 ? <span className="badge badge-warn">{b.errorRows}</span> : 0}</td>
                  <td className="num">
                    <Icons.chevronRight style={{ color: 'var(--text-muted)' }} />
                  </td>
                </tr>
              ))}
              {batches.length === 0 && (
                <tr>
                  <td colSpan={9} className="empty">
                    Henüz aktarım yapılmamış.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <Drawer
        open={drawer.mode === 'upload'}
        title="CSV dosyası yükle"
        subtitle="Aynı belge ikinci kez yüklenirse mükerrer olarak atlanır."
        onClose={closeDrawer}
        footer={
          <>
            <button type="button" onClick={closeDrawer} disabled={busy}>
              Vazgeç
            </button>
            <button type="submit" form="upload-form" className="primary" disabled={!file || busy}>
              {busy ? 'Aktarılıyor…' : 'Aktar'}
            </button>
          </>
        }
      >
        {drawerError && <Alert kind="error">{drawerError}</Alert>}
        <form id="upload-form" className="form" onSubmit={upload}>
          <div className="field">
            <label htmlFor="source">Kaynak sistem</label>
            <select id="source" value={source} onChange={(e) => setSource(e.target.value as SourceSystem)}>
              {SOURCES.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
            <span className="hint">Beklenen sütunlar: {sourceInfo.hint}</span>
          </div>
          <div className="field">
            <label>CSV dosyası</label>
            <label
              className={`dropzone ${dragging ? 'active' : ''}`}
              onDragOver={(e) => {
                e.preventDefault()
                setDragging(true)
              }}
              onDragLeave={() => setDragging(false)}
              onDrop={onDrop}
            >
              <input type="file" accept=".csv,text/csv" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
              {file ? (
                <>
                  <strong>{file.name}</strong>
                  {(file.size / 1024).toFixed(1)} KB · değiştirmek için tıklayın
                </>
              ) : (
                <>
                  <strong>Dosyayı buraya bırakın</strong>
                  ya da seçmek için tıklayın
                </>
              )}
            </label>
          </div>
        </form>
      </Drawer>

      <Drawer
        open={drawer.mode === 'batch'}
        title={selectedBatch ? `Aktarım #${selectedBatch.id}` : ''}
        subtitle={selectedBatch ? `${selectedBatch.sourceSystem} · ${selectedBatch.fileName} · ${formatDateTime(selectedBatch.startedAt)}` : undefined}
        onClose={closeDrawer}
        footer={
          <button type="button" onClick={closeDrawer}>
            Kapat
          </button>
        }
      >
        {selectedBatch && (
          <>
            <div className="grid grid-stats">
              <div className="stat">
                <div className="stat-label">Okunan</div>
                <div className="stat-value">{selectedBatch.totalRows}</div>
              </div>
              <div className="stat">
                <div className="stat-label">Aktarılan</div>
                <div className="stat-value primary">{selectedBatch.importedRows}</div>
              </div>
              <div className="stat">
                <div className="stat-label">Mükerrer</div>
                <div className="stat-value">{selectedBatch.duplicateRows}</div>
              </div>
              <div className="stat">
                <div className="stat-label">Hatalı</div>
                <div className={`stat-value ${selectedBatch.errorRows > 0 ? 'danger' : ''}`}>{selectedBatch.errorRows}</div>
              </div>
            </div>

            <div className="form-section">
              <div className="form-section-title">Reddedilen satırlar · {errors.length} kayıt</div>
              {errors.length === 0 && <div className="muted">Bu aktarımda reddedilen satır yok.</div>}
              <div className="timeline">
                {errors.map((e) => (
                  <div key={e.id} className="timeline-item">
                    <div className="inline-list">
                      <span className={`badge ${e.isDuplicate ? 'badge-muted' : 'badge-closed'}`}>{e.isDuplicate ? 'Mükerrer' : 'Hata'}</span>
                      <span className="when">satır {e.lineNumber}</span>
                    </div>
                    <div style={{ marginTop: '0.25rem' }}>{e.reason}</div>
                    <pre className="json">{e.rawLine}</pre>
                  </div>
                ))}
              </div>
            </div>
          </>
        )}
      </Drawer>
    </>
  )
}
