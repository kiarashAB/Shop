import { Link } from 'react-router-dom';
import useApi from '../hooks/useApi';
import { DoubleChevronLeft } from './Icons';

export default function StoryRow() {
  const { data } = useApi('/products', { sort: 'new', pageSize: 12 });
  const items = data?.items ?? [];
  if (items.length === 0) return null;

  return (
    <section className="mx-auto mt-2 flex max-w-[1440px] items-start gap-4 overflow-x-auto px-6 pb-2">
      <Link to="/shop?sort=new" className="flex h-[140px] w-36 shrink-0 flex-col justify-between rounded-2xl bg-gradient-to-b from-red-50 to-white p-4 text-brand">
        <div className="font-bold leading-7">تازه‌های<br />فروشگاه!</div>
        <DoubleChevronLeft className="text-red-300" />
      </Link>
      {items.map((p) => (
        <Link key={p.id} to={`/product/${p.slug}`} className="flex w-[86px] shrink-0 flex-col items-center gap-2">
          <span className="h-[86px] w-[86px] overflow-hidden rounded-full border-2 border-brand p-[3px]">
            {p.imageUrl && <img src={p.imageUrl} alt={p.name} className="h-full w-full rounded-full object-cover" />}
          </span>
          <span className="line-clamp-2 text-center text-[11px] leading-4 text-gray-600">{p.name}</span>
        </Link>
      ))}
    </section>
  );
}
