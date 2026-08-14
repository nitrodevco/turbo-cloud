import { Navigate } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Layout } from './Layout'

export function RequireAuth() {
  const { token } = useAuth()

  if (!token) return <Navigate to="/login" replace />

  return <Layout />
}
