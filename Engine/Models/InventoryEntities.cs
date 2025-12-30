using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace accounting_engine.Models;

public enum StockMovementType { In, Out, Transfer, Adjustment }
public enum StockMovementStatus { Draft, Completed, Voided }

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class StockMovement
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Ref { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }
    
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    
    public StockMovementStatus Status { get; set; }
    
    public List<StockMovementLine> Lines { get; set; } = new();
}

public class StockMovementLine
{
    public int Id { get; set; }
    
    public int MovementId { get; set; }
    public StockMovement Movement { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18, 4)")]
    public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalCost { get; set; }
}
