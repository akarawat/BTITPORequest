/* ============================================================
   vendor-autocomplete.js — Vendor search & autofill for Create PO
   ============================================================ */
$(function () {
    var $search   = $('#vendorSearch');
    var $dropdown = $('#vendorDropdown');
    var timer;

    // ── ค้นหา vendor แบบ debounce ────────────────────────────
    $search.on('input', function () {
        clearTimeout(timer);
        var q = $(this).val().trim();
        if (q.length < 1) { $dropdown.hide(); return; }

        timer = setTimeout(function () {
            $.getJSON('/Vendor/Search', { q: q }, function (data) {
                $dropdown.empty();
                if (!data || data.length === 0) {
                    $dropdown.hide(); return;
                }
                data.forEach(function (v) {
                    var $item = $('<div>')
                        .addClass('px-3 py-2 vendor-item')
                        .css({ cursor: 'pointer', borderBottom: '1px solid #f0f0f0', fontSize: '.85rem' })
                        .html('<strong>' + $('<div>').text(v.vendorCompany).html() + '</strong>' +
                              '<span class="text-muted ms-2">' + $('<div>').text(v.vendorAttn).html() + '</span>')
                        .data('vendor', v)
                        .on('click', function () { fillVendor($(this).data('vendor')); });
                    $dropdown.append($item);
                });
                $dropdown.show();
            });
        }, 250);
    });

    // ── hover style ───────────────────────────────────────────
    $dropdown.on('mouseenter', '.vendor-item', function () {
        $(this).css('background', '#f0f7ff');
    }).on('mouseleave', '.vendor-item', function () {
        $(this).css('background', '');
    });

    // ── ปิด dropdown เมื่อ click นอก ─────────────────────────
    $(document).on('click', function (e) {
        if (!$(e.target).closest('#vendorSearch, #vendorDropdown').length)
            $dropdown.hide();
    });

    // ── Fill ข้อมูล vendor ลงใน form fields ──────────────────
    function fillVendor(v) {
        $('[name="PO.VendorAttn"]').val(v.vendorAttn);
        $('[name="PO.VendorCompany"]').val(v.vendorCompany);
        $('[name="PO.VendorAddress"]').val(v.vendorAddress);
        $('[name="PO.VendorTel"]').val(v.vendorTel || '');
        $('[name="PO.VendorFax"]').val(v.vendorFax || '');
        $('[name="PO.VendorEmail"]').val(v.vendorEmail || '');
        $search.val('');
        $dropdown.hide();
    }
});
