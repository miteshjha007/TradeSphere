namespace TradeSphere.Domain.Entities
{
    public class PropFirm : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public string Name { get; set; }
        public string? WebsiteUrl { get; set; }
        public string Status { get; set; } = "Active";
        public string? Notes { get; set; }
    }
}
