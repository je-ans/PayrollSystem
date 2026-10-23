(function () {
    function parseTimeInput($input) {
        const val = $input.val();
        if (!val) return null;
        const parts = val.split(':');
        if (parts.length < 2) return null;
        return parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10);
    }

    function getShiftSpanMinutes(startMin, endMin, spansNextDay) {
        if (spansNextDay) {
            return (24 * 60 - startMin) + endMin;
        }
        return endMin - startMin;
    }

    function suggestCreditedHours($form) {
        const startMin = parseTimeInput($form.find('.shift-start-time'));
        const endMin = parseTimeInput($form.find('.shift-end-time'));
        if (startMin === null || endMin === null) return null;

        const spansNextDay = $form.find('.shift-spans-next-day').is(':checked');
        let spanMinutes = getShiftSpanMinutes(startMin, endMin, spansNextDay);
        if (spanMinutes <= 0) return null;

        if ($form.find('.shift-has-lunch').is(':checked')) {
            const lunchStart = parseTimeInput($form.find('.shift-lunch-start'));
            const lunchEnd = parseTimeInput($form.find('.shift-lunch-end'));
            if (lunchStart !== null && lunchEnd !== null && lunchEnd > lunchStart) {
                spanMinutes -= (lunchEnd - lunchStart);
            }
        }

        if (spanMinutes <= 0) return null;
        return Math.round((spanMinutes / 60) * 100) / 100;
    }

    function refreshScheduleSection($form) {
        const isLeave = $form.find('.shift-is-leave').is(':checked');
        const isFlexible = $form.find('.shift-is-flexible').is(':checked');
        const hasLunch = $form.find('.shift-has-lunch').is(':checked');
        const $section = $form.find('.shift-schedule-section');

        if (isLeave) {
            $section.addClass('is-hidden');
            return;
        }

        $section.removeClass('is-hidden');
        $form.find('.shift-time-fields').toggle(!isFlexible);
        $form.find('.shift-lunch-fields').toggle(hasLunch && !isFlexible);
        $form.find('.shift-spans-next-day-wrap').toggle(!isFlexible);
        $form.find('.shift-has-lunch-wrap').toggle(!isFlexible);
    }

    function refreshColorSwatch($form) {
        const color = $form.find('.shift-color-input').val();
        if (color) {
            $form.find('.shift-color-swatch').css('background-color', color);
        }
    }

    $(document).on('change', '#shiftTypeForm .shift-is-leave, #shiftTypeForm .shift-is-flexible, #shiftTypeForm .shift-has-lunch', function () {
        refreshScheduleSection($(this).closest('form'));
    });

    $(document).on('input change', '#shiftTypeForm .shift-color-input', function () {
        refreshColorSwatch($(this).closest('form'));
    });

    $(document).on('click', '#shiftTypeForm .shift-suggest-hours', function (e) {
        e.preventDefault();
        const $form = $(this).closest('form');
        const hours = suggestCreditedHours($form);
        if (hours === null) {
            Swal.fire({ icon: 'warning', text: 'Enter start and end times to calculate credited hours.' });
            return;
        }
        $form.find('.shift-hours').val(hours);
    });

    $(document).on('shown.bs.modal', '#createModal, #editModal', function () {
        const $form = $(this).find('#shiftTypeForm');
        if ($form.length) {
            refreshScheduleSection($form);
            refreshColorSwatch($form);
        }
    });

    $(document).ajaxComplete(function (_event, _xhr, settings) {
        if (!settings.url || settings.url.indexOf('ShiftType') === -1) return;
        setTimeout(function () {
            const $form = $('#shiftTypeForm');
            if ($form.length) {
                refreshScheduleSection($form);
                refreshColorSwatch($form);
            }
        }, 80);
    });

    const $shiftTable = $('#shiftTypeTable');
    if ($shiftTable.length && $.fn.DataTable) {
        const dt = $shiftTable.DataTable({
            responsive: true,
            searching: true,
            ordering: true,
            paging: true,
            info: true,
            dom: '<"row"<"col-sm-12"tr>><"row"<"col-sm-5"i><"col-sm-7"p>>',
            order: [[7, 'asc'], [1, 'asc']],
            columnDefs: [
                { orderable: false, targets: [0, 9] }
            ],
            language: {
                emptyTable: 'No shift types found.',
                zeroRecords: 'No matching shift types found.'
            }
        });

        const $search = $('#shiftTypeSearch');
        if ($search.length) {
            $search.on('input', function () {
                dt.search(this.value).draw();
            });
        }
    }
})();
