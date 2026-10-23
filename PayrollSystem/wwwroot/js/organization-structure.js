(function () {
    function getToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    // Tree expand/collapse
    $(document).on('click', '.org-tree-toggle', function () {
        const $btn = $(this);
        const $children = $btn.closest('.org-tree-node').children('.org-tree-children');
        const expanded = $btn.attr('aria-expanded') === 'true';

        $children.toggleClass('is-collapsed', expanded);
        $btn.attr('aria-expanded', expanded ? 'false' : 'true');
        $btn.find('i').toggleClass('fa-chevron-down', !expanded).toggleClass('fa-chevron-right', expanded);
    });

    // Tree search
    const $search = $('#orgTreeSearch');
    if ($search.length) {
        $search.on('input', function () {
            const query = $(this).val().toLowerCase().trim();
            let visibleCount = 0;

            function filterNode($node) {
                const text = ($node.data('search') || '').toString();
                const selfMatch = !query || text.indexOf(query) >= 0;
                let childMatch = false;

                $node.children('.org-tree-children').children('.org-tree-node').each(function () {
                    if (filterNode($(this))) childMatch = true;
                });

                const show = selfMatch || childMatch;
                $node.toggleClass('is-hidden', !show);
                $node.toggleClass('is-match', !!query && selfMatch);

                if (childMatch) {
                    $node.children('.org-tree-children').removeClass('is-collapsed');
                    $node.find('> .org-tree-row .org-tree-toggle')
                        .attr('aria-expanded', 'true')
                        .find('i').removeClass('fa-chevron-right').addClass('fa-chevron-down');
                }

                if (show) visibleCount++;
                return show;
            }

            $('#orgTree > .org-tree-node').each(function () {
                filterNode($(this));
            });

            $('#orgTreeNoResults').toggle(visibleCount === 0 && !!query);
        });
    }

    // Staff panel refresh
    function getStaffRefreshTarget() {
        if (window.ORG_IS_FULL_PAGE) {
            return { mode: 'reload' };
        }

        const $viewContent = $('#viewModalContent');
        if ($viewContent.length) {
            return { mode: 'partial', $el: $viewContent };
        }

        return { mode: 'reload' };
    }

    function refreshStaffPanel() {
        const unitId = window.ORG_UNIT_ID || $('#staffStructurePanel').data('unit-id');
        const staffUrl = window.ORG_STAFF_URL || (unitId ? '/OrganizationStructure/Staff/' + unitId : null);
        if (!staffUrl) {
            location.reload();
            return;
        }

        const target = getStaffRefreshTarget();
        if (target.mode === 'reload') {
            location.reload();
            return;
        }

        $('#viewModal').data('self-refresh-url', staffUrl);
        $.get(staffUrl).done(function (html) {
            target.$el.html(html);
            $('#viewModal').data('should-reload-on-close', true);
        });
    }

    function postStaffAction(url, id, confirmText) {
        Swal.fire({
            title: 'Confirm',
            text: confirmText,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes'
        }).then(function (result) {
            if (!result.isConfirmed) return;
            $.ajax({
                url: url,
                type: 'POST',
                data: { id: id, __RequestVerificationToken: getToken() },
                success: function (response) {
                    if (response.success) {
                        Swal.fire({ icon: 'success', title: 'Done', text: response.message, timer: 2000, showConfirmButton: false });
                        refreshStaffPanel();
                    } else {
                        Swal.fire({ icon: 'error', title: 'Error', text: response.message });
                    }
                },
                error: function () {
                    Swal.fire({ icon: 'error', title: 'Error', text: 'Request failed.' });
                }
            });
        });
    }

    $(document).on('click', '.org-end-assignment', function () {
        const $btn = $(this);
        postStaffAction($btn.data('url'), $btn.data('id'), 'End this employee assignment as of today?');
    });

    $(document).on('click', '.org-remove-assignment', function () {
        const $btn = $(this);
        postStaffAction($btn.data('url'), $btn.data('id'), 'Remove this employee assignment record?');
    });

    $(document).on('click', '.org-end-role', function () {
        const $btn = $(this);
        postStaffAction($btn.data('url'), $btn.data('id'), 'End this role assignment as of today?');
    });

    $(document).on('click', '.org-remove-role', function () {
        const $btn = $(this);
        postStaffAction($btn.data('url'), $btn.data('id'), 'Remove this role assignment record?');
    });

    // staff panel opened inside #viewModal from tree rows
    $(document).on('click', '.open-modal[data-url*="/OrganizationStructure/Staff/"]', function () {
        const url = $(this).data('url');
        $('#viewModal').data('self-refresh-url', url);
        const match = /Staff\/(\d+)/.exec(url);
        if (match) window.ORG_UNIT_ID = parseInt(match[1], 10);
        window.ORG_STAFF_URL = url;
        window.ORG_IS_FULL_PAGE = false;
    });

    // Staff modals on the unit page are not nested under #viewModal
    $(document).on('show.bs.modal', '#createModal, #editModal', function () {
        if (window.ORG_IS_FULL_PAGE) {
            $(this).removeData('parent-modal');
        }
    });

    // After saving staff/role assignment on the unit page, reload to show updates
    $(document).ajaxSuccess(function (_event, xhr, settings) {
        if (!window.ORG_IS_FULL_PAGE) return;
        if ((settings.type || '').toUpperCase() !== 'POST') return;

        const url = settings.url || '';
        if (!/\/OrganizationStructure\/Save(?:Employee|Role)Assignment/.test(url)) return;

        try {
            const res = typeof xhr.responseJSON === 'object' ? xhr.responseJSON : JSON.parse(xhr.responseText);
            if (res && res.success) {
                $('#createModal, #editModal').removeData('parent-modal');
            }
        } catch (e) {
            /* ignore */
        }
    });

    // After unit delete, navigate to Index (avoids 404 on Unit page reload)
    $(document).ajaxSuccess(function (_event, xhr, settings) {
        const url = settings.url || '';
        if (!/\/OrganizationStructure\/DeleteConfirmed/.test(url)) return;

        try {
            const res = typeof xhr.responseJSON === 'object' ? xhr.responseJSON : JSON.parse(xhr.responseText);
            if (res && res.success && res.redirectUrl) {
                setTimeout(function () {
                    window.location.href = res.redirectUrl;
                }, 900);
            }
        } catch (e) {
            /* ignore */
        }
    });

    // Org chart: zoom, pan, layout, drag-drop reparent, print / export
    (function initOrgChart() {
        const $viewport = $('#orgChartViewport');
        const $canvas = $('#orgChartCanvas');
        const $root = $('#orgChartExportRoot');
        const $chart = $('#orgChart');
        const $printClone = $('#orgChartPrintClone');
        if (!$viewport.length || !$root.length) return;

        let scale = 1;
        let panX = 0;
        let panY = 0;
        let isPanning = false;
        let panStartX = 0;
        let panStartY = 0;
        let originX = 0;
        let originY = 0;

        function applyTransform() {
            $root.css('transform', 'translate(' + panX + 'px,' + panY + 'px) scale(' + scale + ')');
            $('#orgChartZoomLabel').text(Math.round(scale * 100) + '%');
        }

        function fitToView() {
            const viewport = $viewport[0];
            const content = $root[0];
            if (!viewport || !content) return;

            // Reset to measure natural size
            $root.css('transform', 'translate(0px,0px) scale(1)');
            const vw = viewport.clientWidth;
            const vh = viewport.clientHeight;
            const cw = Math.max(content.scrollWidth, content.offsetWidth);
            const ch = Math.max(content.scrollHeight, content.offsetHeight);
            const pad = 48;
            const next = Math.min(1.15, Math.max(0.25, Math.min((vw - pad) / cw, (vh - pad) / ch)));
            scale = next;
            panX = Math.max(16, (vw - cw * scale) / 2);
            panY = Math.max(16, (vh - ch * scale) / 2);
            applyTransform();
        }

        function setZoom(next) {
            const viewport = $viewport[0];
            const cx = viewport.clientWidth / 2;
            const cy = viewport.clientHeight / 2;
            const clamped = Math.min(2.5, Math.max(0.2, next));
            const ratio = clamped / scale;
            panX = cx - (cx - panX) * ratio;
            panY = cy - (cy - panY) * ratio;
            scale = clamped;
            applyTransform();
        }

        function syncPrintCloneLayout(layout) {
            if (!$printClone.length) return;
            $printClone
                .removeClass('layout-classic layout-compact')
                .addClass('layout-' + layout);
        }

        // Auto-fit: prefer Compact when the classic layout is too wide
        setTimeout(function () {
            $root.css('transform', 'translate(0px,0px) scale(1)');
            const cw = $root[0].scrollWidth;
            const vw = $viewport[0].clientWidth;
            if (cw > vw * 1.35) {
                $('.org-chart-layout-toggle [data-layout="compact"]').trigger('click');
            } else {
                fitToView();
            }
        }, 60);

        $(window).on('resize.orgChart', function () {
            clearTimeout(window._orgChartResizeTimer);
            window._orgChartResizeTimer = setTimeout(fitToView, 150);
        });

        $('#orgChartZoomIn').on('click', function () { setZoom(scale + 0.12); });
        $('#orgChartZoomOut').on('click', function () { setZoom(scale - 0.12); });
        $('#orgChartFitBtn').on('click', fitToView);

        // Layout toggle
        $('.org-chart-layout-toggle [data-layout]').on('click', function () {
            const layout = $(this).data('layout');
            $('.org-chart-layout-toggle [data-layout]').removeClass('active');
            $(this).addClass('active');
            $chart.removeClass('layout-classic layout-compact').addClass('layout-' + layout);
            syncPrintCloneLayout(layout);
            setTimeout(fitToView, 40);
        });

        // Pan with pointer drag on canvas (not on cards)
        $canvas.on('mousedown', function (e) {
            if ($(e.target).closest('.org-chart-card, .org-chart-drop-root').length) return;
            isPanning = true;
            panStartX = e.clientX;
            panStartY = e.clientY;
            originX = panX;
            originY = panY;
            $canvas.addClass('is-panning');
            e.preventDefault();
        });

        $(document).on('mousemove.orgChartPan', function (e) {
            if (!isPanning) return;
            panX = originX + (e.clientX - panStartX);
            panY = originY + (e.clientY - panStartY);
            applyTransform();
        });

        $(document).on('mouseup.orgChartPan', function () {
            if (!isPanning) return;
            isPanning = false;
            $canvas.removeClass('is-panning');
        });

        // Wheel zoom
        $viewport.on('wheel', function (e) {
            e.preventDefault();
            const delta = e.originalEvent.deltaY > 0 ? -0.08 : 0.08;
            setZoom(scale + delta);
        });

        // Drag-and-drop reparent
        let dragId = null;
        let dragName = '';
        let dragParentId = '';

        $chart.on('dragstart', '.org-chart-card', function (e) {
            const $card = $(this);
            dragId = parseInt($card.data('id'), 10);
            dragName = $card.data('name') || '';
            dragParentId = ($card.data('parent-id') || '').toString();
            $card.addClass('is-dragging');
            $viewport.addClass('is-dragging-node');
            e.originalEvent.dataTransfer.effectAllowed = 'move';
            e.originalEvent.dataTransfer.setData('text/plain', String(dragId));
        });

        $chart.on('dragend', '.org-chart-card', function () {
            $(this).removeClass('is-dragging');
            $viewport.removeClass('is-dragging-node');
            $('.org-chart-card.is-drop-target, #orgChartDropRoot').removeClass('is-drop-target');
            dragId = null;
        });

        $chart.on('dragover', '.org-chart-card', function (e) {
            e.preventDefault();
            const targetId = parseInt($(this).data('id'), 10);
            if (!dragId || targetId === dragId) return;
            e.originalEvent.dataTransfer.dropEffect = 'move';
            $('.org-chart-card').removeClass('is-drop-target');
            $(this).addClass('is-drop-target');
        });

        $chart.on('dragleave', '.org-chart-card', function () {
            $(this).removeClass('is-drop-target');
        });

        function reparentUnit(id, newParentId, targetLabel) {
            const label = targetLabel || (newParentId == null ? 'root level' : 'the selected unit');
            Swal.fire({
                title: 'Move unit?',
                html: 'Move <strong>' + $('<div>').text(dragName).html() + '</strong> under <strong>' + $('<div>').text(label).html() + '</strong>?',
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Move',
                cancelButtonText: 'Cancel'
            }).then(function (result) {
                if (!result.isConfirmed) return;

                $.ajax({
                    url: window.ORG_CHART_REPARENT_URL,
                    type: 'POST',
                    data: {
                        id: id,
                        newParentId: newParentId,
                        __RequestVerificationToken: getToken()
                    },
                    success: function (response) {
                        if (response.success) {
                            if (response.unchanged) {
                                Swal.fire({ icon: 'info', title: 'No change', text: response.message, timer: 1800, showConfirmButton: false });
                                return;
                            }
                            Swal.fire({
                                icon: 'success',
                                title: 'Moved',
                                text: response.message,
                                timer: 1200,
                                showConfirmButton: false
                            }).then(function () {
                                location.reload();
                            });
                        } else {
                            Swal.fire({ icon: 'error', title: 'Cannot move', text: response.message });
                        }
                    },
                    error: function () {
                        Swal.fire({ icon: 'error', title: 'Error', text: 'Failed to move the unit.' });
                    }
                });
            });
        }

        $chart.on('drop', '.org-chart-card', function (e) {
            e.preventDefault();
            e.stopPropagation();
            const $target = $(this);
            $target.removeClass('is-drop-target');
            const targetId = parseInt($target.data('id'), 10);
            const targetName = $target.data('name') || 'selected unit';
            if (!dragId || targetId === dragId) return;

            // Prevent dropping onto a descendant in the client tree
            const $dragItem = $chart.find('.org-chart-item[data-id="' + dragId + '"]').first();
            if ($dragItem.find('.org-chart-item[data-id="' + targetId + '"]').length) {
                Swal.fire({ icon: 'error', title: 'Invalid move', text: 'Cannot move a unit under one of its descendants.' });
                return;
            }

            if (dragParentId === String(targetId)) {
                Swal.fire({ icon: 'info', title: 'No change', text: 'Already under that unit.', timer: 1600, showConfirmButton: false });
                return;
            }

            reparentUnit(dragId, targetId, targetName);
        });

        const $dropRoot = $('#orgChartDropRoot');
        $dropRoot.on('dragover', function (e) {
            e.preventDefault();
            $(this).addClass('is-drop-target');
            e.originalEvent.dataTransfer.dropEffect = 'move';
        });
        $dropRoot.on('dragleave', function () {
            $(this).removeClass('is-drop-target');
        });
        $dropRoot.on('drop', function (e) {
            e.preventDefault();
            $(this).removeClass('is-drop-target');
            if (!dragId) return;
            if (!dragParentId) {
                Swal.fire({ icon: 'info', title: 'No change', text: 'Already a root unit.', timer: 1600, showConfirmButton: false });
                return;
            }
            reparentUnit(dragId, null, 'root level');
        });

        $('#orgChartPrintBtn').on('click', function () {
            window.print();
        });

        $('#orgChartExportBtn').on('click', function () {
            const exportSource = $printClone.length ? $printClone.parent()[0] : $root[0];
            if (!exportSource) return;

            if (typeof html2canvas !== 'function') {
                Swal.fire({
                    icon: 'error',
                    title: 'Export unavailable',
                    text: 'Could not load the export library. Use Print / PDF instead.'
                });
                return;
            }

            const $btn = $(this);
            $btn.prop('disabled', true);

            // Temporarily show print clone for a clean unscaled capture
            const $printOnly = $('.org-chart-print-only');
            const wasHidden = $printOnly.css('display') === 'none';
            $printOnly.css({
                display: 'block',
                position: 'fixed',
                left: '-10000px',
                top: 0,
                background: '#fff',
                padding: '24px',
                zIndex: -1
            });

            html2canvas(exportSource, {
                backgroundColor: '#ffffff',
                scale: 2,
                useCORS: true,
                logging: false
            }).then(function (canvas) {
                if (wasHidden) $printOnly.css({ display: '', position: '', left: '', top: '', background: '', padding: '', zIndex: '' });
                const link = document.createElement('a');
                const stamp = new Date().toISOString().slice(0, 10);
                link.download = 'organization-chart-' + stamp + '.png';
                link.href = canvas.toDataURL('image/png');
                link.click();
            }).catch(function () {
                if (wasHidden) $printOnly.css({ display: '', position: '', left: '', top: '', background: '', padding: '', zIndex: '' });
                Swal.fire({
                    icon: 'error',
                    title: 'Export failed',
                    text: 'Could not generate the image. Try Print / PDF instead.'
                });
            }).finally(function () {
                $btn.prop('disabled', false);
            });
        });
    })();
})();
