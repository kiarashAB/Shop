import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import api from '../../api/client';
import OrderSummary from '../../components/OrderSummary';
import StatusBadge from '../../components/StatusBadge';
import useApi from '../../hooks/useApi';
import { errMsg } from '../../lib/errors';
import { formatNumber, formatToman } from '../../lib/format';
import { PAYMENT_STATE, formatDateTime } from '../../lib/labels';

export default function OrderDetail() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const [version, setVersion] = useState(0);
  const { data: o, loading, error } = useApi(`/my/orders/${id}`, { v: version });
  const [reason, setReason] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [msg, setMsg] = useState('');
  const [busy, setBusy] = useState(false);

  if (loading && !o) return <p className="py-10 text-center text-gray-400">در حال بارگذاری…</p>;
  if (error || !o) return <p className="py-10 text-center text-red-600">سفارش یافت نشد. <Link to="/account/orders" className="text-brand">بازگشت</Link></p>;

  const cancel = async () => {
    setBusy(true); setMsg('');
    try { await api.post(`/my/orders/${id}/cancel`, { reason }); setCancelling(false); setVersion((v) => v + 1); }
    catch (err) { setMsg(errMsg(err)); }
    finally { setBusy(false); }
  };
  const canCancel = (o.status === 'PendingPayment' || o.status === 'Processing') && o.paidAmount === 0;

  return (
    <div className="space-y-6">
      {params.get('placed') && (
        <div className="rounded-2xl bg-green-50 px-5 py-4 text-sm text-green-800">سفارش شما با شماره <b>{o.number}</b> ثبت شد. پرداخت با هماهنگی فروشگاه انجام می‌شود.</div>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-black">سفارش {o.number}</h1>
        <div className="flex items-center gap-3"><StatusBadge status={o.status} /><span className="text-sm text-gray-500">{PAYMENT_STATE[o.paymentState]}</span></div>
      </div>

      <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
        <h2 className="mb-4 font-bold">کالاها</h2>
        <ul className="divide-y divide-gray-100">
          {o.items.map((i) => (
            <li key={i.productId} className="flex items-center gap-4 py-3 text-sm">
              <div className="h-14 w-14 shrink-0 overflow-hidden rounded-xl bg-gray-100">{i.imageUrl && <img src={i.imageUrl} alt={i.productName} className="h-full w-full object-cover" loading="lazy" />}</div>
              <div className="flex-1">{i.slug ? <Link to={`/product/${i.slug}`} className="hover:text-brand">{i.productName}</Link> : i.productName}</div>
              <div className="text-gray-500">{formatNumber(i.quantity)} × {formatToman(i.unitPrice)}</div>
              <div className="w-32 text-start font-bold">{formatToman(i.lineTotal)}</div>
            </li>
          ))}
        </ul>
        <div className="mt-4 max-w-sm"><OrderSummary quote={{ subtotal: o.subtotal, discountAmount: o.discountAmount, discountCode: o.discountCode, shippingCost: o.shippingCost, total: o.total }} /></div>
        {o.paidAmount > 0 && <p className="mt-3 text-sm text-gray-500">پرداخت‌شده: {formatToman(o.paidAmount)}</p>}
      </section>

      <div className="grid gap-6 md:grid-cols-2">
        <section className="rounded-3xl bg-white p-6 text-sm leading-7 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <h2 className="mb-2 font-bold">آدرس تحویل</h2>
          {o.recipientName} ({o.phone})<br />{o.province}، {o.city}، {o.fullAddress}
          {o.postalCode && <><br />کد پستی: {o.postalCode}</>}
          <div className="mt-2 text-gray-500">روش ارسال: {o.shippingMethodName}</div>
          {o.shipment && <div className="mt-3 rounded-xl bg-indigo-50 px-3 py-2 text-indigo-800">ارسال با {o.shipment.carrier} — کد رهگیری: <b dir="ltr">{o.shipment.trackingCode}</b></div>}
        </section>
        <section className="rounded-3xl bg-white p-6 text-sm shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <h2 className="mb-3 font-bold">تاریخچه سفارش</h2>
          <ol className="space-y-2">
            {o.history.map((h, i) => (
              <li key={i} className="flex items-center justify-between gap-3"><span><StatusBadge status={h.status} />{h.note && <span className="ms-2 text-gray-500">{h.note}</span>}</span><span className="text-xs text-gray-400">{formatDateTime(h.at)}</span></li>
            ))}
          </ol>
        </section>
      </div>

      {canCancel && (
        <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          {!cancelling ? <button onClick={() => setCancelling(true)} className="text-sm text-red-600">لغو سفارش</button> : (
            <div className="space-y-3">
              <p className="text-sm">با لغو سفارش، موجودی کالاها آزاد می‌شود. این کار قابل بازگشت نیست.</p>
              <input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="دلیل لغو" className="w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm outline-none focus:border-brand" />
              {msg && <p className="text-sm text-red-600">{msg}</p>}
              <div className="flex gap-3">
                <button onClick={cancel} disabled={busy || !reason.trim()} className="rounded-xl bg-red-600 px-5 py-2.5 text-sm text-white disabled:opacity-50">{busy ? 'در حال لغو…' : 'تأیید لغو'}</button>
                <button onClick={() => setCancelling(false)} className="rounded-xl border border-gray-300 px-5 py-2.5 text-sm">انصراف</button>
              </div>
            </div>
          )}
        </section>
      )}
      {o.status === 'Cancelled' && o.cancelReason && <p className="text-sm text-gray-500">دلیل لغو: {o.cancelReason}</p>}
    </div>
  );
}
