(function () {
    'use strict';

    const CFG = window.WS_DETAIL_CONFIG || {};

    function clampYear(year) {
        return Math.min(2099, Math.max(2020, year));
    }

    function getMonth() {
        const monthEl = document.getElementById('wsMonth');
        if (!monthEl) return CFG.month || 1;
        return parseInt(monthEl.value, 10) || CFG.month || 1;
    }

    function getYear() {
        const yearEl = document.getElementById('wsYear');
        if (!yearEl) return CFG.year || new Date().getFullYear();
        return parseInt(yearEl.value, 10) || CFG.year || new Date().getFullYear();
    }

    function setMonthYear(month, year) {
        const monthEl = document.getElementById('wsMonth');
        const yearEl = document.getElementById('wsYear');

        if (yearEl) yearEl.value = clampYear(year);
        if (monthEl) {
            monthEl.value = String(month);
            if (typeof $ !== 'undefined' && $(monthEl).hasClass('select2')) {
                $(monthEl).val(String(month)).trigger('change.select2');
            }
        }
    }

    function navigateMonth(delta) {
        let month = getMonth();
        let year = getYear();

        month += delta;
        if (month < 1) {
            month = 12;
            year -= 1;
        } else if (month > 12) {
            month = 1;
            year += 1;
        }

        setMonthYear(month, clampYear(year));
    }

    function setPageLoading(show) {
        const overlay = document.getElementById('wsPageLoading');
        const viewBtn = document.getElementById('wsDetailViewBtn');
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
        const form = document.getElementById('wsDetailFilterForm');
        if (!form) return;

        form.addEventListener('submit', () => setPageLoading(true));
    }

    function initEmployeeSearch() {
        const input = document.getElementById('wsEmpSearch');
        const table = document.querySelector('.ws-table tbody');
        if (!input || !table) return;

        input.addEventListener('input', () => {
            const query = input.value.trim().toLowerCase();
            const rows = table.querySelectorAll('tr.emp-row');

            rows.forEach(row => {
                const name = row.getAttribute('data-emp-name') || '';
                const match = !query || name.includes(query);
                row.classList.toggle('ws-row-hidden', !match);
            });

            table.querySelectorAll('tr.section-header-row').forEach(sectionRow => {
                let sibling = sectionRow.nextElementSibling;
                let hasVisible = false;

                while (sibling && !sibling.classList.contains('section-header-row')) {
                    if (sibling.classList.contains('emp-row') && !sibling.classList.contains('ws-row-hidden')) {
                        hasVisible = true;
                        break;
                    }
                    sibling = sibling.nextElementSibling;
                }

                sectionRow.classList.toggle('ws-row-hidden', !!query && !hasVisible);
            });
        });
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

    function initMonthNav() {
        const prevBtn = document.getElementById('wsMonthPrev');
        const nextBtn = document.getElementById('wsMonthNext');

        if (prevBtn) prevBtn.addEventListener('click', () => navigateMonth(-1));
        if (nextBtn) nextBtn.addEventListener('click', () => navigateMonth(1));
    }

    document.addEventListener('DOMContentLoaded', () => {
        initYearNav();
        initMonthNav();
        initFormLoading();
        initEmployeeSearch();
    });
})();
