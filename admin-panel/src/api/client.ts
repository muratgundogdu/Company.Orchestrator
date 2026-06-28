import axios from 'axios';
import { getActiveToken, clearAuth } from '../auth/storage';

export class ApiError extends Error {
  field?: string;

  constructor(message: string, field?: string) {
    super(message);
    this.name = 'ApiError';
    this.field = field;
  }
}

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000',
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
});

api.interceptors.request.use((config) => {
  const token = getActiveToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401 && !err.config?.url?.includes('/api/auth/login')) {
      clearAuth();
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    const data = err.response?.data as { message?: string; error?: string; field?: string } | undefined;
    const msg =
      data?.message ??
      data?.error ??
      err.message ??
      'Unknown error';
    const field = typeof data?.field === 'string' ? data.field : undefined;
    return Promise.reject(new ApiError(msg, field));
  },
);

export default api;
