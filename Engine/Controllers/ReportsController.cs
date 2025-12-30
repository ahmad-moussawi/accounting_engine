using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace accounting_engine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/reports/trial-balance?currency=USD&date=2023-12-31
    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] string currency, [FromQuery] DateOnly? date)
    {
        if (string.IsNullOrEmpty(currency)) return BadRequest("Currency is required.");

        var query = _context.JournalLines
            .Include(Gl => Gl.Journal)
            .Include(Gl => Gl.Account)
            .Where(Gl => Gl.Currency == currency && Gl.Journal.Status == JournalStatus.Posted);

        if (date.HasValue)
        {
            query = query.Where(Gl => Gl.Journal.Date <= date.Value);
        }

        var trialBalance = await query
            .GroupBy(Gl => new { Gl.AccountId, Gl.Account.Code, Gl.Account.Name, Gl.Account.Type })
            .Select(g => new
            {
                AccountId = g.Key.AccountId,
                Code = g.Key.Code,
                Name = g.Key.Name,
                Type = g.Key.Type.ToString(),
                Balance = g.Sum(x => x.Amount)
            })
            .Where(x => x.Balance != 0) // Optional: Hide zero balance accounts
            .OrderBy(x => x.Code)
            .ToListAsync();

        var totalDebits = trialBalance.Where(x => x.Balance > 0).Sum(x => x.Balance);
        var totalCredits = trialBalance.Where(x => x.Balance < 0).Sum(x => x.Balance);

        // Check if balanced (floating point tolerance might be needed in real world, but simple sum for now)
        var isBalanced = (totalDebits + totalCredits) == 0;

        return Ok(new
        {
            Currency = currency,
            AsOfDate = date ?? DateOnly.FromDateTime(DateTime.Today),
            Accounts = trialBalance,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits,
            IsBalanced = isBalanced
        });
    }

    // GET: api/reports/general-ledger?accountId=1&currency=USD&fromDate=2023-01-01&toDate=2023-12-31
    [HttpGet("general-ledger")]
    public async Task<IActionResult> GetGeneralLedger(
        [FromQuery] int accountId, 
        [FromQuery] string currency, 
        [FromQuery] DateOnly? fromDate, 
        [FromQuery] DateOnly? toDate)
    {
        if (string.IsNullOrEmpty(currency)) return BadRequest("Currency is required.");

        var account = await _context.Accounts.FindAsync(accountId);
        if (account == null) return NotFound("Account not found.");

        var query = _context.JournalLines
            .Include(Gl => Gl.Journal)
            .Where(Gl => Gl.AccountId == accountId && Gl.Currency == currency && Gl.Journal.Status == JournalStatus.Posted);

        if (fromDate.HasValue)
            query = query.Where(Gl => Gl.Journal.Date >= fromDate.Value);
            
        if (toDate.HasValue)
            query = query.Where(Gl => Gl.Journal.Date <= toDate.Value);

        var lines = await query
            .OrderBy(Gl => Gl.Journal.Date)
            .Select(Gl => new
            {
                Gl.Journal.Date,
                Gl.Journal.SourceType,
                Gl.Journal.Reference,
                Gl.Description,
                Debit = Gl.Amount > 0 ? Gl.Amount : 0,
                Credit = Gl.Amount < 0 ? -Gl.Amount : 0,
                Amount = Gl.Amount
            })
            .ToListAsync();
            
        // Calculate running balance
        // Note: This paging/fetching approach assumes small volume. For large ledgers, need better strategy.
        decimal runningBalance = 0; // Ideally should fetch opening balance if fromDate > system start
        
        // If fromDate is specific, we actually need the opening balance from before that date
        if (fromDate.HasValue)
        {
             var openingBalance = await _context.JournalLines
                .Include(Gl => Gl.Journal)
                .Where(Gl => Gl.AccountId == accountId && Gl.Currency == currency && Gl.Journal.Status == JournalStatus.Posted && Gl.Journal.Date < fromDate.Value)
                .SumAsync(Gl => Gl.Amount);
             runningBalance = openingBalance;
        }

        var resultLines = new List<object>();
        foreach(var line in lines)
        {
            runningBalance += line.Amount;
            resultLines.Add(new 
            {
                line.Date,
                SourceType = line.SourceType.ToString(),
                line.Reference,
                line.Description,
                line.Debit,
                line.Credit,
                Balance = runningBalance
            });
        }

        return Ok(new
        {
            Account = new { account.Code, account.Name, account.Type },
            Currency = currency,
            Period = new { From = fromDate, To = toDate },
            OpeningBalance = fromDate.HasValue ? runningBalance - lines.Sum(l => l.Amount) : 0,
            Lines = resultLines,
            ClosingBalance = runningBalance
        });
    }
}
