import { useState, useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Upload, FileText, Loader2, CheckCircle, Import } from 'lucide-react';
import { resumeApi } from '../api/resumeApi';
import clsx from 'clsx';

export default function ResumeUploadPage() {
  const [uploadError, setUploadError] = useState('');
  const qc = useQueryClient();

  const { data: resumes, isLoading } = useQuery({
    queryKey: ['resumes'],
    queryFn: () => resumeApi.list().then((r) => r.data),
  });

  const upload = useMutation({
    mutationFn: (file: File) => resumeApi.upload(file).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['resumes'] });
      setUploadError('');
    },
    onError: (err: any) => setUploadError(err.response?.data?.message || 'Upload failed'),
  });

  const importToProfile = useMutation({
    mutationFn: (id: number) => resumeApi.importToProfile(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['profile'] }),
  });

  const onDrop = useCallback(
    (accepted: File[]) => {
      if (accepted[0]) upload.mutate(accepted[0]);
    },
    [upload]
  );

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: { 'application/pdf': ['.pdf'], 'application/vnd.openxmlformats-officedocument.wordprocessingml.document': ['.docx'] },
    maxSize: 10 * 1024 * 1024,
    maxFiles: 1,
  });

  return (
    <div className="p-8 max-w-3xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-900 mb-6">Resume Upload</h2>

      {/* Drop zone */}
      <div
        {...getRootProps()}
        className={clsx(
          'border-2 border-dashed rounded-2xl p-12 text-center cursor-pointer transition-colors mb-6',
          isDragActive ? 'border-indigo-500 bg-indigo-50' : 'border-gray-300 hover:border-indigo-400 hover:bg-gray-50'
        )}
      >
        <input {...getInputProps()} />
        {upload.isPending ? (
          <Loader2 size={40} className="animate-spin text-indigo-500 mx-auto mb-3" />
        ) : (
          <Upload size={40} className="text-gray-400 mx-auto mb-3" />
        )}
        <p className="text-gray-600 font-medium">
          {isDragActive ? 'Drop your resume here' : 'Drag & drop your resume, or click to browse'}
        </p>
        <p className="text-gray-400 text-sm mt-1">PDF or DOCX, max 10 MB</p>
      </div>

      {uploadError && (
        <div className="bg-red-50 text-red-700 text-sm rounded-lg px-4 py-3 mb-6">{uploadError}</div>
      )}

      {/* Upload history */}
      <h3 className="text-lg font-semibold text-gray-900 mb-3">Uploaded Resumes</h3>
      {isLoading ? (
        <Loader2 size={24} className="animate-spin text-gray-400" />
      ) : !resumes?.length ? (
        <p className="text-gray-400 text-sm">No resumes uploaded yet</p>
      ) : (
        <div className="space-y-3">
          {resumes.map((r) => (
            <div key={r.id} className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-4">
              <FileText size={24} className="text-indigo-500 shrink-0" />
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-gray-900 truncate">{r.fileName}</div>
                <div className="text-xs text-gray-400">
                  {r.fileType.toUpperCase()} · {(r.fileSize / 1024).toFixed(0)} KB ·{' '}
                  {new Date(r.createdAt).toLocaleDateString()}
                </div>
                {r.parsedData?.skills?.length ? (
                  <div className="flex flex-wrap gap-1 mt-1">
                    {r.parsedData.skills.slice(0, 8).map((s) => (
                      <span key={s} className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-xs">{s}</span>
                    ))}
                  </div>
                ) : null}
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <StatusBadge status={r.status} />
                {r.status === 'completed' && (
                  <button
                    onClick={() => importToProfile.mutate(r.id)}
                    disabled={importToProfile.isPending}
                    className="flex items-center gap-1 text-xs bg-indigo-600 text-white px-3 py-1.5 rounded-lg hover:bg-indigo-700 disabled:opacity-50"
                  >
                    <Import size={12} />
                    Import
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  if (status === 'completed')
    return <span className="flex items-center gap-1 text-xs text-green-700"><CheckCircle size={12} /> Parsed</span>;
  if (status === 'processing')
    return <span className="flex items-center gap-1 text-xs text-amber-600"><Loader2 size={12} className="animate-spin" /> Parsing...</span>;
  return <span className="text-xs text-gray-400 capitalize">{status}</span>;
}
