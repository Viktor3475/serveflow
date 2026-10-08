window.serveflowLive = function (subscribe, refresh) {
    const indicator = document.getElementById('live-state');
    let stopped = false, starting = false, refreshing = false, again = false;
    if (typeof signalR === 'undefined') {
        indicator.textContent = 'Live transport unavailable · refreshing every 15 seconds';
        const fallback = setInterval(() => { if (!stopped && document.visibilityState === 'visible') refresh(); }, 15000);
        window.addEventListener('pagehide', () => { stopped = true; clearInterval(fallback); }, { once: true });
        refresh(); return refresh;
    }
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/orders').withAutomaticReconnect([0, 2000, 10000, 30000]).configureLogging(signalR.LogLevel.None).build();
    async function synchronize() {
        if (refreshing) { again = true; return; }
        refreshing = true;
        try { do { again = false; await refresh(); } while (again && !stopped); }
        finally { refreshing = false; }
    }
    async function joined() { await subscribe(connection); indicator.textContent = 'Live updates connected'; await synchronize(); }
    async function start() {
        if (starting || stopped || connection.state !== signalR.HubConnectionState.Disconnected) return;
        starting = true;
        try { await connection.start(); await joined(); }
        catch { indicator.textContent = 'Live connection unavailable · refreshing every 15 seconds'; await connection.stop(); }
        finally { starting = false; }
    }
    connection.on('OrdersChanged', synchronize);
    connection.onreconnecting(() => { indicator.textContent = 'Reconnecting · automatic refresh remains active'; });
    connection.onreconnected(async () => { try { await joined(); } catch { await connection.stop(); } });
    connection.onclose(() => { if (!stopped) indicator.textContent = 'Disconnected · retrying and refreshing automatically'; });
    const polling = setInterval(() => { if (!stopped && document.visibilityState === 'visible') { synchronize(); start(); } }, 15000);
    const renew = setInterval(async () => { if (!stopped && connection.state === signalR.HubConnectionState.Connected) { try { await connection.invoke('Renew'); } catch { await connection.stop(); } } }, 30000);
    const visible = () => { if (document.visibilityState === 'visible') { synchronize(); start(); } };
    document.addEventListener('visibilitychange', visible);
    window.addEventListener('pagehide', () => { stopped = true; clearInterval(polling); clearInterval(renew); document.removeEventListener('visibilitychange', visible); connection.stop(); }, { once: true });
    synchronize(); start();
    return synchronize;
};
