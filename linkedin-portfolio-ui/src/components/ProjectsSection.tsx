import type { ProjectDto } from '../types/profile'

interface Props { projects: ProjectDto[] }

export function ProjectsSection({ projects }: Props) {
  if (!projects.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Projects</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {projects.map((p, i) => (
          <div key={i} className="border border-gray-100 rounded-xl p-4 hover:shadow-md transition-shadow">
            <div className="flex items-start justify-between">
              <h3 className="font-semibold text-gray-900">{p.title}</h3>
              {p.url && (
                <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-blue-500 text-xs ml-2 shrink-0">
                  ↗ Link
                </a>
              )}
            </div>
            {(p.startDate || p.endDate) && (
              <p className="text-xs text-gray-500 mt-1">{p.startDate}{p.endDate ? ` – ${p.endDate}` : ''}</p>
            )}
            {p.description && <p className="text-sm text-gray-600 mt-2">{p.description}</p>}
          </div>
        ))}
      </div>
    </section>
  )
}
