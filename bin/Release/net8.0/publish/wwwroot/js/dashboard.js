/* ============================================================
   BERNINA IT PO Request — dashboard.js
   ============================================================ */

function initDashboardCharts(dailyData, monthlyData, chartYear) {

    // ── Shared colors + legend helpers ─────────────────────
    var colorIT = 'rgba(26, 86, 118, 0.75)';
    var colorOS = 'rgba(186, 117, 23, 0.75)';
    var legendBase = { position: 'top', labels: { font: { size: 11 }, padding: 10, boxWidth: 12 } };

    function makeGenerateLabels(formatFn) {
        return function (chart) {
            var items = Chart.defaults.plugins.legend.labels.generateLabels(chart);
            items.forEach(function (item) {
                var ds = chart.data.datasets[item.datasetIndex];
                var total = ds.data.reduce(function (a, b) { return a + b; }, 0);
                item.text = ds.label + '  =  ' + formatFn(total);
            });
            return items;
        };
    }

    // ── Daily Amount Stacked Bar Chart (IT vs OS) ──────────
    var dailyCtx = document.getElementById('dailyChart');
    if (dailyCtx && dailyData && dailyData.length > 0) {
        // รวบรวม dates ที่ unique แล้ว sort
        var dateSet = {};
        dailyData.forEach(function (d) { dateSet[d.Date] = true; });
        var dailyDates = Object.keys(dateSet).sort();

        // สร้าง map date→amount แยก IT/OS
        var dailyIT = {}, dailyOS = {};
        dailyDates.forEach(function (dt) { dailyIT[dt] = 0; dailyOS[dt] = 0; });
        dailyData.forEach(function (d) {
            if (d.DeptPrefix === 'OS') dailyOS[d.Date] = (dailyOS[d.Date] || 0) + d.Amount;
            else                       dailyIT[d.Date] = (dailyIT[d.Date] || 0) + d.Amount;
        });

        var dailyITArr = dailyDates.map(function (dt) { return dailyIT[dt]; });
        var dailyOSArr = dailyDates.map(function (dt) { return dailyOS[dt]; });
        var dailyITTotal = dailyITArr.reduce(function (a, b) { return a + b; }, 0);
        var dailyOSTotal = dailyOSArr.reduce(function (a, b) { return a + b; }, 0);

        new Chart(dailyCtx, {
            type: 'bar',
            data: {
                labels: dailyDates,
                datasets: [
                    { label: 'IT',                data: dailyITArr, backgroundColor: colorIT, borderRadius: 4 },
                    { label: 'Office Stationery', data: dailyOSArr, backgroundColor: colorOS, borderRadius: 4 }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: 'top',
                        labels: Object.assign({}, legendBase.labels, {
                            generateLabels: makeGenerateLabels(function (n) {
                                return '฿' + Math.round(n).toLocaleString('th-TH');
                            })
                        })
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function (ctx) {
                                return ' ' + ctx.dataset.label + ': ฿' +
                                    Math.round(ctx.parsed.y).toLocaleString('th-TH');
                            },
                            footer: function (items) {
                                var total = items.reduce(function (s, i) { return s + i.parsed.y; }, 0);
                                return 'Total: ฿' + Math.round(total).toLocaleString('th-TH');
                            }
                        }
                    }
                },
                scales: {
                    x: { stacked: true, ticks: { font: { size: 10 } } },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        ticks: {
                            callback: function (val) {
                                if (val >= 1000000) return '฿' + (val / 1000000).toFixed(1) + 'M';
                                if (val >= 1000)    return '฿' + (val / 1000).toFixed(0) + 'k';
                                return '฿' + val;
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

    // ── Monthly Stacked Bar Charts ──────────────────────────
    var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
                  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

    var itCount   = new Array(12).fill(0);
    var osCount   = new Array(12).fill(0);
    var itAmount  = new Array(12).fill(0);
    var osAmount  = new Array(12).fill(0);

    if (monthlyData && monthlyData.length > 0) {
        monthlyData.forEach(function (d) {
            var idx = d.Month - 1;
            if (idx < 0 || idx > 11) return;
            if (d.DeptPrefix === 'IT') {
                itCount[idx]  = d.Count;
                itAmount[idx] = d.Amount;
            } else if (d.DeptPrefix === 'OS') {
                osCount[idx]  = d.Count;
                osAmount[idx] = d.Amount;
            }
        });
    }

    // คำนวณผลรวมทั้งปีสำหรับ legend
    var itCountTotal  = itCount.reduce(function (a, b) { return a + b; }, 0);
    var osCountTotal  = osCount.reduce(function (a, b) { return a + b; }, 0);
    var itAmountTotal = itAmount.reduce(function (a, b) { return a + b; }, 0);
    var osAmountTotal = osAmount.reduce(function (a, b) { return a + b; }, 0);

    // ── Chart 1: Monthly PO Count ──────────────────────────
    var countCtx = document.getElementById('monthlyCountChart');
    if (countCtx) {
        new Chart(countCtx, {
            type: 'bar',
            data: {
                labels: months,
                datasets: [
                    { label: 'IT',                data: itCount,  backgroundColor: colorIT, borderRadius: 3 },
                    { label: 'Office Stationery', data: osCount,  backgroundColor: colorOS, borderRadius: 3 }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: legendBase.position,
                        labels: Object.assign({}, legendBase.labels, {
                            generateLabels: makeGenerateLabels(function (n) { return n + ' POs'; })
                        })
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function (ctx) { return ' ' + ctx.dataset.label + ': ' + ctx.parsed.y + ' POs'; },
                            footer: function (items) {
                                var total = items.reduce(function (s, i) { return s + i.parsed.y; }, 0);
                                return 'Total: ' + total + ' POs';
                            }
                        }
                    }
                },
                scales: {
                    x: { stacked: true, ticks: { font: { size: 10 } } },
                    y: {
                        stacked: true, beginAtZero: true,
                        ticks: {
                            stepSize: 1, font: { size: 10 },
                            callback: function (val) { return Number.isInteger(val) ? val : ''; }
                        }
                    }
                }
            }
        });
    }

    // ── Chart 2: Monthly PO Amount ─────────────────────────
    var amountCtx = document.getElementById('monthlyAmountChart');
    if (amountCtx) {
        new Chart(amountCtx, {
            type: 'bar',
            data: {
                labels: months,
                datasets: [
                    { label: 'IT',                data: itAmount,  backgroundColor: colorIT, borderRadius: 3 },
                    { label: 'Office Stationery', data: osAmount,  backgroundColor: colorOS, borderRadius: 3 }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: legendBase.position,
                        labels: Object.assign({}, legendBase.labels, {
                            generateLabels: makeGenerateLabels(function (n) {
                                return '฿' + Math.round(n).toLocaleString('th-TH');
                            })
                        })
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        callbacks: {
                            label: function (ctx) {
                                return ' ' + ctx.dataset.label + ': ฿' +
                                    Math.round(ctx.parsed.y).toLocaleString('th-TH');
                            },
                            footer: function (items) {
                                var total = items.reduce(function (s, i) { return s + i.parsed.y; }, 0);
                                return 'Total: ฿' + Math.round(total).toLocaleString('th-TH');
                            }
                        }
                    }
                },
                scales: {
                    x: { stacked: true, ticks: { font: { size: 10 } } },
                    y: {
                        stacked: true, beginAtZero: true,
                        ticks: {
                            font: { size: 10 },
                            callback: function (val) {
                                if (val >= 1000000) return '฿' + (val / 1000000).toFixed(1) + 'M';
                                if (val >= 1000)    return '฿' + (val / 1000).toFixed(0) + 'k';
                                return '฿' + val;
                            }
                        }
                    }
                }
            }
        });
    }
}
