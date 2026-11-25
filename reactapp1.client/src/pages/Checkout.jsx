import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../api/client';
import AddressForm from '../components/AddressForm';
import OrderSummary from '../components/OrderSummary';
import SectionTitle from '../components/SectionTitle';
import { useCart } from '../context/CartContext';
import useApi from '../hooks/useApi';
import useQuote from '../hooks/useQuote';
import { errMsg } from '../lib/errors';
import { formatToman } from '../lib/format';

const newKey = () => (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`);

export default function Checkout() {
  const navigate = useNavigate();
  const { items, clear, discountCode, shippingId, setShippingId } = useCart();
  const { data: methods } = useApi('/shipping-methods');
  const [addresses, setAddresses] = useState(null);
  const [addressId, setAddressId] = useState(null);
  const [adding, setAdding] = useState(false);
  const [note, setNote] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const idemKey = useRef(newKey());   // تکرار همین درخواست (کلیک مجدد/قطعی شبکه) سفارش دوم نمی‌سازد

  const loadAddresses = async () => {
    const { data } = await api.get('/me/addresses');
    setAddresses(data);
    setAddressId((cur) => (data.some((a) => a.id === cur) ? cur : (data.find((a) => a.isDefault) ?? data[0])?.id ?? null));
  };
  useEffect(() => { loadAddresses().catch(() => setAddresses([])); }, []);
  useEffect(() => {
    if (methods?.length && !methods.some((m) => m.id === shippingId)) setShippingId(methods[0].id);
  }, [methods, shippingId, setShippingId]);

  const { quote, loading } = useQuote(items, discountCode, shippingId);

  if (items.length === 0 && !busy) {
    return (
      <div className="mx-auto max-w-xl py-28 text-center">
        <h1 className="text-2xl font-black">سبد خرید شما خالی است</h1>
        <Link to="/shop" className="mt-8 inline-block rounded-xl bg-brand px-6 py-3 text-white">مشاهده محصولات</Link>
      </div>
    );
  }

  const place = async () => {
    setError(''); setBusy(true);
    try {
      const { data } = await api.post('/my/orders', {
        addressId, shippingMethodId: shippingId, discountCode: discountCode || null, note: note || null,
        items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })), idempotencyKey: idemKey.current,
      });
      clear();
      navigate(`/account/orders/${data.id}?placed=1`, { replace: true });
    } catch (err) {
      setError(errMsg(err, 'ثبت سفارش انجام نشد.'));
      setBusy(false);
    }
  };

  const canPlace = !busy && !loading && addressId && shippingId && quote?.canCheckout;

  return (
    <div className="mx-auto max-w-[1440px] px-6 py-10">
      <SectionTitle plain="تسویه" accent="حساب" bg="Checkout" />
      <div className="mt-10 grid gap-8 lg:grid-cols-[1fr_380px]">
        <div className="space-y-8">
          <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
            <h2 className="mb-4 font-bold">۱. آدرس تحویل</h2>
            {addresses === null && <p className="text-sm text-gray-400">در حال بارگذاری…</p>}
            {addresses?.length > 0 && (
              <div className="space-y-2">
                {addresses.map((a) => (
                  <label key={a.id} className="flex cursor-pointer gap-3 rounded-xl border border-gray-200 p-3 text-sm has-[:checked]:border-brand">
                    <input type="radio" name="addr" checked={addressId === a.id} onChange={() => setAddressId(a.id)} className="mt-1" />
                    <span>
                      <b>{a.title}</b> — {a.recipientName} ({a.phone})<br />
                      <span className="text-gray-600">{a.province}، {a.city}، {a.fullAddress}</span>
                    </span>
                  </label>
                ))}
              </div>
            )}
            {adding ? (
              <div className="mt-4">
                <AddressForm onSubmit={async (dto) => { const { data } = await api.post('/me/addresses', dto); await loadAddresses(); setAddressId(data.id); setAdding(false); }}
                  onCancel={() => setAdding(false)} />
              </div>
            ) : (
              <button onClick={() => setAdding(true)} className="mt-4 text-sm text-brand">+ افزودن آدرس جدید</button>
            )}
          </section>

          <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
            <h2 className="mb-4 font-bold">۲. روش ارسال</h2>
            <div className="space-y-2">
              {(methods ?? []).map((m) => (
                <label key={m.id} className="flex cursor-pointer items-center justify-between rounded-xl border border-gray-200 p-3 text-sm has-[:checked]:border-brand">
                  <span className="flex items-center gap-2"><input type="radio" name="ship" checked={shippingId === m.id} onChange={() => setShippingId(m.id)} />
                    {m.name}{m.description && <span className="text-gray-400"> — {m.description}</span>}</span>
                  <span>{m.price > 0 ? formatToman(m.price) : 'رایگان'}</span>
                </label>
              ))}
              {methods?.length === 0 && <p className="text-sm text-red-600">هنوز روش ارسالی توسط فروشگاه تعریف نشده است.</p>}
            </div>
          </section>

          <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
            <h2 className="mb-4 font-bold">۳. توضیحات و پرداخت</h2>
            <textarea rows={3} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} placeholder="توضیحات سفارش (اختیاری)"
              className="w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm outline-none focus:border-brand" />
            <p className="mt-4 rounded-xl bg-amber-50 px-4 py-3 text-sm leading-7 text-amber-800">
              پرداخت آنلاین هنوز به درگاه متصل نشده است. سفارش شما با وضعیت «در انتظار پرداخت» ثبت می‌شود و موجودی برایتان رزرو می‌ماند؛
              پرداخت با هماهنگی فروشگاه ثبت و سفارش پردازش می‌شود.
            </p>
          </section>
        </div>

        <aside className="h-fit space-y-5 rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <h2 className="font-bold">خلاصه سفارش</h2>
          <ul className="space-y-2 text-sm">
            {(quote?.lines ?? []).map((l) => (
              <li key={l.productId} className="flex justify-between gap-3">
                <span className="text-gray-600">{l.name} × {l.quantity.toLocaleString('fa-IR')}</span>
                <span className="whitespace-nowrap">{formatToman(l.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <div className={loading ? 'opacity-50' : ''}><OrderSummary quote={quote} /></div>
          {quote?.discountError && <p className="text-xs text-red-600">{quote.discountError}</p>}
          {quote && !quote.canCheckout && <p className="text-xs text-red-600">برخی کالاها مشکل موجودی دارند. <Link to="/cart" className="underline">بازگشت به سبد</Link></p>}
          {error && <p className="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-600">{error}</p>}
          <button onClick={place} disabled={!canPlace}
            className="w-full rounded-2xl bg-brand py-3.5 font-bold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40">
            {busy ? 'در حال ثبت سفارش…' : 'تأیید و ثبت سفارش'}
          </button>
        </aside>
      </div>
    </div>
  );
}
