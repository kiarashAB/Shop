import { useSearchParams } from 'react-router-dom';
import ProductCard from '../components/ProductCard';
import SectionTitle from '../components/SectionTitle';
import useApi from '../hooks/useApi';

const SORTS = [
  { value: 'new', label: 'جدیدترین' },
  { value: 'best', label: 'پرفروش‌ترین' },
  { value: 'price', label: 'ارزان‌ترین' },
];

export default function Shop() {
  const [params, setParams] = useSearchParams();
  const category = params.get('category') || '';
  const search = params.get('search') || '';
  const discounted = params.get('discounted') === 'true';
  const sort = params.get('sort') || 'new';
  const page = Number(params.get('page') || 1);

  const { data: categories } = useApi('/categories');
  const { data, loading, error } = useApi('/products', { categorySlug: category || undefined, search: search || undefined, discounted, sort, page, pageSize: 12 });

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const pageSize = data?.pageSize ?? 12;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  const update = (patch) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) {
      if (v === '' || v === false || v === undefined || v === null) next.delete(k);
      else next.set(k, v);
    }
    if (!('page' in patch)) next.delete('page');
    setParams(next);
  };

  const activeCategory = categories?.find((c) => c.slug === category);

  return (
    <div className="mx-auto max-w-[1440px] px-6 py-10">
      <SectionTitle
        plain={activeCategory ? activeCategory.name : search ? `نتایج جستجوی «${search}»` : 'همه'}
        accent={activeCategory || search ? '' : 'محصولات'}
        bg="Shop"
      />

      <div className="mt-8 grid grid-cols-1 gap-8 md:grid-cols-[220px_1fr]">
        <aside className="space-y-6">
          <div>
            <h3 className="mb-3 font-bold">دسته‌بندی‌ها</h3>
            <ul className="space-y-2 text-sm">
              <li>
                <button onClick={() => update({ category: '' })} className={`hover:text-brand ${!category ? 'font-bold text-brand' : ''}`}>
                  همه محصولات
                </button>
              </li>
              {(categories ?? []).filter((c) => !c.parentId).map((c) => (
                <li key={c.id}>
                  <button onClick={() => update({ category: c.slug })} className={`hover:text-brand ${category === c.slug ? 'font-bold text-brand' : ''}`}>
                    {c.name}
                  </button>
                </li>
              ))}
            </ul>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={discounted} onChange={(e) => update({ discounted: e.target.checked })} />
            فقط کالاهای تخفیف‌دار
          </label>
        </aside>

        <div>
          <div className="mb-6 flex items-center justify-between">
            <span className="text-sm text-gray-500">{total.toLocaleString('fa-IR')} محصول</span>
            <select value={sort} onChange={(e) => update({ sort: e.target.value })} className="rounded-xl border border-gray-200 px-3 py-2 text-sm">
              {SORTS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </div>

          {loading && <p className="py-20 text-center text-gray-400">در حال بارگذاری…</p>}
          {error && <p className="py-20 text-center text-red-500">خطا در دریافت محصولات. دوباره تلاش کنید.</p>}
          {!loading && !error && items.length === 0 && <p className="py-20 text-center text-gray-400">محصولی یافت نشد.</p>}

          {items.length > 0 && (
            <div className="grid grid-cols-2 gap-5 md:grid-cols-3 lg:grid-cols-4">
              {items.map((p) => <ProductCard key={p.id} p={p} />)}
            </div>
          )}

          {pageCount > 1 && (
            <div className="mt-10 flex items-center justify-center gap-2">
              {Array.from({ length: pageCount }, (_, i) => i + 1).map((n) => (
                <button
                  key={n} onClick={() => update({ page: n })}
                  className={`h-9 w-9 rounded-lg text-sm ${n === page ? 'bg-brand text-white' : 'border border-gray-200 hover:border-brand'}`}
                >
                  {n.toLocaleString('fa-IR')}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
