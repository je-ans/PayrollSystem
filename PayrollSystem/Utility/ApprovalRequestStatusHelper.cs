using PayrollSystem.Models;

namespace PayrollSystem.Utility
{
    public static class ApprovalRequestStatusHelper
    {
        public static JustificationStatus ResolveEffectiveStatus(
            JustificationStatus entityStatus,
            ApprovalRequestStatus? workflowStatus)
        {
            if (!workflowStatus.HasValue)
                return entityStatus;

            return workflowStatus.Value switch
            {
                ApprovalRequestStatus.Approved => JustificationStatus.Approved,
                ApprovalRequestStatus.Rejected => JustificationStatus.Rejected,
                ApprovalRequestStatus.Cancelled => JustificationStatus.Rejected,
                _ => JustificationStatus.Pending
            };
        }

        public static bool IsPending(JustificationStatus entityStatus, ApprovalRequestStatus? workflowStatus) =>
            ResolveEffectiveStatus(entityStatus, workflowStatus) == JustificationStatus.Pending;

        public static OvertimeStatus ResolveEffectiveStatus(
            OvertimeStatus entityStatus,
            ApprovalRequestStatus? workflowStatus)
        {
            if (!workflowStatus.HasValue)
                return entityStatus;

            return workflowStatus.Value switch
            {
                ApprovalRequestStatus.Approved => OvertimeStatus.Approved,
                ApprovalRequestStatus.Rejected => OvertimeStatus.Rejected,
                ApprovalRequestStatus.Cancelled => OvertimeStatus.Rejected,
                _ => OvertimeStatus.Pending
            };
        }

        public static bool IsPending(OvertimeStatus entityStatus, ApprovalRequestStatus? workflowStatus) =>
            ResolveEffectiveStatus(entityStatus, workflowStatus) == OvertimeStatus.Pending;
    }
}
