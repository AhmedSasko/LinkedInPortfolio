import type { ExperienceDto } from '../types/profile'

interface Props { experience: ExperienceDto[] }

export function ExperienceSection({ experience }: Props) {
  if (!experience.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Experience</h2>
      <div className="space-y-6">
        {experience.map((e, i) => (
          <div key={i} className="border-l-2 border-blue-200 pl-4">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-semibold text-gray-900">{e.title}</h3>
                <p className="text-blue-600 text-sm">{e.company}</p>
              </div>
              <span className="text-xs text-gray-500 whitespace-nowrap ml-4">
                {e.startDate}{e.isCurrent ? ' – Present' : e.endDate ? ` – ${e.endDate}` : ''}
              </span>
            </div>
            {e.description && <p className="text-gray-600 text-sm mt-2">{e.description}</p>}
          </div>
        ))}
      </div>
    </section>
  )
}
