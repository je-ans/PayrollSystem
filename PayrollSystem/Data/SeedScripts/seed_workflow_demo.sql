/* 
  Demo seed data aligned to Work Schedule workflow:
  1 - Unit/Section/Department (Schedule Approver or Head fallback)
  2 - Division Head
  3 - HR Approver @ Office

  After running: see Data/SeedScripts/WORKFLOW_APPROVAL_TEST_GUIDE.md
*/

SET NOCOUNT ON;

DECLARE @today date = CAST(GETDATE() AS date);
DECLARE @month int = MONTH(@today);
DECLARE @year int = YEAR(@today);

/* -----------------------------------------------------------
   1) Ensure required org hierarchy for upward resolver path
   ----------------------------------------------------------- */
DECLARE @orgRootId int = (SELECT TOP 1 Id FROM dbo.OrganizationStructures WHERE ParentId IS NULL ORDER BY Id);
DECLARE @itDeptId int = (SELECT TOP 1 Id FROM dbo.OrganizationStructures WHERE Code = 'IT');

IF @orgRootId IS NULL OR @itDeptId IS NULL
BEGIN
    RAISERROR('Missing root org or IT department. Seed OrganizationStructures first.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationStructures WHERE Code = 'IT-OFF')
BEGIN
    INSERT INTO dbo.OrganizationStructures (Code, Name, ParentId, Level, IsActive)
    VALUES ('IT-OFF', 'IT Office', @orgRootId, 2, 1);
END

DECLARE @itOfficeId int = (SELECT TOP 1 Id FROM dbo.OrganizationStructures WHERE Code = 'IT-OFF');

IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationStructures WHERE Code = 'IT-DIV')
BEGIN
    INSERT INTO dbo.OrganizationStructures (Code, Name, ParentId, Level, IsActive)
    VALUES ('IT-DIV', 'IT Division', @itOfficeId, 3, 1);
END

DECLARE @itDivisionId int = (SELECT TOP 1 Id FROM dbo.OrganizationStructures WHERE Code = 'IT-DIV');

/* Ensure IT dept hangs under division so upward resolution hits level 3 -> 2 */
UPDATE dbo.OrganizationStructures
SET ParentId = @itDivisionId
WHERE Id = @itDeptId AND (ParentId IS NULL OR ParentId <> @itDivisionId);

DECLARE @itSectionId int = (
    SELECT TOP 1 Id 
    FROM dbo.OrganizationStructures 
    WHERE ParentId = @itDeptId AND Level = 5
    ORDER BY Id
);

IF @itSectionId IS NULL
BEGIN
    INSERT INTO dbo.OrganizationStructures (Code, Name, ParentId, Level, IsActive)
    VALUES ('IT-SEC', 'IT Operations Section', @itDeptId, 5, 1);
    SET @itSectionId = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationStructures WHERE Code = 'IT-UNT')
BEGIN
    INSERT INTO dbo.OrganizationStructures (Code, Name, ParentId, Level, IsActive)
    VALUES ('IT-UNT', 'IT Systems Unit', @itSectionId, 6, 1);
END

DECLARE @itUnitId int = (SELECT TOP 1 Id FROM dbo.OrganizationStructures WHERE Code = 'IT-UNT');

/* -----------------------------------------------------------
   2) Ensure employee records tied to existing application users
   ----------------------------------------------------------- */
DECLARE @sg11Id int = (SELECT TOP 1 Id FROM dbo.RateTypes WHERE Name = 'SG 11');
DECLARE @sg22Id int = (SELECT TOP 1 Id FROM dbo.RateTypes WHERE Name = 'SG 22');
DECLARE @sg18Id int = (SELECT TOP 1 Id FROM dbo.RateTypes WHERE Name = 'SG 18');

