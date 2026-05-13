import { useState, useEffect, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save, RefreshCw, CheckCircle2, XCircle, Circle, AlertCircle } from 'lucide-react';
import { profileApi } from '../api/profileApi';
import type { ScrapeStep, ScrapeProgress } from '../types';

function StepIcon({ status }: { status: ScrapeStep['status'] }) {
  if (status === 'done')    return <CheckCircle2 size={16} className="text-green-500 shrink-0" />;
  if (status === 'running') return <Loader2 size={16} className="animate-spin text-indigo-500 shrink-0" />;
  if (status === 'error')   return <XCircle size={16} className="text-red-500 shrink-0" />;
  return <Circle size={16} className="text-gray-300 shrink-0" />;
}

function ScrapeProgressPanel({ progress }: { progress: ScrapeProgress }) {
  const done = progress.steps.filter(s => s.status === 'done').length;
  const total = progress.steps.length;
  const pct = Math.round((done / total) * 100);

  return (
    <div className="mt-4 border border-gray-200 rounded-xl p-4 bg-gray-50">
      <div className="flex items-center justify-between mb-3">
        <span className="text-sm font-medium text-gray-700">
          {progress.isRunning ? 'Scraping in progress…' : progress.error ? 'Scraping failed' : 'Scraping complete'}
        </span>
        <span className="text-xs text-gray-500">{pct}%</span>
      </div>

      {/* Progress bar */}
      <div className="w-full bg-gray-200 rounded-full h-1.5 mb-4">
        <div
          className={`h-1.5 rounded-full transition-all duration-500 ${progress.error ? 'bg-red-400' : 'bg-indigo-500'}`}
          style={{ width: `${pct}%` }}
        />
      </div>

      {/* Steps */}
      <ul className="space-y-2">
        {progress.steps.map(step => (
          <li key={step.key} className="flex items-center gap-2">
            <StepIcon status={step.status} />
            <span className={`text-sm ${
              step.status === 'done'    ? 'text-gray-500 line-through' :
              step.status === 'running' ? 'text-indigo-700 font-medium' :
              step.status === 'error'   ? 'text-red-600 font-medium' :
              'text-gray-400'
            }`}>
              {step.label}
            </span>
          </li>
        ))}
      </ul>

      {progress.error && (
        <div className="mt-3 flex items-start gap-2 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
          <AlertCircle size={14} className="text-red-500 shrink-0 mt-0.5" />
          <p className="text-xs text-red-700">{progress.error}</p>
        </div>
      )}
    </div>
  );
}

