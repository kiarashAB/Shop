import { Link } from 'react-router-dom';

export default function NotFound() {
  return (
    <div className="mx-auto max-w-xl py-32 text-center">
      <h1 className="text-3xl font-black">صفحه پیدا نشد</h1>
      <p className="mt-4 text-gray-500">این بخش هنوز در مرحلهٔ توسعه است یا آدرس اشتباه است.</p>
      <Link to="/" className="mt-8 inline-block rounded-xl bg-brand px-6 py-3 text-white">بازگشت به صفحه اصلی</Link>
    </div>
  );
}
