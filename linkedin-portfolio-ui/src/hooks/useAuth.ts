import { useState, useCallback } from 'react'
import { jwtDecode } from 'jwt-decode'
import type { AuthUser } from '../types/profile'

interface JwtPayload {
  sub: string
  email: string
  isAdmin: string // "true" or "false"
}

function decodeToken(token: string): AuthUser | null {
  try {
    const payload = jwtDecode<JwtPayload>(token)
    return {
      userId: parseInt(payload.sub, 10),
      email: payload.email,
      isAdmin: payload.isAdmin === 'true',
    }
  } catch {
    return null
  }
}

function isExpired(token: string): boolean {
  try {
    const { exp } = jwtDecode<{ exp?: number }>(token)
    if (!exp) return false
    return Date.now() >= exp * 1000
  } catch {
    return true
  }
}

export function useAuth() {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = localStorage.getItem('token')
    if (!token || isExpired(token)) {
      localStorage.removeItem('token')
      return null
    }
    return decodeToken(token)
  })

  const login = useCallback((token: string) => {
    localStorage.setItem('token', token)
    setUser(decodeToken(token))
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('token')
    setUser(null)
  }, [])

  return { user, isAdmin: user?.isAdmin ?? false, login, logout }
}
