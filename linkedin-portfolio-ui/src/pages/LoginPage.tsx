import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { login } from '../api/authApi'
import { useAuth } from '../hooks/useAuth'

export function LoginPage() {
  const navigate = useNavigate()
  const { login: storeToken } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(() => {
    return new URLSearchParams(window.location.search).get('error') ?? ''
  })
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const { token } = await login({ email, password })
      storeToken(token)
      navigate('/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="bg-white rounded-2xl shadow p-8 w-full max-w-sm space-y-6">
        <h1 className="text-2xl font-bold text-gray-900">Sign in</h1>
        {error && <p className="text-sm text-red-600 bg-red-50 p-3 rounded-lg">{error}</p>}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input
              id="email" type="email" value={email} onChange={e => setEmail(e.target.value)}
              required autoComplete="email"
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label htmlFor="password" className="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <input
              id="password" type="password" value={password} onChange={e => setPassword(e.target.value)}
              required autoComplete="current-password"
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <button
            type="submit" disabled={loading}
            className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
          >
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
        <div className="relative flex items-center">
          <div className="flex-grow border-t border-gray-200" />
          <span className="mx-3 text-xs text-gray-400">or</span>
          <div className="flex-grow border-t border-gray-200" />
        </div>

        <a
          href="/api/auth/linkedin"
          className="flex items-center justify-center gap-2 w-full py-2.5 border border-gray-300 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
        >
          <svg className="w-5 h-5" viewBox="0 0 24 24" fill="#0A66C2">
            <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 0 1-2.063-2.065 2.064 2.064 0 1 1 2.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/>
          </svg>
          Sign in with LinkedIn
        </a>

        <p className="text-sm text-center text-gray-500">
          Don't have an account?{' '}
          <Link to="/register" className="text-blue-600 hover:underline">Register</Link>
        </p>
      </div>
    </main>
  )
}
