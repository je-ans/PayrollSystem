using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PayrollSystem.Data;
using PayrollSystem.Models;

namespace PayrollSystem.ClaimsManagement
{
    public class MyUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        public MyUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager, ApplicationDbContext db,
            IOptions<IdentityOptions> options,
            IMemoryCache cache) : base(userManager, roleManager, options)
        {
            _db = db;
            _userManager = userManager;
            _cache = cache;

        }

        //protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        //{
        //    var identity = await base.GenerateClaimsAsync(user);
        //    var userRoles = await _userManager.GetRolesAsync(user);

        //    //if (userRoles.Any())
        //    //{
        //    //    var userRole = userRoles.First();
        //    //    var role = await _db.Roles.SingleOrDefaultAsync(i => i.Name == userRole);

        //    //    if(role != null)
        //    //    {
        //    //        var permissions = await _db.UserRoleProfiles
        //    //            .Where(i => i.RoleId == role.Id)
        //    //            .Select(i => $"@{i.Task.Parent.TaskName}:{i.Task.TaskName}")
        //    //            .ToListAsync();

        //    //        var allUserPermissions = "";
        //    //        foreach (var right in permissions)
        //    //        {
        //    //            allUserPermissions += $"|{right?.ToUpper()}";
        //    //        }
        //    //        identity.AddClaim(new Claim("UserPermission", allUserPermissions));
        //    //    }
        //    //}

        //    //return identity;



        //    var userRoleProfile = userRoles.FirstOrDefault();

        //    var allUserPermissions = "";

        //    if (userRoleProfile != null)
        //    {
        //        var userRole = await _db.Roles.SingleAsync(i => i.Name == userRoleProfile);

        //        var userPermissions = _db.UserRoleProfiles
        //            .Where(i => i.RoleId == userRole.Id)
        //            .Select(i => i.Task.Parent.TaskName + ":" + i.Task.TaskName).ToList();

        //        foreach (var permission in userPermissions)
        //        {
        //            allUserPermissions += $"@{permission}";
        //        }
        //    }
        //    identity.AddClaim(new Claim("UserPermission", allUserPermissions));
        //    return identity;

        //}

        //protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        //{
        //    var identity = await base.GenerateClaimsAsync(user);
        //    identity.AddClaim(new Claim("Department", user.DepartmentId.ToString()));
        //    identity.AddClaim(new Claim("Designation", user.DesignationId.ToString()));
        //    return identity;
        //}

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            var userRoles = await _userManager.GetRolesAsync(user);

            Console.WriteLine($"User roles: {string.Join(", ", userRoles)}");

            var userRoleProfile = userRoles.FirstOrDefault();

            var allUserPermissions = "";

            if (userRoleProfile != null)
            {
                var cacheKey = $"Role__{userRoleProfile}";
                ApplicationRole userRole;

                if (!_cache.TryGetValue(cacheKey, out userRole))
                {
                    userRole = await _db.Roles.SingleOrDefaultAsync(i => i.Name == userRoleProfile);
                    if (userRole != null)
                    {
                        _cache.Set(cacheKey, userRole, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                            Size = 1
                        });
                    }
                }

                if (userRole != null)
                {
                    var userPermissions = await _db.AssignRolePermissions
                        .AsNoTracking()
                        .Include(i => i.Task)
                        .Include(i => i.Task.Parent)
                        .Where(i => i.RoleId == userRole.Id && i.Task != null && !i.IsDeleted)
                        .Select(i => new
                        {
                            TaskCode = i.Task!.Code,
                            ParentTaskCode = i.Task.Parent != null ? i.Task.Parent.Code : null
                        })
                        .ToListAsync();

                    var permissionStrings = userPermissions.Select(i =>
                        string.IsNullOrEmpty(i.ParentTaskCode)
                            ? (i.TaskCode ?? string.Empty)
                            : ((i.ParentTaskCode ?? string.Empty) + ":" + (i.TaskCode ?? string.Empty)))
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct()
                        .ToList();

                    allUserPermissions = string.Join("|", permissionStrings);
                    identity.AddClaim(new Claim("UserPermission", allUserPermissions));
                }
            }


            return identity;
        }

    }
}
