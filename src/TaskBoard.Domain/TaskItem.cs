namespace TaskBoard.Domain;

public class TaskItem
{
    public int Id { get; set; }

    public int BoardId { get; set; }

    public Board Board { get; set; } = null!;

    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Many-to-many navigation, the other side of Label.Tasks.
    public ICollection<Label> Labels { get; set; } = new List<Label>();
}
