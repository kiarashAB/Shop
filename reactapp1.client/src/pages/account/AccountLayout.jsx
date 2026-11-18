import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

const links = [
  ['/account', 'داشبورد', true],
  ['/account/orders', 'سفارش‌های من'],
  ['/account/addresses', 'آدرس‌های من'],
  ['/account/profile', 'پروفایل و رمز عبور'],
];

export default function AccountLayout() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  return (
    <div className="mx-auto grid max-w-[1440px] gap-8 px-6 py-10 md:grid-cols-[240px_1fr]">
      <aside className="h-fit rounded-3xl bg-white p-4 shadow-[0_8px_24px_rgba(0,0,0,.06)]">
        <nav className="flex flex-row flex-wrap gap-1 md:flex-col">
          {links.map(([to, label, end]) => (
            <NavLink key={to} to={to} end={end}
              className={({ isActive }) => `rounded-xl px-4 py-2.5 text-sm ${isActive ? 'bg-red-50 font-bold text-brand' : 'hover:bg-surface'}`}>{label}</NavLink>
          ))}
          <button onClick={async () => { await logout(); navigate('/'); }} className="rounded-xl px-4 py-2.5 text-start text-sm text-gray-500 hover:bg-surface">خروج از حساب</button>
        </nav>
      </aside>
      <section className="min-w-0"><Outlet /></section>
    </div>
  );
}
