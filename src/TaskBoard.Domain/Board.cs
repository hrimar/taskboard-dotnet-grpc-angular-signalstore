namespace TaskBoard.Domain;

public class Board
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // One-to-many navigation: a board owns its tasks.
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
