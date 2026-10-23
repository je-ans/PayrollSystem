namespace PayrollSystem.Models
{
    public class SystemTask : UserActivity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string TaskName { get; set; }
        public int? ParentId { get; set; }
        public virtual SystemTask? Parent { get; set; }

        public ICollection<SystemTask>? ChildTasks { get; set; }

        public int? OrderNumber { get; set; }
    }
}
