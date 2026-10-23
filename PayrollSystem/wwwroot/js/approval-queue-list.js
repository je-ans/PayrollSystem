(function () {
    const $table = $('#approvalQueueTable');
    if (!$table.length || !$.fn.DataTable) {
        return;
    }

    const hasRows = $table.find('tbody tr[data-queue-row]').length > 0;
    if (!hasRows) {
        return;
    }

    const dt = $table.DataTable({
        responsive: true,
        searching: true,
        ordering: true,
        paging: true,
        info: true,
        dom: '<"row"<"col-sm-12"tr>><"row"<"col-sm-5"i><"col-sm-7"p>>',
        order: [],
        columnDefs: [
            { orderable: false, targets: -1 }
        ],
        language: {
            emptyTable: 'No requests found.',
            zeroRecords: 'No matching requests found.'
        }
    });

    const $search = $('#approvalQueueSearch');
    if ($search.length) {
        $search.on('input', function () {
            dt.search(this.value).draw();
        });
    }
})();
