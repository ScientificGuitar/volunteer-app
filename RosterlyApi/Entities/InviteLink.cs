namespace RosterlyApi.Entities;

public class InviteLink
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
}
