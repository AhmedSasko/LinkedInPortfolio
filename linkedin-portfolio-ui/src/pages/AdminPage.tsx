import { useQuery } from '@tanstack/react-query'
import { fetchStatus, fetchAllProfiles } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'

export function AdminPage() {
  const { data: status, isLoading: statusLoading } = useQuery({
    queryKey: ['status'],
    queryFn: fetchStatus,
  })

  const { data: profiles, isLoading: profilesLoading } = useQuery({
    queryKey: ['profiles'],
    queryFn: fetchAllProfiles,
  })

  return (
    <main className="max-w-lg mx-auto px-4 py-12">
      <div className="bg-white rounded-2xl shadow p-8 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">LinkedIn Sync</h1>
          <Link to="/" className="text-blue-600 text-sm hover:underline">← Portfolio</Link>
        </div>

        {statusLoading ? (
          <LoadingSpinner message="Loading status..." />
        ) : (
          <div className="text-sm text-gray-600 space-y-1">
            <p><span className="font-medium">Last synced:</span> {status?.lastSyncedAt ? new Date(status.lastSyncedAt).toLocaleString() : 'Never'}</p>
            <p><span className="font-medium">Experience entries:</span> {status?.experienceCount ?? 0}</p>
            <p><span className="font-medium">Skills:</span> {status?.skillCount ?? 0}</p>
            <p><span className="font-medium">Projects:</span> {status?.projectCount ?? 0}</p>
            <p><span className="font-medium">Certifications:</span> {status?.certificationCount ?? 0}</p>
          </div>
        )}

      </div>

      {/* Synced Profiles List */}
      <div className="bg-white rounded-2xl shadow p-8 space-y-4">
        <h2 className="text-lg font-semibold text-gray-900">Synced Profiles</h2>
        {profilesLoading ? (
          <LoadingSpinner message="Loading profiles..." />
        ) : !profiles || profiles.length === 0 ? (
          <p className="text-sm text-gray-400">No profiles synced yet.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {profiles.map(p => (
              <div key={p.id} className="py-3 flex items-center justify-between gap-4">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900 truncate">{p.name || '(no name)'}</p>
                  <p className="text-xs text-gray-500 truncate">{p.headline || '—'}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {new Date(p.fetchedAt).toLocaleString()} &nbsp;·&nbsp;
                    {p.experienceCount} exp &nbsp;·&nbsp; {p.skillCount} skills &nbsp;·&nbsp; {p.educationCount} edu
                  </p>
                </div>
                <Link
                  to={`/profile/${p.id}`}
                  className="flex-shrink-0 px-4 py-1.5 text-sm bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                >
                  View
                </Link>
              </div>
            ))}
          </div>
        )}
      </div>
    </main>
  )
}
