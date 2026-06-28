namespace RosterlyApi.Entities;

public class Signup
{
    public Guid Id { get; set; }
    public Guid TimeSlotId { get; set; }
    public string VolunteerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public TimeSlot TimeSlot { get; set; } = null!;
}
