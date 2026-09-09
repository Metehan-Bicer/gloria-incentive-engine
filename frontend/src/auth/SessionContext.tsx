import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'
import { loadSession, saveSession, type Session } from './session'

interface SessionContextValue {
  session: Session
  update: (next: Partial<Session>) => void
}

const SessionContext = createContext<SessionContextValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>(loadSession)

  const value = useMemo<SessionContextValue>(
    () => ({
      session,
      update: (next) => {
        setSession((current) => {
          const merged = { ...current, ...next }
          saveSession(merged)
          return merged
        })
      },
    }),
    [session],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession() {
  const ctx = useContext(SessionContext)
  if (!ctx) throw new Error('useSession must be used inside SessionProvider')
  return ctx
}
