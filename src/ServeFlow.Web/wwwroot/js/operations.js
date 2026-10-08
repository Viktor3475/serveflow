(() => {
    const target = document.getElementById('order-queue'); const message = document.getElementById('queue-message');
    const names = ['Pending', 'Accepted', 'Preparing', 'Ready', 'Served', 'Completed', 'Cancelled'];
    const labels = ['Pending', 'Accept', 'Start preparing', 'Mark ready', 'Mark served', 'Complete', 'Cancel'];
    const element = (tag, text, cls) => { const node = document.createElement(tag); if (text !== undefined) node.textContent = text; if (cls) node.className = cls; return node; };
    let changing = false;
    async function refresh() {
        if (changing) return;
        try {
            const response = await fetch('/api/operations/queue', { credentials: 'same-origin' });
            if (response.redirected) throw new Error('Sign in again to refresh the queue.');
            const queue = await response.json(); if (!response.ok) throw new Error('You no longer have access to these orders.');
            target.replaceChildren(); document.getElementById('queue-count').textContent = `${queue.totalCount} active orders${queue.totalCount > 100 ? ' · showing the oldest 100' : ''}`;
            for (const order of queue.orders) {
                const col = element('div', undefined, 'col-md-6 col-xl-4'); const card = element('article', undefined, 'feature-card h-100');
                card.append(element('span', names[order.status], `badge order-status order-status-${order.status}`), element('h2', `Table ${order.tableNumber}`, 'h4 mt-3'), element('p', `${order.number} · ${new Date(order.createdAt).toLocaleTimeString()}`, 'small text-body-secondary'));
                const lines = element('ul', undefined, 'list-unstyled'); for (const line of order.lines) { const row = element('li', `${line.quantity} × ${line.name}`, 'mb-3 fw-semibold');
                    for (const option of line.options) row.append(element('div', `+ ${option.name}`, 'small fw-normal'));
                    if (line.instructions) row.append(element('div', line.instructions, 'small text-body-secondary fw-normal')); lines.append(row); }
                card.append(lines); if (order.notes) card.append(element('p', order.notes, 'order-note'));
                const actions = element('div', undefined, 'd-flex gap-2 flex-wrap');
                for (const status of order.actions) { const button = element('button', labels[status], status === 6 ? 'btn btn-outline-danger btn-sm' : 'btn btn-success btn-sm'); button.type = 'button'; button.addEventListener('click', () => change(order, status)); actions.append(button); }
                if (!order.actions.length) actions.append(element('p', 'Waiting for the next staff action.', 'small text-body-secondary'));
                card.append(actions); col.append(card); target.append(col);
            }
            if (!queue.orders.length) target.append(element('p', 'No active orders. New orders will appear automatically.', 'text-body-secondary'));
        } catch (error) { target.replaceChildren(); message.textContent = error.message; }
    }
    async function change(order, status) {
        if (changing) return;
        let reason = null;
        if (status === 6) { reason = window.prompt('Why is this order being cancelled? (Required, up to 500 characters)'); if (!reason || !reason.trim()) return; if (reason.length > 500) { message.textContent = 'Cancellation reasons must be at most 500 characters.'; return; } }
        changing = true; message.textContent = ''; target.querySelectorAll('button').forEach(x => x.disabled = true);
        try {
            const response = await fetch(`/api/operations/orders/${order.id}/status`, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': document.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ status, version: order.version, reason }) });
            if (response.redirected) throw new Error('Sign in again before changing an order.');
            const result = await response.json(); if (!response.ok) throw new Error(result.error || 'The order could not be changed. Refresh its current status.');
        } catch (error) { message.textContent = error.message; }
        finally { changing = false; await refresh(); }
    }
    const synchronize = window.serveflowLive(connection => connection.invoke('SubscribeStaff'), refresh);
    document.getElementById('refresh-queue').addEventListener('click', synchronize);
})();
