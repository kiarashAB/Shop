import { Link, useSearchParams } from 'react-router-dom';
import StatusBadge from '../../components/StatusBadge';
import useApi from '../../hooks/useApi';
import { formatToman } from '../../lib/format';
import { PAYMENT_STATE, formatDate } from '../../lib/labels';

export default function Orders() {
  const [params, setParams] = useSearchParams();
  const page = Number(params.get('page') || 1);
  const { data, loading, error } = useApi('/my/orders', { page, pageSize: 10 });
  const items = data?.items ?? [];
  const pages = Math.max(1, Math.ceil((data?.total ?? 0) / (data?.pageSize ?? 10)));

  return (
    <div>
      <h1 className="mb-6 text-2xl font-black">سفارش‌های من</h1>
      {loading && <p className="py-10 text-center text-gray-400">در حال بارگذاری…</p>}
      {error && <p className="py-10 text-center text-red-600">خطا در دریافت سفارش‌ها.</p>}
      {!loading && !error && items.length === 0 && <p className="py-10 text-center text-gray-500">سفارشی وجود ندارد. <Link to="/shop" className="text-brand">شروع خرید</Link></p>}
      {items.length > 0 && (
        <div className="overflow-x-auto rounded-3xl bg-white shadow-[0_8px_24px_rgba(0,0,0,.06)]">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="text-gray-500"><tr className="text-start">
              {['شماره سفارش', 'تاریخ', 'مبلغ', 'پرداخت', 'وضعیت', ''].map((h) => <th key={h} className="px-4 py-3 text-start font-normal">{h}</th>)}
            </tr></thead>
            <tbody>
              {items.map((o) => (
                <tr key={o.id} className="border-t border-gray-100">
                  <td className="px-4 py-3 font-medium">{o.number}</td>
                  <td className="px-4 py-3">{formatDate(o.createdAt)}</td>
                  <td className="px-4 py-3">{formatToman(o.total)}</td>
                  <td className="px-4 py-3">{PAYMENT_STATE[o.paymentState]}</td>
                  <td className="px-4 py-3"><StatusBadge status={o.status} /></td>
                  <td className="px-4 py-3"><Link to={`/account/orders/${o.id}`} className="text-brand">جزئیات</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pages > 1 && (
        <div className="mt-6 flex justify-center gap-2">
          {Array.from({ length: pages }, (_, i) => i + 1).map((n) => (
            <button key={n} onClick={() => setParams({ page: n })} className={`h-9 w-9 rounded-lg text-sm ${n === page ? 'bg-brand text-white' : 'border border-gray-200'}`}>{n.toLocaleString('fa-IR')}</button>
          ))}
        </div>
      )}
    </div>
  );
}
