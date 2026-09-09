import { useEffect, type ReactNode } from 'react'
import { Icons } from './Icons'

interface Props {
  open: boolean
  title: string
  subtitle?: string
  onClose: () => void
  footer?: ReactNode
  wide?: boolean
  children: ReactNode
}

export function Drawer({ open, title, subtitle, onClose, footer, wide, children }: Props) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    document.body.classList.add('drawer-open')
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.classList.remove('drawer-open')
    }
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="drawer-root" role="dialog" aria-modal="true" aria-label={title}>
      <div className="drawer-overlay" onClick={onClose} />
      <aside className={`drawer ${wide ? 'drawer-wide' : ''}`}>
        <header className="drawer-header">
          <div>
            <h2>{title}</h2>
            {subtitle && <p>{subtitle}</p>}
          </div>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Kapat">
            <Icons.close />
          </button>
        </header>
        <div className="drawer-body">{children}</div>
        {footer && <footer className="drawer-footer">{footer}</footer>}
      </aside>
    </div>
  )
}
