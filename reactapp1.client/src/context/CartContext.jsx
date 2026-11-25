import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import api from '../api/client';
import { useAuth } from './AuthContext';

const CartCtx = createContext(null);
export const useCart = () => useContext(CartCtx);

const MAX_QTY = 999;
const KEY = 'cart_items';

// فقط شناسهٔ محصول و تعداد نگه‌داری می‌شود (اطلاعات غیرحساس)؛ قیمت و موجودی همیشه از سرور می‌آید.
const readLocal = () => {
  try {
    const v = JSON.parse(localStorage.getItem(KEY) || '[]');
    return Array.isArray(v) ? v.filter((i) => Number.isInteger(i?.productId) && Number.isInteger(i?.quantity) && i.quantity > 0) : [];
  } catch { return []; }
};
const writeLocal = (items) => { try { localStorage.setItem(KEY, JSON.stringify(items)); } catch { /* حالت خصوصی مرورگر */ } };
const readSession = (k) => { try { return sessionStorage.getItem(k) || ''; } catch { return ''; } };
const writeSession = (k, v) => { try { v ? sessionStorage.setItem(k, v) : sessionStorage.removeItem(k); } catch { /* ignore */ } };

export function CartProvider({ children }) {
  const { user, ready, isCustomer } = useAuth();
  const [items, setItems] = useState(readLocal);
  const [synced, setSynced] = useState(false);
  const [discountCode, setDiscountCodeState] = useState(() => readSession('cart_discount'));
  const [shippingId, setShippingIdState] = useState(() => Number(readSession('cart_shipping')) || null);
  const wasCustomer = useRef(false);

  // ورود مشتری: سبد مهمان با سبد ذخیره‌شدهٔ حساب ادغام می‌شود. خروج: سبد حساب از مرورگر پاک می‌شود.
  useEffect(() => {
    if (!ready) return;
    let cancelled = false;
    if (isCustomer) {
      wasCustomer.current = true;
      setSynced(false);
      const guest = readLocal();
      const req = guest.length ? api.post('/cart/merge', guest) : api.get('/cart');
      req.then((r) => { if (!cancelled) { writeLocal([]); setItems(r.data); } })
        .catch(() => {})
        .finally(() => { if (!cancelled) setSynced(true); });
    } else {
      if (wasCustomer.current) { wasCustomer.current = false; setItems([]); writeLocal([]); }
      setSynced(true);
    }
    return () => { cancelled = true; };
  }, [ready, isCustomer, user?.userName]);

  useEffect(() => {
    if (!ready || !synced) return;
    if (isCustomer) api.put('/cart', items).catch(() => {});
    else writeLocal(items);
  }, [items, ready, synced, isCustomer]);

  const add = useCallback((productId, qty = 1) => {
    setItems((cur) => {
      const found = cur.find((i) => i.productId === productId);
      if (found) return cur.map((i) => (i.productId === productId ? { ...i, quantity: Math.min(i.quantity + qty, MAX_QTY) } : i));
      return [...cur, { productId, quantity: Math.min(qty, MAX_QTY) }];
    });
  }, []);
  const setQty = useCallback((productId, qty) => {
    setItems((cur) => (qty < 1 ? cur.filter((i) => i.productId !== productId)
      : cur.map((i) => (i.productId === productId ? { ...i, quantity: Math.min(qty, MAX_QTY) } : i))));
  }, []);
  const remove = useCallback((productId) => setItems((cur) => cur.filter((i) => i.productId !== productId)), []);
  const clear = useCallback(() => { setItems([]); setDiscountCodeState(''); writeSession('cart_discount', ''); }, []);
  const setDiscountCode = useCallback((c) => { setDiscountCodeState(c); writeSession('cart_discount', c); }, []);
  const setShippingId = useCallback((id) => { setShippingIdState(id); writeSession('cart_shipping', id ? String(id) : ''); }, []);

  const count = useMemo(() => items.reduce((s, i) => s + i.quantity, 0), [items]);
  const value = useMemo(() => ({ items, count, add, setQty, remove, clear, discountCode, setDiscountCode, shippingId, setShippingId, MAX_QTY }),
    [items, count, add, setQty, remove, clear, discountCode, setDiscountCode, shippingId, setShippingId]);
  return <CartCtx.Provider value={value}>{children}</CartCtx.Provider>;
}
