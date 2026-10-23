using PayrollSystem.Models;

namespace PayrollSystem.Models.BuildUp
{
    public class EmployeeOrganizationAssignment
    {
        public int Id { get; set; }

        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int OrganizationStructureId { get; set; }
        public OrganizationStructure? OrganizationStructure { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }


        // The organization unit whose monthly work schedule includes this employee.
        // Only one active assignment per employee should be primary.
        public bool IsPrimary { get; set; }

        public bool IsActive => EffectiveTo == null;
    }
}
