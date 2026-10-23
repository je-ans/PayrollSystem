using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PayrollSystem.Models
{
    public class PayrollPeriodEmployee : UserActivity
    {
        public int PayrollPeriodId { get; set; }
        public PayrollPeriod PayrollPeriod { get; set; }

        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; }
    }
}