using Microsoft.EntityFrameworkCore;
using accounting_engine.Models;

namespace accounting_engine.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLine> InvoiceLines { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Allocation> Allocations { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<StockMovementLine> StockMovementLines { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Journal> Journals { get; set; }
    public DbSet<JournalLine> JournalLines { get; set; }
}
