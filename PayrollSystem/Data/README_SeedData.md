# Sample Data Seeding Guide

## Overview
The `SeedData.cs` file contains comprehensive sample data for testing the payroll system. It automatically runs when the application starts and populates all necessary tables with realistic test data.

## What Gets Seeded

### 1. **System Configuration**
- PayrollConfiguration: Default payroll settings with realistic values
- DayTypes: Regular Day, Saturday, Sunday, Regular Holiday, Special Holiday, Rest Day (with pay multipliers)

### 2. **Organizational Structure**
- Departments: IT, HR, Finance, Operations, Marketing
- Designations: Software Developer, Senior Developer, HR Manager, Accountant, etc.
- RateTypes: Philippine Salary Grades SG 1–SG 33 (2024 First Tranche, Step 1 rates in PHP)

### 3. **Government Contributions**
- GovernmentContributionTypes: SSS, PhilHealth, Pag-IBIG with proper calculation methods
- GovernmentContributionTables: Actual contribution brackets for current year

### 4. **Tax System**
- TaxTables: Philippine tax brackets with progressive calculation

### 5. **Users & Employees**
- Users: Admin, HR Manager, Payroll Manager, 3 regular employees
- Employees: John Doe (IT), Jane Smith (HR), Mike Johnson (Finance) with complete profiles

### 6. **Work Structure**
- Shifts: Regular Shift (8AM-5PM), Night Shift (10PM-6AM)
- WorkSchedules: Monday-Friday schedule
- EmployeeAllowances: Transportation, Meal, Phone allowances

### 7. **Payroll Periods, Assignments, Time Cards & Runs**
- **PayrollPeriods**: Three periods for the current month:
  - **First Half [Month YYYY]** (1st–15th) — semi-monthly
  - **Second Half [Month YYYY]** (16th–end) — semi-monthly
  - **Full Month [Month YYYY]** — full month
- **PayrollPeriodEmployees**: All active employees are assigned to each of the three periods.
- **PayrollPeriodTimeCards**: For each employee in each period, a sample time card with:
  - **Regular Day** hours: 88 hrs (11 working days × 8) for semi-monthly, 176 hrs (22 × 8) for full month
  - No overtime or night differential (you can add these via **Assign Employees & Time Card**)
- **PayrollRuns**: One **Draft** run is created for the **First Half** period so you can test immediately: open it and click **Process Payroll** to see tax and government contributions.

### 8. **Time & Attendance**
- AttendanceRecords and TimeEntries are not seeded; use DTR or manual attendance if needed.

## How to Use

### Automatic Seeding
The seed data runs automatically when the application starts. No manual action required.

### Manual Re-seeding (if needed)
If you need to re-run the seeding:

1. **Delete existing data** (optional - seeding handles duplicates)
2. **Restart the application** - seeding runs on startup

### Testing Payroll (End-to-End)

See **[PAYROLL_TEST_FLOW.md](PAYROLL_TEST_FLOW.md)** for the full process: entering a payroll period, assigning employees and time cards, and running payroll to test tax and government contribution output.

With the seeded data you can run a full payroll and see tax + government contributions:

1. **Login** as admin (`admin@payroll.com` / `Admin123!`) or payroll manager (`payroll@payroll.com` / `Payroll123!`).

2. **Create a payroll period** (if needed):  
   Go to **Payroll → Payroll Periods**. Seed data already creates:
   - **First Half [Month YYYY]** (e.g. 1–15)
   - **Second Half [Month YYYY]** (e.g. 16–end)
   - **Full Month [Month YYYY]**

3. **Assign employees and time cards** (optional; seed already does this):  
   Open a period → **Assign Employees & Time Card**. Seed data has already:
   - Assigned John Doe, Jane Smith, Mike Johnson to each period
   - Added a time card per employee with **Regular Day** hours (88 hrs for semi-monthly, 176 for full month).  
   You can edit hours or add overtime/night differential here.

4. **Use the seeded payroll run (or create one)**:  
   Seed data creates one **Draft** run for **First Half [Month]**:
   - Go to **Payroll → Payroll Runs** and open **"Payroll Run for First Half ..."**.
   - Or create a new run: **Create** → choose **Payroll Period** (e.g. "First Half March 2025") → **Run name** → Save.

5. **Process the payroll**:  
   Open the run → **Process Payroll**. The system will:
   - Use each employee’s time card (or attendance) for the period
   - Compute gross pay from Regular Day hours (and overtime/night diff if any)
   - Look up **Tax Tables** (effective for the period) and compute withholding tax
   - Look up **Government Contribution Tables** (SSS, PhilHealth, Pag-IBIG) by monthly salary and apply employee + employer shares
   - Deduct tax and government employee share from net pay

6. **View output**:  
   After processing, view the run summary and each employee’s **payslip** to see:
   - Gross pay, benefits, deductions (absence, late/undertime, **tax**)
   - **Government contributions** (SSS, PhilHealth, Pag-IBIG)
   - Net pay

## Sample Data Details

### Test Employees
- **John Doe** (EMP001): IT Developer, SG 11 (₱28,512/month)
- **Jane Smith** (EMP002): HR Manager, SG 22 (₱74,836/month)
- **Mike Johnson** (EMP003): Accountant, SG 2 (₱14,372/month)

### Sample Calculations
Each employee has:
- Basic salary based on RateType
- Monthly allowances (Transportation, Meal, Phone)
- Government contributions (SSS, PhilHealth, Pag-IBIG)
- Tax withholding based on progressive tax brackets
- Regular 8-hour attendance for all working days

### Expected Results
- **John Doe**: ~28.5K basic (SG 11) + allowances - deductions - taxes - contributions
- **Jane Smith**: ~74.8K basic (SG 22) + allowances - deductions - taxes - contributions
- **Mike Johnson**: ~14.4K basic (SG 2) + allowances - deductions - taxes - contributions

## Data Relationships

The seeded data maintains proper foreign key relationships:
- Employees → Departments & Designations
- Employees → RateTypes & Users
- AttendanceRecords → Employees & WorkSchedules
- TimeEntries → AttendanceRecords
- GovernmentContributionTables → GovernmentContributionTypes

## Customization

To modify the seed data:
1. Edit `SeedData.cs` methods
2. Change values in the data arrays
3. Restart application to apply changes

## Troubleshooting

If seeding fails:
1. Check database connection
2. Ensure foreign key constraints are satisfied
3. Check application logs for specific errors
4. Try running with a fresh database

## Security Note

The seeded user passwords are for testing only:
- Admin: `Admin123!`
- HR: `Hr123!`
- Payroll: `Payroll123!`
- Employees: `Employee123!`

**Change these passwords in production!**