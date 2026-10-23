
    // Function to handle AJAX errors consistently across the application
    function handleAjaxError(xhr, $button = null, $modal = null) {

        // Get error message from response if available
        let errorMessage = 'An error occurred while processing your request.';
        if (xhr.status === 403) { // Check for Forbidden status
            errorMessage = 'You do not have permission to perform this action.';
        } else if (xhr.responseJSON && xhr.responseJSON.message) {
            errorMessage = xhr.responseJSON.message;
        } else if (xhr.responseText) {
            // Attempt to parse responseText if it's not JSON but might contain a message
            try {
                const responseObj = JSON.parse(xhr.responseText);
                if (responseObj.message) {
                    errorMessage = responseObj.message;
                }
            } catch (e) {
                // If not JSON, use the responseText directly if it's short and human-readable
                if (xhr.responseText.length < 200) { // Arbitrary limit to avoid displaying raw HTML/long messages
                    errorMessage = xhr.responseText;
                }
            }
        }

        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: errorMessage,
            showConfirmButton: true
        });
    }


// Helper function to handle SweetAlert display and parent modal reopening
function displaySwalAndReopenParent(childModal, parentModalSelector, swalOptions, refreshCallback = null) {

    // Set a flag on the child modal to indicate it's being managed by this function
    childModal.data('swal-managed', true);

    childModal.one('hidden.bs.modal', function () {
        const $parentModal = parentModalSelector ? $(parentModalSelector) : null;

        const showSwal = () => {
            Swal.fire(swalOptions).then(() => {
                setTimeout(() => {
                    if ($parentModal) {
                        if (parentModalSelector === '#printPreviewModal' && refreshCallback) {
                            refreshCallback($parentModal);
                        } else {
                            $parentModal.one('shown.bs.modal', function () {
                                if (refreshCallback) {
                                    refreshCallback($parentModal);
                                }
                            });
                            $parentModal.modal('show');
                        }
                    } else if (refreshCallback) {
                        refreshCallback(null); // Pass null if no parent modal
                    }
                    // Clear the flag after the process is complete
                    childModal.removeData('swal-managed');
                }, 200); // Small delay to ensure SweetAlert is visually gone
            });
        };

        showSwal(); // Call showSwal directly
    }).modal('hide');
}

//--------------------------------------------------------------------------------------------------------------------------------

