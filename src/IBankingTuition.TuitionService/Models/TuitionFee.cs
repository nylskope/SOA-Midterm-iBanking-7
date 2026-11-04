namespace IBankingTuition.TuitionService.Models;

public class TuitionFee {
    public string FeeId { get; set; } = string.Empty;
    public string Mssv { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public int RowVersion { get; set; }
    
    public Student Student { get; set; } = null!;
}