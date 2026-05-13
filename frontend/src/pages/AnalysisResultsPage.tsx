import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Zap, CheckCircle, XCircle, Clock } from 'lucide-react';
import { analysisApi } from '../api/analysisApi';
import ScoreGauge from '../components/ScoreGauge';
import SkillRadarChart from '../components/SkillRadarChart';
import clsx from 'clsx';

type TabId = 'scoring' | 'skills' | 'career' | 'ats' | 'seniority' | 'recommendations';

const TABS: { id: TabId; label: string }[] = [
  { id: 'scoring', label: 'Profile Score' },
  { id: 'skills', label: 'Skills' },
  { id: 'career', label: 'Career' },
  { id: 'ats', label: 'ATS' },
  { id: 'seniority', label: 'Seniority' },
  { id: 'recommendations', label: 'Recommendations' },
];

export default function AnalysisResultsPage() {
  const [activeTab, setActiveTab] = useState<TabId>('scoring');
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const qc = useQueryClient();

  const { data: analysis, isLoading } = useQuery({
    queryKey: ['analysis'],
    queryFn: () => analysisApi.getLatest().then((r) => r.data),
    retry: false,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'pending' || status === 'processing' ? 5000 : false;
    },
  });

  const requestAnalysis = useMutation({
    mutationFn: () => analysisApi.requestAnalysis().then((r) => r.data),
    onSuccess: (pending) => {
      setAnalysisError(null);
      qc.setQueryData(['analysis'], pending);
    },
    onError: (err: any) =>
      setAnalysisError(err?.response?.data?.error || 'Failed to start analysis'),
  });

  const result = analysis?.result;
  const isPending = analysis?.status === 'pending' || analysis?.status === 'processing';

  return (
    <div className="p-8 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-2xl font-bold text-gray-900">AI Analysis</h2>
        <div className="flex items-center gap-3">
          {analysis && (
            <StatusBadge status={analysis.status} />
          )}
          <button
            onClick={() => requestAnalysis.mutate()}
            disabled={requestAnalysis.isPending || isPending}
            className="flex items-center gap-2 bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            <Zap size={16} />
            {isPending ? 'Analyzing...' : 'Re-analyze'}
          </button>
        </div>
      </div>

      {analysisError && (
        <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-6">
          {analysisError}
          {analysisError.toLowerCase().includes('profile') && (
            <Link to="/edit" className="ml-2 font-medium underline">Go to Edit Profile →</Link>
          )}
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-20">
          <Loader2 size={32} className="animate-spin text-indigo-500" />
        </div>
      ) : !analysis || analysis.status === 'failed' ? (
        <div className="bg-white rounded-2xl border border-gray-200 p-12 text-center">
          <p className="text-gray-500 mb-4">
            {analysis?.errorMessage || 'No analysis found. Run your first AI analysis.'}
          </p>
          <button
            onClick={() => requestAnalysis.mutate()}
            className="bg-indigo-600 text-white px-6 py-2 rounded-lg text-sm font-medium hover:bg-indigo-700"
          >
            Start Analysis
          </button>
        </div>
      ) : isPending ? (
        <div className="bg-white rounded-2xl border border-gray-200 p-12 text-center">
          <Loader2 size={40} className="animate-spin text-indigo-500 mx-auto mb-4" />
          <p className="text-gray-600 font-medium">AI is analyzing your profile...</p>
          <p className="text-gray-400 text-sm mt-1">This usually takes 30-60 seconds</p>
        </div>
      ) : (
        <>
          {/* Tab bar */}
          <div className="flex gap-1 bg-gray-100 p-1 rounded-xl mb-6 overflow-x-auto">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={clsx(
                  'px-4 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-colors',
                  activeTab === tab.id
                    ? 'bg-white text-indigo-700 shadow-sm'
                    : 'text-gray-500 hover:text-gray-700'
                )}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {/* Tab content */}
          <div className="bg-white rounded-2xl border border-gray-200 p-6">
            {activeTab === 'scoring' && result?.profile_scoring && (
              <ScoringTab data={result.profile_scoring} />
            )}
            {activeTab === 'skills' && result?.skills_intelligence && (
              <SkillsTab data={result.skills_intelligence} />
            )}
            {activeTab === 'career' && result?.career_analysis && (
              <CareerTab data={result.career_analysis} />
            )}
            {activeTab === 'ats' && result?.ats_optimization && (
              <AtsTab data={result.ats_optimization} />
            )}
            {activeTab === 'seniority' && result?.seniority_estimation && (
              <SeniorityTab data={result.seniority_estimation} />
            )}
            {activeTab === 'recommendations' && result?.recommendations && (
              <RecommendationsTab data={result.recommendations} />
            )}
          </div>
        </>
      )}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  if (status === 'completed') return (
    <span className="flex items-center gap-1 text-green-700 bg-green-50 px-3 py-1 rounded-full text-xs font-medium">
      <CheckCircle size={12} /> Completed
    </span>
  );
  if (status === 'failed') return (
    <span className="flex items-center gap-1 text-red-700 bg-red-50 px-3 py-1 rounded-full text-xs font-medium">
      <XCircle size={12} /> Failed
    </span>
  );
  return (
    <span className="flex items-center gap-1 text-amber-700 bg-amber-50 px-3 py-1 rounded-full text-xs font-medium">
      <Clock size={12} /> Processing
    </span>
  );
}

