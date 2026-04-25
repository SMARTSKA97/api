using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.DAL.Models;

/**
 * Hardened Summary Models: Precision-Aligned with Postgres Schema
 * Targeting 85 Lakh Scale high-performance hardened tables.
 */

[Table("fy_summary_admin", Schema = "dashboard")]
public class FySummaryAdmin
{
    [Column("financial_year")]
    public int FinancialYear { get; set; }
    
    [Column("received_fto")]
    public int ReceivedFto { get; set; }
    
    [Column("processed_fto")]
    public int ProcessedFto { get; set; }
    
    [Column("generated_bills")]
    public int GeneratedBills { get; set; }
    
    [Column("forwarded_to_treasury")]
    public int ForwardedToTreasury { get; set; }
    
    [Column("received_by_approver")]
    public int ReceivedByApprover { get; set; }
    
    [Column("rejected_by_approver")]
    public int RejectedByApprover { get; set; }

    [Column("bill_amount")]
    public decimal BillAmount { get; set; }

    [Column("forwarded_amount")]
    public decimal ForwardedAmount { get; set; }

    [Column("fto_amount")]
    public decimal FtoAmount { get; set; }
}

[Table("fy_summary_approver", Schema = "dashboard")]
public class FySummaryApprover 
{
    [Column("financial_year")]
    public int FinancialYear { get; set; }

    [Column("ddo_code")]
    public string DdoCode { get; set; } = "";
    
    [Column("received_fto")]
    public int ReceivedFto { get; set; }
    
    [Column("processed_fto")]
    public int ProcessedFto { get; set; }
    
    [Column("generated_bills")]
    public int GeneratedBills { get; set; }
    
    [Column("forwarded_to_treasury")]
    public int ForwardedToTreasury { get; set; }
    
    [Column("received_by_approver")]
    public int ReceivedByApprover { get; set; }
    
    [Column("rejected_by_approver")]
    public int RejectedByApprover { get; set; }

    [Column("bill_amount")]
    public decimal BillAmount { get; set; }

    [Column("forwarded_amount")]
    public decimal ForwardedAmount { get; set; }

    [Column("fto_amount")]
    public decimal FtoAmount { get; set; }
}

[Table("fy_summary_operator", Schema = "dashboard")]
public class FySummaryOperator
{
    [Column("financial_year")]
    public int FinancialYear { get; set; }

    [Column("ddo_code")]
    public string DdoCode { get; set; } = "";

    [Column("userid")]
    public string UserId { get; set; } = "";
    
    [Column("received_fto")]
    public int ReceivedFto { get; set; }
    
    [Column("processed_fto")]
    public int ProcessedFto { get; set; }
    
    [Column("generated_bills")]
    public int GeneratedBills { get; set; }
    
    [Column("forwarded_to_treasury")]
    public int ForwardedToTreasury { get; set; }
    
    [Column("received_by_approver")]
    public int ReceivedByApprover { get; set; }
    
    [Column("rejected_by_approver")]
    public int RejectedByApprover { get; set; }

    [Column("bill_amount")]
    public decimal BillAmount { get; set; }

    [Column("forwarded_amount")]
    public decimal ForwardedAmount { get; set; }

    [Column("fto_amount")]
    public decimal FtoAmount { get; set; }
}
