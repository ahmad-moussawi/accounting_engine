using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace accounting_engine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JournalsController : ControllerBase
{
    private readonly AppDbContext _context;

    public JournalsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateJournal([FromBody] Journal journal)
    {
        // Validation: Sum must be 0 per currency
        var groups = journal.Lines.GroupBy(x => x.Currency);
        foreach (var group in groups)
        {
            if (group.Sum(x => x.Amount) != 0)
            {
                return BadRequest($"Journal is not balanced for currency {group.Key}. Net amount: {group.Sum(x => x.Amount)}");
            }
        }

        _context.Journals.Add(journal);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetJournal), new { id = journal.Id }, journal);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJournal(int id)
    {
        var journal = await _context.Journals
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == id);
            
        if (journal == null) return NotFound();
        return Ok(journal);
    }
}
