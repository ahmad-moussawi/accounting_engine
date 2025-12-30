using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace accounting_engine.Models;

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }
public enum JournalSourceType { Invoice, Payment, Stock, Manual }
public enum JournalStatus { Draft, Posted, Voided }

public class Account
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string? SubType { get; set; }
    
    public int? ParentId { get; set; }
    public Account? Parent { get; set; }
    public List<Account> Children { get; set; } = new();
}

public class Journal
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public JournalSourceType SourceType { get; set; }
    public int? SourceId { get; set; } // Can refer to Invoice.Id, Payment.Id, etc.
    public string Reference { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public JournalStatus Status { get; set; }
    
    public List<JournalLine> Lines { get; set; } = new();
}

public class JournalLine
{
    public int Id { get; set; }
    
    public int JournalId { get; set; }
    public Journal Journal { get; set; } = null!;
    
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    
    public string Description { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; } // Signed: +Debit, -Credit
    
    public string Currency { get; set; } = string.Empty;
}
