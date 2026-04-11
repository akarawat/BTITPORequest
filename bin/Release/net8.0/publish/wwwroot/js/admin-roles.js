/* ============================================================
   admin-roles.js — Role assignment via AJAX
   ============================================================ */
$(function () {
    var $toast = $('#roleToast');
    var toast = new bootstrap.Toast($toast[0], { delay: 3000 });

    function showToast(msg, isSuccess) {
        $('#roleToastBody').text(msg);
        $toast.removeClass('text-bg-success text-bg-danger')
              .addClass(isSuccess ? 'text-bg-success' : 'text-bg-danger');
        toast.show();
    }

    function setRole(sam, name, email, dept, role) {
        $.ajax({
            url: '/Admin/SetRole',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                samAcc: sam, fullName: name, email: email,
                department: dept, roleName: role
            }),
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    || $('meta[name="csrf-token"]').attr('content') || ''
            },
            success: function (res) {
                if (res.success) {
                    showToast(res.message, true);
                    setTimeout(function () { location.reload(); }, 1000);
                } else {
                    showToast(res.message || 'Error', false);
                }
            },
            error: function (xhr) {
                showToast('Server error: ' + (xhr.responseJSON?.message || xhr.statusText), false);
            }
        });
    }

    // ── Assign Role button ────────────────────────────────────
    $(document).on('click', '.btn-role', function () {
        var $btn = $(this);
        var role = $btn.data('role');
        var $row = $btn.closest('tr');
        var sam  = $row.data('sam');
        var name = $row.data('name');
        var email = $row.data('email');
        var dept = $row.data('dept');

        // toggle: if already active, remove role
        if ($btn.hasClass('active')) {
            role = '';
        }

        var confirmMsg = role
            ? 'Assign ' + role + ' role to ' + name + '?'
            : 'Remove role from ' + name + '?';

        if (!confirm(confirmMsg)) return;
        setRole(sam, name, email, dept, role);
    });

    // ── Remove Role button ────────────────────────────────────
    $(document).on('click', '.btn-remove-role', function () {
        var $row = $(this).closest('tr');
        var name = $row.data('name');
        if (!confirm('Remove role from ' + name + '?')) return;
        setRole($row.data('sam'), name, $row.data('email'), $row.data('dept'), '');
    });
});