$(document).on("click", ".open-modal", function () {
    const $button = $(this);
    const url = $button.data("url");
    const title = $button.data("title");
    const modalType = $button.data("modal-type");
    const parentModalSelector = $button.data("parent-modal");
    const uniqueId = $button.data("unique"); // Get the unique identifier
    const modalSize = $button.data("size"); // Get the custom modal size

    // Modal configurations
    const modalConfigs = {
        create: { baseId: "#createModal", content: "#createModalContent", save: "#createModalSave" },
        edit: { baseId: "#editModal", content: "#editModalContent", save: "#editModalSave" },
        view: { baseId: "#viewModal", content: "#viewModalContent" },
        delete_data: { baseId: "#deleteModal", content: "#deleteModalContent", save: "#deleteModalSave" },
        multiStepFormModal: { baseId: "#createMultiStepFormModal", content: "#createMultiStepFormModalContent", save: "#createMultiStepFormModalSave" }
    };

    // Get base modal configuration
    const modalConfig = modalConfigs[modalType];
    if (!modalConfig) {
        console.error(`Unknown modal type: ${modalType}`);
        return;
    }

    // Determine actual modal ID (with or without unique suffix)
    const modalId = uniqueId ? `${modalConfig.baseId.substring(1)}_${uniqueId}` : modalConfig.baseId.substring(1);
    const modalSelector = `#${modalId}`;

    // Check if this unique modal already exists
    let $targetModal = $(modalSelector);

    // If this unique modal doesn't exist yet, clone the base modal and customize it
    if (uniqueId && $targetModal.length === 0) {
        // Get the base modal
        const $baseModal = $(modalConfig.baseId);

        // Clone the base modal and customize it
        $targetModal = $baseModal.clone();
        $targetModal.attr("id", modalId);

        // Update content container ID
        const contentId = `${modalConfig.content.substring(1)}_${uniqueId}`;
        $targetModal.find(modalConfig.content).attr("id", contentId);

        // Update save button ID if it exists
        if (modalConfig.save) {
            const saveId = `${modalConfig.save.substring(1)}_${uniqueId}`;
            $targetModal.find(modalConfig.save).attr("id", saveId);
        }

        // Add the new modal to the DOM
        $("body").append($targetModal);
    }

    // Apply custom modal size if specified
    if (modalSize) {
        $targetModal.find('.modal-dialog').removeClass('modal-sm modal-lg modal-xl').addClass(modalSize);
    }

    // Get the content container
    const contentSelector = uniqueId ?
        `#${modalConfig.content.substring(1)}_${uniqueId}` :
        modalConfig.content;
    const $targetContent = $(contentSelector);

    // Store the parent modal selector on the target modal
    if (parentModalSelector) {
        $targetModal.data("parent-modal", parentModalSelector);
    }

    // Open the target modal
    const openNewModal = () => {
        $targetModal.find(".modal-title").text(title);
        $targetModal.find(".modal-content").removeClass("show");

        // Store this modal's refresh URL for future use
        $targetModal.data("self-refresh-url", url);

        $("#spinnerOverlay").css({ display: "flex", opacity: 1 });

        $.get(url)
            .done((data) => {
                // Check if the response is a JSON object indicating an error
                if (typeof data === 'object' && data !== null && data.hasOwnProperty('success') && data.success === false) {
                    // Use the existing handleAjaxError function to display the error
                    handleAjaxError({ responseJSON: data }, $button, $targetModal);
                    $("#spinnerOverlay").hide();
                    return; // Stop further processing
                }

                $targetContent.html(data);

                // Customize primary action label for approval process forms
                if (modalConfig.save) {
                    const $saveBtn = $targetModal.find(modalConfig.save);
                    const $form = $targetContent.find('form').first();
                    const formId = $form.attr('id');
                    if (formId === 'processJustificationForm' || formId === 'scheduleChangeProcessForm' || formId === 'processOvertimeForm'
                        || formId === 'dutyExchangeProcessForm' || formId === 'exchangeRestDayProcessForm' || formId === 'undertimeProcessForm') {
                        const reviewAction = ($form.find('[name="ReviewAction"]').val() || '').toString();
                        const isReject = reviewAction === 'Rejected' || reviewAction === '2';
                        $saveBtn.html(isReject
                            ? '<i class="fas fa-times mr-1"></i> Confirm Rejection'
                            : '<i class="fas fa-check mr-1"></i> Confirm Approval');
                    } else if (formId === 'batchPickerForm') {
                        $saveBtn.html('<i class="fas fa-plus-circle mr-1"></i> Create Group');
                    } else if (formId === 'requestOvertimeForm') {
                        $saveBtn.html('<i class="fas fa-paper-plane mr-1"></i> Submit Request');
                    } else if (formId === 'requestUndertimeForm') {
                        $saveBtn.html('<i class="fas fa-paper-plane mr-1"></i> Submit Request');
                    } else if (formId === 'requestDutyExchangeForm' || formId === 'requestExchangeRestDayForm') {
                        $saveBtn.html('<i class="fas fa-paper-plane mr-1"></i> Submit Request');
                    } else if (formId === 'holidayForm') {
                        $saveBtn.html('<i class="fas fa-save mr-1"></i> Save Holiday');
                    } else if (formId === 'generateCalendarForm') {
                        $saveBtn.html('<i class="fas fa-sync-alt mr-1"></i> Generate');
                    } else {
                        $saveBtn.html('<i class="fas fa-save"></i> Save');
                    }
                }

                // Show the modal first
                $targetModal.modal("show");
                $targetModal.find(".modal-content").addClass("show");

                // Wait briefly for modal to be fully visible in DOM
                setTimeout(() => {
                    // Initialize Select2 specifically for this modal
                    initializeSelect2ForModal(modalSelector);

                    // Also parse for validation
                    $.validator?.unobtrusive?.parse($targetContent);
                }, 50);
            })
            .fail((xhr) => {
                handleAjaxError(xhr, $button, $targetModal);
            })
            .always(() => $("#spinnerOverlay").hide());
    };

    // Close parent modal first (if specified and currently visible)
    if (parentModalSelector) {
        const $parentModal = $(parentModalSelector);
        if ($parentModal.hasClass("in") || $parentModal.hasClass("show")) {
            // Parent is open, hide it then open child modal
            $parentModal.one("hidden.bs.modal", openNewModal);
            $parentModal.modal("hide");
        } else {
            // Parent already hidden, directly open child modal
            openNewModal();
        }
    } else {
        openNewModal();
    }
});

