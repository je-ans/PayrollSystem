namespace PayrollSystem.Models
{
    public class Department : UserActivity
    {
        public int Id { get; set; }
        public string DepartmentName { get; set; }
        public string DepartmentDescription { get; set; }

        public bool IsActive { get; set; } = true;


        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }

}
