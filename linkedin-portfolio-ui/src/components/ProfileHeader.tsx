import type { ProfileDto } from '../types/profile'

export function ProfileHeader({ name, headline, location, photoBase64 }: Pick<ProfileDto, 'name' | 'headline' | 'location' | 'photoBase64'>) {
  return (
    <div className="flex items-center gap-6 bg-white rounded-2xl shadow p-8">
      {photoBase64 ? (
        <img
          src={`data:image/jpeg;base64,${photoBase64}`}
          alt={name}
          className="w-32 h-32 rounded-full object-cover ring-4 ring-blue-100 shrink-0"
        />
      ) : (
        <div className="w-32 h-32 rounded-full bg-blue-100 flex items-center justify-center text-4xl font-bold text-blue-400 shrink-0">
          {name.charAt(0)}
        </div>
      )}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">{name}</h1>
        <p className="text-lg text-gray-600 mt-1">{headline}</p>
        {location && <p className="text-sm text-gray-500 mt-1">📍 {location}</p>}
      </div>
    </div>
  )
}