DECLARE @adminUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'admin@payroll.com');
DECLARE @hrUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'hr@payroll.com');
DECLARE @payrollUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'payroll@payroll.com');
DECLARE @johnUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'john.doe@company.com');
DECLARE @janeUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'jane.smith@company.com');
DECLARE @mikeUserId nvarchar(450) = (SELECT TOP 1 Id FROM dbo.[User] WHERE Email = 'mike.johnson@company.com');

IF @adminUserId IS NULL OR @hrUserId IS NULL OR @payrollUserId IS NULL
BEGIN
    RAISERROR('Expected users (admin/hr/payroll) not found in dbo.[User].', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeIdNumber = 'EMP900')
BEGIN
    INSERT INTO dbo.Employees
    (
        Id, ApplicationUserId, EmployeeIdNumber, Surname, FirstName, MiddleName,
        Sex, EmploymentBasis, DateHired, DefaultRateTypeId, IsActive,
        EmailAddress, CreatedOn, UpdatedOn, IsDeleted, GovContributionRule
    )
    VALUES
    (
        NEWID(), @adminUserId, 'EMP900', 'Admin', 'System', NULL,
        'Male', 0, '2024-01-01', @sg18Id, 1,
        'admin@payroll.com', GETDATE(), GETDATE(), 0, 0
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeIdNumber = 'EMP901')
BEGIN
    INSERT INTO dbo.Employees
    (
        Id, ApplicationUserId, EmployeeIdNumber, Surname, FirstName, MiddleName,
        Sex, EmploymentBasis, DateHired, DefaultRateTypeId, IsActive,
        EmailAddress, CreatedOn, UpdatedOn, IsDeleted, GovContributionRule
    )
    VALUES
    (
        NEWID(), @hrUserId, 'EMP901', 'Approver', 'HR', NULL,
        'Female', 0, '2024-01-01', @sg22Id, 1,
        'hr@payroll.com', GETDATE(), GETDATE(), 0, 0
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeIdNumber = 'EMP902')
BEGIN
    INSERT INTO dbo.Employees
    (
        Id, ApplicationUserId, EmployeeIdNumber, Surname, FirstName, MiddleName,
        Sex, EmploymentBasis, DateHired, DefaultRateTypeId, IsActive,
        EmailAddress, CreatedOn, UpdatedOn, IsDeleted, GovContributionRule
    )
    VALUES
    (
        NEWID(), @payrollUserId, 'EMP902', 'Approver', 'Payroll', NULL,
        'Male', 0, '2024-01-01', @sg22Id, 1,
        'payroll@payroll.com', GETDATE(), GETDATE(), 0, 0
    );
END

DECLARE @empAdmin uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @adminUserId);
DECLARE @empHr uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @hrUserId);
DECLARE @empPayroll uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @payrollUserId);
DECLARE @empJohn uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @johnUserId);
DECLARE @empJane uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @janeUserId);
DECLARE @empMike uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Employees WHERE ApplicationUserId = @mikeUserId);

/* -----------------------------------------------------------
   3) Employee organization assignments
   ----------------------------------------------------------- */
;WITH a AS (
    SELECT @empJohn AS EmployeeId, @itUnitId AS OrgId, CAST('2024-01-01' AS date) AS EffectiveFrom, CAST(1 AS bit) AS IsPrimary
    UNION ALL SELECT @empMike, @itUnitId, '2024-01-01', 0          -- also on unit schedule grid
    UNION ALL SELECT @empMike, @itSectionId, '2024-01-01', 1      -- primary section for section-head step
    UNION ALL SELECT @empAdmin, @itDeptId, '2024-01-01', 1
    UNION ALL SELECT @empPayroll, @itDivisionId, '2024-01-01', 1
    UNION ALL SELECT @empHr, @itOfficeId, '2024-01-01', 1
    UNION ALL SELECT @empJane, @itOfficeId, '2024-01-01', 0
)
INSERT INTO dbo.EmployeeOrganizationAssignments (EmployeeId, OrganizationStructureId, EffectiveFrom, EffectiveTo, IsPrimary)
SELECT a.EmployeeId, a.OrgId, a.EffectiveFrom, NULL, a.IsPrimary
FROM a
WHERE a.EmployeeId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.EmployeeOrganizationAssignments eoa
      WHERE eoa.EmployeeId = a.EmployeeId
        AND eoa.OrganizationStructureId = a.OrgId
        AND eoa.EffectiveTo IS NULL
  );

