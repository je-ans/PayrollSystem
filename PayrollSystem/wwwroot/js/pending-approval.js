(function () {
    const $search = $('#pendingApprovalSearch');
    const $grid = $('#pendingApprovalGrid');
    const $empty = $('#pendingApprovalEmptyFilter');
    const $filters = $('#pendingApprovalFilters');
    const $count = $('#pendingApprovalCount');

    if (!$grid.length) {
        return;
    }

    let activeProcessFilter = 'all';

    function applyFilters() {
        const query = ($search.val() || '').trim().toLowerCase();
        let visible = 0;

        $grid.find('.aw-pending-card').each(function () {
            const $card = $(this);
            const haystack = ($card.data('search') || '').toString();
            const processType = ($card.data('process-type') || '').toString();
            const matchesSearch = !query || haystack.indexOf(query) >= 0;
            const matchesProcess = activeProcessFilter === 'all' || processType === activeProcessFilter;
            const match = matchesSearch && matchesProcess;

            $card.toggleClass('d-none', !match);
            if (match) {
                visible++;
            }
        });

        if ($empty.length) {
            $empty.toggleClass('d-none', visible > 0);
        }

        if ($count.length) {
            $count.text(visible + ' pending');
        }
    }

    if ($search.length) {
        $search.on('input', applyFilters);
    }

    if ($filters.length) {
        $filters.on('click', '.aw-filter-pill', function () {
            const $pill = $(this);
            activeProcessFilter = ($pill.data('process-filter') || 'all').toString();
            $filters.find('.aw-filter-pill').removeClass('is-active');
            $pill.addClass('is-active');
            applyFilters();
        });
    }

    applyFilters();
})();
