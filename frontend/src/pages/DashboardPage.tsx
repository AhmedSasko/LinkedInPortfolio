import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Zap } from 'lucide-react';
import { analysisApi } from '../api/analysisApi';
import { profileApi } from '../api/profileApi';
import ScoreGauge from '../components/ScoreGauge';
import SkillRadarChart from '../components/SkillRadarChart';

export default function DashboardPage() {
  const qc = useQueryClient();
  const [analysisError, setAnalysisError] = useState<string | null>(null);

  const { data: profile } = useQuery({
    queryKey: ['profile'],
    queryFn: () => profileApi.getLatest().then((r) => r.data),
    retry: false,
  });

  const { data: analysis, isLoading: analysisLoading } = useQuery({
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
    onSuccess: () => {
      setAnalysisError(null);
      qc.invalidateQueries({ queryKey: ['analysis'] });
    },
    onError: (err: any) =>
      setAnalysisError(err?.response?.data?.error || 'Failed to start analysis'),
  });

  const score = analysis?.overallScore ?? analysis?.result?.profile_scoring?.overall_score;
  const scoring = analysis?.result?.profile_scoring;
  const career = analysis?.result?.career_analysis;
  const seniority = analysis?.result?.seniority_estimation;

  const isPending = analysis?.status === 'pending' || analysis?.status === 'processing';

  return (
    <div className="p-8 max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Dashboard</h2>
          {profile && <p className="text-gray-500 mt-1">{profile.headline}</p>}
        </div>
        <button
          onClick={() => requestAnalysis.mutate()}
          disabled={requestAnalysis.isPending || isPending}
          className="flex items-center gap-2 bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
        >
          {isPending || requestAnalysis.isPending ? (
            <>
              <Loader2 size={16} className="animate-spin" />
              Analyzing...
            </>
          ) : (
            <>
              <Zap size={16} />
              Run AI Analysis
            </>
          )}
        </button>
      </div>

      {analysisError && (
        <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3 mb-6">
          {analysisError}
          {analysisError.toLowerCase().includes('profile') && (
            <Link to="/edit" className="ml-2 font-medium underline">Go to Edit Profile →</Link>
          )}
        </div>
      )}

      {/* Top stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        {/* Score gauge */}
        <div className="bg-white rounded-2xl border border-gray-200 p-6 flex flex-col items-center">
          <h3 className="text-sm font-medium text-gray-500 mb-4">Profile Score</h3>
          {analysisLoading ? (
            <Loader2 size={32} className="animate-spin text-gray-400" />
          ) : score !== undefined ? (
            <ScoreGauge score={score} label="Overall" />
          ) : (
            <div className="text-center text-gray-400 text-sm py-8">
              Run analysis to see your score
            </div>
          )}
        </div>

        {/* Career info */}
        <div className="bg-white rounded-2xl border border-gray-200 p-6">
          <h3 className="text-sm font-medium text-gray-500 mb-4">Career Overview</h3>
          {analysisLoading ? (
            <Loader2 size={20} className="animate-spin text-gray-400" />
          ) : career ? (
            <dl className="space-y-3">
              <div>
                <dt className="text-xs text-gray-400">Experience</dt>
                <dd className="text-sm font-medium text-gray-900">
                  {career.years_of_experience} years
                </dd>
              </div>
              <div>
                <dt className="text-xs text-gray-400">Industry</dt>
                <dd className="text-sm font-medium text-gray-900">{career.industry_focus}</dd>
              </div>
              <div>
                <dt className="text-xs text-gray-400">Trajectory</dt>
                <dd className="text-sm text-gray-700">{career.career_trajectory}</dd>
              </div>
            </dl>
          ) : (
            <p className="text-sm text-gray-400">No analysis yet</p>
          )}
        </div>

        {/* Seniority */}
        <div className="bg-white rounded-2xl border border-gray-200 p-6">
          <h3 className="text-sm font-medium text-gray-500 mb-4">Seniority Level</h3>
          {analysisLoading ? (
            <Loader2 size={20} className="animate-spin text-gray-400" />
          ) : seniority ? (
            <div>
              <div className="text-3xl font-bold text-indigo-600 capitalize mb-1">
                {seniority.estimated_level}
              </div>
              <div className="text-sm text-gray-500 mb-3">
                {seniority.confidence_score}% confidence
              </div>
              {seniority.next_level && (
                <div>
                  <span className="text-xs text-gray-400">Next level: </span>
                  <span className="text-xs font-medium text-gray-700 capitalize">
                    {seniority.next_level}
                  </span>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-gray-400">No analysis yet</p>
          )}
        </div>
      </div>

      {/* Radar chart + quick wins */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-white rounded-2xl border border-gray-200 p-6">
          <h3 className="text-sm font-medium text-gray-500 mb-4">Score Breakdown</h3>
          {scoring ? (
            <SkillRadarChart scoring={scoring} />
          ) : (
            <div className="h-64 flex items-center justify-center text-gray-400 text-sm">
              Run analysis to see breakdown
            </div>
          )}
        </div>

        <div className="bg-white rounded-2xl border border-gray-200 p-6">
          <h3 className="text-sm font-medium text-gray-500 mb-4">Top Improvements</h3>
          {scoring?.improvements?.length ? (
            <ul className="space-y-2">
              {scoring.improvements.slice(0, 6).map((item, i) => (
                <li key={i} className="flex gap-2 text-sm">
                  <span className="text-amber-500 mt-0.5">•</span>
                  <span className="text-gray-700">{item}</span>
                </li>
              ))}
            </ul>
          ) : (
            <div className="text-gray-400 text-sm">No improvements yet</div>
          )}
        </div>
      </div>
    </div>
  );
}
