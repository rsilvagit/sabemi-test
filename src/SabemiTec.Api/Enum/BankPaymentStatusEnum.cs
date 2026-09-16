namespace SabemiTec.Api.Enum;

// Name carries the partner's literal wire value ("PAGO"/"FALHA") — Id is just a synthetic
// identity for Enumeration's equality/GetAll machinery.
public class BankPaymentStatusEnum : Enumeration
{
    public static readonly BankPaymentStatusEnum Paid = new(1, "PAGO");
    public static readonly BankPaymentStatusEnum Failed = new(2, "FALHA");

    public BankPaymentStatusEnum(int id, string name) : base(id, name) { }

    public static BankPaymentStatusEnum? TryParse(string? raw) =>
        GetAll<BankPaymentStatusEnum>().FirstOrDefault(status => string.Equals(status.Name, raw?.Trim(), StringComparison.OrdinalIgnoreCase));
}
