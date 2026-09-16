namespace SabemiTec.Api.Enum;

public class ProcessingStatusEnum : Enumeration
{
    public static readonly ProcessingStatusEnum Pending = new(0, nameof(Pending));
    public static readonly ProcessingStatusEnum Locked = new(1, nameof(Locked));
    public static readonly ProcessingStatusEnum Processed = new(2, nameof(Processed));
    public static readonly ProcessingStatusEnum DeadLettered = new(3, nameof(DeadLettered));

    public ProcessingStatusEnum(int id, string name) : base(id, name) { }
}