/* -----------------------------------------------------------
   4) Employee role assignments to satisfy workflow approvers
   ----------------------------------------------------------- */
;WITH r AS (
    /* Work Schedule — Mike (Section Head) creates when no Scheduler; John approves step 1 */
    SELECT @empJohn AS EmployeeId, @itUnitId AS OrgId, CAST(NULL AS int) AS LeadershipRole, CAST(4 AS int) AS FunctionalRole  -- Schedule Approver
    UNION ALL SELECT @empMike, @itSectionId, 1, NULL -- Section Head (creates schedule via leadership fallback)
    UNION ALL SELECT @empAdmin, @itDeptId, 1, NULL -- Department Head (step 1 fallback)
    UNION ALL SELECT @empPayroll, @itDivisionId, 1, NULL -- Division Head (step 2)
    UNION ALL SELECT @empHr, @itOfficeId, NULL, 3 -- HR Approver (step 3)
    /* DTR */
    UNION ALL SELECT @empAdmin, @itDeptId, NULL, 5 -- DTR Approver
    /* Payroll */
    UNION ALL SELECT @empPayroll, @itOfficeId, NULL, 7 -- Payroll Approver
)
INSERT INTO dbo.EmployeeRoleAssignments (EmployeeId, OrganizationStructureId, LeadershipRole, FunctionalRole, EffectiveFrom, EffectiveTo)
SELECT r.EmployeeId, r.OrgId, r.LeadershipRole, r.FunctionalRole, '2024-01-01', NULL
FROM r
WHERE r.EmployeeId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.EmployeeRoleAssignments era
      WHERE era.EmployeeId = r.EmployeeId
        AND era.OrganizationStructureId = r.OrgId
        AND ISNULL(era.LeadershipRole, -1) = ISNULL(r.LeadershipRole, -1)
        AND ISNULL(era.FunctionalRole, -1) = ISNULL(r.FunctionalRole, -1)
        AND era.EffectiveTo IS NULL
  );

/* -----------------------------------------------------------
   5) Seed current-month Draft Work Schedule for IT Unit
      (left in Draft so you can test Submit in the UI)
   ----------------------------------------------------------- */
DECLARE @scheduleId int;
SELECT @scheduleId = Id
FROM dbo.WorkSchedules
WHERE OrganizationStructureId = @itUnitId
  AND [Month] = @month
  AND [Year] = @year;

IF @scheduleId IS NULL
BEGIN
    INSERT INTO dbo.WorkSchedules
    (
        OrganizationStructureId, [Month], [Year], [Status],
        IsLocked, CreatedBy, CreatedAt, LastModifiedBy, LastModifiedAt
    )
    VALUES
    (
        @itUnitId, @month, @year, 0,
        0, 'seed-script', GETDATE(), 'seed-script', GETDATE()
    );

    SET @scheduleId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    /* Reset seeded schedule to Draft for workflow testing */
    UPDATE dbo.WorkSchedules
    SET [Status] = 0,
        IsLocked = 0,
        LockedBy = NULL,
        LockedAt = NULL,
        SubmittedBy = NULL,
        SubmittedAt = NULL,
        SubmittedByEmployeeId = NULL,
        ApprovedBy = NULL,
        ApprovedAt = NULL,
        RejectedBy = NULL,
        RejectedAt = NULL,
        RejectionReason = NULL,
        ApprovalRequestId = NULL,
        LastModifiedBy = 'seed-script',
        LastModifiedAt = GETDATE()
    WHERE Id = @scheduleId;
END

