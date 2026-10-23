(function () {
    'use strict';

    const CFG = window.WS_OVERVIEW_CONFIG || {};

    function clampYear(year) {
        return Math.min(2099, Math.max(2020, year));
    }

    function initYearNav() {
        const yearInput = document.getElementById('wsYear');
        const prevBtn = document.getElementById('wsYearPrev');
        const nextBtn = document.getElementById('wsYearNext');

        if (!yearInput) return;

        if (prevBtn) {
            prevBtn.addEventListener('click', () => {
                yearInput.value = clampYear(parseInt(yearInput.value, 10) - 1);
            });
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', () => {
                yearInput.value = clampYear(parseInt(yearInput.value, 10) + 1);
            });
        }
    }

    function setPageLoading(show) {
        const overlay = document.getElementById('wsPageLoading');
        const viewBtn = document.getElementById('wsOverviewViewBtn');
        if (overlay) {
            overlay.classList.toggle('is-visible', !!show);
            overlay.setAttribute('aria-hidden', show ? 'false' : 'true');
        }
        if (viewBtn) {
            viewBtn.classList.toggle('is-loading', !!show);
            viewBtn.disabled = !!show;
        }
    }

    function initFormLoading() {
        const form = document.getElementById('wsOverviewFilterForm');
        if (!form) return;
        form.addEventListener('submit', () => setPageLoading(true));
    }

    function showEmptyStateSwal() {
        if (!CFG.emptyState || typeof Swal === 'undefined') return;

        const shownKey = `ws-overview-swal-${CFG.emptyState}-${CFG.year}`;
        if (sessionStorage.getItem(shownKey)) return;
        sessionStorage.setItem(shownKey, '1');

        if (CFG.emptyState === 'no-org') {
            Swal.fire({
                icon: 'info',
                title: 'Select Organization Unit',
                text: 'Choose an organization unit from the filter to view monthly schedules for this year.',
                confirmButtonText: 'OK'
            });
            return;
        }

        if (CFG.emptyState === 'no-employees') {
            Swal.fire({
                icon: 'warning',
                title: 'No Employees Assigned',
                html: 'This unit has no employees with a <strong>primary</strong> organization assignment.<br>Assign employees before building monthly schedules.',
                confirmButtonText: 'Got it'
            });
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        initYearNav();
        initFormLoading();
        showEmptyStateSwal();
    });
})();
