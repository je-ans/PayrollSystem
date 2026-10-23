using System.Threading.Tasks;
using PayrollSystem.Models;

namespace PayrollSystem.Services
{
    public interface IPayrollService
    {
        Task ProcessPayrollCalculationsAsync(PayrollRun payrollRun);
    }
}