// Expanded handler: handle ALL default Bootstrap modal close triggers
$(document).on('click', '.modal .btn-close, .modal .close, .modal [data-dismiss="modal"], .modal [data-bs-dismiss="modal"]', function () {
    const $modal = $(this).closest('.modal');
    if ($modal.data('should-reload-on-close')) {
        $modal.data('reload-on-hide', true);
        $modal.removeData('should-reload-on-close');
    }
});

// Reopen parent modal when child modal closes - with improved handling for Select2
$(document).on("hidden.bs.modal", ".modal", function () {
    const $closedModal = $(this);
    const parentModalSelector = $closedModal.data("parent-modal");

    // If this modal's closing is being managed by displaySwalAndReopenParent, skip generic handling
    if ($closedModal.data('swal-managed')) {
        return;
    }

    // If the closed modal has the 'reload-on-hide' flag, trigger a full page reload
    if ($closedModal.data('reload-on-hide') === true) {
        $closedModal.removeData('reload-on-hide'); // Clean up the flag
        location.reload();
        return; // Prevent further generic handling
    }


    // Clear any focus to prevent aria-hidden warnings
    if (document.activeElement && document.activeElement.blur) {
        document.activeElement.blur();
    }

    if (parentModalSelector) {
        const $parentModal = $(parentModalSelector);

        // Ensure the fade class is present for smooth transition
        if (!$parentModal.hasClass('fade')) {
            $parentModal.addClass('fade');
        }

        // Wait for the child modal to be fully hidden before showing the parent
        setTimeout(() => {
            $parentModal.modal('show');
            // Re-initialize Select2 and fix scrolling after modal is fully shown
            $parentModal.one('shown.bs.modal', function () {
                initializeSelect2ForModal(parentModalSelector);
                fixModalScrolling(parentModalSelector);
            });
        }, 150); // 150ms is Bootstrap's default fade duration
    }
});

// Additional handler for when parent modals are shown after child modal closes
$(document).on("shown.bs.modal", ".modal", function () {
    const $modal = $(this);
    const modalId = '#' + $modal.attr('id');

    // Check if this is a parent modal that was reopened
    if ($modal.data('was-parent-modal')) {
        // Fix scrolling issues only once
        setTimeout(() => {
            fixModalScrolling(modalId);
        }, 50);

        // Remove the flag
        $modal.removeData('was-parent-modal');
    }
});

/**
 * Builds FormData for modal AJAX posts. Fixes Select2 multi-select and checkbox values
 * that are often omitted or incorrect in default FormData serialization.
 */
function enrichModalFormData($form) {
    const formData = new FormData($form[0]);

    $form.find('select[multiple][name]').each(function () {
        const $sel = $(this);
        const name = $sel.attr('name');
        if (!name || $sel.prop('disabled')) {
            return;
        }

        const values = $sel.val() || [];
        [...formData.keys()].forEach(function (key) {
            if (key === name) {
                formData.delete(key);
            }
        });
        values.forEach(function (v) {
            formData.append(name, v);
        });
    });

    $form.find('input[type="checkbox"][name]').each(function () {
        const name = $(this).attr('name');
        if (!name) {
            return;
        }
        formData.delete(name);
        formData.append(name, $(this).is(':checked') ? 'true' : 'false');
    });

    return formData;
}

