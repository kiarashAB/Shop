import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/client';

export default function Login() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ userName: '', password: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const { data } = await api.post('/auth/login', form);
      sessionStorage.setItem('auth_token', data.token);
      sessionStorage.setItem('auth_user', JSON.stringify({ userName: data.userName, fullName: data.fullName, role: data.role }));
      navigate('/');
    } catch (err) {
      setError(err.response?.data?.title || 'ورود ناموفق بود. نام کاربری یا رمز عبور را بررسی کنید.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mx-auto max-w-md px-6 py-20">
      <h1 className="text-center text-2xl font-black">ورود به پنل مدیریت</h1>
      <p className="mt-2 text-center text-sm text-gray-500">
        این ورود مخصوص کارکنان و مدیران فروشگاه است. حساب مشتری هنوز در این پروژه پیاده‌سازی نشده.
      </p>

      <form onSubmit={submit} className="mt-10 space-y-4">
        <div>
          <label className="mb-1.5 block text-sm text-gray-600">نام کاربری</label>
          <input
            required autoFocus value={form.userName}
            onChange={(e) => setForm((f) => ({ ...f, userName: e.target.value }))}
            className="w-full rounded-2xl border border-gray-200 px-4 py-3 text-sm outline-none focus:border-brand"
          />
        </div>
        <div>
          <label className="mb-1.5 block text-sm text-gray-600">رمز عبور</label>
          <input
            required type="password" value={form.password}
            onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
            className="w-full rounded-2xl border border-gray-200 px-4 py-3 text-sm outline-none focus:border-brand"
          />
        </div>

        {error && <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-600">{error}</p>}

        <button
          type="submit" disabled={loading}
          className="w-full rounded-2xl bg-brand py-3.5 font-bold text-white transition hover:opacity-90 disabled:opacity-50"
        >
          {loading ? 'در حال ورود…' : 'ورود'}
        </button>
      </form>
    </div>
  );
}
