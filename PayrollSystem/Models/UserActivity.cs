namespace PayrollSystem.Models
{
    public class UserActivity
    {
        public string? CreatedById { get; set; }

        public ApplicationUser? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? UpdatedById { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