$(document).on('click', '[id^=createModalSave], [id^=editModalSave], [id^=deleteModalSave], [id^=createMultiStepFormModalSave]', function () {
    const $button = $(this);
    const $modal = $button.closest(".modal");
    const $form = $modal.find("form");
    const parentModalSelector = $modal.data("parent-modal");

    if ($form.length === 0) {
        Swal.fire({ icon: 'warning', text: 'No form found!' });
        return;
    }

    $form.removeData('validator');
    $form.removeData('unobtrusiveValidation');

    $.validator.unobtrusive.parse($form);

    if ($form.attr('id') === 'justifyTimeLogForm') {
        const $timeError = $form.find('#justifyTimeSlotsError');
        const hasTime = $form.find('input.dm-time-input').toArray().some(function (el) { return !!el.value; });
        if (!hasTime) {
            $timeError.show();
            return;
        }
        $timeError.hide();
    }

    if ($form.attr('id') === 'scheduleChangeProcessForm' || $form.attr('id') === 'processJustificationForm' || $form.attr('id') === 'processOvertimeForm'
        || $form.attr('id') === 'dutyExchangeProcessForm' || $form.attr('id') === 'exchangeRestDayProcessForm' || $form.attr('id') === 'undertimeProcessForm') {
        const reviewAction = ($form.find('[name="ReviewAction"]').val() || '').toString();
        const isReject = reviewAction === 'Rejected' || reviewAction === '2';
        const $reviewNotes = $form.find('[name="ReviewNotes"]');
        if (!isReject) {
            $reviewNotes.removeAttr('required');
            if ($reviewNotes.rules) {
                $reviewNotes.rules('remove', 'required');
            }
        }
    }

    if ($form.attr('id') === 'batchPickerForm') {
        const selectedIds = ($form.find('#batchPickerSelectedIds').val() || '').trim();
        if (!selectedIds) {
            toastr.warning('Select at least one employee for this group.');
            $button.prop('disabled', false);
            return;
        }
    }

    if (!$form.valid()) {
        return;
    }

    $button.prop('disabled', true);

    const formData = enrichModalFormData($form);

    $.ajax({
        url: $form.attr('action'),
        type: $form.attr('method') || 'POST',
        data: formData,
        contentType: false,
        processData: false,
        success: function (response) {
            if (response.success) {
                const refreshParentModal = ($pModal) => {
                    // Check if this is a print preview modal that needs special handling
                    if (parentModalSelector === '#printPreviewModal') {

                    } else {
                        // Regular parent modal refresh
                        const parentRefreshUrl = $pModal.data("self-refresh-url");

                        if (parentRefreshUrl) {
                            // Try to find the most likely content container
                            let parentContentContainer = $pModal.find('.modal-body > div').first();
                            if (parentContentContainer.length === 0) {
                                parentContentContainer = $pModal.find('.modal-body');
                            }

                            // Show spinner while loading
                            $("#spinnerOverlay").css({ display: "flex", opacity: 1 });

                            // Fetch fresh content
                            $.get(parentRefreshUrl)
                                .done((data) => {
                                    parentContentContainer.html(data);
                                    // Re-initialize Select2 and fix scrolling after modal is fully shown
                                    initializeSelect2ForModal(parentModalSelector);
                                    $.validator?.unobtrusive?.parse(parentContentContainer);
                                    fixModalScrolling(parentModalSelector);
                                })
                                .fail((xhr) => {
                                    handleAjaxError(xhr);
                                })
                                .always(() => $("#spinnerOverlay").hide());
                        }
                    }
                };

                // Display success SweetAlert and then handle parent modal refresh or page reload
                displaySwalAndReopenParent($modal, parentModalSelector, {
                    icon: "success",
                    title: "<strong style='font-size: 34px;'>Success</strong>",
                    html: `<p style="font-size: 20px;">${response.message}</p>`,
                    showConfirmButton: false,
                    timer: 3000
                }, ($pModal) => {
                    if (response.success === true && $pModal) {
                        $pModal.data('should-reload-on-close', true);
                    }
                    if (parentModalSelector) {
                        refreshParentModal($pModal);
                    } else if ($.fn.DataTable.isDataTable('#employeeAllowancesTable')) {
                        $('#employeeAllowancesTable').DataTable().ajax.reload(null, false);
                    } else if (response.needsRefresh !== false) {
                        location.reload();
                    }
                });
            } else { // Handle error response from server
                Swal.fire({
                    icon: 'error',
                    title: "<strong style='font-size: 34px; color: red;'>Oops...</strong>",
                    html: `<p style="font-size: 20px;">${response.message}</p>`,
                    showConfirmButton: true
                });
            }
        },
        error: function (xhr, status, error) {
            const parentModalSelector = $modal.data("parent-modal");
            Swal.fire({
                icon: 'error',
                title: 'An error occurred',
                text: 'Please check the console for details.',
                showConfirmButton: true
            });
        },
        complete: function () {
            $button.prop('disabled', false);
        }
    });
});


