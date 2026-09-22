namespace TaskBoard.Domain;

public class Label
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Hex color, e.g. "#FF8800".</summary>
    public required string Color { get; set; }

    // Many-to-many navigation, populated by EF Core via the TaskItem side.
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
