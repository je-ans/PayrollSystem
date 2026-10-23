namespace PayrollSystem.Models
{
    public class AuditTrail
    {
        public int Id { get; set; }

        public string Action { get; set; }

        public string Module { get; set; }
        public string AffectedTable { get; set; }

        public string OldData { get; set; } // Store JSON representation of old data
        public string NewData { get; set; } // Store JSON representation of new data

        public DateTime TimeStamp { get; set; } = DateTime.Now;

        public string IpAddress { get; set; }

        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

    }
}
