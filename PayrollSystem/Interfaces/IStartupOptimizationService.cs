using System.Threading.Tasks;

namespace PayrollSystem.Interfaces
{
    public interface IStartupOptimizationService
    {
        Task WarmUpAsync();
    }
}