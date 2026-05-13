import { useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../hooks/useAuth';
import type { AuthResponse } from '../types';

export default function AuthCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { setFromResponse } = useAuth();

  useEffect(() => {
    const token = params.get('token');
    const refreshToken = params.get('refreshToken');
    const userId = params.get('userId');
    const email = params.get('email');
    const isAdmin = params.get('isAdmin') === 'true';
    const error = params.get('error');

    if (error) {
      navigate(`/login?error=${encodeURIComponent(error)}`);
      return;
    }

    if (token && refreshToken && userId && email) {
      const authResponse: AuthResponse = {
        accessToken: token,
        refreshToken,
        expiresAt: '',
        userId: Number(userId),
        email,
        isAdmin,
      };
      queryClient.clear();
      setFromResponse(authResponse);
      navigate('/');
    } else {
      navigate('/login');
    }
  }, [params, navigate, setFromResponse]);

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center">
      <div className="text-center">
        <div className="w-8 h-8 border-2 border-indigo-600 border-t-transparent rounded-full animate-spin mx-auto mb-4" />
        <p className="text-gray-500">Completing sign in...</p>
      </div>
    </div>
  );
}
