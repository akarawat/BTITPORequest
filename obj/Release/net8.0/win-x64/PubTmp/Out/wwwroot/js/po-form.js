/* ============================================================
   BERNINA IT PO Request — po-form.js
   Line item management & totals calculation
   ============================================================ */

var lineItems = [];
var lineCounter = 0;

function initPOForm(initialItems) {
    lineItems = [];
    lineCounter = 0;

    if (initialItems && initialItems.length > 0) {
        initialItems.forEach(function (item) {
            addLineRow(item);
        });
    } else {
        addLineRow(); // Start with one empty row
    }

    // Add item button
    $('#addLineBtn').on('click', function () { addLineRow(); });

    // VAT change
    $('#vatSelect').on('change', function () { recalcTotals(); });

    // Form submit
    $('#poForm').on('submit', function () {
        if (lineItems.length === 0) {
            alert('Please add at least one line item.');
            return false;
        }
        // Serialise line items to hidden field
        $('#lineItemsJson').val(JSON.stringify(lineItems));
        return true;
    });

    recalcTotals();
}

function addLineRow(item) {
    lineCounter++;
    var idx = lineCounter;

    var li = {
        _idx: idx,
        lineNo: idx,
        description: (item && item.description) || '',
        quantity: (item && item.quantity) || 1,
        unitPrice: (item && item.unitPrice) || 0
    };
    lineItems.push(li);

    var row = $('<tr class="line-row" data-idx="' + idx + '">').html(
        '<td class="text-muted fw-semibold text-center line-no">' + idx + '</td>' +
        '<td>' +
            '<input type="text" class="form-control form-control-sm desc-input" ' +
                   'value="' + escHtml(li.description) + '" ' +
                   'placeholder="Item description" required ' +
                   'data-idx="' + idx + '" />' +
        '</td>' +
        '<td>' +
            '<input type="number" class="form-control form-control-sm qty-input text-end" ' +
                   'value="' + li.quantity + '" min="0.001" step="0.001" required ' +
                   'data-idx="' + idx + '" />' +
        '</td>' +
        '<td>' +
            '<input type="number" class="form-control form-control-sm price-input text-end" ' +
                   'value="' + li.unitPrice + '" min="0" step="0.01" required ' +
                   'data-idx="' + idx + '" />' +
        '</td>' +
        '<td class="text-end fw-semibold amount-cell">' + formatNum(li.quantity * li.unitPrice) + '</td>' +
        '<td class="text-center">' +
            '<button type="button" class="btn btn-sm btn-outline-danger remove-btn" data-idx="' + idx + '">' +
                '<i class="bi bi-trash"></i>' +
            '</button>' +
        '</td>'
    );

    // Event: description change
    row.find('.desc-input').on('input', function () {
        getItem($(this).data('idx')).description = $(this).val();
    });

    // Event: qty / price change
    row.find('.qty-input, .price-input').on('input change', function () {
        var rowIdx = $(this).data('idx');
        var item = getItem(rowIdx);
        var qty = parseFloat(row.find('.qty-input').val()) || 0;
        var price = parseFloat(row.find('.price-input').val()) || 0;
        item.quantity = qty;
        item.unitPrice = price;
        row.find('.amount-cell').text(formatNum(qty * price));
        recalcTotals();
    });

    // Event: remove
    row.find('.remove-btn').on('click', function () {
        var rowIdx = $(this).data('idx');
        lineItems = lineItems.filter(function (i) { return i._idx !== rowIdx; });
        row.remove();
        reNumberRows();
        recalcTotals();
    });

    $('#lineBody').append(row);
    recalcTotals();
}

function getItem(idx) {
    return lineItems.find(function (i) { return i._idx === idx; });
}

function reNumberRows() {
    $('#lineBody tr').each(function (i) {
        $(this).find('.line-no').text(i + 1);
        lineItems.find(function (li) {
            return li._idx === parseInt($(this).data('idx'));
        });
    });
}

function recalcTotals() {
    var subtotal = lineItems.reduce(function (sum, item) {
        return sum + (item.quantity * item.unitPrice);
    }, 0);
    var vatPct = parseFloat($('#vatSelect').val()) || 0;
    var vat = Math.round(subtotal * vatPct / 100 * 100) / 100;
    var grand = Math.round((subtotal + vat) * 100) / 100;

    $('#displayTotal').text(formatNum(subtotal));
    $('#displayVat').text(formatNum(vat));
    $('#displayGrand').text(formatNum(grand));
    $('#amountWords').text('( ' + numberToWords(grand) + ' )');

    // Hidden fields
    $('#hiddenTotal').val(subtotal.toFixed(2));
    $('#hiddenVat').val(vat.toFixed(2));
    $('#hiddenGrand').val(grand.toFixed(2));
    $('#hiddenGrandText').val(numberToWords(grand));
}

/* ── Helpers ────────────────────────────────────────────── */
function formatNum(n) {
    return parseFloat(n || 0).toLocaleString('en-US', {
        minimumFractionDigits: 2, maximumFractionDigits: 2
    });
}

function escHtml(str) {
    return String(str || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function numberToWords(amount) {
    var baht = Math.floor(amount);
    var satang = Math.round((amount - baht) * 100);
    var words = convertToWords(baht) + ' Baht';
    if (satang > 0) words += ' and ' + convertToWords(satang) + ' Satang';
    return words + ' Only';
}

function convertToWords(n) {
    if (n === 0) return 'Zero';
    var ones = ['','One','Two','Three','Four','Five','Six','Seven','Eight','Nine',
                'Ten','Eleven','Twelve','Thirteen','Fourteen','Fifteen','Sixteen',
                'Seventeen','Eighteen','Nineteen'];
    var tens = ['','','Twenty','Thirty','Forty','Fifty','Sixty','Seventy','Eighty','Ninety'];
    if (n < 20)  return ones[n];
    if (n < 100) return tens[Math.floor(n/10)] + (n%10 ? ' ' + ones[n%10] : '');
    if (n < 1000) return ones[Math.floor(n/100)] + ' Hundred' + (n%100 ? ' ' + convertToWords(n%100) : '');
    if (n < 1e6)  return convertToWords(Math.floor(n/1000)) + ' Thousand' + (n%1000 ? ' ' + convertToWords(n%1000) : '');
    if (n < 1e9)  return convertToWords(Math.floor(n/1e6)) + ' Million' + (n%1e6 ? ' ' + convertToWords(n%1e6) : '');
    return convertToWords(Math.floor(n/1e9)) + ' Billion' + (n%1e9 ? ' ' + convertToWords(n%1e9) : '');
}
