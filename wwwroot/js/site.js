/* ============================================================
   BERNINA IT PO Request — site.js (global)
   ============================================================ */

$(function () {
    // ── Sidebar toggle (mobile) ─────────────────────────────
    $('#sidebarToggle').on('click', function () {
        $('body').toggleClass('sidebar-open');
    });
    $('#sidebarClose, #sidebarOverlay').on('click', function () {
        $('body').removeClass('sidebar-open');
    });

    // ── Auto-dismiss toasts ─────────────────────────────────
    setTimeout(function () {
        $('.toast').each(function () {
            var toast = bootstrap.Toast.getOrCreateInstance(this, { delay: 4000 });
            toast.show();
        });
    }, 100);

    // ── Confirm on dangerous buttons ────────────────────────
    $('[data-confirm]').on('click', function (e) {
        if (!confirm($(this).data('confirm'))) {
            e.preventDefault();
            return false;
        }
    });

    // ── Active nav auto-highlight fallback ──────────────────
    var path = window.location.pathname.split('/')[1];
    $('.bt-nav-link').each(function () {
        var href = $(this).attr('href') || '';
        if (href !== '/' && href.toLowerCase().indexOf(path.toLowerCase()) > -1) {
            $(this).addClass('active');
        }
    });
});
