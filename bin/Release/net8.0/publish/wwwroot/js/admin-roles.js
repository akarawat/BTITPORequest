/* ============================================================
   admin-roles.js — Multi-role assignment via AJAX
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

    function callApi(url, payload, callback) {
        $.ajax({
            url: url,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(payload),
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    || $('meta[name="csrf-token"]').attr('content') || ''
            },
            success: function (res) {
                if (res.success) {
                    showToast(res.message, true);
                    setTimeout(function () { location.reload(); }, 800);
                } else {
                    showToast(res.message || 'Error', false);
                }
            },
            error: function (xhr) {
                showToast('Server error: ' + (xhr.responseJSON?.message || xhr.statusText), false);
            }
        });
    }

    // ── Role button click ─────────────────────────────────────
    // active = already has this role → Remove
    // inactive = doesn't have this role → Add
    $(document).on('click', '.btn-role', function () {
        var $btn  = $(this);
        var role  = $btn.data('role');
        var $row  = $btn.closest('tr');
        var sam   = $row.data('sam');
        var name  = $row.data('name');
        var email = $row.data('email');
        var dept  = $row.data('dept');

        if ($btn.hasClass('active')) {
            // Remove this specific role
            if (!confirm('ลบ Role "' + role + '" ออกจาก ' + name + '?')) return;
            callApi('/Admin/RemoveRole', { samAcc: sam, fullName: name, roleName: role });
        } else {
            // Add this role
            if (!confirm('เพิ่ม Role "' + role + '" ให้ ' + name + '?')) return;
            callApi('/Admin/SetRole', {
                samAcc: sam, fullName: name, email: email,
                department: dept, roleName: role
            });
        }
    });

    // ── Remove all roles button (x icon) ─────────────────────
    $(document).on('click', '.btn-remove-all-roles', function () {
        var $row = $(this).closest('tr');
        var name = $row.data('name');
        var sam  = $row.data('sam');
        if (!confirm('ลบทุก Role ออกจาก ' + name + '?')) return;
        callApi('/Admin/RemoveRole', { samAcc: sam, fullName: name, roleName: '' });
    });

    // ── Remove single role from Roles page ───────────────────
    $(document).on('click', '.btn-remove-role-row', function () {
        var $btn  = $(this);
        var sam   = $btn.data('sam');
        var name  = $btn.data('name');
        var role  = $btn.data('role');
        if (!confirm('ลบ Role "' + role + '" ออกจาก ' + name + '?')) return;
        callApi('/Admin/RemoveRole', { samAcc: sam, fullName: name, roleName: role });
    });
});
