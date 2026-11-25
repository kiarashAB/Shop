import { useEffect, useState } from 'react';
import api from '../../api/client';
import AddressForm from '../../components/AddressForm';
import { errMsg } from '../../lib/errors';

export default function Addresses() {
  const [list, setList] = useState(null);
  const [editing, setEditing] = useState(null);   // null | 'new' | address
  const [error, setError] = useState('');

  const load = () => api.get('/me/addresses').then((r) => setList(r.data)).catch((e) => { setList([]); setError(errMsg(e)); });
  useEffect(() => { load(); }, []);

  const act = async (fn) => { setError(''); try { await fn(); await load(); } catch (e) { setError(errMsg(e)); } };

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-black">آدرس‌های من</h1>
        {!editing && <button onClick={() => setEditing('new')} className="rounded-xl bg-brand px-5 py-2.5 text-sm text-white">+ آدرس جدید</button>}
      </div>
      {error && <p className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-600">{error}</p>}

      {editing && (
        <div className="mb-6 rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <h2 className="mb-4 font-bold">{editing === 'new' ? 'افزودن آدرس' : 'ویرایش آدرس'}</h2>
          <AddressForm initial={editing === 'new' ? null : editing}
            onSubmit={async (dto) => {
              if (editing === 'new') await api.post('/me/addresses', dto); else await api.put(`/me/addresses/${editing.id}`, dto);
              setEditing(null); await load();
            }}
            onCancel={() => setEditing(null)} />
        </div>
      )}

      {list === null && <p className="py-10 text-center text-gray-400">در حال بارگذاری…</p>}
      {list?.length === 0 && !editing && <p className="py-10 text-center text-gray-500">آدرسی ثبت نشده است.</p>}
      <ul className="grid gap-4 md:grid-cols-2">
        {(list ?? []).map((a) => (
          <li key={a.id} className="rounded-3xl bg-white p-5 text-sm leading-7 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
            <div className="flex items-center justify-between"><b>{a.title}</b>{a.isDefault && <span className="rounded-full bg-red-50 px-3 py-0.5 text-xs text-brand">پیش‌فرض</span>}</div>
            <div>{a.recipientName} — {a.phone}</div>
            <div className="text-gray-600">{a.province}، {a.city}، {a.fullAddress}</div>
            {a.postalCode && <div className="text-gray-500">کد پستی: {a.postalCode}</div>}
            <div className="mt-3 flex gap-4 text-xs">
              <button onClick={() => setEditing(a)} className="text-brand">ویرایش</button>
              {!a.isDefault && <button onClick={() => act(() => api.post(`/me/addresses/${a.id}/default`))} className="text-gray-600">پیش‌فرض کن</button>}
              <button onClick={() => { if (window.confirm('این آدرس حذف شود؟')) act(() => api.delete(`/me/addresses/${a.id}`)); }} className="text-red-600">حذف</button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
