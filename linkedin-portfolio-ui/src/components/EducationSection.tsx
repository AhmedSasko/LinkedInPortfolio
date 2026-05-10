import type { EducationDto } from '../types/profile'

interface Props { education: EducationDto[] }

export function EducationSection({ education }: Props) {
  if (!education.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Education</h2>
      <div className="space-y-4">
        {education.map((e, i) => (
          <div key={i} className="border-l-2 border-green-200 pl-4">
            <h3 className="font-semibold text-gray-900">{e.school}</h3>
            <p className="text-sm text-gray-600">{[e.degree, e.fieldOfStudy].filter(Boolean).join(' · ')}</p>
            {(e.startYear || e.endYear) && (
              <p className="text-xs text-gray-500">{e.startYear}{e.endYear ? ` – ${e.endYear}` : ''}</p>
            )}
          </div>
        ))}
      </div>
    </section>
  )
}