export default function ProfileEditPage() {
  const qc = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [cookie, setCookie] = useState('');
  const [linkedInUrl, setLinkedInUrl] = useState('');
  const [scrapeError, setScrapeError] = useState('');
  const [success, setSuccess] = useState('');
  const [isSyncing, setIsSyncing] = useState(false);
  const [syncError, setSyncError] = useState('');
  const [isScraping, setIsScraping] = useState(false);
  const [scrapeProgress, setScrapeProgress] = useState<ScrapeProgress | null>(null);

  const { data: profile, isLoading } = useQuery({
    queryKey: ['profile'],
    queryFn: () => profileApi.getLatest().then((r) => r.data),
    retry: false,
  });

  const [form, setForm] = useState({ name: '', headline: '', location: '', about: '' });

  // Handle LinkedIn sync callback redirect
  useEffect(() => {
    if (searchParams.get('synced') === 'true') {
      setSuccess('Name and photo synced from LinkedIn');
      qc.invalidateQueries({ queryKey: ['profile'] });
      setSearchParams({}, { replace: true });
      setTimeout(() => setSuccess(''), 3000);
    }
  }, []);

  // Sync form whenever profile changes (initial load + after every scrape)
  useEffect(() => {
    if (profile) {
      setForm({
        name: profile.name ?? '',
        headline: profile.headline ?? '',
        location: profile.location ?? '',
        about: profile.about ?? '',
      });
    }
  }, [profile]);

  // Poll scrape progress while scraping
  useEffect(() => {
    if (!isScraping) return;

    const interval = setInterval(async () => {
      try {
        const { data } = await profileApi.getScrapeProgress();
        setScrapeProgress(data);

        if (!data.isRunning) {
          setIsScraping(false);
          clearInterval(interval);

          if (data.result) {
            qc.invalidateQueries({ queryKey: ['profile'] });
            setSuccess('Profile scraped successfully');
            setTimeout(() => {
              setSuccess('');
              setScrapeProgress(null);
            }, 4000);
          }
        }
      } catch {
        setIsScraping(false);
        clearInterval(interval);
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [isScraping]);

  const updateProfile = useMutation({
    mutationFn: () => profileApi.update({ ...profile, ...form }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['profile'] });
      setSuccess('Profile updated successfully');
      setTimeout(() => setSuccess(''), 3000);
    },
  });

  const scrape = useMutation({
    mutationFn: () => profileApi.scrapeStart(linkedInUrl, cookie),
    onSuccess: () => {
      setScrapeError('');
      setScrapeProgress(null);
      setIsScraping(true);
    },
    onError: (err: any) => {
      const data = err.response?.data;
      const msg = data?.error || data?.message ||
        (data?.errors ? Object.values(data.errors).flat().join(', ') : null) ||
        'Failed to start scraping';
      setScrapeError(msg);
    },
  });

  const handleLinkedInSync = async () => {
    setIsSyncing(true);
    setSyncError('');
    try {
      const { data } = await profileApi.getLinkedInSyncUrl();
      window.location.href = data.url;
    } catch (err: any) {
      setIsSyncing(false);
      setSyncError(err?.response?.data?.message || err?.message || 'Failed to initiate LinkedIn sync');
    }
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    updateProfile.mutate();
  };

  if (isLoading)
    return (
      <div className="flex justify-center py-20">
        <Loader2 size={32} className="animate-spin text-indigo-500" />
      </div>
    );

  return (
    <div className="p-8 max-w-2xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-900 mb-6">Edit Profile</h2>

      {success && (
        <div className="bg-green-50 text-green-700 text-sm rounded-lg px-4 py-3 mb-4 flex items-center gap-2">
          <CheckCircle2 size={15} />
          {success}
        </div>
      )}

      {/* Import options */}
      <div className="bg-white rounded-2xl border border-gray-200 p-6 mb-6">
        <h3 className="font-medium text-gray-900 mb-4">Import from LinkedIn</h3>
        <div className="space-y-5">

          {/* OAuth sync (name + photo only) */}
          <div>
            <button
              onClick={handleLinkedInSync}
              disabled={isSyncing}
              className="flex items-center gap-2 text-sm bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              <RefreshCw size={15} className={isSyncing ? 'animate-spin' : ''} />
              {isSyncing ? 'Redirecting to LinkedIn...' : 'Sync Name & Photo via LinkedIn'}
            </button>
            <p className="text-xs text-gray-400 mt-1">
              LinkedIn's API only provides name and profile photo. For full profile import use the cookie method below.
            </p>
            {syncError && <p className="text-red-600 text-xs mt-1">{syncError}</p>}
          </div>

          <div className="border-t border-gray-100" />

          {/* Cookie scraper (full profile) */}
          <div className="space-y-2">
            <label className="block text-sm font-medium text-gray-700">
              Full profile import with li_at cookie
            </label>
            <input
              type="text"
              value={linkedInUrl}
              onChange={(e) => setLinkedInUrl(e.target.value)}
              placeholder="https://www.linkedin.com/in/your-username"
              disabled={isScraping}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50"
            />
            <div className="flex gap-2">
              <input
                type="text"
                value={cookie}
                onChange={(e) => setCookie(e.target.value)}
                placeholder="Paste your li_at cookie value"
                disabled={isScraping}
                className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50"
              />
              <button
                onClick={() => scrape.mutate()}
                disabled={scrape.isPending || isScraping || !cookie || !linkedInUrl}
                className="bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm hover:bg-indigo-700 disabled:opacity-50 whitespace-nowrap"
              >
                {scrape.isPending || isScraping ? (
                  <span className="flex items-center gap-1.5">
                    <Loader2 size={14} className="animate-spin" />
                    Scraping…
                  </span>
                ) : 'Scrape'}
              </button>
            </div>
            {scrapeError && <p className="text-red-600 text-xs mt-1">{scrapeError}</p>}

            {/* Live progress panel */}
            {scrapeProgress && <ScrapeProgressPanel progress={scrapeProgress} />}
          </div>
        </div>
      </div>

      {/* Manual edit form */}
      <div className="bg-white rounded-2xl border border-gray-200 p-6">
        <h3 className="font-medium text-gray-900 mb-4">Edit Manually</h3>
        <form onSubmit={handleSubmit} className="space-y-4">
          <Field label="Full Name" value={form.name} onChange={(v) => setForm({ ...form, name: v })} />
          <Field label="Headline" value={form.headline} onChange={(v) => setForm({ ...form, headline: v })} />
          <Field label="Location" value={form.location} onChange={(v) => setForm({ ...form, location: v })} />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">About</label>
            <textarea
              value={form.about}
              onChange={(e) => setForm({ ...form, about: e.target.value })}
              rows={6}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
            />
          </div>
          <button
            type="submit"
            disabled={updateProfile.isPending}
            className="flex items-center gap-2 bg-indigo-600 text-white px-5 py-2 rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50"
          >
            <Save size={15} />
            {updateProfile.isPending ? 'Saving...' : 'Save Changes'}
          </button>
        </form>
      </div>
    </div>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
      />
    </div>
  );
}
