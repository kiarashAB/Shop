import { Link } from 'react-router-dom';
import CategoryGrid from '../components/CategoryGrid';
import HeroSlider from '../components/HeroSlider';
import ProductCard from '../components/ProductCard';
import PromoCards from '../components/PromoCards';
import SectionTitle from '../components/SectionTitle';
import StoryRow from '../components/StoryRow';
import useApi from '../hooks/useApi';
import { ArrowLeft } from '../components/Icons';

function BestSellers() {
  const { data, loading } = useApi('/products', { sort: 'best', pageSize: 8 });
  const items = data?.items ?? [];
  if (!loading && items.length === 0) return null;
  return (
    <section className="mx-auto mt-16 max-w-[1440px] px-6">
      <SectionTitle
        plain="پرفروش ترین" accent="محصولات" bg="Best sellers"
        action={<Link to="/shop?sort=best" className="mb-1 flex items-center gap-2 rounded-lg border border-brand px-4 py-2 text-sm text-brand">مشاهده و خرید <ArrowLeft /></Link>}
      />
      <div className="mt-10 grid grid-cols-2 gap-5 md:grid-cols-4">
        {items.map((p) => <ProductCard key={p.id} p={p} />)}
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <>
      <StoryRow />
      <HeroSlider />
      <PromoCards />
      <CategoryGrid />
      <BestSellers />
    </>
  );
}
