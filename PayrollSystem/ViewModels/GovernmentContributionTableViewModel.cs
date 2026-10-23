using System;
using System.ComponentModel.DataAnnotations;
using PayrollSystem.Models;

namespace PayrollSystem.ViewModels
{
    public class GovernmentContributionTableViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Contribution Type is required.")]
        [Display(Name = "Contribution Type")]
        public int GovernmentContributionTypeId { get; set; }
        public virtual GovernmentContributionType? GovernmentContributionType { get; set; }

        // ── Salary Range ──────────────────────────────
        [Required(ErrorMessage = "Minimum Salary is required.")]
        [Display(Name = "Minimum Salary")]
        public decimal MinSalary { get; set; }

        [Display(Name = "Maximum Salary")]
        public decimal? MaxSalary { get; set; }

        // ── Fixed Amount Fields (SSS) ─────────────────
        [Display(Name = "Employee Share (Fixed)")]
        public decimal? EmployeeShare { get; set; }

        [Display(Name = "Employer Share (Fixed)")]
        public decimal? EmployerShare { get; set; }

        [Display(Name = "Total Contribution (Fixed)")]
        public decimal? TotalContribution { get; set; }

        // ── Percentage Fields (PhilHealth, Pag-IBIG) ──
        [Display(Name = "Employee Share % (e.g. 0.02 = 2%)")]
        public decimal? EmployeeSharePercentage { get; set; }

        [Display(Name = "Employer Share % (e.g. 0.02 = 2%)")]
        public decimal? EmployerSharePercentage { get; set; }

        // ── Caps / Floor ──────────────────────────────
        [Display(Name = "Maximum Employee Contribution")]
        public decimal? MaxEmployeeContribution { get; set; }

        [Display(Name = "Maximum Employer Contribution")]
        public decimal? MaxEmployerContribution { get; set; }

        [Display(Name = "Minimum Employee Contribution")]
        public decimal? MinEmployeeContribution { get; set; }

        // ── Effectivity ───────────────────────────────
        [Required(ErrorMessage = "Effective Start Date is required.")]
        [Display(Name = "Effective Start Date")]
        public DateTime EffectiveStartDate { get; set; } = DateTime.Today;

        [Display(Name = "Effective End Date")]
        public DateTime? EffectiveEndDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }
}