using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PayrollSystem.ClaimsManagement;
using PayrollSystem.Data;
using PayrollSystem.Models;
using PayrollSystem.Utility;
using PayrollSystem.ViewModels;

namespace PayrollSystem.Controllers
{
    public class PersonalInformationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditHelper _auditHelper;
        private readonly ILogger<PersonalInformationController> _logger;

        public PersonalInformationController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditHelper auditHelper,
            ILogger<PersonalInformationController> logger)
        {
            _userManager = userManager;
            _db = db;
            _auditHelper = auditHelper;
            _logger = logger;
        }

        //=======================================================Private Method=================================================================

        private async Task LogAuditTrailAsync(string action, object oldData, object newData)
        {
            try
            {
                await _auditHelper.LogAuditTrail(action, "PersonalInformation", "Personal Information Records", oldData, newData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit trail for {Action}. OldData: {OldData}, NewData: {NewData}",
                    action, oldData, newData);
            }
        }

        private JsonResult ValidationErrorResponse()
        {
            var errorMessages = ModelStateHelper.GetModelErrors(ModelState);
            _logger.LogWarning("Validation failed: {Errors}", string.Join("; ", errorMessages));
            return Json(new { success = false, message = string.Join("; ", errorMessages) });
        }

        //=======================================================Get Personal Information List=============================================================

        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> Index()
        {
            var personalInformationRecords = await _db.PersonalInformation
                .Include(pi => pi.Employee)
                .Where(pi => !pi.IsDeleted)
                .AsNoTracking()
                .ToListAsync();

            return View(personalInformationRecords);
        }

        //=======================================================Create Personal Information==============================================================

        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> CreatePersonalInformation(Guid employeeId)
        {
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound("Employee not found.");

            var viewModel = new PersonalInformationViewModel { EmployeeId = employeeId };
            ViewBag.EmployeeName = employee.FullName;
            return PartialView("Partials/_CreatePersonalInformation", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> CreatePersonalInformation(PersonalInformationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            try
            {
                if (await _db.PersonalInformation.AnyAsync(pi =>
                    pi.EmployeeId == model.EmployeeId && !pi.IsDeleted))
                {
                    return Json(new { success = false, message = "Personal information for this employee already exists." });
                }

                var personalInformation = new PersonalInformation
                {
                    EmployeeId = model.EmployeeId,
                    Surname = model.Surname,
                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    NameExtension = model.NameExtension,
                    DateOfBirth = model.DateOfBirth,
                    PlaceOfBirth = model.PlaceOfBirth,
                    Sex = model.Sex,
                    CivilStatus = model.CivilStatus,
                    Citizenship = model.Citizenship,
                    Height = model.Height,
                    Weight = model.Weight,
                    BloodType = model.BloodType,
                    GsisIdNo = model.GsisIdNo,
                    PagIbigIdNo = model.PagIbigIdNo,
                    PhilHealthNo = model.PhilHealthNo,
                    SssNo = model.SssNo,
                    TinNo = model.TinNo,
                    AgencyEmployeeNo = model.AgencyEmployeeNo,
                    TelephoneNo = model.TelephoneNo,
                    MobileNo = model.MobileNo,
                    EmailAddress = model.EmailAddress,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedOn = DateTime.UtcNow
                };

                // Handle Addresses
                personalInformation.ResidentialAddress = new Address
                {
                    HouseBlockLotNo = model.ResidentialHouseBlockLotNo,
                    Street = model.ResidentialStreet,
                    SubdivisionVillage = model.ResidentialSubdivisionVillage,
                    Barangay = model.ResidentialBarangay,
                    CityMunicipality = model.ResidentialCityMunicipality,
                    Province = model.ResidentialProvince,
                    ZipCode = model.ResidentialZipCode
                };
                _db.Addresses.Add(personalInformation.ResidentialAddress);

                personalInformation.PermanentAddress = new Address
                {
                    HouseBlockLotNo = model.PermanentHouseBlockLotNo,
                    Street = model.PermanentStreet,
                    SubdivisionVillage = model.PermanentSubdivisionVillage,
                    Barangay = model.PermanentBarangay,
                    CityMunicipality = model.PermanentCityMunicipality,
                    Province = model.PermanentProvince,
                    ZipCode = model.PermanentZipCode
                };
                _db.Addresses.Add(personalInformation.PermanentAddress);

                _db.PersonalInformation.Add(personalInformation);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("Failed to save personal information for employee ID: {EmployeeId}", model.EmployeeId);
                    return Json(new { success = false, message = "Failed to create personal information." });
                }

                await LogAuditTrailAsync("Create Personal Information", null, personalInformation);
                return Json(new { success = true, message = "Personal information created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating personal information for employee ID: {EmployeeId}", model.EmployeeId);
                return Json(new { success = false, message = "An unexpected error occurred." });
            }
        }

        //=======================================================Edit Personal Information===================================================================

        [HttpGet]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> EditPersonalInformation(Guid id)
        {
            var personalInformation = await _db.PersonalInformation
                .Include(pi => pi.Employee)
                .Include(pi => pi.ResidentialAddress)
                .Include(pi => pi.PermanentAddress)
                .Where(pi => pi.Id == id && !pi.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (personalInformation == null) return NotFound();

            var viewModel = new PersonalInformationViewModel
            {
                Id = personalInformation.Id,
                EmployeeId = personalInformation.EmployeeId,
                Surname = personalInformation.Surname,
                FirstName = personalInformation.FirstName,
                MiddleName = personalInformation.MiddleName,
                NameExtension = personalInformation.NameExtension,
                DateOfBirth = personalInformation.DateOfBirth,
                PlaceOfBirth = personalInformation.PlaceOfBirth,
                Sex = personalInformation.Sex,
                CivilStatus = personalInformation.CivilStatus,
                Citizenship = personalInformation.Citizenship,
                Height = personalInformation.Height,
                Weight = personalInformation.Weight,
                BloodType = personalInformation.BloodType,
                GsisIdNo = personalInformation.GsisIdNo,
                PagIbigIdNo = personalInformation.PagIbigIdNo,
                PhilHealthNo = personalInformation.PhilHealthNo,
                SssNo = personalInformation.SssNo,
                TinNo = personalInformation.TinNo,
                AgencyEmployeeNo = personalInformation.AgencyEmployeeNo,
                TelephoneNo = personalInformation.TelephoneNo,
                MobileNo = personalInformation.MobileNo,
                EmailAddress = personalInformation.EmailAddress,

                ResidentialHouseBlockLotNo = personalInformation.ResidentialAddress?.HouseBlockLotNo,
                ResidentialStreet = personalInformation.ResidentialAddress?.Street,
                ResidentialSubdivisionVillage = personalInformation.ResidentialAddress?.SubdivisionVillage,
                ResidentialBarangay = personalInformation.ResidentialAddress?.Barangay,
                ResidentialCityMunicipality = personalInformation.ResidentialAddress?.CityMunicipality,
                ResidentialProvince = personalInformation.ResidentialAddress?.Province,
                ResidentialZipCode = personalInformation.ResidentialAddress?.ZipCode,

                PermanentHouseBlockLotNo = personalInformation.PermanentAddress?.HouseBlockLotNo,
                PermanentStreet = personalInformation.PermanentAddress?.Street,
                PermanentSubdivisionVillage = personalInformation.PermanentAddress?.SubdivisionVillage,
                PermanentBarangay = personalInformation.PermanentAddress?.Barangay,
                PermanentCityMunicipality = personalInformation.PermanentAddress?.CityMunicipality,
                PermanentProvince = personalInformation.PermanentAddress?.Province,
                PermanentZipCode = personalInformation.PermanentAddress?.ZipCode
            };
            ViewBag.EmployeeName = personalInformation.Employee.FullName;
            return PartialView("Partials/_EditPersonalInformation", viewModel);
        }

        [HttpPost]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> EditPersonalInformation(Guid id, PersonalInformationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return ValidationErrorResponse();
            }

            var personalInformation = await _db.PersonalInformation
                .Include(pi => pi.ResidentialAddress)
                .Include(pi => pi.PermanentAddress)
                .FirstOrDefaultAsync(pi => pi.Id == id && !pi.IsDeleted);

            if (personalInformation == null)
            {
                _logger.LogError("Personal information with ID {Id} not found.", id);
                return Json(new { success = false, message = "Personal information not found." });
            }

            // Check if personal information for this employee already exists for another record (if EmployeeId changed)
            if (personalInformation.EmployeeId != model.EmployeeId &&
                await _db.PersonalInformation.AnyAsync(pi => pi.EmployeeId == model.EmployeeId && pi.Id != id && !pi.IsDeleted))
            {
                return Json(new { success = false, message = "Personal information for this employee already exists." });
            }

            var oldPersonalInformationData = JsonSerializer.Deserialize<PersonalInformation>(
                JsonSerializer.Serialize(personalInformation));

            // Manual mapping
            personalInformation.Surname = model.Surname;
            personalInformation.FirstName = model.FirstName;
            personalInformation.MiddleName = model.MiddleName;
            personalInformation.NameExtension = model.NameExtension;
            personalInformation.DateOfBirth = model.DateOfBirth;
            personalInformation.PlaceOfBirth = model.PlaceOfBirth;
            personalInformation.Sex = model.Sex;
            personalInformation.CivilStatus = model.CivilStatus;
            personalInformation.Citizenship = model.Citizenship;
            personalInformation.Height = model.Height;
            personalInformation.Weight = model.Weight;
            personalInformation.BloodType = model.BloodType;
            personalInformation.GsisIdNo = model.GsisIdNo;
            personalInformation.PagIbigIdNo = model.PagIbigIdNo;
            personalInformation.PhilHealthNo = model.PhilHealthNo;
            personalInformation.SssNo = model.SssNo;
            personalInformation.TinNo = model.TinNo;
            personalInformation.AgencyEmployeeNo = model.AgencyEmployeeNo;
            personalInformation.TelephoneNo = model.TelephoneNo;
            personalInformation.MobileNo = model.MobileNo;
            personalInformation.EmailAddress = model.EmailAddress;
            personalInformation.UpdatedById = _userManager.GetUserId(User);
            personalInformation.UpdatedOn = DateTime.UtcNow;

            // Update Residential Address
            if (personalInformation.ResidentialAddress != null)
            {
                personalInformation.ResidentialAddress.HouseBlockLotNo = model.ResidentialHouseBlockLotNo;
                personalInformation.ResidentialAddress.Street = model.ResidentialStreet;
                personalInformation.ResidentialAddress.SubdivisionVillage = model.ResidentialSubdivisionVillage;
                personalInformation.ResidentialAddress.Barangay = model.ResidentialBarangay;
                personalInformation.ResidentialAddress.CityMunicipality = model.ResidentialCityMunicipality;
                personalInformation.ResidentialAddress.Province = model.ResidentialProvince;
                personalInformation.ResidentialAddress.ZipCode = model.ResidentialZipCode;
                _db.Addresses.Update(personalInformation.ResidentialAddress);
            }
            else if (!string.IsNullOrEmpty(model.ResidentialHouseBlockLotNo) ||
                     !string.IsNullOrEmpty(model.ResidentialStreet) ||
                     !string.IsNullOrEmpty(model.ResidentialSubdivisionVillage) ||
                     !string.IsNullOrEmpty(model.ResidentialBarangay) ||
                     !string.IsNullOrEmpty(model.ResidentialCityMunicipality) ||
                     !string.IsNullOrEmpty(model.ResidentialProvince) ||
                     !string.IsNullOrEmpty(model.ResidentialZipCode))
            {
                personalInformation.ResidentialAddress = new Address
                {
                    HouseBlockLotNo = model.ResidentialHouseBlockLotNo,
                    Street = model.ResidentialStreet,
                    SubdivisionVillage = model.ResidentialSubdivisionVillage,
                    Barangay = model.ResidentialBarangay,
                    CityMunicipality = model.ResidentialCityMunicipality,
                    Province = model.ResidentialProvince,
                    ZipCode = model.ResidentialZipCode
                };
                _db.Addresses.Add(personalInformation.ResidentialAddress);
            }

            // Update Permanent Address
            if (personalInformation.PermanentAddress != null)
            {
                personalInformation.PermanentAddress.HouseBlockLotNo = model.PermanentHouseBlockLotNo;
                personalInformation.PermanentAddress.Street = model.PermanentStreet;
                personalInformation.PermanentAddress.SubdivisionVillage = model.PermanentSubdivisionVillage;
                personalInformation.PermanentAddress.Barangay = model.PermanentBarangay;
                personalInformation.PermanentAddress.CityMunicipality = model.PermanentCityMunicipality;
                personalInformation.PermanentAddress.Province = model.PermanentProvince;
                personalInformation.PermanentAddress.ZipCode = model.PermanentZipCode;
                _db.Addresses.Update(personalInformation.PermanentAddress);
            }
            else if (!string.IsNullOrEmpty(model.PermanentHouseBlockLotNo) ||
                     !string.IsNullOrEmpty(model.PermanentStreet) ||
                     !string.IsNullOrEmpty(model.PermanentSubdivisionVillage) ||
                     !string.IsNullOrEmpty(model.PermanentBarangay) ||
                     !string.IsNullOrEmpty(model.PermanentCityMunicipality) ||
                     !string.IsNullOrEmpty(model.PermanentProvince) ||
                     !string.IsNullOrEmpty(model.PermanentZipCode))
            {
                personalInformation.PermanentAddress = new Address
                {
                    HouseBlockLotNo = model.PermanentHouseBlockLotNo,
                    Street = model.PermanentStreet,
                    SubdivisionVillage = model.PermanentSubdivisionVillage,
                    Barangay = model.PermanentBarangay,
                    CityMunicipality = model.PermanentCityMunicipality,
                    Province = model.PermanentProvince,
                    ZipCode = model.PermanentZipCode
                };
                _db.Addresses.Add(personalInformation.PermanentAddress);
            }

            try
            {
                _db.PersonalInformation.Update(personalInformation);
                var result = await _db.SaveChangesAsync();

                if (result <= 0)
                {
                    _logger.LogError("No records updated for personal information ID {Id}", id);
                    return Json(new { success = false, message = "No changes were made." });
                }

                await LogAuditTrailAsync("Update Personal Information", oldPersonalInformationData, personalInformation);
                return Json(new { success = true, message = "Personal information updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating personal information ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while updating." });
            }
        }


        //=======================================================View Personal Information===================================================================


        [HttpGet]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> ViewPersonalInformation(Guid id)
        {
            var personalInformation = await _db.PersonalInformation
                .Include(pi => pi.Employee)
                .Include(pi => pi.ResidentialAddress)
                .Include(pi => pi.PermanentAddress)
                .Where(pi => pi.Id == id && !pi.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (personalInformation == null)
            {
                _logger.LogWarning("Personal information with ID {Id} not found.", id);
                return NotFound();
            }

            var viewModel = new PersonalInformationViewModel
            {
                Id = personalInformation.Id,
                EmployeeId = personalInformation.EmployeeId,
                Surname = personalInformation.Surname,
                FirstName = personalInformation.FirstName,
                MiddleName = personalInformation.MiddleName,
                NameExtension = personalInformation.NameExtension,
                DateOfBirth = personalInformation.DateOfBirth,
                PlaceOfBirth = personalInformation.PlaceOfBirth,
                Sex = personalInformation.Sex,
                CivilStatus = personalInformation.CivilStatus,
                Citizenship = personalInformation.Citizenship,
                Height = personalInformation.Height,
                Weight = personalInformation.Weight,
                BloodType = personalInformation.BloodType,
                GsisIdNo = personalInformation.GsisIdNo,
                PagIbigIdNo = personalInformation.PagIbigIdNo,
                PhilHealthNo = personalInformation.PhilHealthNo,
                SssNo = personalInformation.SssNo,
                TinNo = personalInformation.TinNo,
                AgencyEmployeeNo = personalInformation.AgencyEmployeeNo,
                TelephoneNo = personalInformation.TelephoneNo,
                MobileNo = personalInformation.MobileNo,
                EmailAddress = personalInformation.EmailAddress,

                ResidentialHouseBlockLotNo = personalInformation.ResidentialAddress?.HouseBlockLotNo,
                ResidentialStreet = personalInformation.ResidentialAddress?.Street,
                ResidentialSubdivisionVillage = personalInformation.ResidentialAddress?.SubdivisionVillage,
                ResidentialBarangay = personalInformation.ResidentialAddress?.Barangay,
                ResidentialCityMunicipality = personalInformation.ResidentialAddress?.CityMunicipality,
                ResidentialProvince = personalInformation.ResidentialAddress?.Province,
                ResidentialZipCode = personalInformation.ResidentialAddress?.ZipCode,

                PermanentHouseBlockLotNo = personalInformation.PermanentAddress?.HouseBlockLotNo,
                PermanentStreet = personalInformation.PermanentAddress?.Street,
                PermanentSubdivisionVillage = personalInformation.PermanentAddress?.SubdivisionVillage,
                PermanentBarangay = personalInformation.PermanentAddress?.Barangay,
                PermanentCityMunicipality = personalInformation.PermanentAddress?.CityMunicipality,
                PermanentProvince = personalInformation.PermanentAddress?.Province,
                PermanentZipCode = personalInformation.PermanentAddress?.ZipCode
            };
            ViewBag.EmployeeName = personalInformation.Employee.FullName;
            return PartialView("Partials/_ViewPersonalInformation", viewModel);
        }


        //=======================================================Delete Personal Information===================================================================

        [HttpGet]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> DeletePersonalInformation(Guid id)
        {
            var personalInformation = await _db.PersonalInformation
                .Include(pi => pi.Employee)
                .AsNoTracking()
                .Where(pi => pi.Id == id && !pi.IsDeleted)
                .FirstOrDefaultAsync();

            if (personalInformation == null)
            {
                _logger.LogWarning("Personal information with ID {Id} not found.", id);
                return NotFound();
            }

            // If no restrictions, show normal delete confirmation
            var deleteViewModel = new PersonalInformationViewModel
            {
                Id = personalInformation.Id,
                EmployeeId = personalInformation.EmployeeId,
                Surname = personalInformation.Surname,
                FirstName = personalInformation.FirstName,
                MiddleName = personalInformation.MiddleName,
                NameExtension = personalInformation.NameExtension,
            };
            ViewBag.EmployeeName = personalInformation.Employee.FullName;
            return PartialView("Partials/_DeletePersonalInformation", deleteViewModel);
        }

        [HttpPost]
        [Permission("PERSONALINFORMATION_VIEW")]
        public async Task<ActionResult> DeleteConfirmed(Guid id)
        {
            var personalInformation = await _db.PersonalInformation.FindAsync(id);
            if (personalInformation == null || personalInformation.IsDeleted)
            {
                _logger.LogWarning("Personal information with ID {Id} not found or already deleted.", id);
                return Json(new { success = false, message = "Personal information not found or already deleted." });
            }

            var oldData = new PersonalInformationViewModel
            {
                Id = personalInformation.Id,
                EmployeeId = personalInformation.EmployeeId,
                Surname = personalInformation.Surname,
                FirstName = personalInformation.FirstName,
                MiddleName = personalInformation.MiddleName,
                NameExtension = personalInformation.NameExtension,
                DateOfBirth = personalInformation.DateOfBirth,
                PlaceOfBirth = personalInformation.PlaceOfBirth,
                Sex = personalInformation.Sex,
                CivilStatus = personalInformation.CivilStatus,
                Citizenship = personalInformation.Citizenship,
                Height = personalInformation.Height,
                Weight = personalInformation.Weight,
                BloodType = personalInformation.BloodType,
                GsisIdNo = personalInformation.GsisIdNo,
                PagIbigIdNo = personalInformation.PagIbigIdNo,
                PhilHealthNo = personalInformation.PhilHealthNo,
                SssNo = personalInformation.SssNo,
                TinNo = personalInformation.TinNo,
                AgencyEmployeeNo = personalInformation.AgencyEmployeeNo,
                TelephoneNo = personalInformation.TelephoneNo,
                MobileNo = personalInformation.MobileNo,
                EmailAddress = personalInformation.EmailAddress,
            };

            personalInformation.IsDeleted = true;
            personalInformation.UpdatedById = _userManager.GetUserId(User);
            personalInformation.UpdatedOn = DateTime.UtcNow;

            try
            {
                _db.PersonalInformation.Update(personalInformation);
                var result = await _db.SaveChangesAsync();

                if (result > 0)
                {
                    await LogAuditTrailAsync("Delete Personal Information", oldData, null);
                    return Json(new { success = true, message = "Personal information deleted successfully." });
                }

                _logger.LogError("Failed to delete personal information ID {Id}.", id);
                return Json(new { success = false, message = "Failed to delete personal information." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting personal information ID {Id}.", id);
                return Json(new { success = false, message = "An error occurred while deleting." });
            }
        }

        //==========================================================================================================================
    }
}
