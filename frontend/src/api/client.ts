import { loadSession } from '../auth/session'

export class ApiError extends Error {
  status: number
  title: string

  constructor(status: number, title: string, detail?: string) {
    super(detail || title)
    this.status = status
    this.title = title
  }
}

function headers(extra?: HeadersInit): Headers {
  const session = loadSession()
  const result = new Headers(extra)
  result.set('X-Role', session.role)
  if (session.employeeNo) result.set('X-Employee-No', session.employeeNo)
  return result
}

async function handle<T>(response: Response): Promise<T> {
  if (response.ok) {
    if (response.status === 204) return undefined as T
    return (await response.json()) as T
  }

  let title = `HTTP ${response.status}`
  let detail: string | undefined
  try {
    const body = await response.json()
    title = body.title ?? title
    detail = body.detail ?? (body.errors ? JSON.stringify(body.errors) : undefined)
  } catch {
    // no json body
  }

  if (response.status === 401) title = 'Oturum bilgisi eksik'
  if (response.status === 403 && !detail) detail = 'Bu işlem için yetkiniz yok.'

  throw new ApiError(response.status, title, detail)
}

export const api = {
  get<T>(url: string): Promise<T> {
    return fetch(url, { headers: headers() }).then((r) => handle<T>(r))
  },

  post<T>(url: string, body?: unknown): Promise<T> {
    return fetch(url, {
      method: 'POST',
      headers: headers(body === undefined ? undefined : { 'Content-Type': 'application/json' }),
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then((r) => handle<T>(r))
  },

  put<T>(url: string, body: unknown): Promise<T> {
    return fetch(url, {
      method: 'PUT',
      headers: headers({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(body),
    }).then((r) => handle<T>(r))
  },

  delete<T>(url: string): Promise<T> {
    return fetch(url, { method: 'DELETE', headers: headers() }).then((r) => handle<T>(r))
  },

  upload<T>(url: string, file: File): Promise<T> {
    const form = new FormData()
    form.append('file', file)
    return fetch(url, { method: 'POST', headers: headers(), body: form }).then((r) => handle<T>(r))
  },
}
