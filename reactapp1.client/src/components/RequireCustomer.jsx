import { Link, Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

// صفحات مشتری: بدون ورود به /login می‌رود و بعد از ورود برمی‌گردد. کاربر کارمندی حساب مشتری ندارد.
export default function RequireCustomer({ children }) {
  const { user, ready, isCustomer } = useAuth();
  const loc = useLocation();
  if (!ready) return <p className="py-32 text-center text-gray-400">در حال بارگذاری…</p>;
  if (!user) return <Navigate to={`/login?next=${encodeURIComponent(loc.pathname + loc.search)}`} replace />;
  if (!isCustomer) {
    return (
      <div className="mx-auto max-w-xl py-24 text-center">
        <p className="text-gray-600">این بخش مخصوص حساب مشتری است. شما با حساب مدیریتی وارد شده‌اید.</p>
        <Link to="/" className="mt-6 inline-block rounded-xl bg-brand px-6 py-3 text-white">بازگشت به فروشگاه</Link>
      </div>
    );
  }
  return children;
}
