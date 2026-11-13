import axios from 'axios';

// نشست مدیریتی با کوکی HttpOnly نگه‌داری می‌شود (قابل خواندن با JavaScript نیست)؛
// هیچ توکن یا داده مالی/انباری در مرورگر ذخیره نمی‌شود.
const api = axios.create({ baseURL: '/api', timeout: 15000, withCredentials: true });

export default api;
