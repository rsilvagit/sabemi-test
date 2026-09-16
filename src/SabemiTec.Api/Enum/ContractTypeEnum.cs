namespace SabemiTec.Api.Enum;

// Demo-only classification for the mocked `contract` table (see migration 0002) — not
// derived from any webhook field.
public class ContractTypeEnum : Enumeration
{
    public static readonly ContractTypeEnum Loan = new(1, "Emprestimo");
    public static readonly ContractTypeEnum Insurance = new(2, "Seguro");

    public ContractTypeEnum(int id, string name) : base(id, name) { }
}
