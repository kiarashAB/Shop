using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

public record LoginDto(string UserName, string Password);

public record PurchaseItemDto(int ProductId, int Quantity, decimal UnitCost, decimal DiscountAmount = 0);
public record CreatePurchaseDto(
    int SupplierId, int WarehouseId, DateTime Date, string? SupplierInvoiceNumber, string? Notes,
    List<PurchaseItemDto> Items, decimal PaidAmount = 0, PaymentMethod? Method = null,
    int? PaymentAccountId = null, string? PaymentReference = null, string? IdempotencyKey = null);
public record ReverseDto(string Reason);

public record PaymentDto(int PartyId, decimal Amount, PaymentMethod Method, int FinancialAccountId, DateTime Date,
    string? ReferenceNumber, int? PurchaseReceiptId, string IdempotencyKey, string? Note);
public record PaymentRequest(PaymentDirection Direction, int PartyId, decimal Amount, PaymentMethod Method, int AccountId,
    string? Reference, DateTime Date, int? ReceiptId, string IdempotencyKey, string? Note);

public record IssueStockDto(int ProductId, int WarehouseId, int Quantity, StockReason Reason, string? Note, DateTime? Date);

public record PartyDto(string Name, string? Phone, string? Email, string? Address, bool IsCustomer, bool IsSupplier, string? Notes);

public record ProductDto(string Name, string Slug, string Sku, string? PartNumber, int CategoryId, int? BrandId, string? Description,
    decimal SalePrice, decimal DiscountPercent, string? Warranty, ProductCondition Condition, bool IsPublished,
    string? SeoTitle, string? SeoDescription, string? SeoKeywords, List<string>? ImageUrls);

public record PostingLine(int AccountId, decimal Debit, decimal Credit, int? PartyId = null, string? Description = null);
public record LedgerRow(DateTime Date, string DocumentNumber, string Description, DocumentType Type, decimal Debit, decimal Credit, decimal Balance, int FinancialDocumentId);
public record PartyLedgerResult(decimal OpeningBalance, IReadOnlyList<LedgerRow> Rows, decimal Balance);
public record TrialBalanceRow(string Code, string Name, decimal Debit, decimal Credit);
public record TrialBalanceResult(IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool IsBalanced);
