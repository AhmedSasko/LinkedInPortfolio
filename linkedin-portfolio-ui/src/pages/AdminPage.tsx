import { useQuery } from '@tanstack/react-query'
import { fetchAllProfiles } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function AdminPage() {
  const { logout } = useAuth()
  const navigate = useNavigate()

  const { data: profiles, isLoading, isError } = useQuery({
    queryKey: ['admin-profiles'],
    queryFn: fetchAllProfiles,
  })

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <main className="max-w-2xl mx-auto px-4 py-12">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Admin — All Profiles</h1>
        <div className="flex gap-3">
          <Link to="/" className="text-sm text-blue-600 hover:underline">← Portfolio</Link>
          <button onClick={handleLogout} className="text-sm text-gray-500 hover:text-gray-700">Sign out</button>
        </div>
      </div>

      {isLoading && <LoadingSpinner message="Loading profiles..." />}
      {isError && <p className="text-red-600 text-sm">Failed to load profiles.</p>}

      {profiles && profiles.length === 0 && (
        <p className="text-gray-500 text-sm">No profiles yet.</p>
      )}

      {profiles && profiles.length > 0 && (
        <div className="space-y-4">
          {profiles.map(p => (
            <div key={p.id} className="bg-white rounded-xl shadow p-5 flex items-center justify-between gap-4">
              <div className="min-w-0">
                <p className="font-semibold text-gray-900">{p.name || '(no name)'}</p>
                <p className="text-sm text-gray-500">{p.headline || '—'}</p>
                <p className="text-xs text-gray-400 mt-1">
                  {new Date(p.fetchedAt).toLocaleString()} &nbsp;·&nbsp;
                  {p.experienceCount} exp &nbsp;·&nbsp; {p.skillCount} skills &nbsp;·&nbsp; {p.educationCount} edu
                </p>
              </div>
              <Link
                to={`/profile/${p.id}`}
                className="flex-shrink-0 px-4 py-1.5 text-sm bg-blue-600 hover:bg-blue-700 text-white rounded-lg"
              >
                View
              </Link>
            </div>
          ))}
        </div>
      )}
    </main>
  )
}
