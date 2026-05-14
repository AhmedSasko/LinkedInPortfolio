import { useQuery } from '@tanstack/react-query';
import { Loader2, MapPin, Briefcase, GraduationCap, Award, FolderKanban, Globe } from 'lucide-react';
import { profileApi } from '../api/profileApi';
import type { ProfileExperience } from '../types';

export default function PortfolioPage() {
  const { data: profile, isLoading } = useQuery({
    queryKey: ['profile'],
    queryFn: () => profileApi.getLatest().then((r) => r.data),
    retry: false,
  });

  if (isLoading)
    return (
      <div className="flex justify-center py-20">
        <Loader2 size={32} className="animate-spin text-indigo-500" />
      </div>
    );

  if (!profile)
    return (
      <div className="p-8 text-center text-gray-500">
        No profile found. Connect LinkedIn or scrape your profile to get started.
      </div>
    );

  return (
    <div className="p-8 max-w-3xl mx-auto">
      {/* Header */}
      <div className="bg-white rounded-2xl border border-gray-200 p-6 mb-6">
        <div className="flex items-center gap-4">
          {profile.photoUrl ? (
            <img
              src={profile.photoUrl}
              alt={profile.name ?? ''}
              className="w-20 h-20 rounded-full object-cover border border-gray-200"
            />
          ) : (
            <div className="w-20 h-20 rounded-full bg-indigo-100 flex items-center justify-center text-2xl font-bold text-indigo-600">
              {profile.name?.charAt(0)}
            </div>
          )}
          <div>
            <h2 className="text-2xl font-bold text-gray-900">{profile.name}</h2>
            {profile.headline && <p className="text-gray-500 mt-0.5">{profile.headline}</p>}
            {profile.location && (
              <p className="flex items-center gap-1 text-sm text-gray-400 mt-1">
                <MapPin size={13} /> {profile.location}
              </p>
            )}
          </div>
        </div>
        {profile.about && (
          <p className="text-sm text-gray-600 mt-4 leading-relaxed">{profile.about}</p>
        )}
      </div>

      {/* Experience */}
      {profile.experiences.length > 0 && (
        <Section icon={<Briefcase size={18} />} title="Experience">
          {profile.experiences.map((exp, i) => (
            <ExperienceItem key={i} exp={exp} />
          ))}
        </Section>
      )}

      {/* Education */}
      {profile.educations.length > 0 && (
        <Section icon={<GraduationCap size={18} />} title="Education">
          {profile.educations.map((edu, i) => (
            <div key={i} className="py-3 border-b border-gray-100 last:border-0">
              <div className="font-medium text-gray-900">{edu.school}</div>
              {edu.degree && (
                <div className="text-sm text-gray-500">
                  {edu.degree}
                  {edu.fieldOfStudy ? ` · ${edu.fieldOfStudy}` : ''}
                </div>
              )}
              {(edu.startYear || edu.endYear) && (
                <div className="text-xs text-gray-400 mt-0.5">
                  {edu.startYear} — {edu.endYear ?? 'Present'}
                </div>
              )}
            </div>
          ))}
        </Section>
      )}

      {/* Skills */}
      {profile.skills.length > 0 && (
        <Section icon={<Award size={18} />} title="Skills">
          <div className="flex flex-wrap gap-2">
            {profile.skills.map((s, i) => (
              <span key={i} className="bg-indigo-50 text-indigo-700 px-3 py-1 rounded-full text-sm">
                {s.name}
              </span>
            ))}
          </div>
        </Section>
      )}

      {/* Certifications */}
      {profile.certifications.length > 0 && (
        <Section icon={<Award size={18} />} title="Certifications">
          {profile.certifications.map((c, i) => (
            <div key={i} className="py-2">
              <div className="font-medium text-gray-900 text-sm">{c.name}</div>
              {c.issuingOrganization && <div className="text-xs text-gray-500">{c.issuingOrganization}</div>}
            </div>
          ))}
        </Section>
      )}

      {/* Projects */}
      {profile.projects.length > 0 && (
        <Section icon={<FolderKanban size={18} />} title="Projects">
          {profile.projects.map((p, i) => (
            <div key={i} className="py-3 border-b border-gray-100 last:border-0">
              <div className="font-medium text-gray-900">{p.title}</div>
              {p.description && <p className="text-sm text-gray-500 mt-0.5">{p.description}</p>}
              {p.url && (
                <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-xs text-indigo-600 hover:underline">
                  {p.url}
                </a>
              )}
            </div>
          ))}
        </Section>
      )}

      {/* Languages */}
      {profile.languages?.length > 0 && (
        <Section icon={<Globe size={18} />} title="Languages">
          <div className="flex flex-wrap gap-3">
            {profile.languages.map((l, i) => (
              <div key={i} className="flex flex-col items-center bg-indigo-50 rounded-xl px-4 py-2">
                <span className="text-sm font-medium text-indigo-800">{l.name}</span>
                {l.proficiency && (
                  <span className="text-xs text-indigo-500 mt-0.5">{l.proficiency}</span>
                )}
              </div>
            ))}
          </div>
        </Section>
      )}
    </div>
  );
}

function Section({ icon, title, children }: { icon: React.ReactNode; title: string; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-6 mb-4">
      <h3 className="flex items-center gap-2 text-base font-semibold text-gray-900 mb-4">
        <span className="text-indigo-500">{icon}</span>
        {title}
      </h3>
      {children}
    </div>
  );
}

function ExperienceItem({ exp }: { exp: ProfileExperience }) {
  return (
    <div className="py-3 border-b border-gray-100 last:border-0">
      <div className="flex items-start justify-between">
        <div>
          <div className="font-medium text-gray-900">{exp.title}</div>
          <div className="text-sm text-gray-500">{exp.company}</div>
        </div>
        <div className="text-xs text-gray-400 text-right">
          {exp.startDate} — {exp.isCurrent ? 'Present' : exp.endDate}
        </div>
      </div>
      {exp.description && <p className="text-sm text-gray-600 mt-1 leading-relaxed">{exp.description}</p>}
    </div>
  );
}
