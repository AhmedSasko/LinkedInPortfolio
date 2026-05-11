import { useState, useEffect, useCallback } from 'react'
import { jwtDecode } from 'jwt-decode'
import type { AuthUser } from '../types/profile'

interface JwtPayload {
  sub: string
  email: string
  isAdmin: string // "true" or "false"
}

// Module-level store so all useAuth() instances share one source of truth.
// Avoids React Context boilerplate while still notifying all subscribers on change.
type Listener = (user: AuthUser | null) => void
let _user: AuthUser | null = null
const _listeners = new Set<Listener>()

function _setUser(user: AuthUser | null) {
  _user = user
  _listeners.forEach(fn => fn(user))
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

// Bootstrap module-level state from localStorage once on first import.
;(() => {
  const token = localStorage.getItem('token')
  if (!token || isExpired(token)) {
    localStorage.removeItem('token')
    _user = null
  } else {
    _user = decodeToken(token)
  }
})()

export function useAuth() {
  const [user, setUser] = useState<AuthUser | null>(_user)

  useEffect(() => {
    // Subscribe to module-level updates so all instances stay in sync.
    _listeners.add(setUser)
    // Also sync with any change that happened between render and effect.
    setUser(_user)
    return () => { _listeners.delete(setUser) }
  }, [])

  const login = useCallback((token: string) => {
    if (isExpired(token)) return
    localStorage.setItem('token', token)
    _setUser(decodeToken(token))
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('token')
    _setUser(null)
  }, [])

  return { user, isAdmin: user?.isAdmin ?? false, login, logout }
}
