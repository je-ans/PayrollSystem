using PayrollSystem.Models;

namespace PayrollSystem.Models.BuildUp
{
    public class EmployeeDesignation
    {
        public int Id { get; set; }

        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int DesignationId { get; set; }
        public Designation? Designation { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
