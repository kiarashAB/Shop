# راه‌اندازی (یک‌بار)

1. در Visual Studio پروژه را باز کنید (`ReactApp1.slnx`).
2. تنظیمات محرمانه (در فایل ذخیره نکنید):
   ```
   cd ReactApp1.Server
   dotnet user-secrets init
   dotnet user-secrets set "Seed:AdminPassword" "رمز-قوی-حداقل-۱۰-کاراکتر"
   ```
3. ساخت Migration اولیه (هنوز ساخته نشده، چون در محیط من dotnet نبود):
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add Initial
   ```
   در حالت Development هنگام اجرا خودکار `Migrate` می‌شود. برای پروداکشن `dotnet ef database update` بزنید.
4. فرانت: `cd reactapp1.client && npm install`
5. اجرا: F5 در Visual Studio، یا `dotnet run` در پوشهٔ Server.
6. تست‌ها (LocalDB لازم است): `dotnet test`

## قراردادهای ثابت
- همهٔ مبالغ در دیتابیس **ریال**، `decimal(18,0)`؛ نمایش در UI **تومان** (تقسیم بر ۱۰).
- مانده طرف‌حساب = بدهکار − بستانکار. مثبت = طلب ما، منفی = بدهی ما.
- موجودی فقط با سند تغییر می‌کند (`InventoryService.MoveStockAsync`)، سند مالی فقط با `LedgerService.PostAsync`.
- سند قطعی‌شده ویرایش/حذف نمی‌شود؛ فقط سند معکوس.
