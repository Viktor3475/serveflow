(() => {
    const app = document.getElementById('receipt-app'); const id = app.dataset.orderId; const storageKey = `serveflow-receipt:${id}`;
    const token = location.hash.slice(1) || sessionStorage.getItem(storageKey);
    if (/^[a-f0-9]{64}$/.test(token || '')) sessionStorage.setItem(storageKey, token);
    // The private receipt capability remains in the fragment so the link can be saved/shared deliberately; fragments never reach HTTP access logs.
    const message = document.getElementById('receipt-message');
    async function refresh() {
        try {
            if (!/^[a-f0-9]{64}$/.test(token || '')) throw new Error('Open your original private receipt link.');
            const response = await fetch(`/api/dining/receipts/${id}`, { headers: { 'X-Receipt-Token': token }, credentials: 'same-origin' });
            const receipt = await response.json(); if (!response.ok) throw new Error(receipt.error || 'Could not open receipt.');
            const money = value => `${Number(value).toFixed(2)} ${receipt.currencyCode}`;
            document.getElementById('receipt-number').textContent = receipt.number;
            document.getElementById('receipt-table').textContent = `${receipt.restaurantName} · Table ${receipt.tableNumber} · ${new Date(receipt.createdAt).toLocaleString()}`;
            document.getElementById('receipt-status').textContent = ['Pending', 'Accepted', 'Preparing', 'Ready', 'Served', 'Completed', 'Cancelled'][receipt.status] || receipt.status;
            const lines = document.getElementById('receipt-lines'); lines.replaceChildren();
            for (const line of receipt.lines) { const row = document.createElement('li'); row.className = 'border-bottom py-3'; row.textContent = `${line.quantity} × ${line.name} — ${money((line.price + line.options.reduce((s, x) => s + x.price, 0)) * line.quantity)}`;
                for (const option of line.options) { const detail = document.createElement('div'); detail.className = 'small text-body-secondary'; detail.textContent = `+ ${option.name} (${money(option.price)} per item)`; row.append(detail); }
                if (line.instructions) { const detail = document.createElement('div'); detail.textContent = line.instructions; row.append(detail); } lines.append(row); }
            document.getElementById('receipt-notes').textContent = receipt.notes || ''; document.getElementById('receipt-total').textContent = `Total ${money(receipt.total)}`; message.textContent = '';
        } catch (error) { message.textContent = error.message; }
    }
    const synchronize = window.serveflowLive(connection => connection.invoke('SubscribeReceipt', id, token), refresh);
    document.getElementById('refresh-receipt').addEventListener('click', synchronize);
})();
