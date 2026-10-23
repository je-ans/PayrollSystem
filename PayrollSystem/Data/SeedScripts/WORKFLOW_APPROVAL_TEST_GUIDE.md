# Work Schedule Approval — Test Guide

Use this after running `seed_workflow_demo.sql` against your database.

## 1. Run the seed

1. Open SQL Server Management Studio (or Azure Data Studio).
2. Connect to the same database your app uses.
3. Execute:

   `Data/SeedScripts/seed_workflow_demo.sql`

4. Confirm the script prints **Workflow demo seed complete** and returns org/role rows.

The script:

- Builds org path: `ORG → IT-OFF → IT-DIV → IT → IT-SEC → IT-UNT`
- Sets the **3-step** Work Schedule workflow (see below)
- Assigns approver roles matching those steps
- Removes **Scheduler** from John so **Mike (Section Head)** can create the schedule
- Creates a **Draft** monthly schedule for **IT Systems Unit** (current month)

---

## 2. Test accounts

| Login | Password (default seed) | Role in workflow |
|-------|-------------------------|------------------|
| `mike.johnson@company.com` | `Employee123!` | **Create + Submit** schedule (Section Head, no Scheduler on unit) |
| `john.doe@company.com` | `Employee123!` | **Approve step 1** (Unit Schedule Approver) |
| `admin@payroll.com` | `Admin123!` | Step 1 fallback (Department Head) + SuperAdmin override |
| `payroll@payroll.com` | `Payroll123!` | **Approve step 2** (Division Head) |
| `hr@payroll.com` | `Hr123!` | **Approve step 3** (HR Approver) |

---

## 3. Expected workflow steps (Work Schedule)

Active workflow **Work Schedule Workflow** (ProcessType = 1):

| Step | Name | Resolver rule | Seeded approver |
|------|------|---------------|-----------------|
| 1 | Unit/Section/Department Approver | Tries in order: Schedule Approver @ Unit → Head @ Unit → Head @ Section → Head @ Department | John Doe (Unit Schedule Approver) |
| 2 | Division Approver | Leadership @ Division — Head | Payroll Approver |
| 3 | HR Approver | Functional @ Office — HR Approver | HR Approver |

Anchor org for routing: **IT Systems Unit** (`IT-UNT`).

Skipped steps are hidden in the timeline; remaining steps are renumbered 1, 2, 3.

---

## 4. UI test — happy path (approve all steps)

### Step A — Create & Submit (Mike)

1. Log in as **mike.johnson@company.com**.
2. Go to **Employee Portal → Monthly Schedule** (or **Attendance Monitoring → Work Schedule**).
3. Select org unit **IT Systems Unit** for the **current month**.
4. Confirm status is **Draft** — edit shifts if needed.
5. Click **Submit**.
6. Expected:
   - Status → **Submitted**
   - Timeline shows step 1 **Pending** (John)
   - Mike does **not** see Approve (he is not step 1 approver)

### Step B — Approve step 1 (John)

1. Log in as **john.doe@company.com**.
2. Open **Pending Approvals** or the schedule detail.
3. Click **Approve**.
4. Expected: Step 1 approved, step 2 pending (Payroll / Division Head).

### Step C — Approve step 2 (Payroll)

1. Log in as **payroll@payroll.com**.
2. **Pending Approvals** → **Review** → **Approve**.
3. Expected: Step 2 approved, step 3 pending (HR).

### Step D — Approve step 3 (HR) — final

1. Log in as **hr@payroll.com**.
2. Open the schedule → **Approve**.
3. Expected:
   - All steps **Approved**
   - Schedule status → **Approved**
   - Schedule locked

---

## 5. UI test — rejection path

1. Reset schedule to Draft (re-run seed script, or use **Reopen** if already approved).
2. Submit as Mike.
3. Log in as the user who owns the **current pending step** (check timeline).
4. Click **Reject** and enter a reason.
5. Expected:
   - Request → **Rejected**
   - Schedule → **Rejected** with reason shown
   - Mike can edit and **Submit** again after fixes

---

## 6. UI test — skipped steps (optional)

If you remove a role assignment (e.g. Unit Schedule Approver) and submit again:

- That step is marked **Skipped** in the database
- Workflow continues to the next resolvable step
- If **all** steps are skipped, the request auto-approves

To try: Organization Structure → IT Systems Unit → end John's Schedule Approver role → submit as Mike. Step 1 should fall back to Section Head (Mike) or Department Head (Admin).

---

## 7. SQL checks (troubleshooting)

```sql
-- Workflow steps
SELECT s.StepOrder, s.Name, a.SourceType, a.OrganizationLevel, a.LeadershipRole, a.FunctionalRole, a.Priority
FROM dbo.ApprovalWorkflowSteps s
JOIN dbo.ApprovalWorkflowStepApprovers a ON a.WorkflowStepId = s.Id
JOIN dbo.ApprovalWorkflows w ON w.Id = s.WorkflowId
WHERE w.ProcessType = 1 AND w.IsActive = 1
ORDER BY s.StepOrder, a.Priority;

-- Schedule status
SELECT Id, OrganizationStructureId, [Month], [Year], [Status], ApprovalRequestId
FROM dbo.WorkSchedules
WHERE OrganizationStructureId = (SELECT Id FROM dbo.OrganizationStructures WHERE Code = 'IT-UNT');

-- Who can create (no Scheduler on unit = Head can edit)
SELECT os.Code, era.FunctionalRole, e.EmployeeIdNumber
FROM dbo.EmployeeRoleAssignments era
JOIN dbo.OrganizationStructures os ON os.Id = era.OrganizationStructureId
JOIN dbo.Employees e ON e.Id = era.EmployeeId
WHERE os.Code = 'IT-UNT' AND era.EffectiveTo IS NULL;
```

---

## 8. Common issues

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Head cannot edit/create schedule | **Scheduler** still assigned on the unit | End Scheduler role, or use **Assign Scheduler** on schedule detail |
| Submit button hidden for Mike | No edit access on IT-UNT | Re-run seed (removes John's Scheduler); confirm Mike is Section Head |
| No Approve for John after submit | Workflow still has old 5-step or 2-step config | Re-run `seed_workflow_demo.sql` (section 6 resets workflow) |
| HR step skipped | HR Approver not at **Office** level | Seed assigns HR @ IT-OFF; workflow step 3 uses Office level |
| Step 1 resolves to wrong person | Higher-priority role exists | Check fallback order: Unit Schedule Approver before Section Head |
| Approve button visible but action fails | User has approver role but it is not their step | Rebuild app — Approve only shows for the **current** step approver |
| Wrong approver at step 1 / 2 / 3 | Old 2-step or 5-step workflow still in DB | Restart app (DbInitializer syncs to 3 steps) or re-run `seed_workflow_demo.sql` |
| Submit then instantly **Approved** | Submitter was also step 1 approver, or all steps skipped | Rebuild app — submitter cannot approve own schedule; assign a different step 1 approver |
| Scheduler at section cannot submit unit schedule | Scheduler scope was section-only | Rebuild app — Scheduler role now includes descendant units |

---

## 9. Assigning a Scheduler (optional)

When a unit has no Scheduler, the Section/Department Head can create the schedule. To delegate:

1. Open **Monthly Schedule** detail for the unit.
2. Click **Assign Scheduler**.
3. Pick an employee assigned to that unit.

After a Scheduler is assigned, only that role (plus admins) can edit/submit; the Head can still use **Assign Scheduler** to change who holds the role.
