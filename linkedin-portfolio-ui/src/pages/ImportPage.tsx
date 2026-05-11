import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { importProfile } from '../api/profileApi'

export function ImportPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [url, setUrl] = useState('')

  const mutation = useMutation({
    mutationFn: () => importProfile({ linkedInUrl: url }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profile'] })
      navigate('/')
    },
  })

  return (
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
      <div className="bg-white rounded-2xl shadow p-8 w-full max-w-md space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Import LinkedIn Profile</h1>
          <p className="text-sm text-gray-500 mt-1">
            Paste your public LinkedIn profile URL to import your data.
          </p>
        </div>

        {mutation.isError && (
          <p className="text-sm text-red-600 bg-red-50 p-3 rounded-lg">
            {axios.isAxiosError(mutation.error) && mutation.error.response?.data?.message
              ? mutation.error.response.data.message
              : mutation.error instanceof Error ? mutation.error.message : 'Import failed'}
          </p>
        )}

        <form
          onSubmit={e => { e.preventDefault(); mutation.mutate() }}
          className="space-y-4"
        >
          <div>
            <label htmlFor="linkedin-url" className="block text-sm font-medium text-gray-700 mb-1">
              LinkedIn Profile URL
            </label>
            <input
              id="linkedin-url"
              type="url"
              value={url}
              onChange={e => setUrl(e.target.value)}
              required
              placeholder="https://www.linkedin.com/in/yourname"
              className="w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <button
            type="submit"
            disabled={mutation.isPending}
            className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
          >
            {mutation.isPending ? 'Importing…' : 'Import Profile'}
          </button>
        </form>

        {mutation.isPending && (
          <p className="text-sm text-center text-gray-500">
            Scraping your profile — this takes 20–40 seconds…
          </p>
        )}
      </div>
    </main>
  )
}
