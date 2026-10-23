using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PayrollSystem.Utility;

namespace PayrollSystem.ClaimsManagement
{
    public abstract class AttributeAuthorizationHandler<TRequirement, TAttribute> : AuthorizationHandler<TRequirement>
        where TRequirement : IAuthorizationRequirement where TAttribute : Attribute
    {
        private readonly IHttpContextAccessor _contextAccessor;

        public AttributeAuthorizationHandler(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement)
        {
            var metadata = _contextAccessor.HttpContext?.GetEndpoint()?.Metadata;
            var attributes = metadata == null
                ? new List<PermissionAttribute>()
                : metadata.OfType<PermissionAttribute>().ToList();

            return HandleRequirementAsync(context, requirement, attributes);
        }

        protected abstract Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TRequirement requirement,
            IEnumerable<PermissionAttribute> attributes);
    }

    public class PermissionAuthorizationRequirement : IAuthorizationRequirement
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermissionAttribute : AuthorizeAttribute
    {
        public string Name { get; }

        public PermissionAttribute(string name) : base("Permission")
        {
            Name = name;
        }
    }

    public class PermissionAuthorizationHandler : AttributeAuthorizationHandler<PermissionAuthorizationRequirement, PermissionAttribute>
    {
        public PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionAuthorizationRequirement requirement,
            IEnumerable<PermissionAttribute> attributes)
        {
            if (attributes == null || !attributes.Any())
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (PermissionHelper.IsPrivilegedUser(context.User))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            foreach (var permissionAttribute in attributes)
            {
                if (permissionAttribute == null || string.IsNullOrWhiteSpace(permissionAttribute.Name))
                    continue;

                if (PermissionHelper.HasPermission(context.User, permissionAttribute.Name))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            context.Fail();
            return Task.CompletedTask;
        }
    }
}
