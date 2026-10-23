namespace PayrollSystem.Models
{
    public class Designation : UserActivity
    {
        public int Id { get; set; }
        public string DesignationName { get; set; }
        public string DesignationDescription { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    }
}
