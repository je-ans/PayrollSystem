// Scroll persistence for this page (restore after DataTables renders, reload only)
let __scrollRestored = false;
const __scrollKey = 'scroll-pos:' + location.pathname + location.search;
function __tryRestoreScroll() {
    if (__scrollRestored) return;
    try {
        const navEntry = (performance && performance.getEntriesByType) ? performance.getEntriesByType('navigation')[0] : null;
        const navType = navEntry ? navEntry.type : (performance && performance.navigation ? (performance.navigation.type === 1 ? 'reload' : '') : '');
        if (navType !== 'reload') return;
        const savedY = sessionStorage.getItem(__scrollKey);
        if (savedY !== null && !location.hash) {
            setTimeout(function () {
                window.scrollTo(0, parseInt(savedY, 10) || 0);
            }, 0);
            __scrollRestored = true;
        }
    } catch (e) { }
}
