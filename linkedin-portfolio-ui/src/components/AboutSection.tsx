interface Props { about: string }

export function AboutSection({ about }: Props) {
  if (!about) return null
  return (
    <section className="bg-white rounded-2xl shadow p-8">
      <h2 className="text-xl font-bold text-gray-900 mb-3">About</h2>
      <p className="text-gray-700 whitespace-pre-line leading-relaxed">{about}</p>
    </section>
  )
}
