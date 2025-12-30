using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace accounting_engine.Models;

public enum ContactType { Customer, Vendor }
public enum ProductType { Service, Goods }
public enum InvoiceType { Sales, Purchase }
public enum InvoiceStatus { Draft, Authorised, Paid, Voided }
public enum PaymentType { Inbound, Outbound }

public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContactType Type { get; set; }
    public string? TaxId { get; set; }
    public string Currency { get; set; } = string.Empty;
    
    public int? ReceivablesAccountId { get; set; }
    public Account? ReceivablesAccount { get; set; }
    
    public int? PayablesAccountId { get; set; }
    public Account? PayablesAccount { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public ProductType Type { get; set; }
    
    public int? SalesAccountId { get; set; }
    public Account? SalesAccount { get; set; }
    
    public int? ExpenseAccountId { get; set; }
    public Account? ExpenseAccount { get; set; }
    
    public int? InventoryAccountId { get; set; }
    public Account? InventoryAccount { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public InvoiceType Type { get; set; }
    
    public int ContactId { get; set; }
    public Contact? Contact { get; set; }
    
    public string Ref { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18, 4)")]
    public decimal ExchangeRate { get; set; }
    public InvoiceStatus Status { get; set; }
    
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }
    
    [Column(TypeName = "decimal(18, 2)")]
    public decimal BalanceDue { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public int Id { get; set; }
    
    public int InvoiceId { get; set; }
    [JsonIgnore]
    public Invoice? Invoice { get; set; }
    
    public int ProductId { get; set; }
    [JsonIgnore]
    public Product? Product { get; set; }
    
    public string Description { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18, 4)")]
    public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Subtotal { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Total { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;
    
    public int BankAccountId { get; set; }
    public Account BankAccount { get; set; } = null!;
    
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18, 4)")]
    public decimal ExchangeRate { get; set; }
    public string Ref { get; set; } = string.Empty;
    public PaymentType Type { get; set; }
}

public class Allocation
{
    public int Id { get; set; }
    
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }
}
