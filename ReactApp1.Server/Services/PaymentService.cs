using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

public class PaymentService(AppDbContext db, LedgerService ledger)
{
    /// ثبت مستقل پرداخت/دریافت. تکراری‌نبودن با IdempotencyKey (ایندکس یکتا + بررسی قبلی) تضمین می‌شود.
    public async Task<Payment> RegisterAsync(PaymentRequest req, int userId, CancellationToken ct)
    {
        var existing = await db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == req.IdempotencyKey, ct);
        if (existing is not null) return existing;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var payment = await PostPaymentAsync(req, userId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return payment;
    }

    /// بدون SaveChanges و بدون تراکنش؛ برای استفاده داخل تراکنش‌های بزرگ‌تر (مثلاً تأیید رسید خرید).
    public async Task<Payment> PostPaymentAsync(PaymentRequest req, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.IdempotencyKey)) throw new DomainException("کلید یکتای عملیات (IdempotencyKey) الزامی است.");
        if (req.Amount <= 0) throw new DomainException("مبلغ باید بزرگ‌تر از صفر باشد.");

        var party = await db.Parties.FindAsync([req.PartyId], ct) ?? throw new DomainException("طرف‌حساب یافت نشد.", 404);
        if (req.Direction == PaymentDirection.ToSupplier && !party.IsSupplier) throw new DomainException("طرف‌حساب انتخاب‌شده تأمین‌کننده نیست.");
        if (req.Direction == PaymentDirection.FromCustomer && !party.IsCustomer) throw new DomainException("طرف‌حساب انتخاب‌شده مشتری نیست.");

        var account = await db.FinancialAccounts.FindAsync([req.AccountId], ct) ?? throw new DomainException("حساب صندوق/بانک یافت نشد.", 404);
        if (!account.IsActive || account.Kind is not (AccountKind.Cash or AccountKind.Bank))
            throw new DomainException("حساب انتخاب‌شده صندوق یا بانک فعال نیست.");

        if (req.ReceiptId is int rid)
        {
            if (req.Direction != PaymentDirection.ToSupplier) throw new DomainException("اتصال به رسید خرید فقط برای پرداخت به تأمین‌کننده ممکن است.");
            var receipt = await db.PurchaseReceipts.FindAsync([rid], ct) ?? throw new DomainException("رسید خرید یافت نشد.", 404);
            if (receipt.SupplierId != party.Id) throw new DomainException("رسید متعلق به این تأمین‌کننده نیست.");
            if (receipt.Status != ReceiptStatus.Confirmed) throw new DomainException("فقط برای رسید تأییدشده می‌توان پرداخت ثبت کرد.");
            var paid = await db.Payments.Where(p => p.PurchaseReceiptId == rid && p.Status == PaymentStatus.Posted).SumAsync(p => p.Amount, ct);
            if (paid + req.Amount > receipt.TotalAmount)
                throw new DomainException($"مبلغ از مانده رسید بیشتر است (مانده: {receipt.TotalAmount - paid:N0} ریال).");
        }

        var number = await ledger.NextNumberAsync("PAY", ct);
        FinancialDocument doc;
        if (req.Direction == PaymentDirection.ToSupplier)
        {
            var payable = await ledger.GetSystemAccountAsync(AccountKind.Payable, ct);
            doc = await ledger.PostAsync(DocumentType.SupplierPayment, req.Date, $"پرداخت به {party.Name} ({number})", "Payment", null,
                [new(payable.Id, req.Amount, 0, party.Id), new(account.Id, 0, req.Amount)], userId, ct: ct);
        }
        else
        {
            var receivable = await ledger.GetSystemAccountAsync(AccountKind.Receivable, ct);
            doc = await ledger.PostAsync(DocumentType.CustomerReceipt, req.Date, $"دریافت از {party.Name} ({number})", "Payment", null,
                [new(account.Id, req.Amount, 0), new(receivable.Id, 0, req.Amount, party.Id)], userId, ct: ct);
        }

        var payment = new Payment
        {
            Number = number, Direction = req.Direction, PartyId = party.Id, Amount = req.Amount, Method = req.Method,
            FinancialAccountId = account.Id, ReferenceNumber = req.Reference, Date = req.Date, PurchaseReceiptId = req.ReceiptId,
            IdempotencyKey = req.IdempotencyKey, FinancialDocument = doc, Note = req.Note, CreatedByUserId = userId
        };
        db.Payments.Add(payment);
        ledger.Audit(userId, req.Direction.ToString(), "Payment", null, $"{number} / {req.Amount:N0} / party {party.Id}");
        return payment;
    }
}
