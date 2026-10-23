(function () {
    'use strict';

    const CFG = window.WS_CONFIG || window.WS_DETAIL_CONFIG || {};
    const DETAIL_CFG = window.WS_DETAIL_CONFIG || {};
    const MONTH = CFG.month ?? DETAIL_CFG.month;
    const YEAR = CFG.year ?? DETAIL_CFG.year;
    const IS_EDITABLE = CFG.isEditable ?? DETAIL_CFG.isEditable;

    function getOrgUnitId() {
        const select = document.getElementById('wsOrganizationStructureId')
            || document.querySelector('select[name="organizationStructureId"]');
        if (select) {
            const fromSelect = parseInt(select.value, 10);
            if (fromSelect > 0) return fromSelect;
        }

        const hidden = document.getElementById('wsOrganizationStructureId')
            || document.getElementById('wsOrganizationStructureIdValue')
            || document.querySelector('input[type="hidden"][name="organizationStructureId"]');
        if (hidden) {
            const fromHidden = parseInt(hidden.value, 10);
            if (fromHidden > 0) return fromHidden;
        }

        const fromConfig = parseInt(CFG.organizationStructureId, 10);
        if (Number.isFinite(fromConfig) && fromConfig > 0) return fromConfig;

        const fromDetail = parseInt(DETAIL_CFG.organizationStructureId, 10);
        return Number.isFinite(fromDetail) && fromDetail > 0 ? fromDetail : 0;
    }
    let TOKEN = CFG.antiForgeryToken;

    // Extract token properly if it contains HTML
    if (TOKEN && TOKEN.includes('value="')) {
        const match = TOKEN.match(/value="([^"]+)"/);
        if (match) {
            TOKEN = match[1];
        }
    }

    // Also try to get token from hidden input if available
    if (!TOKEN || TOKEN === '@Html.AntiForgeryToken()') {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (tokenInput) {
            TOKEN = tokenInput.value;
        }
    }

    let selectedShift = null;
    let longPressTimer = null;
    let isLongPressing = false;
    let currentHoverCell = null;
    let activeCell = null;

    // Toast notification
    function toast(msg, type = 'success') {
        const el = document.getElementById('wsToast');
        if (!el) return;
        el.textContent = msg;
        el.className = `ws-toast ws-toast-show ws-toast-${type}`;
        clearTimeout(el._timer);
        el._timer = setTimeout(() => {
            el.classList.remove('ws-toast-show');
        }, 2800);
    }

    // API call helper
    async function post(url, data) {
        try {
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': TOKEN,
                    'X-Requested-With': 'XMLHttpRequest'
                },
                credentials: 'same-origin',
                body: JSON.stringify(data)
            });

            const contentType = res.headers.get('content-type') || '';
            const isJson = contentType.includes('application/json');

            if (!res.ok) {
                let errorMsg = `HTTP ${res.status}`;
                if (isJson) {
                    const errorData = await res.json();
                    errorMsg = errorData.error || errorData.message || errorData.title || errorMsg;
                } else {
                    const text = await res.text();
                    if (text.includes('<!DOCTYPE') || text.includes('<html')) {
                        errorMsg = res.status === 403
                            ? 'You are not allowed to perform this action.'
                            : 'The server returned an unexpected response. Please refresh and try again.';
                    } else if (text) {
                        errorMsg = text;
                    }
                }
                return { ok: false, data: { error: errorMsg } };
            }

            if (!isJson) {
                return {
                    ok: false,
                    data: { error: 'The server returned an unexpected response. Please refresh and try again.' }
                };
            }

            const json = await res.json();
            return { ok: true, data: json };
        } catch (error) {
            console.error('Fetch error:', error);
            return { ok: false, data: { error: error.message } };
        }
    }

    // Format numbers
    function formatNum(n) {
        if (n === undefined || n === null) return '0';
        const num = parseFloat(n);
        if (isNaN(num)) return '0';
        return num.toFixed(3).replace(/\.?0+$/, '') || '0';
    }

    // Update summary row
    function updateSummaryRow(s) {
        const daysEl = document.querySelector(`[data-emp-summary-days="${s.employeeId}"]`);
        const hoursEl = document.querySelector(`[data-emp-summary-hours="${s.employeeId}"]`);
        const offEl = document.querySelector(`[data-emp-summary-off="${s.employeeId}"]`);
        setSummaryCell(daysEl, formatNum(s.dutyDays));
        setSummaryCell(hoursEl, formatNum(s.dutyHours));
        setSummaryCell(offEl, s.leaveOffCount || 0);
    }

    function setSummaryCell(el, value) {
        if (!el) return;
        const badge = el.querySelector('.ws-summary-badge');
        if (badge) badge.textContent = value;
        else el.textContent = value;
    }

    // Update on-duty count
    function updateOnDutyCell(day) {
        const cells = document.querySelectorAll(`.cell[data-day="${day}"]`);
        let on = 0;
        let total = cells.length;
        cells.forEach(c => {
            const shiftId = parseInt(c.dataset.shiftId, 10);
            if (!shiftId) return;
            const opt = document.querySelector(`#shiftPicker option[value="${shiftId}"]`);
            if (opt && opt.dataset.isLeave === 'false') on++;
        });
        const odCell = document.querySelector(`.on-duty-cell[data-day="${day}"]`);
        if (odCell) {
            const total = cells.length;
            const target = odCell.querySelector('.on-duty-pill') || odCell;
            target.textContent = `${on}/${total}`;
        }
    }

    // Apply shift to cell UI
    function applyShiftToCell(cell, shift) {
        const span = cell.querySelector('.cell-text');
        const isBlank = !shift || !shift.id;
        if (span) {
            span.textContent = isBlank ? '' : shift.code;
            span.title = isBlank ? '' : (shift.label || '');
        }
        cell.style.setProperty('--shift-color', isBlank ? '#f3f4f6' : shift.color);
        cell.dataset.shiftId = isBlank ? '' : String(shift.id);

        const opt = !isBlank
            ? document.querySelector(`#shiftPicker option[value="${shift.id}"]`)
            : null;
        const isOff = opt && opt.dataset.isLeave === 'true';
        cell.classList.toggle('cell-off', !!isOff);
        cell.classList.toggle('cell-duty', !isOff && !isBlank);
        cell.classList.toggle('cell-blank', isBlank);
        if (span) span.classList.toggle('cell-text-off', !!isOff);
    }

    // Save cell to server
    async function saveCell(cell, shift, showToast = false) {
        const empId = cell.dataset.emp;
        const day = parseInt(cell.dataset.day, 10);
        const currentShiftId = parseInt(cell.dataset.shiftId, 10);

        if (currentShiftId === shift.id) return false;

        const orgUnitId = getOrgUnitId();
        if (!orgUnitId) {
            toast('Select an organization unit before assigning shifts.', 'error');
            return false;
        }

        applyShiftToCell(cell, shift);
        cell.classList.add('cell-saving');

        const { ok, data } = await post('/WorkSchedule/SaveEntry', {
            month: MONTH,
            year: YEAR,
            organizationStructureId: orgUnitId,
            employeeId: empId,
            day: day,
            shiftTypeId: shift.id,
            remarks: ''
        });

        cell.classList.remove('cell-saving');

        const summary = data.updatedSummary ?? data.UpdatedSummary;
        const succeeded = data.success ?? data.Success;

        if (ok && succeeded !== false) {
            if (summary) {
                updateSummaryRow(summary);
                updateOnDutyCell(day);
            }
            cell.classList.add('cell-saved-flash');
            setTimeout(() => cell.classList.remove('cell-saved-flash'), 500);
            if (showToast) toast(`✓ ${shift.code} assigned`, 'success');
            return true;
        } else {
            // Revert on error
            const originalOpt = document.querySelector(`#shiftPicker option[value="${currentShiftId}"]`);
            if (originalOpt) {
                const originalShift = {
                    id: currentShiftId,
                    code: originalOpt.dataset.code,
                    color: originalOpt.dataset.color
                };
                applyShiftToCell(cell, originalShift);
            }
            toast(`Error: ${data.error || data.Error || 'Save failed'}`, 'error');
            return false;
        }
    }

    // Prevent text selection during drag
    function preventSelection(cell) {
        cell.addEventListener('dragstart', (e) => {
            e.preventDefault();
            return false;
        });

        cell.addEventListener('selectstart', (e) => {
            e.preventDefault();
            return false;
        });
    }

    // Long press functions
    function startLongPress(cell) {
        if (!selectedShift) return;

        longPressTimer = setTimeout(async () => {
            isLongPressing = true;
            activeCell = cell;

            cell.classList.add('long-press-active');
            await saveCell(cell, selectedShift, false);

            // Create floating indicator
            const indicator = document.createElement('div');
            indicator.className = 'long-press-indicator';
            indicator.textContent = '✚ ' + selectedShift.code;
            document.body.appendChild(indicator);

            const updateIndicator = (e) => {
                if (indicator) {
                    indicator.style.left = (e.clientX + 15) + 'px';
                    indicator.style.top = (e.clientY + 15) + 'px';
                }
            };

            const handleMouseEnter = async (e) => {
                if (!isLongPressing || !selectedShift) return;
                const hoveredCell = e.currentTarget;
                if (hoveredCell !== currentHoverCell) {
                    currentHoverCell = hoveredCell;
                    await saveCell(hoveredCell, selectedShift, false);
                    hoveredCell.style.backgroundColor = '#dbeafe';
                    setTimeout(() => {
                        if (hoveredCell) hoveredCell.style.backgroundColor = '';
                    }, 150);
                }
            };

            const handleMouseMove = (e) => {
                if (isLongPressing && indicator) {
                    updateIndicator(e);
                }
            };

            const handleMouseUp = () => {
                endLongPress();
                if (indicator) indicator.remove();
                document.removeEventListener('mousemove', handleMouseMove);
                document.removeEventListener('mouseup', handleMouseUp);
            };

            document.querySelectorAll('.cell-editable').forEach(c => {
                c.addEventListener('mouseenter', handleMouseEnter);
            });

            document.addEventListener('mousemove', handleMouseMove);
            document.addEventListener('mouseup', handleMouseUp);

            window._longPressCleanup = () => {
                document.querySelectorAll('.cell-editable').forEach(c => {
                    c.removeEventListener('mouseenter', handleMouseEnter);
                });
                if (indicator) indicator.remove();
            };

            toast('Long-press active - drag over cells to assign', 'info');
        }, 500);
    }

    function endLongPress() {
        if (longPressTimer) {
            clearTimeout(longPressTimer);
            longPressTimer = null;
        }
        if (isLongPressing) {
            isLongPressing = false;
            if (activeCell) {
                activeCell.classList.remove('long-press-active');
                activeCell = null;
            }
            if (window._longPressCleanup) {
                window._longPressCleanup();
                window._longPressCleanup = null;
            }
            document.querySelectorAll('.cell-editable').forEach(c => {
                c.style.opacity = '';
                c.style.backgroundColor = '';
            });
            currentHoverCell = null;
        }
    }

    // Workflow action function
    function buildDetailUrl() {
        const orgUnitId = getOrgUnitId();
        const controller = CFG.detailController || DETAIL_CFG.detailController || 'WorkSchedule';
        return `/${controller}/Detail?month=${MONTH}&year=${YEAR}&organizationStructureId=${orgUnitId}`;
    }

    async function workflowAction(url, body, confirmOptions) {
        if (confirmOptions && typeof Swal !== 'undefined') {
            const result = await Swal.fire({
                icon: confirmOptions.icon || 'question',
                title: confirmOptions.title || 'Confirm',
                text: confirmOptions.text || '',
                showCancelButton: true,
                confirmButtonText: confirmOptions.confirmText || 'Yes, continue',
                cancelButtonText: 'Cancel',
                reverseButtons: true
            });
            if (!result.isConfirmed) return;
        } else if (confirmOptions && typeof confirmOptions === 'string') {
            if (!confirm(confirmOptions)) return;
        }

        const orgUnitId = getOrgUnitId();
        if (!orgUnitId) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'warning', title: 'Organization Unit Required', text: 'Select an organization unit before running this action.' });
            } else {
                toast('Organization unit is required before running this action.', 'error');
            }
            return;
        }
        body.organizationStructureId = orgUnitId;

        // Show loading state on the button
        const btnSubmit = document.getElementById('btnSubmit');
        const btnApprove = document.getElementById('btnApprove');
        const btnReject = document.getElementById('btnReject');
        const btnReopen = document.getElementById('btnReopen');

        const buttons = [btnSubmit, btnApprove, btnReject, btnReopen];
        buttons.forEach(btn => {
            if (btn) {
                btn.disabled = true;
                const originalHtml = btn.innerHTML;
                btn.dataset.originalHtml = originalHtml;
                btn.innerHTML = '<i class="bi bi-hourglass-split"></i> Processing...';
            }
        });

        const { ok, data } = await post(url, body);

        // Re-enable buttons
        buttons.forEach(btn => {
            if (btn) {
                btn.disabled = false;
                if (btn.dataset.originalHtml) {
                    btn.innerHTML = btn.dataset.originalHtml;
                }
            }
        });

        if (ok && (data.success ?? data.Success) === true) {
            const redirectUrl = CFG.workflowSuccessRedirect || DETAIL_CFG.workflowSuccessRedirect;
            const detailUrl = buildDetailUrl();
            if (typeof Swal !== 'undefined') {
                await Swal.fire({
                    icon: 'success',
                    title: 'Success',
                    text: redirectUrl ? 'Action completed successfully.' : 'Schedule updated successfully.',
                    timer: 1400,
                    showConfirmButton: false
                });
            } else {
                toast(redirectUrl ? 'Success' : 'Success — reloading…', 'success');
            }
            if (redirectUrl) {
                location.href = redirectUrl;
            } else if (detailUrl.includes('organizationStructureId=0')) {
                location.reload();
            } else {
                location.href = detailUrl;
            }
        } else {
            const errorMsg = data.error || data.message || 'Action failed';
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'error', title: 'Error', text: errorMsg });
            } else {
                toast(`Error: ${errorMsg}`, 'error');
            }
        }
    }

    // Main initialization
    if (IS_EDITABLE) {
        // Setup quick select buttons
        document.querySelectorAll('.shift-quick-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.shift-quick-btn').forEach(b => {
                    b.classList.remove('active');
                });

                btn.classList.add('active');

                selectedShift = {
                    id: parseInt(btn.dataset.shiftId, 10),
                    code: btn.dataset.code,
                    color: btn.dataset.color,
                    isLeave: btn.dataset.isLeave === 'true'
                };

                toast(`✓ ${selectedShift.code} selected - Click or long-press & drag to assign`, 'success');
            });
        });

        // Setup cells
        document.querySelectorAll('.cell-editable').forEach(cell => {
            preventSelection(cell);

            // Single click
            cell.addEventListener('click', async (e) => {
                e.stopPropagation();

                if (longPressTimer) {
                    clearTimeout(longPressTimer);
                    longPressTimer = null;
                }

                if (selectedShift) {
                    await saveCell(cell, selectedShift, true);
                } else {
                    const picker = document.getElementById('shiftPicker');
                    if (picker) {
                        const rect = cell.getBoundingClientRect();
                        picker.style.display = 'block';
                        picker.style.left = `${rect.left + window.scrollX}px`;
                        picker.style.top = `${rect.bottom + window.scrollY + 2}px`;
                        picker.value = cell.dataset.shiftId;
                        picker._activeCell = cell;
                        picker.focus();
                    } else {
                        toast('Please select a shift type first', 'info');
                    }
                }
            });

            // Mouse down for long press
            cell.addEventListener('mousedown', (e) => {
                if (selectedShift) {
                    e.preventDefault();
                    startLongPress(cell);
                }
            });

            // Cancel long press on mouse up
            cell.addEventListener('mouseup', () => {
                endLongPress();
            });

            // Cancel on mouse leave
            cell.addEventListener('mouseleave', () => {
                if (longPressTimer && !isLongPressing) {
                    clearTimeout(longPressTimer);
                    longPressTimer = null;
                }
            });
        });

        // Dropdown picker
        const picker = document.getElementById('shiftPicker');
        if (picker) {
            picker.addEventListener('change', async () => {
                if (!picker._activeCell) return;

                const opt = picker.options[picker.selectedIndex];
                const shift = {
                    id: parseInt(picker.value, 10),
                    code: opt.dataset.code,
                    color: opt.dataset.color,
                    isLeave: opt.dataset.isLeave === 'true'
                };

                picker.style.display = 'none';
                const cell = picker._activeCell;
                picker._activeCell = null;

                await saveCell(cell, shift, true);
            });

            // Hide picker when clicking outside
            document.addEventListener('click', (e) => {
                if (picker && picker.style.display !== 'none' && !picker.contains(e.target)) {
                    picker.style.display = 'none';
                    picker._activeCell = null;
                }
            });

            picker.addEventListener('click', e => e.stopPropagation());
        }
    }

    // Setup workflow buttons
    const workflowPayload = () => ({
        month: MONTH,
        year: YEAR,
        organizationStructureId: getOrgUnitId()
    });

    const btnSubmit = document.getElementById('btnSubmit');
    if (btnSubmit) {
        btnSubmit.addEventListener('click', () =>
            workflowAction('/WorkSchedule/Submit', workflowPayload(), {
                title: 'Submit Schedule?',
                text: 'This schedule will become read-only until it is reviewed and approved.',
                confirmText: 'Yes, submit'
            }));
    }

    const btnApprove = document.getElementById('btnApprove');
    if (btnApprove) {
        btnApprove.addEventListener('click', () =>
            workflowAction('/WorkSchedule/Approve', workflowPayload(), {
                icon: 'success',
                title: 'Approve Schedule?',
                text: 'Approved schedules are used for DTR attendance rules.',
                confirmText: 'Yes, approve'
            }));
    }

    const btnReject = document.getElementById('btnReject');
    if (btnReject) {
        btnReject.addEventListener('click', async () => {
            if (typeof Swal === 'undefined') {
                const reason = prompt('Enter rejection reason:');
                if (!reason || !reason.trim()) return;
                await workflowAction('/WorkSchedule/Reject', { ...workflowPayload(), reason: reason.trim() }, null);
                return;
            }

            const result = await Swal.fire({
                icon: 'warning',
                title: 'Reject Schedule',
                input: 'textarea',
                inputLabel: 'Rejection reason',
                inputPlaceholder: 'Enter the reason for rejection...',
                inputAttributes: { 'aria-label': 'Rejection reason' },
                showCancelButton: true,
                confirmButtonText: 'Reject',
                confirmButtonColor: '#dc3545',
                cancelButtonText: 'Cancel',
                reverseButtons: true,
                inputValidator: (value) => {
                    if (!value || !value.trim()) return 'Rejection reason is required';
                    return undefined;
                }
            });

            if (result.isConfirmed && result.value) {
                await workflowAction('/WorkSchedule/Reject', { ...workflowPayload(), reason: result.value.trim() }, null);
            }
        });
    }

    const btnReopen = document.getElementById('btnReopen');
    if (btnReopen) {
        btnReopen.addEventListener('click', () =>
            workflowAction('/WorkSchedule/Reopen', workflowPayload(), {
                icon: 'warning',
                title: 'Reopen to Draft?',
                text: 'DTR will fall back to default rules until the schedule is approved again.',
                confirmText: 'Yes, reopen'
            }));
    }

    // Debug: Log initialization
    console.log('Work Schedule JS initialized', {
        month: MONTH,
        year: YEAR,
        organizationStructureId: getOrgUnitId(),
        isEditable: IS_EDITABLE,
        hasToken: !!TOKEN,
        submitButtonExists: !!document.getElementById('btnSubmit'),
        approveButtonExists: !!document.getElementById('btnApprove')
    });
})();