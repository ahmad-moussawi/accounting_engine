using System.Net.Http.Json;
using TUnit.Core;
using accounting_engine.Data;
using accounting_engine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests;

public class FullScenarioTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FullScenarioTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Create a dedicated service provider for the InMemory provider
                // This ensures that the InMemory services don't conflict with any Sqlite services 
                // that might be lingering in the main container or auto-discovered.
                var efServiceProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                // Remove existing registration
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<AppDbContext>();
                
                // Add InMemory registration with specific internal provider
                var dbName = "TestDb_" + Guid.NewGuid();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                    options.UseInternalServiceProvider(efServiceProvider);
                    options.ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task CompleteAccountingFlow_ShouldProduceCorrectReports()
    {
        // 1. Setup Data
        using (var scope = _factory.Services.CreateScope())
        {
            // Reset database state just in case
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            
            await SetupMasterData(context);
        }

        // 2. Post Sales Invoice (Sell Service to Customer)
        // Invoice: 1000 USD
        var salesInvoice = new Invoice
        {
            Type = InvoiceType.Sales,
            ContactId = 1, // Customer
            Ref = "INV-001",
            Date = new DateOnly(2023, 1, 1),
            DueDate = new DateOnly(2023, 1, 31),
            Currency = "USD",
            ExchangeRate = 1,
            TotalAmount = 1000,
            Lines = new List<InvoiceLine>
            {
                new InvoiceLine { ProductId = 1, Description = "Consulting", Quantity = 1, UnitPrice = 1000, Subtotal = 1000, Total = 1000 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", salesInvoice);
        // response.EnsureSuccessStatusCode();
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Sales Invoice Failed: {error}");
        }

        // 3. Post Purchase Invoice (Buy Goods from Vendor)
        // Invoice: 500 USD
        var purchaseInvoice = new Invoice
        {
            Type = InvoiceType.Purchase,
            ContactId = 2, // Vendor
            Ref = "BILL-001",
            Date = new DateOnly(2023, 1, 2),
            DueDate = new DateOnly(2023, 1, 31),
            Currency = "USD",
            ExchangeRate = 1,
            TotalAmount = 500,
            Lines = new List<InvoiceLine>
            {
                new InvoiceLine { ProductId = 2, Description = "Laptop", Quantity = 1, UnitPrice = 500, Subtotal = 500, Total = 500 }
            }
        };

        response = await _client.PostAsJsonAsync("/api/invoices", purchaseInvoice);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Purchase Invoice Failed: {error}");
        }

        // 4. Verify Reports
        // Trial Balance
        var tbResponse = await _client.GetFromJsonAsync<TrialBalanceReport>("/api/reports/trial-balance?currency=USD");
        await Assert.That(tbResponse).IsNotNull();
        await Assert.That(tbResponse!.IsBalanced).IsTrue();
        await Assert.That(tbResponse.TotalDebits).IsEqualTo(1500);
        await Assert.That(tbResponse.TotalCredits).IsEqualTo(-1500);

        // Check Individual Account Balances
        var ar = tbResponse.Accounts.FirstOrDefault(a => a.Code == "1100"); // AR
        await Assert.That(ar).IsNotNull();
        await Assert.That(ar!.Balance).IsEqualTo(1000);

        var revenue = tbResponse.Accounts.FirstOrDefault(a => a.Code == "4000"); // Revenue
        await Assert.That(revenue).IsNotNull();
        await Assert.That(revenue!.Balance).IsEqualTo(-1000);

        var inventory = tbResponse.Accounts.FirstOrDefault(a => a.Code == "1200"); // Inventory
        await Assert.That(inventory).IsNotNull();
        // Purchase logic was: Dr Expense/Inventory, Cr AP
        // Product 2 is Goods -> InventoryAccount (1200)
        await Assert.That(inventory!.Balance).IsEqualTo(500);

        var ap = tbResponse.Accounts.FirstOrDefault(a => a.Code == "2100"); // AP
        await Assert.That(ap).IsNotNull();
        await Assert.That(ap!.Balance).IsEqualTo(-500);
    }

    private async Task SetupMasterData(AppDbContext context)
    {
        // Avoid adding if already present (though InMemory should be fresh with EnsureDeleted above)
        if (await context.Accounts.AnyAsync()) return;

        // Accounts
        var ar = new Account { Id = 1, Code = "1100", Name = "Accounts Receivable", Type = AccountType.Asset };
        var inventory = new Account { Id = 2, Code = "1200", Name = "Inventory", Type = AccountType.Asset };
        var ap = new Account { Id = 3, Code = "2100", Name = "Accounts Payable", Type = AccountType.Liability };
        var revenue = new Account { Id = 4, Code = "4000", Name = "Sales Revenue", Type = AccountType.Revenue };
        var expense = new Account { Id = 5, Code = "5000", Name = "General Expense", Type = AccountType.Expense };

        context.Accounts.AddRange(ar, inventory, ap, revenue, expense);

        // Contacts
        context.Contacts.Add(new Contact 
        { 
            Id = 1, 
            Name = "Alice Client", 
            Type = ContactType.Customer, 
            ReceivablesAccountId = 1,
            Currency = "USD"
        });
        
        context.Contacts.Add(new Contact 
        { 
            Id = 2, 
            Name = "Bob Supplier", 
            Type = ContactType.Vendor, 
            PayablesAccountId = 3, 
            Currency = "USD"
        });

        // Products
        context.Products.Add(new Product 
        { 
            Id = 1, 
            Name = "Consulting Service", 
            Type = ProductType.Service, 
            SalesAccountId = 4 
        });
        
        context.Products.Add(new Product 
        { 
            Id = 2, 
            Name = "Office Equipment", 
            Type = ProductType.Goods, 
            InventoryAccountId = 2,
            ExpenseAccountId = 5
        });

        await context.SaveChangesAsync();
    }
}

// Helper classes remain the same
public class TrialBalanceReport
{
    public string Currency { get; set; }
    public bool IsBalanced { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public List<TrialBalanceAccount> Accounts { get; set; }
}

public class TrialBalanceAccount
{
    public int AccountId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal Balance { get; set; }
}
