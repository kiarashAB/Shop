import { Link } from 'react-router-dom';
import useApi from '../hooks/useApi';
import { ArrowLeft } from './Icons';

export default function PromoCards() {
  const { data } = useApi('/banners', { placement: 'Promo' });
  const items = (data ?? []).slice(0, 3);
  if (items.length === 0) return null;

  return (
    <section className="mx-auto mt-16 grid max-w-[1440px] gap-5 px-6 md:grid-cols-3">
      {items.map((b) => (
        <Link
          key={b.id} to={b.linkUrl || '/shop'}
          className="relative flex h-[265px] items-center overflow-visible rounded-[28px] bg-navy pr-8 text-white"
          style={{ backgroundImage: 'repeating-linear-gradient(135deg, rgba(255,255,255,.04) 0 6px, transparent 6px 14px)' }}
        >
          <div className="z-10 flex flex-col items-start gap-4">
            {b.discountLabel && <span className="rounded-lg bg-brand px-3 py-1.5 text-sm">{b.discountLabel}</span>}
            <span className="text-3xl font-black">{b.title}</span>
            <span className="flex items-center gap-2 text-sm">{b.buttonText || 'مشاهده محصولات'} <ArrowLeft /></span>
          </div>
          <img src={b.imageUrl} alt={b.title} className="absolute -top-8 bottom-0 left-0 h-[calc(100%+2rem)] max-w-[45%] rounded-bl-[28px] object-contain object-bottom" />
        </Link>
      ))}
    </section>
  );
}
