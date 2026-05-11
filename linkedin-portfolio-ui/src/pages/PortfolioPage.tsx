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
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

function NavBar() {
  const { isAdmin, logout } = useAuth()
  const navigate = useNavigate()

  function handleSignOut() {
    logout()
    navigate('/login')
  }

  return (
    <nav className="bg-white border-b border-gray-200 px-4 py-3">
      <div className="max-w-3xl mx-auto flex items-center justify-between">
        <span className="font-bold text-gray-900 text-lg">LinkedIn Portfolio</span>
        <div className="flex items-center gap-4">
          <Link to="/import" className="text-sm text-blue-600 hover:underline">
            Import Profile
          </Link>
          {isAdmin && (
            <Link to="/admin" className="text-sm text-blue-600 hover:underline">
              Admin
            </Link>
          )}
          <button
            onClick={handleSignOut}
            className="text-sm text-gray-500 hover:text-gray-700"
          >
            Sign out
          </button>
        </div>
      </div>
    </nav>
  )
}

export function PortfolioPage() {
  const { data: profile, isLoading, isError } = useQuery({
    queryKey: ['profile'],
    queryFn: fetchProfile,
    retry: false,
  })

  if (isLoading) return <LoadingSpinner message="Loading profile..." />

  if (isError) return (
    <>
      <NavBar />
      <div className="text-center py-20 text-red-500">
        Failed to load profile. Is the API running?
      </div>
    </>
  )

  if (!profile) return (
    <>
      <NavBar />
      <div className="text-center py-20 text-gray-500">
        <p className="text-lg">No profile data yet.</p>
        <Link to="/import" className="mt-3 inline-block text-blue-600 hover:underline">
          Import your LinkedIn profile
        </Link>
      </div>
    </>
  )

  return (
    <>
      <NavBar />
      <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
        <ProfileHeader
          name={profile.name}
          headline={profile.headline}
          location={profile.location}
          photoBase64={profile.photoBase64}
        />
        <AboutSection about={profile.about} />
        <ExperienceSection experience={profile.experiences} />
        <EducationSection education={profile.educations} />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <SkillsSection skills={profile.skills} />
          <CertificationsSection certifications={profile.certifications} />
        </div>
        <ProjectsSection projects={profile.projects} />
        <p className="text-center text-xs text-gray-400">
          Last synced: {new Date(profile.fetchedAt).toLocaleString()}
        </p>
      </main>
    </>
  )
}
