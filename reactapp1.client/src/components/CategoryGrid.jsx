import { Link } from 'react-router-dom';
import useApi from '../hooks/useApi';
import { formatNumber } from '../lib/format';
import { ArrowLeft } from './Icons';
import SectionTitle from './SectionTitle';

export default function CategoryGrid() {
  const { data } = useApi('/categories');
  const items = (data ?? []).filter((c) => !c.parentId);
  if (items.length === 0) return null;

  return (
    <section className="mt-16 bg-surface py-14">
      <div className="mx-auto max-w-[1440px] px-6">
        <SectionTitle plain="دسته بندی" accent="محصولات" bg="Categories" />
        <div className="mt-10 grid grid-cols-2 gap-5 md:grid-cols-3 lg:grid-cols-5">
          {items.map((c) => (
            <Link key={c.id} to={`/shop?category=${c.slug}`} className="rounded-3xl bg-white p-4 shadow-[0_8px_24px_rgba(0,0,0,.06)] transition hover:-translate-y-1">
              <div className="aspect-square overflow-hidden rounded-2xl bg-gray-100">
                {c.imageUrl && <img src={c.imageUrl} alt={c.name} className="h-full w-full object-cover" />}
              </div>
              <div className="mt-4 w-fit bg-gradient-to-t from-sand from-40% to-transparent to-40% font-bold">{c.name}</div>
              <div className="mt-2 flex items-center justify-between text-xs text-gray-500">
                <span>{formatNumber(c.productCount)}+ محصول</span>
                <ArrowLeft className="text-brand" width={16} height={16} />
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
