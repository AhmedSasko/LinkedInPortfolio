import {
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  Radar,
  ResponsiveContainer,
  Tooltip,
} from 'recharts';
import type { ProfileScoringResult } from '../types';

interface Props {
  scoring: ProfileScoringResult;
}

export default function SkillRadarChart({ scoring }: Props) {
  const data = [
    { subject: 'Completeness', value: scoring.completeness_score ?? 0 },
    { subject: 'Headline', value: scoring.headline_score ?? 0 },
    { subject: 'About', value: scoring.about_score ?? 0 },
    { subject: 'Experience', value: scoring.experience_score ?? 0 },
    { subject: 'Skills', value: scoring.skills_score ?? 0 },
    { subject: 'Education', value: scoring.education_score ?? 0 },
    { subject: 'Projects', value: scoring.projects_score ?? 0 },
  ];

  return (
    <ResponsiveContainer width="100%" height={280}>
      <RadarChart data={data}>
        <PolarGrid />
        <PolarAngleAxis dataKey="subject" tick={{ fontSize: 12 }} />
        <Radar
          name="Score"
          dataKey="value"
          stroke="#6366f1"
          fill="#6366f1"
          fillOpacity={0.3}
        />
        <Tooltip formatter={(v) => [`${v}/100`]} />
      </RadarChart>
    </ResponsiveContainer>
  );
}
