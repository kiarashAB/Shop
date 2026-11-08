namespace ReactApp1.Server.Domain;

public enum UserRole { SuperAdmin, Admin, ProductManager, OrderManager, InventoryManager, Accountant, Support, ContentManager }
public enum ProductCondition { New, Sealed, Used }
public enum ReceiptStatus { Draft, Confirmed, Reversed }
public enum StockReason { Purchase, PurchaseReversal, Sale, InternalUse, Damage, Waste, Return, Adjustment }
public enum AccountKind { Cash, Bank, Inventory, Receivable, Payable, Sales, CostOfGoods, OtherExpense }
public enum DocumentType { Purchase, SupplierPayment, CustomerReceipt, Sale, StockWriteOff, Adjustment, Reversal }
public enum DocumentStatus { Posted, Reversed }
public enum PaymentDirection { ToSupplier, FromCustomer }
public enum PaymentMethod { Cash, Card, Transfer, Cheque, Gateway }
public enum PaymentStatus { Posted, Reversed }
public enum BannerPlacement { Hero, Promo }
