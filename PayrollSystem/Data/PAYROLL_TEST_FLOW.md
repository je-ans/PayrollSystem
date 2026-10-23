# Payroll Test Flow: From Period to Payslip

This guide walks you through testing payroll end-to-end: **creating a payroll period**, **assigning employees and time cards**, and **running payroll** to see tax and government contributions in the output.

---

## Quick Test (Using Seeded Data)

If you have already run the application with seed data:

1. **Log in** as `admin@payroll.com` / `Admin123!` (or `payroll@payroll.com` / `Payroll123!`).

2. Go to **Payroll Management → Payroll Runs**.

3. Open the **Draft** run named *"Payroll Run for First Half [Month YYYY]"*.

4. Click **Process Payroll**. The system will:
   - Use each employee’s **time card** (88 regular hours for the first half).
   - Compute **gross pay** from Regular Day hours and **allowances**.
   - Apply **withholding tax** (BIR TRAIN brackets, effective for the period).
   - Apply **SSS, PhilHealth, Pag-IBIG** from contribution tables effective for the period.
   - Set **Net Pay** = Gross + Benefits − Deductions − Tax − Gov’t employee share.

5. **View output**: Run summary and each employee’s **payslip** (tax and government contributions listed).

---

## Full Process Flow (Manual Steps)

### Step 1: Create a Payroll Period

1. Go to **Payroll Management → Payroll Periods**.
2. Click **Add Payroll Period**.
3. Fill in:
   - **Period Name**: e.g. `First Half March 2026`
   - **Period Start Date**: e.g. `2026-03-01`
   - **Period End Date**: e.g. `2026-03-15`
   - **Pay Date**: e.g. `2026-03-20`
4. Save.

The period is used to define the date range for the payroll (semi-monthly: 1–15 or 16–EOM; or full month).

---

### Step 2: Assign Employees and Time Card

1. In **Payroll Periods**, find the period you created.
2. Open the **Action** dropdown → **Employee Time Card** (or **Assign Employees & Time Card**).
3. You’ll see:
   - **Employees assigned** to this period (add/remove if your app supports it).
   - **Time card** per employee: hours by **Day Type** (Regular Day, Overtime, Night Differential, etc.).

4. For each employee, enter or confirm:
   - **Regular Day** hours (e.g. 88 for first half = 11 days × 8 hrs).
   - **Overtime** / **Night Differential** if any.
5. **Save** the assignment and time card.

**Notes:**

- If **no time card** exists, payroll may fall back to **attendance records** for the period (if you use DTR/attendance).
- **Day types** and **Day Type Rates** (multipliers) must be set up under Payroll Configuration / Day Types so that Regular, OT, and Night Diff pay correctly.

---

### Step 3: Create a Payroll Run (Two Options)

**Option A – From Payroll Period (create and process in one go)**

1. In **Payroll Periods**, open the **Action** dropdown for the period.
2. Click **Run Payroll**.
3. Confirm. The system will:
   - Create a **PayrollRun** for that period.
   - **Process** it immediately (compute payslips, tax, gov’t contributions).
   - Mark the run as **Completed** and show totals.

**Option B – From Payroll Runs (create draft, then process)**

1. Go to **Payroll Management → Payroll Runs**.
2. Click **Create** (or Add).
3. Select the **Payroll Period** (e.g. "First Half March 2026") and enter a **Run name**.
4. Save. The run is created in **Draft** status.
5. Open the run and click **Process Payroll**. The system runs the same calculation as in Option A and sets status to **Completed**.

---

### Step 4: View Output

After processing:

- **Run summary**: Total Employees, Total Gross Pay, Total Deductions (includes tax + gov’t employee share), Total Net Pay.
- **Per employee**: Open the run and view each **Payroll Entry** or **Payslip**. You should see:
  - **Earnings**: Regular Pay, Overtime, Night Differential, Benefits (allowances).
  - **Deductions**: Absence, Late/Undertime (if any), **Withholding Tax**.
  - **Government contributions**: **SSS**, **PhilHealth**, **Pag-IBIG** (employee share; employer share may be shown for reporting).
  - **Net Pay**.

---

## What the Calculation Uses

| Item | Source |
|------|--------|
| **Gross pay** | Time card (or attendance): Regular + Overtime + Night Differential hours × rates from Day Type Rates. |
| **Taxable income** | Gross pay − non-taxable benefits (configurable). |
| **Withholding tax** | **Tax tables** effective for the period date range; income normalized to bracket’s **Income Basis** (e.g. Annual), then tax converted back to period. |
| **Gov’t contributions** | **Government contribution tables** (SSS, PhilHealth, Pag-IBIG) effective for the period; lookup by **monthly basic salary**; Fixed / Percentage / Tiered + Min/Max caps. |
| **Net pay** | Gross + Benefits − Deductions (absence, late/undertime, **tax**) − **total gov’t employee share**. |

---

## Sample Data (Seeded)

After seeding you get:

- **Payroll periods**: First Half [Month], Second Half [Month], Full Month [Month] for current month.
- **Employees**: John Doe (SG 11), Jane Smith (SG 22), Mike Johnson (SG 2) with Salary Grade rate types and allowances.
- **Assignments**: All three employees assigned to each period.
- **Time cards**: One per employee per period (e.g. 88 regular hours for first half, 176 for full month).
- **One Draft run**: For **First Half** so you can open it and click **Process Payroll** to test immediately.

Ensure **Tax Tables** and **Government Contribution Tables** have **Effective Start Date** (and optionally End Date) covering the period dates; the payroll service filters by these dates.

---

## Checklist for a Successful Test

- [ ] At least one **Payroll Configuration** is active (working days, etc.).
- [ ] **Day Types** and **Day Type Rates** exist (e.g. Regular Day with basic multiplier 1.0).
- [ ] **Tax Tables** are active and effective for the period (e.g. current year start date).
- [ ] **Government Contribution Tables** (SSS, PhilHealth, Pag-IBIG) are active and effective for the period.
- [ ] Employees have a **Default Rate Type** (monthly/daily/hourly) so basic salary and hourly rate resolve.
- [ ] For the chosen period, **employees are assigned** and each has a **time card** (or attendance) so gross pay is non-zero.
- [ ] **Run Payroll** or **Process Payroll** is clicked so the run status becomes **Completed** and payslips show tax and gov’t contributions.

---

## Troubleshooting

- **No tax / no gov’t contributions**: Check that Tax Tables and Government Contribution Tables have **Effective Start Date** ≤ period end and (if set) **Effective End Date** ≥ period start.
- **Zero gross pay**: Ensure the period has **time cards** (or attendance) with hours and that employees have **Default Rate Type** and **Day Type Rates** are set.
- **“No employees assigned”**: Use **Assign Employees & Time Card** for that period and add employees + time card, then run payroll again (create a new run if the previous one was already processed).
