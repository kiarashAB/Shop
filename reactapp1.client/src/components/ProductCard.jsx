import { Link } from 'react-router-dom';
import { formatNumber, formatToman } from '../lib/format';

export default function ProductCard({ p }) {
  const hasDiscount = p.discountPercent > 0;
  return (
    <Link to={`/product/${p.slug}`} className="group rounded-3xl bg-white p-4 shadow-[0_8px_24px_rgba(0,0,0,.06)] transition hover:-translate-y-1">
      <div className="relative aspect-[4/3] overflow-hidden rounded-2xl bg-gray-100">
        {p.imageUrl && <img src={p.imageUrl} alt={p.name} className="h-full w-full object-cover transition group-hover:scale-105" />}
        {hasDiscount && <span className="absolute left-2 top-2 rounded-full bg-brand px-2.5 py-0.5 text-xs text-white">٪{formatNumber(p.discountPercent)}</span>}
        {!p.inStock && <span className="absolute inset-x-0 bottom-0 bg-navy/80 py-1 text-center text-xs text-white">ناموجود</span>}
      </div>
      <div className="mt-3 line-clamp-2 min-h-10 text-sm leading-5">{p.name}</div>
      <div className="mt-2 flex items-baseline gap-2">
        <span className="font-bold">{formatToman(p.finalPrice)}</span>
        {hasDiscount && <span className="text-xs text-gray-400 line-through">{formatToman(p.price)}</span>}
      </div>
    </Link>
  );
}
