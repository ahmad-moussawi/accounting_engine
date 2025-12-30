using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace accounting_engine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public InvoicesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvoice([FromBody] Invoice invoice)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Save Invoice
            invoice.Status = InvoiceStatus.Authorised;
            invoice.BalanceDue = invoice.TotalAmount; // simplification
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // 2. Create Journal
            var journal = new Journal
            {
                Date = invoice.Date,
                SourceType = JournalSourceType.Invoice,
                SourceId = invoice.Id,
                Reference = invoice.Ref,
                Narration = $"Invoice {invoice.Ref}",
                Status = JournalStatus.Posted
            };
            _context.Journals.Add(journal);
            await _context.SaveChangesAsync();

            // 3. Create Journal Lines
            // Logic differs for Sales vs Purchase
            if (invoice.Type == InvoiceType.Sales)
            {
                 // Dr Receivable
                 var contact = await _context.Contacts.FindAsync(invoice.ContactId);
                 if (contact?.ReceivablesAccountId != null)
                 {
                     _context.JournalLines.Add(new JournalLine
                     {
                         JournalId = journal.Id,
                         AccountId = contact.ReceivablesAccountId.Value,
                         Description = "Accounts Receivable",
                         Amount = invoice.TotalAmount, // Debit +
                         Currency = invoice.Currency
                     });
                 }
                 
                 // Cr Sales Revenue & Tax (Simplified: assuming implicit accounts or data provided)
                 // NOTE: In a real system, we'd distribute by InvoiceLines -> Product -> SalesAccount
                 // For now, we aggregate by product account.
                 foreach (var line in invoice.Lines)
                 {
                     var product = await _context.Products.FindAsync(line.ProductId);
                     if (product?.SalesAccountId != null)
                     {
                         _context.JournalLines.Add(new JournalLine
                         {
                             JournalId = journal.Id,
                             AccountId = product.SalesAccountId.Value,
                             Description = $"{product.Name} - {line.Description}",
                             Amount = -line.Subtotal, // Credit -
                             Currency = invoice.Currency
                         });
                     }
                     // Tax? Assuming single tax account for now or ignoring if 0
                 }
            }
            else if (invoice.Type == InvoiceType.Purchase)
            {
                // Cr Accounts Payable
                var contact = await _context.Contacts.FindAsync(invoice.ContactId);
                if (contact?.PayablesAccountId != null)
                {
                    _context.JournalLines.Add(new JournalLine
                    {
                        JournalId = journal.Id,
                        AccountId = contact.PayablesAccountId.Value,
                        Description = "Accounts Payable",
                        Amount = -invoice.TotalAmount, // Credit -
                        Currency = invoice.Currency
                    });
                }
                
                // Dr Expense / Inventory
                foreach (var line in invoice.Lines)
                {
                    var product = await _context.Products.FindAsync(line.ProductId);
                    // Decide based on product type or explicit account map
                    var accountId = product?.Type == ProductType.Goods 
                        ? product.InventoryAccountId 
                        : product.ExpenseAccountId;

                    if (accountId != null)
                    {
                        _context.JournalLines.Add(new JournalLine
                        {
                            JournalId = journal.Id,
                            AccountId = accountId.Value,
                            // Description = $"{product.Name} - {line.Description}",
                            Description = product.Name + " - " + line.Description, // Simple concat
                            Amount = line.Subtotal, // Debit +
                            Currency = invoice.Currency
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoice(int id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);
            
        if (invoice == null) return NotFound();
        return Ok(invoice);
    }

    [HttpPost("{id}/void")]
    public async Task<IActionResult> VoidInvoice(int id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
             invoice.Status = InvoiceStatus.Voided;
             // Logic to find original journal and create reversal
             var originalJournal = await _context.Journals
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.SourceType == JournalSourceType.Invoice && j.SourceId == id && j.Status == JournalStatus.Posted);

             if (originalJournal != null)
             {
                 var reversal = new Journal
                 {
                     Date = DateOnly.FromDateTime(DateTime.Now),
                     SourceType = JournalSourceType.Invoice,
                     SourceId = id,
                     Reference = $"{invoice.Ref} - Void",
                     Narration = $"Reversal of {originalJournal.Narration}",
                     Status = JournalStatus.Posted
                 };
                 _context.Journals.Add(reversal);
                 await _context.SaveChangesAsync();

                 foreach (var line in originalJournal.Lines)
                 {
                     _context.JournalLines.Add(new JournalLine
                     {
                         JournalId = reversal.Id,
                         AccountId = line.AccountId,
                         Description = $"Reversal - {line.Description}",
                         Amount = -line.Amount, // Swap sign
                         Currency = line.Currency
                     });
                 }
                 originalJournal.Status = JournalStatus.Voided; // Or keep Posted and just have reversal? Req says "void the journal entry", typically we mark original as Voided OR just post reversal. I'll mark voided.
             }

             await _context.SaveChangesAsync();
             await transaction.CommitAsync();
             return Ok();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
