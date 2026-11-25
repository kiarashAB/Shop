import { useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { errMsg } from '../lib/errors';

const field = 'w-full rounded-2xl border border-gray-200 px-4 py-3 text-sm outline-none focus:border-brand';

export default function Login() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const rawNext = params.get('next') || '/';
  const next = rawNext.startsWith('/') && !rawNext.startsWith('//') ? rawNext : '/';
  const { user, ready, login, register } = useAuth();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ userName: '', password: '', fullName: '', phone: '', email: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  if (ready && user) return <Navigate to={user.role === 'Customer' ? next : '/'} replace />;

  const submit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (mode === 'login') await login(form.userName.trim(), form.password);
      else await register(form.fullName, form.phone, form.email, form.password);
      navigate(next, { replace: true });
    } catch (err) {
      setError(errMsg(err, 'ورود ناموفق بود. اطلاعات را بررسی کنید.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mx-auto max-w-md px-6 py-16">
      <div className="mb-8 grid grid-cols-2 rounded-2xl bg-surface p-1 text-sm">
        {[['login', 'ورود'], ['register', 'ثبت‌نام']].map(([k, label]) => (
          <button key={k} type="button" onClick={() => { setMode(k); setError(''); }}
            className={`rounded-xl py-2.5 ${mode === k ? 'bg-white font-bold shadow-sm' : 'text-gray-500'}`}>{label}</button>
        ))}
      </div>

      <form onSubmit={submit} className="space-y-4">
        {mode === 'register' && (
          <>
            <div><label className="mb-1.5 block text-sm text-gray-600">نام و نام خانوادگی</label>
              <input required value={form.fullName} onChange={set('fullName')} className={field} /></div>
            <div><label className="mb-1.5 block text-sm text-gray-600">شماره موبایل</label>
              <input required inputMode="tel" placeholder="09123456789" value={form.phone} onChange={set('phone')} className={field} /></div>
            <div><label className="mb-1.5 block text-sm text-gray-600">ایمیل (اختیاری)</label>
              <input type="email" value={form.email} onChange={set('email')} className={field} /></div>
          </>
        )}
        {mode === 'login' && (
          <div><label className="mb-1.5 block text-sm text-gray-600">شماره موبایل / نام کاربری</label>
            <input required autoFocus value={form.userName} onChange={set('userName')} className={field} /></div>
        )}
        <div><label className="mb-1.5 block text-sm text-gray-600">رمز عبور{mode === 'register' && ' (حداقل ۸ کاراکتر)'}</label>
          <input required type="password" minLength={mode === 'register' ? 8 : undefined} value={form.password} onChange={set('password')} className={field} /></div>

        {error && <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-600">{error}</p>}

        <button type="submit" disabled={loading} className="w-full rounded-2xl bg-brand py-3.5 font-bold text-white transition hover:opacity-90 disabled:opacity-50">
          {loading ? 'لطفاً صبر کنید…' : mode === 'login' ? 'ورود' : 'ساخت حساب'}
        </button>
        <p className="text-center text-xs text-gray-400">مدیران و کارکنان هم از همین فرم با نام کاربری خود وارد می‌شوند.</p>
      </form>
    </div>
  );
}
