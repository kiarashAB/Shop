import { useEffect, useState } from 'react';
import api from '../api/client';

// قیمت‌گذاری سبد توسط سرور (قیمت، موجودی، تخفیف، ارسال و جمع نهایی). فرانت‌اند هیچ محاسبهٔ مالی انجام نمی‌دهد.
export default function useQuote(items, discountCode, shippingMethodId) {
  const [state, setState] = useState({ quote: null, loading: false, error: null });
  const key = JSON.stringify([items, discountCode, shippingMethodId]);

  useEffect(() => {
    if (items.length === 0) { setState({ quote: null, loading: false, error: null }); return; }
    const controller = new AbortController();
    setState((s) => ({ ...s, loading: true, error: null }));
    api.post('/cart/quote', {
      items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
      discountCode: discountCode || null,
      shippingMethodId: shippingMethodId || null,
    }, { signal: controller.signal })
      .then((r) => setState({ quote: r.data, loading: false, error: null }))
      .catch((e) => { if (!controller.signal.aborted) setState({ quote: null, loading: false, error: e }); });
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return state;
}
