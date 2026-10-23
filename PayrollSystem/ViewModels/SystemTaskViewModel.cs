using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class SystemTaskViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string TaskName { get; set; }
        public int? ParentId { get; set; }
        public SystemTask? Parent { get; set; }

        public ICollection<SystemTask>? ChildTasks { get; set; }

        public int? OrderNumber { get; set; }
    }
}