/* Remove old approval requests tied to this schedule so submit can run again */
DELETE ars
FROM dbo.ApprovalRequestSteps ars
INNER JOIN dbo.ApprovalRequests ar ON ar.Id = ars.ApprovalRequestId
WHERE ar.ProcessType = 1 AND ar.ReferenceId = CAST(@scheduleId AS nvarchar(50));

DELETE FROM dbo.ApprovalRequests
WHERE ProcessType = 1 AND ReferenceId = CAST(@scheduleId AS nvarchar(50));

DECLARE @shiftWorkId int = (SELECT TOP 1 Id FROM dbo.ShiftTypes WHERE Code = 'REG-8-5');
DECLARE @shiftOffId int = (SELECT TOP 1 Id FROM dbo.ShiftTypes WHERE Code = 'DAY OFF');

;WITH d AS
(
    SELECT CAST(DATEFROMPARTS(@year, @month, 1) AS date) AS Dt
    UNION ALL
    SELECT DATEADD(day, 1, Dt)
    FROM d
    WHERE MONTH(DATEADD(day, 1, Dt)) = @month
)
INSERT INTO dbo.ScheduleEntries
(
    WorkScheduleId, EmployeeId, [Day], ShiftTypeId, Remarks, CreatedBy, CreatedAt
)
SELECT
    @scheduleId,
    e.EmployeeId,
    DAY(d.Dt),
    CASE WHEN DATENAME(weekday, d.Dt) IN ('Saturday', 'Sunday') THEN @shiftOffId ELSE @shiftWorkId END,
    NULL,
    'seed-script',
    GETDATE()
FROM d
CROSS APPLY (VALUES (@empJohn), (@empMike)) e(EmployeeId)
WHERE e.EmployeeId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.ScheduleEntries se
      WHERE se.WorkScheduleId = @scheduleId
        AND se.EmployeeId = e.EmployeeId
        AND se.[Day] = DAY(d.Dt)
  )
OPTION (MAXRECURSION 40);

/* -----------------------------------------------------------
   6) Align Work Schedule approval workflow to 3 steps
   ----------------------------------------------------------- */
DECLARE @wsWorkflowId int = (
    SELECT TOP 1 Id
    FROM dbo.ApprovalWorkflows
    WHERE ProcessType = 1 AND IsActive = 1
    ORDER BY Id
);

IF @wsWorkflowId IS NULL
BEGIN
    INSERT INTO dbo.ApprovalWorkflows (Name, ProcessType, IsActive)
    VALUES ('Work Schedule Workflow', 1, 1);
    SET @wsWorkflowId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    UPDATE dbo.ApprovalWorkflows
    SET Name = 'Work Schedule Workflow'
    WHERE Id = @wsWorkflowId;
END

DELETE a
FROM dbo.ApprovalWorkflowStepApprovers a
INNER JOIN dbo.ApprovalWorkflowSteps s ON s.Id = a.WorkflowStepId
WHERE s.WorkflowId = @wsWorkflowId;

DELETE FROM dbo.ApprovalWorkflowSteps WHERE WorkflowId = @wsWorkflowId;

DECLARE @step1 int;
INSERT INTO dbo.ApprovalWorkflowSteps (WorkflowId, StepOrder, Name)
VALUES (@wsWorkflowId, 1, 'Unit/Section/Department Approver');
SET @step1 = SCOPE_IDENTITY();

INSERT INTO dbo.ApprovalWorkflowStepApprovers
    (WorkflowStepId, SourceType, OrganizationLevel, LeadershipRole, FunctionalRole, Priority)
VALUES
    (@step1, 2, 6, NULL, 4, 1),  -- Schedule Approver @ Unit
    (@step1, 2, 5, NULL, 4, 2),  -- Schedule Approver @ Section
    (@step1, 2, 4, NULL, 4, 3),  -- Schedule Approver @ Department
    (@step1, 1, 6, 1, NULL, 4),  -- Head @ Unit
    (@step1, 1, 6, 2, NULL, 5),  -- OIC @ Unit
    (@step1, 1, 5, 1, NULL, 6),  -- Head @ Section
    (@step1, 1, 5, 2, NULL, 7),  -- OIC @ Section
    (@step1, 1, 4, 1, NULL, 8),  -- Head @ Department
    (@step1, 1, 4, 2, NULL, 9);  -- OIC @ Department

