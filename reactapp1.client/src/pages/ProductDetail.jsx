import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft } from '../components/Icons';
import useApi from '../hooks/useApi';
import { formatNumber, formatToman } from '../lib/format';

export default function ProductDetail() {
  const { slug } = useParams();
  const { data: p, loading, error } = useApi(`/products/${slug}`);
  const [activeImg, setActiveImg] = useState(0);

  if (loading) return <p className="py-32 text-center text-gray-400">در حال بارگذاری…</p>;
  if (error?.response?.status === 404 || (!loading && !p)) {
    return (
      <div className="mx-auto max-w-xl py-32 text-center">
        <h1 className="text-2xl font-black">محصول یافت نشد</h1>
        <Link to="/shop" className="mt-6 inline-block rounded-xl bg-brand px-6 py-3 text-white">بازگشت به فروشگاه</Link>
      </div>
    );
  }
  if (error) return <p className="py-32 text-center text-red-500">خطا در دریافت اطلاعات محصول.</p>;

  const hasDiscount = p.discountPercent > 0;
  const images = p.images?.length ? p.images : [{ url: null, alt: p.name }];

  return (
    <div className="mx-auto max-w-[1440px] px-6 py-10">
      <nav className="mb-6 flex items-center gap-2 text-sm text-gray-500">
        <Link to="/" className="hover:text-brand">خانه</Link> /
        <Link to={`/shop?category=${p.categorySlug}`} className="hover:text-brand">{p.categoryName}</Link> /
        <span className="text-gray-700">{p.name}</span>
      </nav>

      <div className="grid grid-cols-1 gap-10 md:grid-cols-2">
        <div>
          <div className="aspect-square overflow-hidden rounded-3xl bg-gray-100">
            {images[activeImg]?.url && <img src={images[activeImg].url} alt={images[activeImg].alt || p.name} className="h-full w-full object-cover" />}
          </div>
          {images.length > 1 && (
            <div className="mt-4 flex gap-3">
              {images.map((img, i) => (
                <button key={i} onClick={() => setActiveImg(i)} className={`h-16 w-16 overflow-hidden rounded-xl border-2 ${i === activeImg ? 'border-brand' : 'border-transparent'} bg-gray-100`}>
                  {img.url && <img src={img.url} alt={img.alt || ''} className="h-full w-full object-cover" />}
                </button>
              ))}
            </div>
          )}
        </div>

        <div>
          {p.brandName && <div className="mb-2 text-sm text-gray-500">{p.brandName}</div>}
          <h1 className="text-2xl font-black leading-snug">{p.name}</h1>
          <div className="mt-4 text-sm text-gray-500">کد کالا: {p.sku}{p.partNumber ? ` · ${p.partNumber}` : ''}</div>

          <div className="mt-6 flex items-baseline gap-3">
            <span className="text-3xl font-black">{formatToman(p.finalPrice)}</span>
            {hasDiscount && (
              <>
                <span className="text-base text-gray-400 line-through">{formatToman(p.price)}</span>
                <span className="rounded-full bg-brand px-2.5 py-0.5 text-xs text-white">٪{formatNumber(p.discountPercent)} تخفیف</span>
              </>
            )}
          </div>

          <div className={`mt-4 w-fit rounded-xl px-4 py-2 text-sm ${p.inStock ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
            {p.inStock ? 'موجود در انبار' : 'ناموجود'}
          </div>

          {p.warranty && <div className="mt-4 text-sm text-gray-600">گارانتی: {p.warranty}</div>}
          <div className="mt-2 text-sm text-gray-600">وضعیت: {p.condition === 'New' ? 'نو' : p.condition}</div>

          <button
            disabled={!p.inStock}
            className="mt-8 flex w-full items-center justify-center gap-2 rounded-2xl bg-brand py-4 font-bold text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
          >
            افزودن به سبد خرید <ArrowLeft />
          </button>

          {p.description && (
            <div className="mt-10">
              <h2 className="mb-3 border-b border-gray-200 pb-2 font-bold">توضیحات محصول</h2>
              <p className="text-sm leading-7 text-gray-600 whitespace-pre-line">{p.description}</p>
            </div>
          )}

          {p.specifications?.length > 0 && (
            <div className="mt-10">
              <h2 className="mb-3 border-b border-gray-200 pb-2 font-bold">مشخصات فنی</h2>
              <table className="w-full text-sm">
                <tbody>
                  {p.specifications.map((s, i) => (
                    <tr key={i} className={i % 2 ? 'bg-gray-50' : ''}>
                      <td className="py-2 pr-3 text-gray-500">{s.name}</td>
                      <td className="py-2 font-medium">{s.value}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
