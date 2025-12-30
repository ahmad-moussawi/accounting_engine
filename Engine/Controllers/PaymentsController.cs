using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace accounting_engine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PaymentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] Payment payment)
    {
         using var transaction = await _context.Database.BeginTransactionAsync();
         try
         {
             _context.Payments.Add(payment);
             await _context.SaveChangesAsync();
             
             // Journal: Receive Payment
             // Dr Bank, Cr Receivable
             if (payment.Type == PaymentType.Inbound)
             {
                 var journal = new Journal
                 {
                     Date = payment.Date,
                     SourceType = JournalSourceType.Payment,
                     SourceId = payment.Id,
                     Reference = payment.Ref,
                     Status = JournalStatus.Posted
                 };
                 _context.Journals.Add(journal);
                 await _context.SaveChangesAsync();
                 
                 // Dr Bank
                 _context.JournalLines.Add(new JournalLine
                 {
                     JournalId = journal.Id,
                     AccountId = payment.BankAccountId,
                     Description = "Bank",
                     Amount = payment.Amount,
                     Currency = payment.Currency
                 });
                 
                 // Cr Receivable
                 var contact = await _context.Contacts.FindAsync(payment.ContactId);
                 if (contact?.ReceivablesAccountId != null)
                 {
                     _context.JournalLines.Add(new JournalLine
                     {
                         JournalId = journal.Id,
                         AccountId = contact.ReceivablesAccountId.Value,
                         Description = "Accounts Receivable",
                         Amount = -payment.Amount,
                         Currency = payment.Currency
                     });
                 }
                 await _context.SaveChangesAsync();
             }

             await transaction.CommitAsync();
             return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
         }
         catch (Exception ex)
         {
             await transaction.RollbackAsync();
             return BadRequest(ex.Message);
         }
    }
    
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return NotFound();
        return Ok(payment);
    }
}
