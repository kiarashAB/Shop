const base = { width: 22, height: 22, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 1.6, strokeLinecap: 'round', strokeLinejoin: 'round' };
export const SearchIcon = (p) => <svg {...base} {...p}><circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" /></svg>;
export const UserIcon = (p) => <svg {...base} {...p}><circle cx="12" cy="8" r="4" /><path d="M4 21c0-4 4-6 8-6s8 2 8 6" /></svg>;
export const CartIcon = (p) => <svg {...base} {...p}><path d="M5 8h14l-1 12H6L5 8Z" /><path d="M9 8a3 3 0 0 1 6 0" /></svg>;
export const ArrowLeft = (p) => <svg {...base} width={18} height={18} {...p}><path d="M19 12H5m6-6-6 6 6 6" /></svg>;
export const ChevronLeft = (p) => <svg {...base} width={20} height={20} {...p}><path d="m15 6-6 6 6 6" /></svg>;
export const ChevronRight = (p) => <svg {...base} width={20} height={20} {...p}><path d="m9 6 6 6-6 6" /></svg>;
export const DoubleChevronLeft = (p) => <svg {...base} width={26} height={26} {...p}><path d="m12 6-6 6 6 6M19 6l-6 6 6 6" /></svg>;
