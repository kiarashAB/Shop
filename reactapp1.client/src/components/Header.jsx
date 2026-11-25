import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import useApi from '../hooks/useApi';
import { useAuth } from '../context/AuthContext';
import { useCart } from '../context/CartContext';
import { CartIcon, SearchIcon, UserIcon } from './Icons';

export default function Header() {
  const { data: categories } = useApi('/categories');
  const [q, setQ] = useState('');
  const navigate = useNavigate();
  const { user, logout, isCustomer } = useAuth();
  const { count } = useCart();

  const submit = (e) => {
    e.preventDefault();
    if (q.trim()) navigate(`/shop?search=${encodeURIComponent(q.trim())}`);
  };

  const navClass = ({ isActive }) => `whitespace-nowrap hover:text-brand ${isActive ? 'text-brand' : ''}`;

  return (
    <header className="mx-auto max-w-[1440px] px-6 pt-1">
      <div className="flex items-center gap-6 py-3">
        <Link to="/" className="border-b border-navy pb-0.5 text-3xl font-light tracking-wide whitespace-nowrap">
          گلرخ
        </Link>
        <form onSubmit={submit} className="flex h-12 flex-1 items-center gap-2 rounded-2xl border border-gray-200 bg-white px-4 shadow-sm">
          <SearchIcon className="text-gray-400" />
          <input
            value={q} onChange={(e) => setQ(e.target.value)} placeholder="جستجوی محصولات…"
            className="h-full flex-1 bg-transparent text-sm outline-none placeholder:text-gray-400"
          />
        </form>
        <div className="flex items-center gap-5 text-sm">
          {user ? (
            <div className="flex items-center gap-4">
              <Link to={isCustomer ? '/account' : '/login'} className="flex items-center gap-2 hover:text-brand whitespace-nowrap">
                <UserIcon /> {user.fullName}
              </Link>
              <button onClick={async () => { await logout(); navigate('/'); }} className="text-gray-500 hover:text-brand">خروج</button>
            </div>
          ) : (
            <Link to="/login" className="flex items-center gap-2 hover:text-brand whitespace-nowrap"><UserIcon /> ورود/ثبت‌نام</Link>
          )}
          <Link to="/cart" className="relative flex items-center gap-2 hover:text-brand whitespace-nowrap" aria-label="سبد خرید">
            <CartIcon /> سبد خرید
            {count > 0 && (
              <span className="absolute -top-2 right-3 flex h-5 min-w-5 items-center justify-center rounded-full bg-brand px-1 text-[11px] text-white">
                {count.toLocaleString('fa-IR')}
              </span>
            )}
          </Link>
        </div>
      </div>

      <nav className="flex items-center gap-8 py-4 text-[15px]">
        <NavLink to="/" end className={navClass}>صفحه اصلی</NavLink>
        {categories?.filter((c) => !c.parentId).map((c) => (
          <NavLink key={c.id} to={`/shop?category=${c.slug}`} className={navClass}>{c.name}</NavLink>
        ))}
        <Link to="/shop?discounted=true" className="mr-auto rounded-xl bg-red-50 px-4 py-2 text-sm text-brand whitespace-nowrap">پیشنهاد ویژه</Link>
      </nav>
    </header>
  );
}
