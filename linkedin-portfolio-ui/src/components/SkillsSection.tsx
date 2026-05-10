import type { SkillDto } from '../types/profile'

interface Props { skills: SkillDto[] }

export function SkillsSection({ skills }: Props) {
  if (!skills.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Skills</h2>
      <div className="flex flex-wrap gap-2">
        {skills.map((s, i) => (
          <span key={i} className="px-3 py-1 bg-blue-50 text-blue-700 rounded-full text-sm font-medium">
            {s.name}{s.endorsementCount > 0 ? ` · ${s.endorsementCount}` : ''}
          </span>
        ))}
      </div>
    </section>
  )
}
