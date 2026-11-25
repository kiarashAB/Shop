import { formatToman } from '../lib/format';

export default function OrderSummary({ quote }) {
  if (!quote) return null;
  return (
    <dl className="space-y-3 text-sm">
      <div className="flex justify-between"><dt className="text-gray-500">جمع کالاها</dt><dd>{formatToman(quote.subtotal)}</dd></div>
      {quote.discountAmount > 0 && (
        <div className="flex justify-between text-green-700"><dt>تخفیف ({quote.discountCode})</dt><dd>− {formatToman(quote.discountAmount)}</dd></div>
      )}
      <div className="flex justify-between"><dt className="text-gray-500">هزینه ارسال</dt><dd>{quote.shippingCost > 0 ? formatToman(quote.shippingCost) : 'رایگان / انتخاب نشده'}</dd></div>
      <div className="flex justify-between border-t border-gray-200 pt-3 text-base font-bold"><dt>مبلغ نهایی</dt><dd>{formatToman(quote.total)}</dd></div>
    </dl>
  );
}
