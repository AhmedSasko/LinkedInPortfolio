import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function AuthCallbackPage() {
  const navigate = useNavigate()
  const { login } = useAuth()

  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    const token = params.get('token')
    const error = params.get('error')

    if (error) {
      navigate(`/login?error=${encodeURIComponent(error)}`)
      return
    }

    if (token) {
      login(token)
      navigate('/')
    } else {
      navigate('/login')
    }
  }, [])

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center">
      <p className="text-gray-500 text-sm">Signing you in…</p>
    </div>
  )
}
