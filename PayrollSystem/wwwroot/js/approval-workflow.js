(function () {
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function confirmDelete(title, text) {
        return Swal.fire({
            icon: 'warning',
            title: title,
            text: text,
            showCancelButton: true,
            confirmButtonColor: '#dc2626',
            confirmButtonText: 'Yes, delete',
            cancelButtonText: 'Cancel'
        }).then(r => r.isConfirmed);
    }

    $(document).on('click', '.aw-delete-step', async function () {
        const stepId = $(this).data('step-id');
        const stepName = $(this).data('step-name');
        if (!stepId) return;

        const ok = await confirmDelete('Delete step?', `"${stepName}" and its approver rules will be removed.`);
        if (!ok) return;

        $.post('/ApprovalWorkflow/DeleteStep', {
            id: stepId,
            __RequestVerificationToken: getToken()
        }).done(function (res) {
            if (res.success) {
                location.reload();
            } else {
                Swal.fire({ icon: 'error', text: res.message || 'Could not delete step.' });
            }
        }).fail(function () {
            Swal.fire({ icon: 'error', text: 'Could not delete step.' });
        });
    });

    $(document).on('click', '.aw-delete-approver', async function () {
        const approverId = $(this).data('approver-id');
        if (!approverId) return;

        const ok = await confirmDelete('Remove approver?', 'This fallback rule will be removed from the step.');
        if (!ok) return;

        $.post('/ApprovalWorkflow/DeleteApprover', {
            id: approverId,
            __RequestVerificationToken: getToken()
        }).done(function (res) {
            if (res.success) {
                location.reload();
            } else {
                Swal.fire({ icon: 'error', text: res.message || 'Could not remove approver.' });
            }
        }).fail(function () {
            Swal.fire({ icon: 'error', text: 'Could not remove approver.' });
        });
    });

    // Index — render workflow cards
    const $grid = $('#awWorkflowGrid');
    if ($grid.length) {
        $.get('/ApprovalWorkflow/GetWorkflows').done(function (res) {
            if (!res.success) {
                $grid.html('<div class="col-12 text-center text-danger py-4">Could not load workflows.</div>');
                return;
            }

            if (!res.data?.length) {
                $grid.html(`
                    <div class="aw-empty-state col-12">
                        <div><i class="fas fa-project-diagram d-block"></i></div>
                        <h5>No workflows configured</h5>
                        <p>Add a default fallback workflow and per-process workflows in System Settings.</p>
                    </div>`);
                return;
            }

            const workflowCards = res.data.map(function (w) {
                const icon = w.processIcon || 'fa-project-diagram';
                const accent = w.accentClass || 'aw-accent-slate';
                const statusBadge = w.isActive
                    ? '<span class="badge badge-success">Active</span>'
                    : '<span class="badge badge-secondary">Inactive</span>';
                const fallbackNote = w.isFallback
                    ? '<span class="badge badge-info ml-1">Fallback</span>'
                    : '';

                return `
                <div class="aw-workflow-card${w.isFallback ? ' aw-fallback-card' : ''}">
                    <div class="aw-workflow-card-header">
                        <div class="aw-process-icon ${accent}"><i class="fas ${icon}"></i></div>
                        <div>
                            <h4 class="aw-workflow-card-title">${w.name}</h4>
                            <div class="aw-workflow-card-meta">${w.processTypeDisplay}${fallbackNote}</div>
                        </div>
                    </div>
                    <div class="aw-workflow-card-footer">
                        <span class="aw-step-pill">
                            <i class="fas fa-list-ol"></i> ${w.stepCount} step${w.stepCount === 1 ? '' : 's'}
                        </span>
                        ${statusBadge}
                    </div>
                    <div class="d-flex flex-wrap gap-1 mt-1">
                        <a href="/ApprovalWorkflow/Configure/${w.id}" class="btn btn-primary btn-sm flex-grow-1">
                            <i class="fas fa-sliders-h mr-1"></i> Configure Steps
                        </a>
                        <a href="javascript:void(0)" class="btn btn-outline-secondary btn-sm open-modal"
                           data-title="Edit Workflow"
                           data-url="/ApprovalWorkflow/Edit/${w.id}"
                           data-modal-type="edit">
                            <i class="fas fa-cog"></i>
                        </a>
                    </div>
                </div>`;
            }).join('');

            $grid.html(workflowCards);
        });
    }
})();
