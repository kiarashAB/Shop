import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import useApi from '../hooks/useApi';
import { ArrowLeft, ChevronLeft, ChevronRight } from './Icons';

export default function HeroSlider() {
  const { data } = useApi('/banners', { placement: 'Hero' });
  const slides = data ?? [];
  const [i, setI] = useState(0);

  useEffect(() => {
    if (slides.length < 2) return;
    const t = setInterval(() => setI((n) => (n + 1) % slides.length), 6000);
    return () => clearInterval(t);
  }, [slides.length]);

  if (slides.length === 0) return null;
  const s = slides[i % slides.length];

  return (
    <section className="relative mx-auto mt-4 h-[460px] max-w-[1440px] overflow-hidden rounded-[44px] bg-gradient-to-br from-stone-200 to-stone-100">
      <img src={s.imageUrl} alt={s.title} className="absolute bottom-0 left-0 h-full max-w-[55%] object-contain object-bottom" />
      <div className="absolute inset-y-0 right-0 flex w-1/2 flex-col justify-center gap-6 pr-16">
        <h1 className="inline-block w-fit bg-gradient-to-t from-sand from-35% to-transparent to-35% text-5xl font-black leading-tight">{s.title}</h1>
        {s.discountLabel && <div className="text-4xl font-black text-brand">{s.subtitle} <span className="text-5xl">{s.discountLabel}</span></div>}
        {!s.discountLabel && s.subtitle && <p className="text-xl text-gray-600">{s.subtitle}</p>}
        {s.linkUrl && (
          <Link to={s.linkUrl} className="flex w-fit items-center gap-2 border-b border-navy/40 pb-1 text-lg hover:text-brand">
            {s.buttonText || 'مشاهده و خرید'} <ArrowLeft />
          </Link>
        )}
      </div>
      {slides.length > 1 && (
        <>
          <button onClick={() => setI((i + 1) % slides.length)} aria-label="بعدی" className="absolute left-2 top-1/2 -translate-y-1/2 p-3"><ChevronLeft /></button>
          <button onClick={() => setI((i - 1 + slides.length) % slides.length)} aria-label="قبلی" className="absolute right-2 top-1/2 -translate-y-1/2 p-3"><ChevronRight /></button>
        </>
      )}
    </section>
  );
}
