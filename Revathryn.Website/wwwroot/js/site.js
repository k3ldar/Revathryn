// Close the collapsed navigation after a link is selected on small screens.
(function () {
    const navCollapse = document.getElementById('siteNavigation');
    if (!navCollapse) {
        return;
    }

    navCollapse.addEventListener('click', function (event) {
        const link = event.target.closest('.nav-link');
        if (!link || !window.bootstrap) {
            return;
        }

        const collapse = window.bootstrap.Collapse.getInstance(navCollapse);
        if (collapse) {
            collapse.hide();
        }
    });
})();
