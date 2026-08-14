import { getStoredToken } from '../lib/auth'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const token = getStoredToken()

  const headers = new Headers(options.headers)
  headers.set('Content-Type', 'application/json')

  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(path, { ...options, headers })

  if (res.status === 401) {
    localStorage.removeItem('turbo-admin-auth')
    window.location.href = '/login'
    throw new ApiError(401, 'Session expired.')
  }

  if (!res.ok) {
    let message = `Request failed (${res.status})`

    try {
      const body = (await res.json()) as { error?: string }
      if (body.error) message = body.error
    } catch {
      // no JSON body
    }

    throw new ApiError(res.status, message)
  }

  if (res.status === 204) return undefined as T

  return (await res.json()) as T
}
