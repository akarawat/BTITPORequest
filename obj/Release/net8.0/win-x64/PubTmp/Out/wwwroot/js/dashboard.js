/* ============================================================
   BERNINA IT PO Request — dashboard.js
   ============================================================ */

function initDashboardCharts(dailyData, statusData) {
    // ── Daily Amount Bar Chart ──────────────────────────────
    var dailyCtx = document.getElementById('dailyChart');
    if (dailyCtx && dailyData && dailyData.length > 0) {
        new Chart(dailyCtx, {
            type: 'bar',
            data: {
                labels: dailyData.map(function (d) { return d.date; }),
                datasets: [{
                    label: 'Amount (Baht)',
                    data: dailyData.map(function (d) { return d.amount; }),
                    backgroundColor: 'rgba(26, 86, 118, 0.7)',
                    borderColor: 'rgba(26, 86, 118, 1)',
                    borderWidth: 1,
                    borderRadius: 5
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                return ' ฿' + ctx.parsed.y.toLocaleString('th-TH', { minimumFractionDigits: 2 });
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (val) {
                                return '฿' + val.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    } else if (dailyCtx) {
        dailyCtx.parentElement.innerHTML =
            '<div class="text-center text-muted py-4"><i class="bi bi-bar-chart fs-3 d-block mb-2 opacity-25"></i>No data for this period</div>';
    }

    // ── Status Doughnut ─────────────────────────────────────
    var statusCtx = document.getElementById('statusChart');
    if (statusCtx && statusData && statusData.length > 0) {
        var colors = {
            'Draft': '#adb5bd',
            'Requested': '#0d6efd',
            'Issued': '#0dcaf0',
            'Pending Approval': '#ffc107',
            'Authorized': '#6610f2',
            'Completed': '#198754',
            'Rejected': '#dc3545'
        };
        new Chart(statusCtx, {
            type: 'doughnut',
            data: {
                labels: statusData.map(function (d) { return d.status; }),
                datasets: [{
                    data: statusData.map(function (d) { return d.count; }),
                    backgroundColor: statusData.map(function (d) {
                        return colors[d.status] || '#6c757d';
                    }),
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                cutout: '65%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { font: { size: 11 }, padding: 10 }
                    }
                }
            }
        });
    } else if (statusCtx) {
        statusCtx.parentElement.innerHTML =
            '<div class="text-center text-muted py-4"><i class="bi bi-pie-chart fs-3 d-block mb-2 opacity-25"></i>No data</div>';
    }
}