/*----------------------------------------------For Select2 Initialization----------------------------------------------*/

// Select2 initialization function that properly handles both modal and non-modal contexts
function initializeSelect2ForModal(modalId) {
    // Common Select2 configuration with search enabled
    var defaultConfig = {
        allowClear: true,
        width: '100%',
        minimumResultsForSearch: 0
    };

    // Handle unique modal IDs - important for nested modals
    var modalSelector = modalId;
    if (modalId === 'body' || !modalId) {
        modalSelector = 'body';
    } else if (!modalId.startsWith('#')) {
        modalSelector = '#' + modalId;
    }

    // Find the proper dropdown parent container
    var $modalContainer = $(modalSelector);

    // Use modal-content if available, otherwise use the modal or body
    var $dropdownParent;
    if ($modalContainer.find('.modal-content').length && modalSelector !== 'body') {
        $dropdownParent = $modalContainer.find('.modal-content').first();
        // Update defaultConfig with the correct dropdown parent for modals
        defaultConfig.dropdownParent = $dropdownParent;
    } else if ($modalContainer.length && $modalContainer.hasClass('modal')) {
        $dropdownParent = $modalContainer;
        // Update defaultConfig with the correct dropdown parent for modals
        defaultConfig.dropdownParent = $dropdownParent;
    }
    // For body elements, don't set a dropdown parent - let Select2 use default behavior

    // Define selectors array including all your select types
    var selectors = [
        { selector: '.general-select', placeholder: "--Select--" },
        { selector: '.role-select', placeholder: "--Select a Role--" },
        { selector: '.dept-select', placeholder: "--Select Department--" },
        { selector: '.designation-select', placeholder: "--Select Designation--" },
        { selector: '.parentId-select', placeholder: "--Select Parent Name--" },
        { selector: '.taskId-select', placeholder: "--Select a Task--" },
        { selector: '.status-select', placeholder: "--Select Status--" },
        { selector: '.formType-select', placeholder: "--Select Form Type--" },
        { selector: '.year-select', placeholder: "--Select Year--" },
    ];

    // Initialize all select2 elements
    selectors.forEach(function (item) {
        $(modalSelector + ' ' + item.selector).each(function () {
            var $this = $(this);

            // Skip if already initialized by other scripts
            if ($this.hasClass("validation-initialized") ||
                $this.data('validation-initialized') ||
                $this.data('historical-initialized') ||
                $this.data('historical-select2-initialized')) {
                return;
            }

            // Get data-placeholder attribute if available, otherwise use the item placeholder
            var placeholder = $this.data('placeholder') || item.placeholder || "Select an option";

            // Destroy if already initialized
            if ($this.hasClass("select2-hidden-accessible")) {
                try {
                    $this.select2('destroy');
                } catch (e) {
                    // console.error('Error destroying previous Select2:', e); // This line is removed
                }
            }

            // Create a specific config for this element
            var elementConfig = $.extend({}, defaultConfig, {
                placeholder: placeholder
            });

            // Initialize the select2
            $this.select2(elementConfig);

            // Mark as initialized to prevent duplicate initialization
            $this.data('select2-initialized', true);

            // Handle validation styling
            $this.on('change', function () {
                if ($(this).val()) {
                    $(this).removeClass('input-validation-error')
                        .next('.select2-container')
                        .find('.select2-selection')
                        .removeClass('input-validation-error');
                }
                $(this).valid();
            });

            // Apply initial validation state
            if ($(this).hasClass('input-validation-error')) {
                $(this).next('.select2-container')
                    .find('.select2-selection')
                    .addClass('input-validation-error');
            }
        });
    });
}

