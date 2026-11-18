import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import OrderSummary from '../components/OrderSummary';
import SectionTitle from '../components/SectionTitle';
import { useCart } from '../context/CartContext';
import useApi from '../hooks/useApi';
import useQuote from '../hooks/useQuote';
import { formatNumber, formatToman } from '../lib/format';

export default function Cart() {
  const navigate = useNavigate();
  const { items, setQty, remove, discountCode, setDiscountCode, shippingId, setShippingId, MAX_QTY } = useCart();
  const { data: methods } = useApi('/shipping-methods');
  const [codeInput, setCodeInput] = useState(discountCode);

  // اگر روش ارسالی انتخاب نشده یا دیگر فعال نیست، اولین روش فعال انتخاب می‌شود.
  useEffect(() => {
    if (methods?.length && !methods.some((m) => m.id === shippingId)) setShippingId(methods[0].id);
  }, [methods, shippingId, setShippingId]);

  const { quote, loading, error } = useQuote(items, discountCode, shippingId);

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-xl py-28 text-center">
        <h1 className="text-2xl font-black">سبد خرید شما خالی است</h1>
        <Link to="/shop" className="mt-8 inline-block rounded-xl bg-brand px-6 py-3 text-white">مشاهده محصولات</Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1440px] px-6 py-10">
      <SectionTitle plain="سبد" accent="خرید" bg="Cart" />
      {error && <p className="mt-8 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-600">خطا در محاسبه سبد. دوباره تلاش کنید.</p>}

      <div className="mt-10 grid gap-8 lg:grid-cols-[1fr_360px]">
        <ul className="space-y-4">
          {(quote?.lines ?? items.map((i) => ({ productId: i.productId, quantity: i.quantity, name: '…', slug: '', unitPrice: 0, lineTotal: 0 }))).map((l) => (
            <li key={l.productId} className="flex gap-4 rounded-3xl bg-white p-4 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
              <div className="h-24 w-24 shrink-0 overflow-hidden rounded-2xl bg-gray-100">
                {l.imageUrl && <img src={l.imageUrl} alt={l.name} className="h-full w-full object-cover" loading="lazy" />}
              </div>
              <div className="flex flex-1 flex-col justify-between">
                <div>
                  {l.slug ? <Link to={`/product/${l.slug}`} className="font-medium hover:text-brand">{l.name}</Link> : <span>{l.name}</span>}
                  {l.problem && <p className="mt-1 text-xs text-red-600">{l.problem}</p>}
                </div>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                  <div className="flex items-center rounded-xl border border-gray-200">
                    <button onClick={() => setQty(l.productId, l.quantity - 1)} className="h-9 w-9 hover:text-brand" aria-label="کاهش">−</button>
                    <span className="w-9 text-center text-sm">{formatNumber(l.quantity)}</span>
                    <button onClick={() => setQty(l.productId, Math.min(l.quantity + 1, MAX_QTY))} className="h-9 w-9 hover:text-brand" aria-label="افزایش">+</button>
                  </div>
                  <div className="text-sm">
                    {l.unitPrice > 0 && <span className="text-gray-400">{formatToman(l.unitPrice)} × {formatNumber(l.quantity)} = </span>}
                    <span className="font-bold">{l.lineTotal > 0 ? formatToman(l.lineTotal) : ''}</span>
                  </div>
                  <button onClick={() => remove(l.productId)} className="text-xs text-gray-400 hover:text-red-600">حذف</button>
                </div>
              </div>
            </li>
          ))}
        </ul>

        <aside className="h-fit space-y-6 rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <div>
            <label className="mb-2 block text-sm font-bold">کد تخفیف</label>
            <div className="flex gap-2">
              <input value={codeInput} onChange={(e) => setCodeInput(e.target.value)} placeholder="مثلاً WELCOME10" dir="ltr"
                className="min-w-0 flex-1 rounded-xl border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand" />
              {discountCode
                ? <button onClick={() => { setDiscountCode(''); setCodeInput(''); }} className="rounded-xl border border-gray-300 px-4 text-sm">حذف</button>
                : <button onClick={() => setDiscountCode(codeInput.trim())} className="rounded-xl bg-navy px-4 text-sm text-white">اعمال</button>}
            </div>
            {quote?.discountError && <p className="mt-2 text-xs text-red-600">{quote.discountError}</p>}
          </div>

          {methods?.length > 0 && (
            <div>
              <div className="mb-2 text-sm font-bold">روش ارسال</div>
              <div className="space-y-2">
                {methods.map((m) => (
                  <label key={m.id} className="flex cursor-pointer items-center justify-between gap-3 rounded-xl border border-gray-200 px-3 py-2 text-sm has-[:checked]:border-brand">
                    <span className="flex items-center gap-2">
                      <input type="radio" name="ship" checked={shippingId === m.id} onChange={() => setShippingId(m.id)} /> {m.name}
                    </span>
                    <span className="text-gray-500">{m.price > 0 ? formatToman(m.price) : 'رایگان'}</span>
                  </label>
                ))}
              </div>
            </div>
          )}

          <div className={loading ? 'opacity-50' : ''}><OrderSummary quote={quote} /></div>

          <button disabled={!quote?.canCheckout || loading} onClick={() => navigate('/checkout')}
            className="w-full rounded-2xl bg-brand py-3.5 font-bold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40">
            ادامه به تسویه‌حساب
          </button>
          {quote && !quote.canCheckout && <p className="text-xs text-red-600">برای ادامه، ردیف‌های دارای مشکل را اصلاح یا حذف کنید.</p>}
          <Link to="/shop" className="block text-center text-sm text-gray-500 hover:text-brand">ادامه خرید</Link>
        </aside>
      </div>
    </div>
  );
}
