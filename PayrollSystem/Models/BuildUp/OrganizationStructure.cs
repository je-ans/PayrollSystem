using PayrollSystem.Models;

namespace PayrollSystem.Models.BuildUp
{
    public class OrganizationStructure
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public OrganizationStructure? Parent { get; set; }

        public ICollection<OrganizationStructure> Children { get; set; }
            = new List<OrganizationStructure>();

        // only descriptive, not logic-based
        public OrganizationLevel Level { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
