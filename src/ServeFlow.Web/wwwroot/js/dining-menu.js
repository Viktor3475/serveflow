(() => {
    'use strict';
    const app = document.getElementById('dining-app');
    const message = document.getElementById('dining-message');
    const submit = document.getElementById('place-order');
    const notes = document.getElementById('order-notes');
    const search = document.getElementById('menu-search');
    const category = document.getElementById('menu-category');
    let menu, cart = [], pending = null, key, busy = false;
    const money = value => `${Number(value).toFixed(2)} ${menu.currencyCode}`;
    const node = (tag, text, className) => { const el = document.createElement(tag); if (text !== undefined) el.textContent = text; if (className) el.className = className; return el; };
    const randomToken = () => Array.from(crypto.getRandomValues(new Uint8Array(32)), b => b.toString(16).padStart(2, '0')).join('');
    const save = () => sessionStorage.setItem(key, JSON.stringify({ cart, notes: notes.value, pending }));
    const locked = () => busy || pending !== null;
    function renderMenu() {
        const target = document.getElementById('menu-items'); target.replaceChildren();
        const term = search.value.toLocaleLowerCase();
        for (const item of menu.items.filter(x => (!category.value || x.categoryId === category.value) && `${x.name} ${x.description}`.toLocaleLowerCase().includes(term))) {
            const column = node('div', undefined, 'col-md-6'); const card = node('article', undefined, 'feature-card h-100');
            if (item.hasImage) { const img = node('img', undefined, 'product-preview mb-3'); img.src = app.dataset.imageUrl + item.id; img.alt = item.name; img.loading = 'lazy'; card.append(img); }
            card.append(node('span', item.category, 'eyebrow'), node('h2', item.name, 'h4 mt-2'), node('p', item.description, 'text-body-secondary'), node('p', money(item.price), 'fw-semibold'));
            const controls = [];
            for (const option of item.options) { const label = node('label', undefined, 'd-block mb-2'); const checkbox = node('input'); checkbox.type = 'checkbox'; checkbox.className = 'form-check-input me-2'; checkbox.disabled = locked() || !item.available; label.append(checkbox, node('span', `${option.name} (+${money(option.price)})`)); card.append(label); controls.push({ checkbox, option }); }
            const label = node('label', 'Special instructions', 'form-label'); const instructions = node('input', undefined, 'form-control mb-3'); instructions.maxLength = 500; instructions.disabled = locked() || !item.available; label.append(instructions); card.append(label);
            const add = node('button', item.available ? 'Add to order' : 'Unavailable', 'btn btn-outline-success'); add.type = 'button'; add.disabled = locked() || !item.available;
            add.addEventListener('click', () => {
                if (cart.length >= 50 || cart.reduce((s, x) => s + x.quantity, 0) >= 100) { message.textContent = 'This order has reached its item limit.'; return; }
                cart.push({ itemId: item.id, version: item.version, quantity: 1, instructions: instructions.value,
                    options: controls.filter(x => x.checkbox.checked).map(x => ({ id: x.option.id, version: x.option.version })),
                    name: item.name, price: item.price, optionDetails: controls.filter(x => x.checkbox.checked).map(x => x.option) });
                save(); renderCart();
            }); card.append(add); column.append(card); target.append(column);
        }
        if (!target.childElementCount) target.append(node('p', 'No dishes match your search.'));
    }
    function renderCart() {
        const target = document.getElementById('cart-lines'); target.replaceChildren(); let cents = 0;
        cart.forEach((line, index) => {
            const row = node('div', undefined, 'border-bottom py-3'); row.append(node('strong', line.name));
            for (const option of line.optionDetails) row.append(node('div', `+ ${option.name}`, 'small text-body-secondary'));
            if (line.instructions) row.append(node('div', line.instructions, 'small text-body-secondary'));
            const label = node('label', 'Quantity ', 'd-block mt-2'); const quantity = node('input', undefined, 'form-control form-control-sm dining-quantity'); quantity.type = 'number'; quantity.min = '1'; quantity.max = '20'; quantity.value = line.quantity; quantity.disabled = locked();
            quantity.addEventListener('change', () => { const next = Math.max(1, Math.min(20, Number.parseInt(quantity.value, 10) || 1)); if (cart.reduce((s, x) => s + x.quantity, 0) - line.quantity + next > 100) { message.textContent = 'An order can contain at most 100 items.'; renderCart(); return; } line.quantity = next; save(); renderCart(); }); label.append(quantity); row.append(label);
            const remove = node('button', 'Remove', 'btn btn-link btn-sm px-0'); remove.type = 'button'; remove.disabled = locked(); remove.setAttribute('aria-label', `Remove ${line.name}`); remove.addEventListener('click', () => { cart.splice(index, 1); save(); renderCart(); }); row.append(remove);
            cents += (Math.round(line.price * 100) + line.optionDetails.reduce((s, x) => s + Math.round(x.price * 100), 0)) * line.quantity; target.append(row);
        });
        if (!cart.length) target.append(node('p', 'Choose a dish to start your order.', 'text-body-secondary'));
        document.getElementById('cart-total').textContent = money(cents / 100); notes.disabled = locked(); submit.disabled = busy || !cart.length; submit.textContent = pending ? 'Retry the same order' : 'Send order';
        document.getElementById('refresh-menu').disabled = locked();
    }
    async function openReceipt(id, token) { sessionStorage.removeItem(key); location.assign(`/Dining/Receipt/${id}#${token}`); }
    async function send() {
        if (busy || !cart.length) return;
        busy = true; message.textContent = '';
        try {
            if (!pending) pending = { idempotencyKey: crypto.randomUUID(), receiptToken: randomToken(), notes: notes.value,
                lines: cart.map(({ itemId, version, quantity, instructions, options }) => ({ itemId, version, quantity, instructions, options })) };
            save(); renderCart(); renderMenu();
            // Recover a committed order after a lost response, without creating a second order.
            const recovered = await fetch('/api/dining/receipts/recover', { headers: { 'X-Receipt-Token': pending.receiptToken }, credentials: 'same-origin' });
            if (recovered.ok) { const receipt = await recovered.json(); await openReceipt(receipt.id, pending.receiptToken); return; }
            if (recovered.status !== 404) throw new Error('Could not check the previous submission. Retry with the same order.');
            const response = await fetch(app.dataset.orderUrl, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': document.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify(pending) });
            if (response.redirected) throw new Error('Your staff session expired. Sign in again, then retry this order.');
            const result = await response.json();
            if (response.ok) { await openReceipt(result.id, pending.receiptToken); return; }
            if ([400, 403, 409, 429].includes(response.status)) { pending = null; save(); }
            throw new Error(result.error || 'Could not submit. Retry with the same order.');
        } catch (error) { message.textContent = error.message; }
        finally { busy = false; renderCart(); renderMenu(); }
    }
    async function load() {
        try {
            const response = await fetch(app.dataset.menuUrl, { credentials: 'same-origin' }); const result = await response.json(); if (!response.ok) throw new Error(result.error || 'Could not open the menu.'); menu = result;
            key = `serveflow-cart:${app.dataset.orderUrl}:${menu.tableId}`;
            const saved = JSON.parse(sessionStorage.getItem(key) || 'null'); if (saved) { cart = saved.cart; notes.value = saved.notes; pending = saved.pending; }
            document.getElementById('restaurant-name').textContent = menu.restaurantName; document.getElementById('table-label').textContent = `Table ${menu.tableNumber}`;
            category.replaceChildren(node('option', 'All categories')); category.firstChild.value = '';
            const categories = new Map(menu.items.map(x => [x.categoryId, x.category])); for (const [id, name] of categories) { const option = node('option', name); option.value = id; category.append(option); }
            // A refresh explicitly reviews current prices/options. Unavailable selections stay in the cart and will be rejected by the server.
            if (!pending) cart = cart.map(line => { const item = menu.items.find(x => x.id === line.itemId); if (!item) return line; return { ...line, name: item.name, price: item.price, version: item.version,
                options: line.options.map(o => { const current = item.options.find(x => x.id === o.id); return current ? { id: current.id, version: current.version } : o; }),
                optionDetails: line.optionDetails.map(o => item.options.find(x => x.id === o.id) || o) }; });
            save(); renderMenu(); renderCart();
            if (pending) message.textContent = 'A submission is awaiting confirmation. Retry the same order to recover its receipt safely.';
        } catch (error) { message.textContent = error.message; submit.disabled = true; }
    }
    search.addEventListener('input', () => { if (menu) renderMenu(); }); category.addEventListener('change', () => { if (menu) renderMenu(); }); notes.addEventListener('input', () => { if (key) save(); }); submit.addEventListener('click', send);
    document.getElementById('refresh-menu').addEventListener('click', () => { message.textContent = 'Review the refreshed prices before sending your order.'; load(); }); load();
})();
