import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

interface LoginResponse {
  token: string
  username: string
  role: string
}

interface AuthState {
  token: string | null
  username: string | null
  role: string | null
}

interface AuthContextValue extends AuthState {
  login: (username: string, password: string) => Promise<void>
  logout: () => void
}

const STORAGE_KEY = 'turbo-admin-auth'

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredAuth(): AuthState {
  const raw = localStorage.getItem(STORAGE_KEY)

  if (!raw) return { token: null, username: null, role: null }

  try {
    return JSON.parse(raw) as AuthState
  } catch {
    return { token: null, username: null, role: null }
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(readStoredAuth)

  const login = useCallback(async (username: string, password: string) => {
    const res = await fetch('/api/admin/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    })

    if (!res.ok) {
      throw new Error('Invalid username or password.')
    }

    const data = (await res.json()) as LoginResponse
    const next: AuthState = {
      token: data.token,
      username: data.username,
      role: data.role,
    }

    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setState(next)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setState({ token: null, username: null, role: null })
  }, [])

  const value = useMemo(
    () => ({ ...state, login, logout }),
    [state, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)

  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')

  return ctx
}

export function getStoredToken(): string | null {
  return readStoredAuth().token
}
