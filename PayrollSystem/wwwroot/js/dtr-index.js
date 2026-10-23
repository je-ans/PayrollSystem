(function () {
    const config = window.DTR_INDEX_CONFIG || {};
    const isEmployeeOnly = !!config.isEmployeeOnly;
    const currentEmployeeId = config.currentEmployeeId || '';

    function getFilters() {
        return {
            employeeId: isEmployeeOnly ? currentEmployeeId : $('#employeeId').val(),
            month: parseInt($('#month').val(), 10),
            year: parseInt($('#year').val(), 10)
        };
    }

    function showEmpty(message, icon) {
        icon = icon || 'fa-info-circle';
        $('#dtrEmptyState').show().html(
            '<div class="dtr-empty-hero">' +
            '<div><i class="fas ' + icon + ' d-block"></i></div>' +
            '<p class="mb-0">' + message + '</p></div>'
        );
        $('#dtrContentArea').hide().removeClass('is-loading');
        $('#dtrPanelLoading').removeClass('is-visible');
        setOverlayLoading(false);
        setButtonLoading(false);
    }

    function setOverlayLoading(show) {
        const $overlay = $('#dtrLoadingOverlay');
        $overlay.toggleClass('is-visible', !!show);
        $overlay.attr('aria-hidden', show ? 'false' : 'true');
    }

    function setPanelLoading(show) {
        const $area = $('#dtrContentArea');
        const $panel = $('#dtrPanelLoading');
        $area.toggleClass('is-loading', !!show);
        $panel.toggleClass('is-visible', !!show);
        $panel.attr('aria-hidden', show ? 'false' : 'true');
    }

    function setButtonLoading(loading) {
        const $btn = $('#btnGenerate');
        const $icon = $btn.find('.fas');

        if (loading) {
            $btn.prop('disabled', true).addClass('is-loading');
            // Change icon to spinner with animation
            $icon.removeClass('fa-search').addClass('fa-spinner fa-spin');
        } else {
            $btn.prop('disabled', false).removeClass('is-loading');
            // Change back to search icon
            $icon.removeClass('fa-spinner fa-spin').addClass('fa-search');
        }
    }

    function buildUrl(base, filters) {
        let url = base + '?month=' + filters.month + '&year=' + filters.year;
        if (!isEmployeeOnly && filters.employeeId) {
            url += '&employeeId=' + filters.employeeId;
        }
        return url;
    }

    function applySummaryStats($root) {
        const $summary = $root.find('[data-dtr-summary]');
        if (!$summary.length) {
            return;
        }

        try {
            const stats = JSON.parse($summary.attr('data-dtr-summary'));
            $('#dtrStatDays').text(stats.workDays ?? '—');
            $('#dtrStatMissing').text(stats.missingPunches ?? '—');
            $('#dtrStatPending').text(stats.pendingJustifications ?? '—');
            $('#dtrStatTardy').text(stats.tardinessDays ?? '—');
            $('#dtrSummaryGrid').show();
        } catch (e) {
            /* ignore */
        }
    }

    function initLogFilters() {
        const $wrapper = $('#dtrManagementContainer .dtr-table-wrapper');
        if (!$wrapper.length) {
            return;
        }

        function getActiveFilter() {
            const $active = $wrapper.find('.dtr-filter-chip.active');
            return $active.length ? $active.data('filter') : 'all';
        }

        function applyRowVisibility() {
            const filter = getActiveFilter();
            const query = ($('#dtrLogSearch').val() || '').trim().toLowerCase();

            $wrapper.find('tbody tr.dtr-row').each(function () {
                const $row = $(this);
                let show = true;

                if (filter === 'missing') show = $row.data('missing') === 1 || $row.data('missing') === '1';
                else if (filter === 'pending') show = $row.data('pending') === 1 || $row.data('pending') === '1';
                else if (filter === 'tardy') show = $row.data('tardy') === 1 || $row.data('tardy') === '1';

                const searchText = ($row.data('search') || '').toString().toLowerCase();
                const matchesSearch = !query || searchText.indexOf(query) !== -1;

                $row.toggleClass('dtr-row-hidden', !(show && matchesSearch));
                $row.toggleClass('dtr-row-match', !!query && matchesSearch && show);
            });

            const visible = $wrapper.find('tbody tr.dtr-row:not(.dtr-row-hidden)').length;
            const total = $wrapper.find('tbody tr.dtr-row').length;
            const countLabel = $wrapper.find('.dtr-logs-count');
            if (countLabel.length) {
                countLabel.text(visible + ' of ' + total + ' day' + (total === 1 ? '' : 's'));
            }

            const $filterEmpty = $('#dtrLogFilterEmpty');
            const $tableWrap = $wrapper.find('.dtr-table-responsive');
            if ($filterEmpty.length) {
                const showEmpty = total > 0 && visible === 0;
                $filterEmpty.toggle(showEmpty);
                $tableWrap.toggle(!showEmpty);
            }
        }

        $wrapper.off('click.dtrFilter', '.dtr-filter-chip');
        $wrapper.on('click.dtrFilter', '.dtr-filter-chip', function () {
            $wrapper.find('.dtr-filter-chip').removeClass('active');
            $(this).addClass('active');
            applyRowVisibility();
        });

        $('#dtrLogSearch').off('input.dtrSearch').on('input.dtrSearch', function () {
            applyRowVisibility();
        });
    }

    function loadAll() {
        const filters = getFilters();

        if (!isEmployeeOnly && !filters.employeeId) {
            showEmpty('Select an employee, month, and year, then click <strong>Load DTR</strong>.');
            return;
        }

        $('#dtrEmptyState').hide();
        $('#dtrContentArea').show();
        $('#dtrSummaryGrid').hide();
        setPanelLoading(true);
        setOverlayLoading(false);
        setButtonLoading(true);

        const tableUrl = buildUrl(config.managementUrl, filters);
        const docUrl = buildUrl(config.documentUrl, filters);

        $.when($.get(tableUrl), $.get(docUrl))
            .done(function (tableRes, docRes) {
                const tableHtml = tableRes[0];
                const docHtml = docRes[0];

                $('#dtrManagementContainer').html(tableHtml);
                applySummaryStats($('#dtrManagementContainer'));
                initLogFilters();

                if (docHtml && String(docHtml).trim() !== '') {
                    $('#document-content').html(docHtml);
                    $(document).trigger('documentContentLoaded');
                } else {
                    $('#document-content').html(
                        '<div class="dtr-empty-hero py-4">' +
                        '<div><i class="fas fa-file-alt d-block"></i></div>' +
                        '<p class="mb-0 text-muted">No official form data for this period.</p></div>'
                    );
                }

                $('#dtrTabLogs').tab('show');
            })
            .fail(function () {
                showEmpty('Unable to load DTR data. Please try again.', 'fa-exclamation-triangle');
            })
            .always(function () {
                setPanelLoading(false);
                setButtonLoading(false);
            });
    }

    // Initialize form submit handler
    $('#dtrFilterForm').on('submit', function (e) {
        e.preventDefault();
        loadAll();
    });

    // Auto-load if configured
    if (config.autoLoad) {
        loadAll();
    } else if (!isEmployeeOnly) {
        showEmpty('Select an employee, month, and year, then click <strong>Load DTR</strong>.');
    }
})();