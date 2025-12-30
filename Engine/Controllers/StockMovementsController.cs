using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace accounting_engine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockMovementsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StockMovementsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateStockMovement([FromBody] StockMovement movement)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            // Journal Logic
            // Example for "Out" (Sale) -> Dr COGS, Cr Inventory
            if (movement.Type == StockMovementType.Out)
            {
                var journal = new Journal
                {
                    Date = movement.Date,
                    SourceType = JournalSourceType.Stock,
                    SourceId = movement.Id,
                    Reference = movement.Ref,
                    Status = JournalStatus.Posted
                };
                _context.Journals.Add(journal);
                await _context.SaveChangesAsync();

                foreach (var line in movement.Lines)
                {
                    var product = await _context.Products.FindAsync(line.ProductId);
                    // Assuming we have COGS account on product (ExpenseAccountId)
                    // and Inventory account on product (InventoryAccountId)
                    
                    if (product?.ExpenseAccountId != null)
                    {
                        // Dr COGS
                         _context.JournalLines.Add(new JournalLine
                         {
                             JournalId = journal.Id,
                             AccountId = product.ExpenseAccountId.Value,
                             Description = "COGS",
                             Amount = line.TotalCost,
                             Currency = "USD" // Simplification: need to know currency of stock value
                         });
                    }
                    if (product?.InventoryAccountId != null)
                    {
                        // Cr Inventory
                         _context.JournalLines.Add(new JournalLine
                         {
                             JournalId = journal.Id,
                             AccountId = product.InventoryAccountId.Value,
                             Description = "Inventory Asset",
                             Amount = -line.TotalCost,
                             Currency = "USD"
                         });
                    }
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return CreatedAtAction(nameof(GetStockMovement), new { id = movement.Id }, movement);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStockMovement(int id)
    {
         var movement = await _context.StockMovements
            .Include(m => m.Lines)
            .FirstOrDefaultAsync(m => m.Id == id);
            
        if (movement == null) return NotFound();
        return Ok(movement);
    }
}
