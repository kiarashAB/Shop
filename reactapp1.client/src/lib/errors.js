// پیام خطا از پاسخ ProblemDetails سرور (title) یا خطاهای اعتبارسنجی؛ در غیر این صورت پیام عمومی.
export function errMsg(err, fallback = 'خطایی رخ داد. دوباره تلاش کنید.') {
  const d = err?.response?.data;
  if (d?.title && err.response.status !== 400) return d.title;
  if (d?.errors) return Object.values(d.errors).flat().join(' ') || fallback;
  if (d?.title) return d.title;
  if (d?.message) return d.message;
  if (!err?.response) return 'ارتباط با سرور برقرار نشد.';
  return fallback;
}
