// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

const toastDelayMilliseconds = 15000;

document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.toast').forEach((toastElement) => {
        const toast = new bootstrap.Toast(toastElement, {
            autohide: true,
            delay: toastDelayMilliseconds
        });
        toast.show();
    });
});
