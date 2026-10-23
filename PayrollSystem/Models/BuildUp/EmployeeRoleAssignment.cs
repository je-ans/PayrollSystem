using PayrollSystem.Models;

namespace PayrollSystem.Models.BuildUp
{
    public class EmployeeRoleAssignment
    {
        public int Id { get; set; }

        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int OrganizationStructureId { get; set; }
        public OrganizationStructure? OrganizationStructure { get; set; }

        public OrganizationLeadershipRole? LeadershipRole { get; set; }
        public OrganizationFunctionalRole? FunctionalRole { get; set; }

        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        public bool IsActive => EffectiveTo == null;
    }
}
