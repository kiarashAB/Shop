import { useState } from 'react';
import { errMsg } from '../lib/errors';

const field = 'w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm outline-none focus:border-brand';
const empty = { title: '', recipientName: '', phone: '', province: '', city: '', postalCode: '', fullAddress: '', isDefault: false };

// فرم آدرس (ایجاد/ویرایش). اعتبارسنجی نهایی سمت سرور است و پیام خطا همان‌جا نمایش داده می‌شود.
export default function AddressForm({ initial, onSubmit, onCancel, submitLabel = 'ذخیره آدرس' }) {
  const [f, setF] = useState({ ...empty, ...(initial ?? {}), postalCode: initial?.postalCode ?? '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => setF((v) => ({ ...v, [k]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const submit = async (e) => {
    e.preventDefault();
    setError(''); setBusy(true);
    try { await onSubmit({ ...f, postalCode: f.postalCode || null }); }
    catch (err) { setError(errMsg(err)); }
    finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="grid gap-3 sm:grid-cols-2">
      <input required placeholder="عنوان (مثلاً خانه)" value={f.title} onChange={set('title')} className={field} />
      <input required placeholder="نام گیرنده" value={f.recipientName} onChange={set('recipientName')} className={field} />
      <input required inputMode="tel" placeholder="موبایل گیرنده" value={f.phone} onChange={set('phone')} className={field} />
      <input inputMode="numeric" placeholder="کد پستی (۱۰ رقم)" value={f.postalCode} onChange={set('postalCode')} className={field} />
      <input required placeholder="استان" value={f.province} onChange={set('province')} className={field} />
      <input required placeholder="شهر" value={f.city} onChange={set('city')} className={field} />
      <textarea required rows={3} placeholder="نشانی کامل" value={f.fullAddress} onChange={set('fullAddress')} className={`${field} sm:col-span-2`} />
      <label className="flex items-center gap-2 text-sm sm:col-span-2">
        <input type="checkbox" checked={f.isDefault} onChange={set('isDefault')} /> آدرس پیش‌فرض
      </label>
      {error && <p className="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-600 sm:col-span-2">{error}</p>}
      <div className="flex gap-3 sm:col-span-2">
        <button type="submit" disabled={busy} className="rounded-xl bg-brand px-6 py-2.5 text-sm text-white disabled:opacity-50">{busy ? 'در حال ذخیره…' : submitLabel}</button>
        {onCancel && <button type="button" onClick={onCancel} className="rounded-xl border border-gray-300 px-6 py-2.5 text-sm">انصراف</button>}
      </div>
    </form>
  );
}
