using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PayrollSystem.Utility
{
    public static class ModelStateHelper
    {
        // Utility method to extract model errors
        public static string GetModelErrors(ModelStateDictionary modelState)
        {
            return string.Join(", ", modelState.Values
                .SelectMany(v => v.Errors)  // Flatten all error collections
                .Select(e => e.ErrorMessage));  // Extract the error message
        }
    }
}
