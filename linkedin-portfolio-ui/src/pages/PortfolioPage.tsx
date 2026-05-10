import { useQuery } from '@tanstack/react-query'
import { fetchProfile } from '../api/profileApi'
import { ProfileHeader } from '../components/ProfileHeader'
import { AboutSection } from '../components/AboutSection'
import { ExperienceSection } from '../components/ExperienceSection'
import { EducationSection } from '../components/EducationSection'
import { SkillsSection } from '../components/SkillsSection'
import { ProjectsSection } from '../components/ProjectsSection'
import { CertificationsSection } from '../components/CertificationsSection'
import { LoadingSpinner } from '../components/LoadingSpinner'
import { Link } from 'react-router-dom'

export function PortfolioPage() {
  const { data: profile, isLoading, isError } = useQuery({
    queryKey: ['profile'],
    queryFn: fetchProfile,
    retry: false,
  })

  if (isLoading) return <LoadingSpinner message="Loading profile..." />

  if (isError) return (
    <div className="text-center py-20 text-red-500">
      Failed to load profile. Is the API running?
    </div>
  )

  if (!profile) return (
    <div className="text-center py-20 text-gray-500">
      <p className="text-lg">No profile data yet.</p>
      <Link to="/admin" className="mt-3 inline-block text-blue-600 hover:underline">
        Go to Admin → Sync from LinkedIn
      </Link>
    </div>
  )

  return (
    <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
      <ProfileHeader
        name={profile.name}
        headline={profile.headline}
        location={profile.location}
        photoBase64={profile.photoBase64}
      />
      <AboutSection about={profile.about} />
      <ExperienceSection experience={profile.experience} />
      <EducationSection education={profile.education} />
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <SkillsSection skills={profile.skills} />
        <CertificationsSection certifications={profile.certifications} />
      </div>
      <ProjectsSection projects={profile.projects} />
      <p className="text-center text-xs text-gray-400">
        Last synced: {new Date(profile.fetchedAt).toLocaleString()}
      </p>
    </main>
  )
}
