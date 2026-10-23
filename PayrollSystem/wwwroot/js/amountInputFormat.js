// Document Source Type Form Handler
(function () {
    'use strict';

    /**
     * Format amount input with commas for thousands separator
     * @param {HTMLInputElement} input - The input element to format
     */
    function formatAmount(input) {
        if (!input || !input.value) return;

        // Store the cursor position
        const cursorPosition = input.selectionStart;

        // Remove any non-digit characters except decimal point and minus sign
        let value = input.value.replace(/[^\d.\-]/g, '');

        // Handle empty or invalid input
        if (!value || value === '.' || value === '-') {
            input.value = value;
            return;
        }

        // Handle negative sign - ensure only one at the start
        const isNegative = value.startsWith('-');
        value = value.replace(/-/g, ''); // Remove all minus signs
        if (isNegative) {
            value = '-' + value; // Add back only one minus sign at the start
        }

        // Split into whole and decimal parts
        const parts = value.split('.');

        // Ensure only one decimal point
        if (parts.length > 2) {
            value = parts[0] + '.' + parts.slice(1).join('');
            parts.length = 2;
            parts[1] = parts.slice(1).join('');
        }

        // Remove leading zeros from the whole number part
        if (parts[0]) {
            // Remove leading zeros but keep at least one digit
            parts[0] = parts[0].replace(/^0+/, '') || '0';

            // If we removed all digits, set to '0'
            if (parts[0] === '') {
                parts[0] = '0';
            }
        }

        // Format the whole number part with commas (only if it's not just '0')
        if (parts[0] && parts[0] !== '0') {
            // Use a more robust regex for comma formatting
            parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        }

        // Reconstruct the value
        value = parts.join('.');

        // Only update if the value actually changed to avoid cursor jumping
        if (input.value !== value) {
            const oldLength = input.value.length;
            input.value = value;

            // Calculate new cursor position
            const newLength = input.value.length;
            let newCursorPosition = cursorPosition;

            // Adjust cursor position based on added/removed commas
            if (newLength > oldLength) {
                // If commas were added, move cursor forward
                const addedCommas = (newLength - oldLength);
                newCursorPosition += addedCommas;
            } else if (newLength < oldLength) {
                // If commas were removed, move cursor backward
                const removedCommas = (oldLength - newLength);
                newCursorPosition = Math.max(0, newCursorPosition - removedCommas);
            }

            // Ensure cursor position is within bounds
            newCursorPosition = Math.min(newCursorPosition, value.length);
            input.setSelectionRange(newCursorPosition, newCursorPosition);
        }
    }

    /**
     * Format a number with commas and decimal places
     * @param {number} value - The number to format
     * @param {number} [decimals=2] - Number of decimal places
     * @returns {string} Formatted number string
     */
    function formatNumber(value, decimals = 2) {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        }).format(value);
    }

    /**
     * Initialize form handlers when DOM is ready
     */
    function initializeForm() {
        const form = document.querySelector('form');
        const amountInputs = document.querySelectorAll('.amount-input');


        // Add form submit handler to remove commas from all amount inputs before submission
        if (form) {
            form.addEventListener('submit', function (e) {
                amountInputs.forEach(function (input) {
                    input.value = input.value.replace(/,/g, '');
                });
            });
        }

        // Add event listeners to all amount inputs
        amountInputs.forEach(function (amountInput) {

            // Add input event listener for amount formatting
            amountInput.addEventListener('input', function (e) {
                // Prevent the default input event to avoid recursion
                e.stopPropagation();
                
                // Get the current value
                let value = this.value;
                
                // Check if the last character entered is valid
                const lastChar = value[value.length - 1];
                if (lastChar && !/[\d.\-]/.test(lastChar)) {
                    // If not valid, remove the last character
                    this.value = value.slice(0, -1);
                    return;
                }
                
                // Ensure minus sign is only at the start
                if (value.indexOf('-') > 0) {
                    this.value = value.replace(/-/g, '');
                    return;
                }
                
                formatAmount(this);
            });

            // Add keypress event listener to prevent invalid characters
            amountInput.addEventListener('keypress', function (e) {
                // Allow: backspace, delete, tab, escape, enter, decimal point, minus sign
                if ([8, 9, 13, 27, 46, 110, 190, 45].indexOf(e.keyCode) !== -1 ||
                    // Allow: Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
                    (e.keyCode >= 35 && e.keyCode <= 39) ||
                    // Allow: home, end, left, right
                    (e.ctrlKey && [65, 67, 86, 88].indexOf(e.keyCode) !== -1)) {
                    return;
                }
                
                // Check if the key pressed is a number, decimal point, or minus sign
                if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && 
                    (e.keyCode < 96 || e.keyCode > 105) && 
                    e.keyCode !== 190 && e.keyCode !== 110 && e.keyCode !== 45) {
                    e.preventDefault();
                }
                
                // Prevent multiple decimal points
                if ((e.keyCode === 190 || e.keyCode === 110) && this.value.indexOf('.') !== -1) {
                    e.preventDefault();
                }
                
                // Only allow minus sign at the start of the input
                if (e.keyCode === 45 && this.selectionStart !== 0) {
                    e.preventDefault();
                }

                // Prevent minus sign if one already exists
                if (e.keyCode === 45 && this.value.indexOf('-') !== -1) {
                    e.preventDefault();
                }
            });

            // Format initial value if it exists - with a small delay to ensure value is set
            setTimeout(function () {
                if (amountInput.value && amountInput.value.trim() !== '') {
                    formatAmount(amountInput);
                }
            }, 100);
        });

        // Initialize jQuery validation for amount inputs if jQuery is available
        if (typeof $ !== 'undefined' && $.validator) {
            $.validator.addMethod('max', function (value, element, param) {
                // Remove commas before comparing
                const numericValue = parseFloat(value.replace(/,/g, '')) || 0;
                return this.optional(element) || numericValue <= param;
            }, function (param, element) {
                // Format the max value with commas and decimal places
                const formattedMax = formatNumber(param);
                return `Please enter a value less than or equal to ${formattedMax}`;
            });

            // Add min validation method
            $.validator.addMethod('min', function (value, element, param) {
                // Remove commas before comparing
                const numericValue = parseFloat(value.replace(/,/g, '')) || 0;
                return this.optional(element) || numericValue >= param;
            }, function (param, element) {
                // Format the min value with commas and decimal places
                const formattedMin = formatNumber(param);
                return `Please enter a value greater than or equal to ${formattedMin}`;
            });
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeForm);
    } else {
        initializeForm();
    }

    // Make functions available globally
    window.formatAmount = formatAmount;
    window.formatNumber = formatNumber;
})();