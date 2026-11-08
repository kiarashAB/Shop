using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

/// تنها نقطهٔ ثبت سند مالی. هر رویداد مالی فقط از اینجا سند می‌شود و ردیف دفتر طرف‌حساب هم همین‌جا ساخته می‌شود.
public class LedgerService(AppDbContext db)
{
    public async Task<string> NextNumberAsync(string key, CancellationToken ct)
    {
        // افزایش اتمیک روی ردیف شمارنده؛ داخل تراکنش فراخوان، تا انتهای تراکنش قفل می‌ماند و شمارهٔ تکراری نمی‌دهد.
        var n = await db.DocumentCounters.Where(c => c.Key == key)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastValue, c => c.LastValue + 1), ct);
        if (n == 0)
        {
            db.DocumentCounters.Add(new DocumentCounter { Key = key, LastValue = 1 });
            await db.SaveChangesAsync(ct);
            return $"{key}-{1:D6}";
        }
        var last = await db.DocumentCounters.AsNoTracking().Where(c => c.Key == key).Select(c => c.LastValue).SingleAsync(ct);
        return $"{key}-{last:D6}";
    }

    public async Task<FinancialAccount> GetSystemAccountAsync(AccountKind kind, CancellationToken ct) =>
        await db.FinancialAccounts.FirstOrDefaultAsync(a => a.Kind == kind && a.IsSystem, ct)
        ?? throw new DomainException($"حساب سیستمی {kind} تعریف نشده است.", 500);

    public void Audit(int? userId, string action, string entity, int? entityId, string? details = null) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, Entity = entity, EntityId = entityId, Details = details });

    /// سند را به Context اضافه می‌کند (SaveChanges با فراخوان است تا همه‌چیز در یک تراکنش ذخیره شود).
    public async Task<FinancialDocument> PostAsync(DocumentType type, DateTime date, string description, string sourceType, int? sourceId,
        IReadOnlyList<PostingLine> lines, int userId, int? reversalOfId = null, CancellationToken ct = default)
    {
        if (lines.Count < 2) throw new DomainException("سند مالی حداقل دو ردیف نیاز دارد.");
        foreach (var l in lines)
        {
            if (l.Debit < 0 || l.Credit < 0 || (l.Debit > 0) == (l.Credit > 0))
                throw new DomainException("هر ردیف سند باید دقیقاً یکی از بدهکار یا بستانکار را (با مبلغ مثبت) داشته باشد.");
        }
        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            throw new DomainException($"سند تراز نیست: بدهکار {totalDebit:N0} ≠ بستانکار {totalCredit:N0}.", 500);

        var doc = new FinancialDocument
        {
            Number = await NextNumberAsync("FD", ct), Date = date, Type = type, Description = description,
            SourceType = sourceType, SourceId = sourceId, ReversalOfId = reversalOfId,
            TotalDebit = totalDebit, TotalCredit = totalCredit, CreatedByUserId = userId
        };
        foreach (var l in lines)
        {
            doc.Lines.Add(new FinancialDocumentLine { AccountId = l.AccountId, PartyId = l.PartyId, Debit = l.Debit, Credit = l.Credit, Description = l.Description });
            if (l.PartyId is int pid)
                db.PartyTransactions.Add(new PartyTransaction { PartyId = pid, FinancialDocument = doc, Date = date, Type = type, Description = description, Debit = l.Debit, Credit = l.Credit });
        }
        db.FinancialDocuments.Add(doc);
        return doc;
    }

    /// سند قطعی‌شده هرگز ویرایش یا حذف نمی‌شود؛ فقط سند معکوس صادر می‌شود.
    public async Task<FinancialDocument> ReverseDocumentAsync(int documentId, string reason, int userId, CancellationToken ct)
    {
        var original = await db.FinancialDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new DomainException("سند یافت نشد.", 404);
        if (original.Status != DocumentStatus.Posted) throw new DomainException("این سند قبلاً معکوس شده است.", 409);
        var lines = original.Lines.Select(l => new PostingLine(l.AccountId, l.Credit, l.Debit, l.PartyId, l.Description)).ToList();
        var rev = await PostAsync(DocumentType.Reversal, DateTime.UtcNow, $"سند معکوس {original.Number}: {reason}", original.SourceType, original.SourceId, lines, userId, original.Id, ct);
        original.Status = DocumentStatus.Reversed;
        return rev;
    }

    public async Task<PartyLedgerResult> GetPartyLedgerAsync(int partyId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = db.PartyTransactions.AsNoTracking().Where(t => t.PartyId == partyId);
        decimal opening = 0;
        if (from is not null)
            opening = await q.Where(t => t.Date < from).SumAsync(t => t.Debit - t.Credit, ct);
        if (from is not null) q = q.Where(t => t.Date >= from);
        if (to is not null) q = q.Where(t => t.Date <= to);

        var raw = await q.OrderBy(t => t.Date).ThenBy(t => t.Id)
            .Select(t => new { t.Date, t.FinancialDocument!.Number, t.Description, t.Type, t.Debit, t.Credit, t.FinancialDocumentId })
            .ToListAsync(ct);
        var rows = new List<LedgerRow>(raw.Count);
        var bal = opening;
        foreach (var r in raw)
        {
            bal += r.Debit - r.Credit;
            rows.Add(new LedgerRow(r.Date, r.Number, r.Description, r.Type, r.Debit, r.Credit, bal, r.FinancialDocumentId));
        }
        return new PartyLedgerResult(opening, rows, bal);
    }

    public async Task<TrialBalanceResult> GetTrialBalanceAsync(CancellationToken ct)
    {
        var rows = await db.FinancialDocumentLines.AsNoTracking()
            .GroupBy(l => new { l.Account!.Code, l.Account.Name })
            .Select(g => new TrialBalanceRow(g.Key.Code, g.Key.Name, g.Sum(x => x.Debit), g.Sum(x => x.Credit)))
            .OrderBy(r => r.Code).ToListAsync(ct);
        var td = rows.Sum(r => r.Debit);
        var tc = rows.Sum(r => r.Credit);
        return new TrialBalanceResult(rows, td, tc, td == tc);
    }
}
