import { useEffect, useState } from 'react';
import api from '../../api/client';
import { errMsg } from '../../lib/errors';

const field = 'w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm outline-none focus:border-brand';

function Msg({ m }) {
  if (!m) return null;
  return <p className={`rounded-xl px-3 py-2 text-sm ${m.ok ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'}`}>{m.text}</p>;
}

export default function Profile() {
  const [p, setP] = useState(null);
  const [msg, setMsg] = useState(null);
  const [pw, setPw] = useState({ currentPassword: '', newPassword: '' });
  const [pwMsg, setPwMsg] = useState(null);

  useEffect(() => { api.get('/me/profile').then((r) => setP({ ...r.data, email: r.data.email ?? '' })).catch((e) => setMsg({ text: errMsg(e) })); }, []);

  const save = async (e) => {
    e.preventDefault(); setMsg(null);
    try { await api.put('/me/profile', { fullName: p.fullName, email: p.email || null }); setMsg({ ok: true, text: 'اطلاعات ذخیره شد.' }); }
    catch (err) { setMsg({ text: errMsg(err) }); }
  };
  const changePw = async (e) => {
    e.preventDefault(); setPwMsg(null);
    try { await api.post('/me/change-password', pw); setPw({ currentPassword: '', newPassword: '' }); setPwMsg({ ok: true, text: 'رمز عبور تغییر کرد.' }); }
    catch (err) { setPwMsg({ text: errMsg(err) }); }
  };

  if (!p) return <p className="py-10 text-center text-gray-400">{msg?.text ?? 'در حال بارگذاری…'}</p>;
  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-black">پروفایل</h1>
      <form onSubmit={save} className="space-y-3 rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
        <label className="block text-sm text-gray-600">نام و نام خانوادگی<input required value={p.fullName} onChange={(e) => setP({ ...p, fullName: e.target.value })} className={`${field} mt-1.5`} /></label>
        <label className="block text-sm text-gray-600">شماره موبایل (نام کاربری)<input disabled value={p.phone} className={`${field} mt-1.5 bg-gray-50`} /></label>
        <label className="block text-sm text-gray-600">ایمیل<input type="email" value={p.email} onChange={(e) => setP({ ...p, email: e.target.value })} className={`${field} mt-1.5`} /></label>
        <Msg m={msg} />
        <button className="rounded-xl bg-brand px-6 py-2.5 text-sm text-white">ذخیره</button>
      </form>

      <form onSubmit={changePw} className="space-y-3 rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
        <h2 className="font-bold">تغییر رمز عبور</h2>
        <input required type="password" placeholder="رمز عبور فعلی" value={pw.currentPassword} onChange={(e) => setPw({ ...pw, currentPassword: e.target.value })} className={field} />
        <input required type="password" minLength={8} placeholder="رمز عبور جدید (حداقل ۸ کاراکتر)" value={pw.newPassword} onChange={(e) => setPw({ ...pw, newPassword: e.target.value })} className={field} />
        <Msg m={pwMsg} />
        <button className="rounded-xl bg-navy px-6 py-2.5 text-sm text-white">تغییر رمز</button>
      </form>
    </div>
  );
}
