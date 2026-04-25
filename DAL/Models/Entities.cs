using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.DAL.Models;

[Table("users", Schema = "master")]
public class User
{
    [Key]
    [Column("userid")]
    public string UserId { get; set; } = null!;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [Column("role")]
    public string Role { get; set; } = null!;

    [Column("ddo_code")]
    public string? DdoCode { get; set; }
}

[Table("ddo", Schema = "master")]
public class Ddo
{
    [Key]
    [Column("ddo_code")]
    public string DdoCode { get; set; } = null!;

    [Column("ddo_name")]
    public string DdoName { get; set; } = null!;
}

[Table("fto_list", Schema = "fto")]
public class FtoList
{
    [Column("fto_no")]
    public string FtoNo { get; set; } = null!;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("userid")]
    public string UserId { get; set; } = null!;

    [Column("ddo_code")]
    public string DdoCode { get; set; } = null!;

    [Column("fto_status")]
    public int FtoStatus { get; set; }

    [Column("financial_year")]
    public int FinancialYear { get; set; }

    [Column("fto_creation_date")]
    public DateTime FtoCreationDate { get; set; }

    [Column("fto_processed_date")]
    public DateTime? FtoProcessedDate { get; set; }

    [Column("bill_no")]
    public Guid? BillNo { get; set; }
}

[Table("bill_list", Schema = "bills")]
public class BillList
{
    [Key]
    [Column("bill_no")]
    public Guid BillNo { get; set; }

    [Column("ref_no")]
    public string RefNo { get; set; } = null!;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("bill_date")]
    public DateTime BillDate { get; set; }

    [Column("bill_status")]
    public int BillStatus { get; set; }

    [Column("userid")]
    public string UserId { get; set; } = null!;

    [Column("ddo_code")]
    public string DdoCode { get; set; } = null!;

    [Column("financial_year")]
    public int FinancialYear { get; set; }
}

[Table("bill_status_log", Schema = "bills")]
public class BillStatusLog
{
    [Key]
    [Column("log_id")]
    public long LogId { get; set; }

    [Column("bill_no")]
    public Guid BillNo { get; set; }

    [Column("old_status")]
    public int? OldStatus { get; set; }

    [Column("new_status")]
    public int NewStatus { get; set; }

    [Column("changed_by_userid")]
    public string ChangedByUserId { get; set; } = null!;

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }
}

public abstract class DailyLedgerBase
{
    [Column("financial_year")]
    public int FinancialYear { get; set; }

    [Column("ledger_date")]
    public DateTime LedgerDate { get; set; }

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

[Table("daily_ledger_admin", Schema = "dashboard")]
public class DailyLedgerAdmin : DailyLedgerBase { }

[Table("daily_ledger_approver", Schema = "dashboard")]
public class DailyLedgerApprover : DailyLedgerBase
{
    [Column("ddo_code")]
    public string DdoCode { get; set; } = null!;
}

[Table("daily_ledger_operator", Schema = "dashboard")]
public class DailyLedgerOperator : DailyLedgerBase
{
    [Column("ddo_code")]
    public string DdoCode { get; set; } = null!;

    [Column("userid")]
    public string UserId { get; set; } = null!;
}
