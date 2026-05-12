import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { fetchProfile, updateProfile, scrapeProfile } from '../api/profileApi'
import type { UpdateProfileRequest, ExperienceDto, EducationDto, SkillDto, ProjectDto, CertificationDto } from '../types/profile'

const emptyExperience = (): ExperienceDto => ({ title: '', company: '', startDate: '', endDate: '', description: '', isCurrent: false })
const emptyEducation = (): EducationDto => ({ school: '', degree: '', fieldOfStudy: '', startYear: '', endYear: '' })
const emptySkill = (): SkillDto => ({ name: '', endorsementCount: 0 })
const emptyProject = (): ProjectDto => ({ title: '', description: '', url: '', startDate: '', endDate: '' })
const emptyCertification = (): CertificationDto => ({ name: '', issuingOrganization: '', issueDate: '', credentialUrl: '' })

export function ProfileEditPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: profile, isLoading } = useQuery({
    queryKey: ['profile'],
    queryFn: fetchProfile,
  })

  const [form, setForm] = useState<UpdateProfileRequest>({
    name: '', headline: '', location: '', about: '', photoUrl: '',
    experiences: [], educations: [], skills: [], projects: [], certifications: [],
  })

  useEffect(() => {
    if (profile) {
      setForm({
        name: profile.name ?? '',
        headline: profile.headline ?? '',
        location: profile.location ?? '',
        about: profile.about ?? '',
        photoUrl: profile.photoUrl ?? '',
        experiences: profile.experiences ?? [],
        educations: profile.educations ?? [],
        skills: profile.skills ?? [],
        projects: profile.projects ?? [],
        certifications: profile.certifications ?? [],
      })
    }
  }, [profile])

  const [showSync, setShowSync] = useState(false)
  const [syncUrl, setSyncUrl] = useState('')
  const [syncCookie, setSyncCookie] = useState('')

  const syncMutation = useMutation({
    mutationFn: () => scrapeProfile(syncUrl, syncCookie),
    onSuccess: (data) => {
      setForm({
        name: data.name ?? '',
        headline: data.headline ?? '',
        location: data.location ?? '',
        about: data.about ?? '',
        photoUrl: data.photoUrl ?? '',
        experiences: data.experiences ?? [],
        educations: data.educations ?? [],
        skills: data.skills ?? [],
        projects: data.projects ?? [],
        certifications: data.certifications ?? [],
      })
      setShowSync(false)
      queryClient.invalidateQueries({ queryKey: ['profile'] })
    },
  })

  const mutation = useMutation({
    mutationFn: () => updateProfile(form),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profile'] })
      navigate('/')
    },
  })

  if (isLoading) return <div className="min-h-screen bg-gray-50 flex items-center justify-center"><p className="text-gray-500">Loading…</p></div>

  const setField = (field: keyof UpdateProfileRequest, value: unknown) =>
    setForm(f => ({ ...f, [field]: value }))

  // Generic helpers for list editing
  const addItem = <T,>(field: keyof UpdateProfileRequest, empty: () => T) =>
    setForm(f => ({ ...f, [field]: [...(f[field] as T[]), empty()] }))

  const removeItem = (field: keyof UpdateProfileRequest, idx: number) =>
    setForm(f => ({ ...f, [field]: (f[field] as unknown[]).filter((_, i) => i !== idx) }))

  const updateItem = <T,>(field: keyof UpdateProfileRequest, idx: number, updated: T) =>
    setForm(f => ({ ...f, [field]: (f[field] as T[]).map((item, i) => i === idx ? updated : item) }))

  return (
    <main className="min-h-screen bg-gray-50 py-8 px-4">
      <div className="max-w-2xl mx-auto space-y-8">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">Edit Profile</h1>
          <button onClick={() => navigate('/')} className="text-sm text-gray-500 hover:text-gray-700">← Back</button>
        </div>

        {mutation.isError && (
          <p className="text-sm text-red-600 bg-red-50 p-3 rounded-lg">Failed to save. Please try again.</p>
        )}

        {/* Sync from LinkedIn */}
        <div className="bg-blue-50 border border-blue-200 rounded-2xl p-6 space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold text-blue-900">Sync from LinkedIn</h2>
              <p className="text-sm text-blue-600 mt-0.5">Auto-fill your profile from your LinkedIn page</p>
            </div>
            <button
              onClick={() => setShowSync(v => !v)}
              className="text-sm text-blue-700 hover:text-blue-900 font-medium"
            >
              {showSync ? 'Cancel' : 'Set up'}
            </button>
          </div>

          {showSync && (
            <div className="space-y-3">
              <Field label="Your LinkedIn Profile URL">
                <input
                  className={inputCls}
                  value={syncUrl}
                  onChange={e => setSyncUrl(e.target.value)}
                  placeholder="https://www.linkedin.com/in/yourname"
                />
              </Field>
              <Field label="LinkedIn Session Cookie (li_at)">
                <input
                  className={inputCls}
                  type="password"
                  value={syncCookie}
                  onChange={e => setSyncCookie(e.target.value)}
                  placeholder="Paste your li_at cookie value"
                />
                <p className="text-xs text-blue-500 mt-1">
                  From browser DevTools → Application → Cookies → linkedin.com
                </p>
              </Field>
              {syncMutation.isError && (
                <p className="text-sm text-red-600 bg-red-50 p-3 rounded-lg">
                  {axios.isAxiosError(syncMutation.error) && syncMutation.error.response?.data?.message
                    ? syncMutation.error.response.data.message
                    : 'Sync failed. Please check your cookie and try again.'}
                </p>
              )}
              <button
                onClick={() => syncMutation.mutate()}
                disabled={syncMutation.isPending || !syncUrl || !syncCookie}
                className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
              >
                {syncMutation.isPending ? 'Syncing\u2026 (30\u201360s)' : 'Sync Profile'}
              </button>
            </div>
          )}
        </div>

        {/* Basic Info */}
        <Section title="Basic Info">
          <Field label="Name"><input className={inputCls} value={form.name} onChange={e => setField('name', e.target.value)} placeholder="Your full name" /></Field>
          <Field label="Headline"><input className={inputCls} value={form.headline} onChange={e => setField('headline', e.target.value)} placeholder="Software Engineer at Acme" /></Field>
          <Field label="Location"><input className={inputCls} value={form.location} onChange={e => setField('location', e.target.value)} placeholder="Riyadh, Saudi Arabia" /></Field>
          <Field label="Photo URL"><input className={inputCls} value={form.photoUrl ?? ''} onChange={e => setField('photoUrl', e.target.value)} placeholder="https://…" /></Field>
          <Field label="About">
            <textarea className={`${inputCls} h-28 resize-none`} value={form.about} onChange={e => setField('about', e.target.value)} placeholder="Brief bio…" />
          </Field>
        </Section>

        {/* Experience */}
        <Section title="Experience" onAdd={() => addItem('experiences', emptyExperience)}>
          {form.experiences.map((exp, i) => (
            <ItemCard key={i} onRemove={() => removeItem('experiences', i)}>
              <Field label="Title"><input className={inputCls} value={exp.title} onChange={e => updateItem('experiences', i, { ...exp, title: e.target.value })} /></Field>
              <Field label="Company"><input className={inputCls} value={exp.company} onChange={e => updateItem('experiences', i, { ...exp, company: e.target.value })} /></Field>
              <div className="grid grid-cols-2 gap-3">
                <Field label="Start"><input className={inputCls} value={exp.startDate} onChange={e => updateItem('experiences', i, { ...exp, startDate: e.target.value })} placeholder="Jan 2020" /></Field>
                <Field label="End"><input className={inputCls} value={exp.endDate} onChange={e => updateItem('experiences', i, { ...exp, endDate: e.target.value })} placeholder="Present" /></Field>
              </div>
              <Field label="Description">
                <textarea className={`${inputCls} h-20 resize-none`} value={exp.description} onChange={e => updateItem('experiences', i, { ...exp, description: e.target.value })} />
              </Field>
              <label className="flex items-center gap-2 text-sm text-gray-600">
                <input type="checkbox" checked={exp.isCurrent} onChange={e => updateItem('experiences', i, { ...exp, isCurrent: e.target.checked })} />
                Current role
              </label>
            </ItemCard>
          ))}
        </Section>

        {/* Education */}
        <Section title="Education" onAdd={() => addItem('educations', emptyEducation)}>
          {form.educations.map((edu, i) => (
            <ItemCard key={i} onRemove={() => removeItem('educations', i)}>
              <Field label="School"><input className={inputCls} value={edu.school} onChange={e => updateItem('educations', i, { ...edu, school: e.target.value })} /></Field>
              <Field label="Degree"><input className={inputCls} value={edu.degree} onChange={e => updateItem('educations', i, { ...edu, degree: e.target.value })} /></Field>
              <Field label="Field of Study"><input className={inputCls} value={edu.fieldOfStudy} onChange={e => updateItem('educations', i, { ...edu, fieldOfStudy: e.target.value })} /></Field>
              <div className="grid grid-cols-2 gap-3">
                <Field label="Start Year"><input className={inputCls} value={edu.startYear} onChange={e => updateItem('educations', i, { ...edu, startYear: e.target.value })} /></Field>
                <Field label="End Year"><input className={inputCls} value={edu.endYear} onChange={e => updateItem('educations', i, { ...edu, endYear: e.target.value })} /></Field>
              </div>
            </ItemCard>
          ))}
        </Section>

        {/* Skills */}
        <Section title="Skills" onAdd={() => addItem('skills', emptySkill)}>
          {form.skills.map((sk, i) => (
            <ItemCard key={i} onRemove={() => removeItem('skills', i)}>
              <Field label="Skill Name"><input className={inputCls} value={sk.name} onChange={e => updateItem('skills', i, { ...sk, name: e.target.value })} /></Field>
            </ItemCard>
          ))}
        </Section>

        {/* Projects */}
        <Section title="Projects" onAdd={() => addItem('projects', emptyProject)}>
          {form.projects.map((proj, i) => (
            <ItemCard key={i} onRemove={() => removeItem('projects', i)}>
              <Field label="Title"><input className={inputCls} value={proj.title} onChange={e => updateItem('projects', i, { ...proj, title: e.target.value })} /></Field>
              <Field label="Description"><textarea className={`${inputCls} h-20 resize-none`} value={proj.description} onChange={e => updateItem('projects', i, { ...proj, description: e.target.value })} /></Field>
              <Field label="URL"><input className={inputCls} value={proj.url} onChange={e => updateItem('projects', i, { ...proj, url: e.target.value })} /></Field>
            </ItemCard>
          ))}
        </Section>

        {/* Certifications */}
        <Section title="Certifications" onAdd={() => addItem('certifications', emptyCertification)}>
          {form.certifications.map((cert, i) => (
            <ItemCard key={i} onRemove={() => removeItem('certifications', i)}>
              <Field label="Name"><input className={inputCls} value={cert.name} onChange={e => updateItem('certifications', i, { ...cert, name: e.target.value })} /></Field>
              <Field label="Issuing Organization"><input className={inputCls} value={cert.issuingOrganization} onChange={e => updateItem('certifications', i, { ...cert, issuingOrganization: e.target.value })} /></Field>
              <Field label="Issue Date"><input className={inputCls} value={cert.issueDate} onChange={e => updateItem('certifications', i, { ...cert, issueDate: e.target.value })} /></Field>
              <Field label="Credential URL"><input className={inputCls} value={cert.credentialUrl} onChange={e => updateItem('certifications', i, { ...cert, credentialUrl: e.target.value })} /></Field>
            </ItemCard>
          ))}
        </Section>

        <button
          onClick={() => mutation.mutate()}
          disabled={mutation.isPending}
          className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold rounded-xl transition-colors"
        >
          {mutation.isPending ? 'Saving…' : 'Save Profile'}
        </button>
      </div>
    </main>
  )
}

const inputCls = 'w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500'

function Section({ title, children, onAdd }: { title: string; children: React.ReactNode; onAdd?: () => void }) {
  return (
    <div className="bg-white rounded-2xl shadow p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-800">{title}</h2>
        {onAdd && <button onClick={onAdd} className="text-sm text-blue-600 hover:text-blue-800 font-medium">+ Add</button>}
      </div>
      {children}
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="block text-sm font-medium text-gray-700">{label}</label>
      {children}
    </div>
  )
}

function ItemCard({ children, onRemove }: { children: React.ReactNode; onRemove: () => void }) {
  return (
    <div className="border border-gray-200 rounded-xl p-4 space-y-3 relative">
      <button onClick={onRemove} className="absolute top-3 right-3 text-gray-400 hover:text-red-500 text-xs">Remove</button>
      {children}
    </div>
  )
}
