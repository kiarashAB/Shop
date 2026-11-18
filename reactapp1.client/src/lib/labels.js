export const ORDER_STATUS = {
  PendingPayment: { label: 'در انتظار پرداخت', cls: 'bg-amber-50 text-amber-700' },
  Processing: { label: 'در حال پردازش', cls: 'bg-blue-50 text-blue-700' },
  Shipped: { label: 'ارسال شده', cls: 'bg-indigo-50 text-indigo-700' },
  Delivered: { label: 'تحویل شده', cls: 'bg-green-50 text-green-700' },
  Cancelled: { label: 'لغو شده', cls: 'bg-gray-100 text-gray-600' },
};
export const PAYMENT_STATE = { Paid: 'پرداخت شده', Partial: 'پرداخت ناقص', Unpaid: 'پرداخت نشده' };
export const formatDate = (iso) => new Date(iso).toLocaleDateString('fa-IR', { year: 'numeric', month: 'long', day: 'numeric' });
export const formatDateTime = (iso) => new Date(iso).toLocaleString('fa-IR');
