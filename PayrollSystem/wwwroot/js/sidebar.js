document.addEventListener('DOMContentLoaded', function () {
    const body = document.body;
    const toggleButton = document.querySelector('[data-widget="pushmenu"]');
    const sidebarStateKey = 'sidebarCollapsed';

    // Function to set the sidebar state
    function setSidebarState() {
        if (body.classList.contains('sidebar-collapse')) {
            localStorage.setItem(sidebarStateKey, 'true');
        } else {
            localStorage.removeItem(sidebarStateKey);
        }
    }

    // Function to apply the saved sidebar state on load
    function applySidebarState() {
        if (localStorage.getItem(sidebarStateKey) === 'true') {
            body.classList.add('sidebar-collapse');
        } else {
            body.classList.remove('sidebar-collapse');
        }
    }

    // Apply state on initial load
    applySidebarState();

    // Save state when the toggle button is clicked
    if (toggleButton) {
        toggleButton.addEventListener('click', function () {
            // AdminLTE toggles the class after this event, so we use a slight delay
            setTimeout(setSidebarState, 50);
        });
    }

    // Handle AdminLTE's direct manipulation (e.g., on window resize)
    // This observes changes to the body's class list
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.attributeName === 'class') {
                setSidebarState();
            }
        });
    });

    observer.observe(body, { attributes: true });
});
