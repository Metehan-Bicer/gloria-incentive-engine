import type { ReactNode } from 'react'

export function Alert({ kind = 'info', children }: { kind?: 'info' | 'error' | 'success'; children: ReactNode }) {
  return <div className={`alert alert-${kind}`}>{children}</div>
}