function ScoringTab({ data }: { data: any }) {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
      <div className="flex flex-col items-center">
        <ScoreGauge score={data.overall_score ?? 0} label="Profile Score" size={220} />
        <div className="grid grid-cols-2 gap-3 mt-6 w-full">
          {[
            ['Completeness', data.completeness_score],
            ['Headline', data.headline_score],
            ['About', data.about_score],
            ['Experience', data.experience_score],
            ['Skills', data.skills_score],
            ['Education', data.education_score],
          ].map(([label, score]) => (
            <div key={label as string} className="bg-gray-50 rounded-lg p-3">
              <div className="text-xs text-gray-500">{label}</div>
              <div className="text-lg font-bold text-gray-900">{score ?? '—'}</div>
            </div>
          ))}
        </div>
      </div>
      <div>
        <h4 className="font-medium text-gray-900 mb-3">Score Radar</h4>
        <SkillRadarChart scoring={data} />
        <div className="mt-4">
          <h4 className="font-medium text-gray-900 mb-2">Top Improvements</h4>
          <ul className="space-y-1">
            {data.improvements?.map((item: string, i: number) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-amber-500">•</span> {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

function SkillsTab({ data }: { data: any }) {
  return (
    <div className="space-y-6">
      <div>
        <h4 className="font-medium text-gray-900 mb-2">Assessment</h4>
        <p className="text-sm text-gray-600">{data.current_skills_assessment}</p>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Skill Gaps</h4>
          <div className="flex flex-wrap gap-2">
            {data.skill_gaps?.map((s: string) => (
              <span key={s} className="bg-red-50 text-red-700 px-3 py-1 rounded-full text-xs">{s}</span>
            ))}
          </div>
        </div>
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Trending Skills to Add</h4>
          <div className="flex flex-wrap gap-2">
            {data.trending_skills?.map((s: string) => (
              <span key={s} className="bg-green-50 text-green-700 px-3 py-1 rounded-full text-xs">{s}</span>
            ))}
          </div>
        </div>
      </div>
      <div>
        <h4 className="font-medium text-gray-900 mb-2">Recommendations</h4>
        <ul className="space-y-1">
          {data.recommendations?.map((r: string, i: number) => (
            <li key={i} className="text-sm text-gray-600 flex gap-2">
              <span className="text-indigo-500">→</span> {r}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function CareerTab({ data }: { data: any }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Stat label="Years Experience" value={`${data.years_of_experience ?? '—'}`} />
        <Stat label="Progression Score" value={`${data.progression_score ?? '—'}/100`} />
        <Stat label="Industry" value={data.industry_focus ?? '—'} />
        <Stat label="Velocity" value={data.career_velocity ?? '—'} />
      </div>
      <div>
        <h4 className="font-medium text-gray-900 mb-2">Trajectory</h4>
        <p className="text-sm text-gray-600">{data.career_trajectory}</p>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Predicted Next Roles</h4>
          <ul className="space-y-1">
            {data.predicted_next_roles?.map((r: string, i: number) => (
              <li key={i} className="text-sm text-indigo-700 bg-indigo-50 px-3 py-1.5 rounded-lg">{r}</li>
            ))}
          </ul>
        </div>
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Career Risks</h4>
          <ul className="space-y-1">
            {data.career_risks?.map((r: string, i: number) => (
              <li key={i} className="text-sm text-red-700 flex gap-2">
                <span>⚠</span> {r}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

function AtsTab({ data }: { data: any }) {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-6">
        <ScoreGauge score={data.overall_ats_score ?? 0} label="ATS Score" size={160} />
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Quick Wins</h4>
          <ul className="space-y-2">
            {data.quick_wins?.map((w: any, i: number) => (
              <li key={i} className="text-sm">
                <span className="font-medium text-gray-800">{w.action ?? w.improvement ?? String(w)}</span>
                {w.impact && typeof w.impact === 'string' && (
                  <span className="ml-2 text-xs text-amber-600 bg-amber-50 px-2 py-0.5 rounded-full">{w.impact}</span>
                )}
              </li>
            ))}
          </ul>
        </div>
      </div>
      <div>
        <h4 className="font-medium text-gray-900 mb-2">Missing Keywords</h4>
        <div className="flex flex-wrap gap-2">
          {data.missing_keywords?.map((k: string) => (
            <span key={k} className="bg-orange-50 text-orange-700 px-3 py-1 rounded-full text-xs">{k}</span>
          ))}
        </div>
      </div>
    </div>
  );
}

function SeniorityTab({ data }: { data: any }) {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-6">
        <div className="text-center">
          <div className="text-5xl font-bold text-indigo-600 capitalize">{data.estimated_level}</div>
          <div className="text-sm text-gray-500 mt-1">{data.confidence_score}% confidence</div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Stat label="Years Exp." value={`${data.years_of_experience ?? '—'}`} />
          <Stat label="Next Level" value={data.next_level ?? '—'} />
        </div>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Evidence</h4>
          <ul className="space-y-1">
            {data.evidence?.map((e: string, i: number) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-green-500">✓</span> {e}
              </li>
            ))}
          </ul>
        </div>
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Gap to Next Level</h4>
          <ul className="space-y-1">
            {data.next_level_gap?.map((g: string, i: number) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-amber-500">→</span> {g}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

function RecommendationsTab({ data }: { data: any }) {
  return (
    <div className="space-y-6">
      <div>
        <h4 className="font-medium text-gray-900 mb-3">Immediate Actions (This Week)</h4>
        <div className="space-y-2">
          {data.immediate_actions?.map((a: any, i: number) => (
            <div key={i} className="bg-indigo-50 rounded-lg p-3">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-medium text-indigo-900">{a.title}</span>
                <span className={clsx(
                  'text-xs px-2 py-0.5 rounded-full',
                  a.impact === 'high' ? 'bg-red-100 text-red-700' :
                  a.impact === 'medium' ? 'bg-amber-100 text-amber-700' :
                  'bg-gray-100 text-gray-600'
                )}>{a.impact}</span>
              </div>
              <p className="text-xs text-indigo-700">{a.description}</p>
            </div>
          ))}
        </div>
      </div>
      <div>
        <h4 className="font-medium text-gray-900 mb-3">Learning Path</h4>
        <div className="space-y-2">
          {data.learning_path?.map((item: any, i: number) => (
            <div key={i} className="flex items-center gap-3 bg-gray-50 rounded-lg p-3">
              <div className="flex-1">
                <div className="text-sm font-medium text-gray-900">{item.name}</div>
                <div className="text-xs text-gray-500">{item.platform} · {item.reason}</div>
              </div>
              <span className={clsx(
                'text-xs px-2 py-0.5 rounded-full',
                item.priority === 'high' ? 'bg-red-100 text-red-700' : 'bg-gray-100 text-gray-600'
              )}>{item.priority}</span>
            </div>
          ))}
        </div>
      </div>
      {data.personal_brand_tips?.length > 0 && (
        <div>
          <h4 className="font-medium text-gray-900 mb-2">Personal Branding Tips</h4>
          <ul className="space-y-1">
            {data.personal_brand_tips.map((t: string, i: number) => (
              <li key={i} className="text-sm text-gray-600 flex gap-2">
                <span className="text-indigo-500">💡</span> {t}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-gray-50 rounded-lg p-3 text-center">
      <div className="text-xs text-gray-400">{label}</div>
      <div className="text-base font-bold text-gray-900 mt-0.5">{value}</div>
    </div>
  );
}
