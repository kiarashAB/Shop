import { Link } from 'react-router-dom';
import StatusBadge from '../../components/StatusBadge';
import { useAuth } from '../../context/AuthContext';
import useApi from '../../hooks/useApi';
import { formatToman } from '../../lib/format';
import { formatDate } from '../../lib/labels';

export default function Dashboard() {
  const { user } = useAuth();
  const { data, loading, error } = useApi('/my/orders', { page: 1, pageSize: 5 });
  const items = data?.items ?? [];
  const active = items.filter((o) => o.status === 'PendingPayment' || o.status === 'Processing' || o.status === 'Shipped').length;

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-black">سلام {user.fullName} 👋</h1>
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="rounded-3xl bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,.06)]"><div className="text-sm text-gray-500">کل سفارش‌ها</div><div className="mt-2 text-3xl font-black">{(data?.total ?? 0).toLocaleString('fa-IR')}</div></div>
        <div className="rounded-3xl bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,.06)]"><div className="text-sm text-gray-500">در جریان (از ۵ سفارش اخیر)</div><div className="mt-2 text-3xl font-black">{active.toLocaleString('fa-IR')}</div></div>
        <Link to="/account/addresses" className="rounded-3xl bg-white p-5 shadow-[0_8px_24px_rgba(0,0,0,.06)] hover:text-brand"><div className="text-sm text-gray-500">دسترسی سریع</div><div className="mt-3 font-bold">مدیریت آدرس‌ها ←</div></Link>
      </div>

      <section className="rounded-3xl bg-white p-6 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
        <div className="mb-4 flex items-center justify-between"><h2 className="font-bold">سفارش‌های اخیر</h2><Link to="/account/orders" className="text-sm text-brand">همه سفارش‌ها</Link></div>
        {loading && <p className="text-sm text-gray-400">در حال بارگذاری…</p>}
        {error && <p className="text-sm text-red-600">خطا در دریافت سفارش‌ها.</p>}
        {!loading && !error && items.length === 0 && <p className="text-sm text-gray-500">هنوز سفارشی ثبت نکرده‌اید. <Link to="/shop" className="text-brand">مشاهده محصولات</Link></p>}
        <ul className="divide-y divide-gray-100">
          {items.map((o) => (
            <li key={o.id}><Link to={`/account/orders/${o.id}`} className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm hover:text-brand">
              <span>{o.number} · {formatDate(o.createdAt)}</span><StatusBadge status={o.status} /><span className="font-bold">{formatToman(o.total)}</span>
            </Link></li>
          ))}
        </ul>
      </section>
    </div>
  );
}