DECLARE @step2 int;
INSERT INTO dbo.ApprovalWorkflowSteps (WorkflowId, StepOrder, Name)
VALUES (@wsWorkflowId, 2, 'Division Approver');
SET @step2 = SCOPE_IDENTITY();

INSERT INTO dbo.ApprovalWorkflowStepApprovers
    (WorkflowStepId, SourceType, OrganizationLevel, LeadershipRole, FunctionalRole, Priority)
VALUES
    (@step2, 1, 3, 1, NULL, 1),  -- Division Head
    (@step2, 1, 3, 2, NULL, 2);  -- Division OIC

DECLARE @step3 int;
INSERT INTO dbo.ApprovalWorkflowSteps (WorkflowId, StepOrder, Name)
VALUES (@wsWorkflowId, 3, 'HR Approver');
SET @step3 = SCOPE_IDENTITY();

INSERT INTO dbo.ApprovalWorkflowStepApprovers
    (WorkflowStepId, SourceType, OrganizationLevel, LeadershipRole, FunctionalRole, Priority)
VALUES
    (@step3, 2, 2, NULL, 3, 1),  -- HR Approver @ Office
    (@step3, 1, 2, 1, NULL, 2),  -- Office Head
    (@step3, 1, 2, 2, NULL, 3);  -- Office OIC

/* Remove Scheduler role from John if present so Section Head can create the schedule */
UPDATE dbo.EmployeeRoleAssignments
SET EffectiveTo = @today
WHERE EmployeeId = @empJohn
  AND OrganizationStructureId = @itUnitId
  AND FunctionalRole = 1
  AND EffectiveTo IS NULL;

/* -----------------------------------------------------------
   7) Verification summary
   ----------------------------------------------------------- */
PRINT '--- Workflow demo seed complete ---';
PRINT 'IT Unit Id: ' + CAST(@itUnitId AS varchar(20));
PRINT 'Schedule Id: ' + CAST(@scheduleId AS varchar(20)) + ' (' + CAST(@month AS varchar(2)) + '/' + CAST(@year AS varchar(4)) + ', Draft)';
PRINT '';
PRINT 'Test accounts (passwords from app seed):';
PRINT '  mike.johnson@company.com - Section Head (create/submit schedule when no Scheduler)';
PRINT '  john.doe@company.com     - Unit Schedule Approver (approve step 1)';
PRINT '  admin@payroll.com        - Department Head (step 1 fallback approver)';
PRINT '  payroll@payroll.com      - Division Head (approve step 2)';
PRINT '  hr@payroll.com           - HR Approver (approve step 3)';
PRINT '';
PRINT 'See Data/SeedScripts/WORKFLOW_APPROVAL_TEST_GUIDE.md for step-by-step UI testing.';

SELECT 'Org hierarchy' AS [Check], Code, Name, Level, ParentId
FROM dbo.OrganizationStructures
WHERE Code IN ('ORG','IT-OFF','IT-DIV','IT','IT-SEC','IT-UNT')
ORDER BY Id;

SELECT 'Role assignments' AS [Check],
       e.EmployeeIdNumber,
       os.Code AS OrgCode,
       era.LeadershipRole,
       era.FunctionalRole
FROM dbo.EmployeeRoleAssignments era
INNER JOIN dbo.Employees e ON e.Id = era.EmployeeId
INNER JOIN dbo.OrganizationStructures os ON os.Id = era.OrganizationStructureId
WHERE era.EffectiveTo IS NULL
  AND os.Code IN ('IT-UNT','IT-SEC','IT','IT-DIV','IT-OFF')
ORDER BY os.Code, e.EmployeeIdNumber;
