import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { fetchStatus, triggerSync } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'

export function AdminPage() {
  const queryClient = useQueryClient()
  const [syncMessage, setSyncMessage] = useState<{ text: string; success: boolean } | null>(null)

  const { data: status, isLoading: statusLoading } = useQuery({
    queryKey: ['status'],
    queryFn: fetchStatus,
  })

  const syncMutation = useMutation({
    mutationFn: triggerSync,
    onSuccess: (result) => {
      setSyncMessage({ text: result.message, success: result.success })
      if (result.success) {
        queryClient.invalidateQueries({ queryKey: ['profile'] })
        queryClient.invalidateQueries({ queryKey: ['status'] })
      }
    },
    onError: () => {
      setSyncMessage({ text: 'Unexpected error during sync.', success: false })
    },
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

        <button
          onClick={() => { setSyncMessage(null); syncMutation.mutate() }}
          disabled={syncMutation.isPending}
          className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
        >
          {syncMutation.isPending ? 'Syncing... (this takes 30–60s)' : 'Sync from LinkedIn'}
        </button>

        {syncMutation.isPending && <LoadingSpinner message="Scraping LinkedIn profile..." />}

        {syncMessage && (
          <div className={`p-4 rounded-xl text-sm ${syncMessage.success ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-800'}`}>
            {syncMessage.text}
          </div>
        )}
      </div>
    </main>
  )
}
