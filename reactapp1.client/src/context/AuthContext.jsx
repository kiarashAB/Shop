import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import api from '../api/client';

const AuthCtx = createContext(null);
export const useAuth = () => useContext(AuthCtx);

// نشست با کوکی HttpOnly نگه‌داری می‌شود؛ اینجا فقط مشخصات نمایشی کاربر (نام/نقش) در حافظه است.
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    api.get('/auth/me').then((r) => setUser(r.data)).catch(() => setUser(null)).finally(() => setReady(true));
  }, []);

  const login = useCallback(async (userName, password) => {
    const { data } = await api.post('/auth/login', { userName, password });
    setUser(data);
    return data;
  }, []);

  const register = useCallback(async (fullName, phone, email, password) => {
    const { data } = await api.post('/auth/register', { fullName, phone, email: email || null, password });
    setUser(data);
    return data;
  }, []);

  const logout = useCallback(async () => {
    try { await api.post('/auth/logout'); } catch { /* نشست قبلاً منقضی شده */ }
    setUser(null);
  }, []);

  const value = useMemo(() => ({ user, ready, login, register, logout, isCustomer: user?.role === 'Customer' }), [user, ready, login, register, logout]);
  return <AuthCtx.Provider value={value}>{children}</AuthCtx.Provider>;
}