$(document).ready(function () {
    // Initialize all select2 elements when the document is ready
    initializeSelect2ForModal('body');

    // Initialize when standard modals are shown
    $('#createModal, #editModal, #createMultiStepFormModal, #viewModal').on('shown.bs.modal', function () {
        initializeSelect2ForModal('#' + $(this).attr('id'));
    });

    // Special handling for dynamically created modals with unique IDs
    $(document).on('shown.bs.modal', '.modal', function () {
        var modalId = $(this).attr('id');

        // Ensure modal is scrollable
        var $modal = $(this);
        var $modalDialog = $modal.find('.modal-dialog');
        var $modalContent = $modal.find('.modal-content');

        // Reset any inline styles that might interfere with scrolling
        $modal.css('overflow', '');
        $modalDialog.css('overflow', '');
        $modalContent.css('overflow', '');

        // Ensure body has modal-open class for proper backdrop
        $('body').addClass('modal-open');
    });

    // Handle modal hidden event to clean up
    $(document).on('hidden.bs.modal', '.modal', function () {
        var $modal = $(this);

        // Clean up any inline styles
        $modal.css('overflow', '');
        $modal.find('.modal-dialog').css('overflow', '');
        $modal.find('.modal-content').css('overflow', '');

        // Clean up data attributes
        $modal.removeData('last-scroll-fix');
        $modal.removeData('processing-scroll-fix');
        $modal.removeData('was-parent-modal');

        // Remove modal-open class if no other modals are open
        if ($('.modal.show').length === 0) {
            $('body').removeClass('modal-open');
        }
    });

    // Reinitialize after AJAX completes (for forms loaded via AJAX)
    $(document).ajaxComplete(function (event, xhr, settings) {
        // Only initialize if it was a GET request to load content
        if (settings.type === 'GET' && settings.url.includes('/')) {
            setTimeout(function () {
                // Find all visible modals and initialize select2 in them
                $('.modal:visible').each(function () {
                    initializeSelect2ForModal('#' + $(this).attr('id'));
                });

                // Also reinitialize select2 in the body (for non-modal elements)
                initializeSelect2ForModal('body');
            }, 100);
        }
    });

    // Add page load complete handler to catch dynamically added elements
    $(window).on('load', function () {
        setTimeout(function () {
            initializeSelect2ForModal('body');
        }, 500);
    });
});


// Function to initialize all select2 elements across the page
function initializeAllSelect2() {
    // Initialize select2 in body first
    initializeSelect2ForModal('body');

    // Then initialize in all visible modals
    $('.modal:visible').each(function () {
        initializeSelect2ForModal('#' + $(this).attr('id'));
    });
}

// Function to fix modal scrolling issues
function fixModalScrolling(modalSelector) {
    const $modal = $(modalSelector);
    if ($modal.length === 0) return;

    // Check if we've already fixed this modal recently to prevent redundant calls
    const lastFixed = $modal.data('last-scroll-fix');
    const now = Date.now();
    if (lastFixed && (now - lastFixed) < 500) {
        return;
    }

    // Check if modal is in a processing state
    if ($modal.data('processing-scroll-fix')) {
        return;
    }

    // Mark as processing
    $modal.data('processing-scroll-fix', true);

    // Reset any problematic inline styles
    $modal.css({
        'overflow': '',
        'overflow-y': '',
        'overflow-x': ''
    });

    $modal.find('.modal-dialog').css({
        'overflow': '',
        'overflow-y': '',
        'overflow-x': ''
    });

    $modal.find('.modal-content').css({
        'overflow': '',
        'overflow-y': '',
        'overflow-x': ''
    });

    // Ensure proper modal state
    if (!$modal.hasClass('show')) {
        $modal.addClass('show');
    }

    // Ensure body has modal-open class
    $('body').addClass('modal-open');

    // Force a reflow to ensure proper rendering
    $modal[0].offsetHeight;

    // Mark when we last fixed this modal
    $modal.data('last-scroll-fix', now);

    // Remove processing flag after a short delay
    setTimeout(() => {
        $modal.removeData('processing-scroll-fix');
    }, 100);
}

// Add a global function that can be called from anywhere to initialize select2
window.initializeAllSelect2 = initializeAllSelect2;
window.fixModalScrolling = fixModalScrolling;

//---------------------------------------------------------------------------------------------------------------------------------------------------------------

$(document).ready(function () {
    $('[data-toggle="tooltip"]').tooltip();
});

//---------------------------------------------------------------------------------------------------------------------------------------------------------------