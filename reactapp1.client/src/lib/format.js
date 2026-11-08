// همهٔ مبالغ در دیتابیس «ریال» هستند؛ نمایش در UI به «تومان».
export const rialToToman = (rial) => Math.round(Number(rial) / 10);
export const formatToman = (rial) => `${rialToToman(rial).toLocaleString('fa-IR')} تومان`;
export const formatNumber = (n) => Number(n).toLocaleString('fa-IR');
