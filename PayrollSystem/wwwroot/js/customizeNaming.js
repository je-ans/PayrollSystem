
// ======================== NAME CUSTOMIZATION =================================== //

let isCustomNameModified = false;
const firstName = document.getElementById('firstName');
const middleName = document.getElementById('middleName');
const lastName = document.getElementById('lastName');
const customizeName = document.getElementById('customizeName');
const nameFields = [firstName, middleName, lastName];

// Function to update the custom name field
function updateCustomName() {
    if (isCustomNameModified) return;

    const fName = firstName.value.trim();
    const mName = middleName.value.trim();
    const lName = lastName.value.trim();

    let customName = fName;
    if (mName) {
        customName += ' ' + mName.charAt(0) + '.';
    }
    if (lName) {
        customName += ' ' + lName;
    }

    customizeName.value = customName;
}

nameFields.forEach(field => {
    field.addEventListener('input', updateCustomName);
});

customizeName.addEventListener('input', function () {
    const currentValue = this.value;
    const generatedValue = generateDefaultName();
    isCustomNameModified = (currentValue !== generatedValue);
});

function generateDefaultName() {
    const fName = firstName.value.trim();
    const mName = middleName.value.trim();
    const lName = lastName.value.trim();

    let name = fName;
    if (mName) {
        name += ' ' + mName.charAt(0) + '.';
    }
    if (lName) {
        name += ' ' + lName;
    }
    return name;
}

updateCustomName();


// ================================ DESIGNATION CUSTOMIZATION ========================================== //

const designationSelect = document.getElementById('designation');
const customizeDesignation = document.getElementById('customizeDesignation');

// Function to update the custom designation based on the selected option
function updateCustomDesignation() {
    if (designationSelect && designationSelect.selectedIndex >= 0) {
        const selectedOption = designationSelect.options[designationSelect.selectedIndex];
        if (
            selectedOption &&
            selectedOption.value &&
            selectedOption.value !== '' &&
            selectedOption.text.trim() !== '--Select a Designation--'
        ) {
            customizeDesignation.value = selectedOption.text;

            const inputEvent = new Event('input', { bubbles: true });
            customizeDesignation.dispatchEvent(inputEvent);
        }
    }
}

if (designationSelect && customizeDesignation) {
    designationSelect.addEventListener('change', updateCustomDesignation);

    setTimeout(updateCustomDesignation, 100);

    if (typeof $ !== 'undefined' && typeof $.fn.select2 !== 'undefined') {
        $(designationSelect).on('select2:select', updateCustomDesignation);
    }
}