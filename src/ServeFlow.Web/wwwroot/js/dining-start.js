(async () => {
    const message = document.getElementById('dining-message');
    const token = location.hash.slice(1);
    history.replaceState(null, '', location.pathname);
    if (!/^[a-f0-9]{64}$/.test(token)) { message.textContent = 'Scan the QR code at your table to begin.'; return; }
    try {
        const response = await fetch('/api/dining/session', { method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': document.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ token }) });
        const result = await response.json();
        if (!response.ok) throw new Error(result.error || 'Could not open this table. Please try scanning again.');
        location.replace('/Dining');
    } catch (error) { message.textContent = error.message; }
})();
