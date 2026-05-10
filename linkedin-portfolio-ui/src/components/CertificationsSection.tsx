import type { CertificationDto } from '../types/profile'

interface Props { certifications: CertificationDto[] }

export function CertificationsSection({ certifications }: Props) {
  if (!certifications.length) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-4">Certifications</h2>
      <div className="space-y-3">
        {certifications.map((c, i) => (
          <div key={i} className="flex items-start gap-3">
            <div className="w-2 h-2 rounded-full bg-purple-400 mt-2 shrink-0" />
            <div>
              <p className="font-medium text-gray-900">
                {c.credentialUrl ? (
                  <a href={c.credentialUrl} target="_blank" rel="noopener noreferrer" className="hover:text-blue-600">
                    {c.name}
                  </a>
                ) : c.name}
              </p>
              <p className="text-sm text-gray-500">{c.issuingOrganization}{c.issueDate ? ` · ${c.issueDate}` : ''}</p>
            </div>
          </div>
        ))}
      </div>
    </section>
  )
}
