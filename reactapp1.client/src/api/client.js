import axios from 'axios';

// توکن مدیریتی فقط در حافظهٔ Session نگه‌داری می‌شود؛ هیچ داده مالی یا انباری در مرورگر ذخیره نمی‌شود.
const api = axios.create({ baseURL: '/api', timeout: 15000 });

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('auth_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export default api;
