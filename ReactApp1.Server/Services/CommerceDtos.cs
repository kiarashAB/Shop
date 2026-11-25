using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

public record RegisterDto(string FullName, string Phone, string? Email, string Password);
public record UpdateProfileDto(string FullName, string? Email);
public record ChangePasswordDto(string CurrentPassword, string NewPassword);
public record AddressDto(string Title, string RecipientName, string Phone, string Province, string City, string? PostalCode, string FullAddress, bool IsDefault);

public record CartItemDto(int ProductId, int Quantity);
public record QuoteRequest(List<CartItemDto>? Items, string? DiscountCode, int? ShippingMethodId);
public record QuoteLine(int ProductId, string Name, string Slug, string? ImageUrl, int Quantity, decimal ListPrice, decimal UnitPrice,
    decimal LineTotal, int Available, bool InStock, string? Problem);
public record QuoteResult(IReadOnlyList<QuoteLine> Lines, decimal Subtotal, decimal DiscountAmount, string? DiscountCode, string? DiscountError,
    decimal ShippingCost, decimal Total, bool CanCheckout);

public record CreateOrderDto(int AddressId, int ShippingMethodId, string? DiscountCode, string? Note, List<CartItemDto>? Items, string IdempotencyKey);
public record OrderStatusDto(OrderStatus Status, string? Note);
public record ShipmentDto(string Carrier, string TrackingCode, string? Note);
public record CancelOrderDto(string Reason);
public record OrderPaymentDto(decimal Amount, PaymentMethod Method, int FinancialAccountId, DateTime? Date, string? ReferenceNumber, string IdempotencyKey, string? Note);

public record DiscountDto(string Code, DiscountType Type, decimal Value, decimal MinOrderAmount, int? MaxUses, DateTime? StartsAt, DateTime? EndsAt, bool IsActive);
public record ShippingMethodDto(string Name, string? Description, decimal Price, int SortOrder, bool IsActive);
