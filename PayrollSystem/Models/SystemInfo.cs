namespace PayrollSystem.Models
{
    public class SystemInfo : UserActivity
    {
        public int Id { get; set; }
        public string SystemName { get; set; }

        public string ShortName { get; set; }
        public string Description { get; set; }

        public string LogoUrl { get; set; }
    }
}
