(() => {
    const source = document.getElementById('dashboard-data'); if (!source || typeof Chart === 'undefined') return;
    const data = JSON.parse(source.dataset.chart); const charts = [];
    const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const colors = () => { const style = getComputedStyle(document.documentElement); return { text: style.getPropertyValue('--bs-secondary-color'), grid: style.getPropertyValue('--bs-border-color') }; };
    const palette = colors();
    charts.push(new Chart(document.getElementById('revenue-chart'), { type: 'bar', data: { labels: data.days.map(x => x.date), datasets: [
        { label: `Collected (${data.currency})`, data: data.days.map(x => x.revenue), backgroundColor: '#408267', borderRadius: 5, yAxisID: 'money' },
        { type: 'line', label: 'Orders placed', data: data.days.map(x => x.orders), borderColor: '#bc8b40', backgroundColor: '#bc8b40', tension: .25, pointRadius: data.days.length > 31 ? 0 : 3, yAxisID: 'orders' }
    ] }, options: { responsive: true, maintainAspectRatio: false, animation: reduced ? false : { duration: 400 }, plugins: { legend: { labels: { color: palette.text, usePointStyle: true, boxWidth: 8 } }, tooltip: { callbacks: { label: context => context.dataset.yAxisID === 'money' ? `${context.dataset.label}: ${Number(context.raw).toFixed(2)}` : `${context.dataset.label}: ${context.raw}` } } }, scales: {
        x: { grid: { display: false }, ticks: { color: palette.text, maxTicksLimit: 8 } }, money: { beginAtZero: true, position: 'left', grid: { color: palette.grid }, ticks: { color: palette.text } }, orders: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, ticks: { color: palette.text, precision: 0 } }
    } } }));
    const popular = document.getElementById('popular-chart'); if (popular) charts.push(new Chart(popular, { type: 'bar', data: { labels: data.popular.map(x => x.name), datasets: [{ label: 'Units ordered', data: data.popular.map(x => x.units), backgroundColor: '#408267', borderRadius: 5 }] }, options: { indexAxis: 'y', responsive: true, maintainAspectRatio: false, animation: reduced ? false : { duration: 400 }, plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true, ticks: { color: palette.text, precision: 0 }, grid: { color: palette.grid } }, y: { ticks: { color: palette.text }, grid: { display: false } } } } }));
    document.addEventListener('serveflow:theme', () => { const next = colors(); for (const chart of charts) { for (const scale of Object.values(chart.options.scales)) { scale.ticks.color = next.text; if (scale.grid && scale.grid.display !== false) scale.grid.color = next.grid; } if (chart.options.plugins.legend.labels) chart.options.plugins.legend.labels.color = next.text; chart.update('none'); } });
    window.addEventListener('pagehide', () => charts.forEach(chart => chart.destroy()), { once: true });
})();
