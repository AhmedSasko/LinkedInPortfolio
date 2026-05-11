import { useQuery } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { fetchProfileById } from '../api/profileApi'
import { LoadingSpinner } from '../components/LoadingSpinner'

export function ProfileDetailPage() {
  const { id } = useParams<{ id: string }>()
  const profileId = parseInt(id ?? '0')

  const { data: profile, isLoading, isError } = useQuery({
    queryKey: ['profile', profileId],
    queryFn: () => fetchProfileById(profileId),
    enabled: profileId > 0,
  })

  if (isLoading) return <div className="max-w-3xl mx-auto px-4 py-12"><LoadingSpinner message="Loading profile..." /></div>
  if (isError || !profile) return (
    <div className="max-w-3xl mx-auto px-4 py-12 text-center text-red-600">
      Profile not found. <Link to="/admin" className="text-blue-600 underline">Back to Admin</Link>
    </div>
  )

  return (
    <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
      <div className="flex items-center justify-between">
        <Link to="/admin" className="text-blue-600 text-sm hover:underline">← Back to Admin</Link>
        <span className="text-xs text-gray-400">Synced {new Date(profile.fetchedAt).toLocaleString()}</span>
      </div>

      {/* Header */}
      <div className="bg-white rounded-2xl shadow p-6 flex gap-4 items-start">
        {profile.photoBase64 && (
          <img
            src={profile.photoBase64.startsWith('data:') ? profile.photoBase64 : `data:image/jpeg;base64,${profile.photoBase64}`}
            alt={profile.name}
            className="w-20 h-20 rounded-full object-cover flex-shrink-0"
          />
        )}
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{profile.name || '—'}</h1>
          {profile.headline && <p className="text-gray-600 mt-1">{profile.headline}</p>}
          {profile.location && <p className="text-sm text-gray-400 mt-1">📍 {profile.location}</p>}
          {profile.about && <p className="text-sm text-gray-700 mt-3">{profile.about}</p>}
        </div>
      </div>

      {/* Experience */}
      {profile.experiences.length > 0 && (
        <Section title="Experience">
          {profile.experiences.map((e, i) => (
            <Item key={i} title={e.title} subtitle={e.company} meta={`${e.startDate}${e.endDate ? ` – ${e.endDate}` : e.isCurrent ? ' – Present' : ''}`} body={e.description} />
          ))}
        </Section>
      )}

      {/* Education */}
      {profile.educations.length > 0 && (
        <Section title="Education">
          {profile.educations.map((e, i) => (
            <Item key={i} title={e.school} subtitle={[e.degree, e.fieldOfStudy].filter(Boolean).join(' · ')} meta={`${e.startYear}${e.endYear ? ` – ${e.endYear}` : ''}`} />
          ))}
        </Section>
      )}

      {/* Skills */}
      {profile.skills.length > 0 && (
        <Section title="Skills">
          <div className="flex flex-wrap gap-2">
            {profile.skills.map((s, i) => (
              <span key={i} className="bg-blue-50 text-blue-700 text-sm px-3 py-1 rounded-full">
                {s.name}{s.endorsementCount > 0 && <span className="ml-1 text-blue-400">·{s.endorsementCount}</span>}
              </span>
            ))}
          </div>
        </Section>
      )}

      {/* Projects */}
      {profile.projects.length > 0 && (
        <Section title="Projects">
          {profile.projects.map((p, i) => (
            <Item key={i} title={p.title} subtitle={p.url ? <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline text-sm">{p.url}</a> : undefined} body={p.description} />
          ))}
        </Section>
      )}

      {/* Certifications */}
      {profile.certifications.length > 0 && (
        <Section title="Certifications">
          {profile.certifications.map((c, i) => (
            <Item key={i} title={c.name} subtitle={c.issuingOrganization} meta={c.issueDate}
              body={c.credentialUrl ? <a href={c.credentialUrl} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline text-sm">View credential</a> : undefined} />
          ))}
        </Section>
      )}

      {profile.experiences.length === 0 && profile.educations.length === 0 && profile.skills.length === 0 && (
        <div className="bg-white rounded-2xl shadow p-6 text-center text-gray-400 text-sm">
          No sections data — this profile snapshot has only basic info.
        </div>
      )}
    </main>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-2xl shadow p-6">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">{title}</h2>
      <div className="space-y-4">{children}</div>
    </div>
  )
}

function Item({ title, subtitle, meta, body }: {
  title: string
  subtitle?: React.ReactNode
  meta?: string
  body?: React.ReactNode
}) {
  return (
    <div className="border-l-2 border-blue-100 pl-4">
      <p className="font-medium text-gray-900">{title}</p>
      {subtitle && <p className="text-sm text-gray-600">{subtitle}</p>}
      {meta && <p className="text-xs text-gray-400 mt-0.5">{meta}</p>}
      {body && <div className="text-sm text-gray-600 mt-1">{body}</div>}
    </div>
  )
}
