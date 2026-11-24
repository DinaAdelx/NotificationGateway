// Multi-Tenant Notification Gateway — Live Multi-Provider Test Client
// SignalR + Web Audio API + Multi-Channel Delivery Tracking + Webhook Inspector

class NotificationGatewayApp {
    constructor() {
        this.tenantId = '11111111-1111-1111-1111-111111111111';
        this.applicationId = '22222222-2222-2222-2222-222222222222';
        this.userId = 'cust-500';

        this.hubConnection = null;
        this.unreadCount = 0;
        this.signalrCount = 0;
        this.audioCtx = null;
        this.soundEnabled = true;
        this.lastNotificationId = null;

        this.init();
    }

    async init() {
        this.bindUiEvents();
        await this.startSignalR();
        this.pollDeliveries();
    }

    // Pure Browser Web Audio API Synthesizer (Zero External MP3/Audio File Dependencies)
    playChime(isUrgent = false) {
        if (!this.soundEnabled) return;

        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!this.audioCtx) {
                this.audioCtx = new AudioContext();
            }

            if (this.audioCtx.state === 'suspended') {
                this.audioCtx.resume();
            }

            const now = this.audioCtx.currentTime;

            if (isUrgent) {
                // Urgent Three-tone chord: 523.25Hz (C5), 659.25Hz (E5), 783.99Hz (G5)
                [523.25, 659.25, 783.99].forEach((freq, idx) => {
                    const osc = this.audioCtx.createOscillator();
                    const gain = this.audioCtx.createGain();
                    osc.type = 'triangle';
                    osc.frequency.setValueAtTime(freq, now + idx * 0.08);

                    gain.gain.setValueAtTime(0.15, now + idx * 0.08);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.08 + 0.4);

                    osc.connect(gain);
                    gain.connect(this.audioCtx.destination);

                    osc.start(now + idx * 0.08);
                    osc.stop(now + idx * 0.08 + 0.4);
                });
            } else {
                // Gentle Two-tone chime: 587.33Hz (D5) -> 880.00Hz (A5)
                [587.33, 880.00].forEach((freq, idx) => {
                    const osc = this.audioCtx.createOscillator();
                    const gain = this.audioCtx.createGain();
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, now + idx * 0.12);

                    gain.gain.setValueAtTime(0.2, now + idx * 0.12);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.12 + 0.35);

                    osc.connect(gain);
                    gain.connect(this.audioCtx.destination);

                    osc.start(now + idx * 0.12);
                    osc.stop(now + idx * 0.12 + 0.35);
                });
            }
        } catch (e) {
            console.warn('Audio playback not permitted or unavailable:', e);
        }
    }

    // Connect to ASP.NET Core SignalR Notification Hub
    async startSignalR() {
        this.updateConnectionStatus('connecting');

        if (this.hubConnection) {
            try { await this.hubConnection.stop(); } catch (e) {}
        }

        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Wire Inbound Notification Handlers
        this.hubConnection.on('OnNotificationReceived', (notif) => this.handleIncomingNotification(notif));
        this.hubConnection.on('ReceiveNotification', (notif) => this.handleIncomingNotification(notif));

        this.hubConnection.onreconnecting(() => this.updateConnectionStatus('connecting'));
        this.hubConnection.onreconnected(async () => {
            this.updateConnectionStatus('connected');
            await this.joinUserGroup();
        });
        this.hubConnection.onclose(() => this.updateConnectionStatus('disconnected'));

        try {
            await this.hubConnection.start();
            this.updateConnectionStatus('connected');
            await this.joinUserGroup();
        } catch (err) {
            console.error('SignalR start error:', err);
            this.updateConnectionStatus('disconnected');
        }
    }

    async joinUserGroup() {
        if (!this.hubConnection) return;
        try {
            await this.hubConnection.invoke('JoinTenantGroup', this.tenantId, this.userId);
            console.log(`Joined group for tenant ${this.tenantId} / user ${this.userId}`);
        } catch (err) {
            console.warn('Join group invocation failed:', err);
        }
    }

    updateConnectionStatus(status) {
        const dot = document.getElementById('statusIndicatorDot');
        const text = document.getElementById('connectionStatusText');
        const badge = document.getElementById('connectionBadge');
        const btnToggle = document.getElementById('toggleConnText');

        dot.className = `status-dot ${status} me-2`;

        if (status === 'connected') {
            text.textContent = 'SignalR Connected';
            badge.className = 'badge bg-success-subtle text-success border border-success d-flex align-items-center py-2 px-3';
            btnToggle.textContent = 'Disconnect';
        } else if (status === 'connecting') {
            text.textContent = 'Connecting...';
            badge.className = 'badge bg-warning-subtle text-warning border border-warning d-flex align-items-center py-2 px-3';
            btnToggle.textContent = 'Connecting';
        } else {
            text.textContent = 'Disconnected';
            badge.className = 'badge bg-danger-subtle text-danger border border-danger d-flex align-items-center py-2 px-3';
            btnToggle.textContent = 'Connect';
        }
    }

    handleIncomingNotification(notif) {
        console.log('Incoming SignalR Notification:', notif);

        // Deduplicate cards if both OnNotificationReceived & ReceiveNotification fire
        const existingCard = document.getElementById(`sig_card_${notif.id || notif.deliveryId}`);
        if (existingCard) return;

        this.playChime(false);
        this.unreadCount++;
        this.signalrCount++;
        this.updateBadges();

        const empty = document.getElementById('signalrEmpty');
        if (empty) empty.style.display = 'none';

        const list = document.getElementById('signalrList');
        const card = document.createElement('div');
        const cardId = `sig_card_${notif.id || notif.deliveryId || Date.now()}`;
        card.id = cardId;
        card.className = 'notification-item type-success unread';

        const timeStr = new Date(notif.timestamp || Date.now()).toLocaleTimeString();
        const title = notif.title || notif.subject || 'Gateway Alert';
        const body = notif.message || notif.body || '';

        card.innerHTML = `
            <div class="d-flex justify-content-between align-items-start">
                <div>
                    <span class="badge badge-signalr me-1">SignalR</span>
                    <strong class="text-dark">${this.escapeHtml(title)}</strong>
                </div>
                <small class="text-muted font-monospace">${timeStr}</small>
            </div>
            <div class="mt-2 text-secondary small">${body}</div>
            <div class="mt-2 d-flex justify-content-between align-items-center small text-muted">
                <span>To: <span class="font-monospace">${this.escapeHtml(notif.recipientAddress || this.userId)}</span></span>
                <button class="btn btn-sm btn-link p-0 text-decoration-none btn-mark-read">
                    <i class="bi bi-check2"></i> Mark Read
                </button>
            </div>
        `;

        list.insertBefore(card, list.firstChild);

        card.querySelector('.btn-mark-read').addEventListener('click', () => {
            card.classList.remove('unread');
            card.querySelector('.btn-mark-read').style.display = 'none';
            if (this.unreadCount > 0) {
                this.unreadCount--;
                this.updateBadges();
            }
        });
    }

    updateBadges() {
        const badge = document.getElementById('unreadCountBadge');
        if (this.unreadCount > 0) {
            badge.textContent = this.unreadCount;
            badge.classList.remove('d-none');
        } else {
            badge.classList.add('d-none');
        }
        document.getElementById('signalrCount').textContent = this.signalrCount;
    }

    // Submit notification to API
    async sendNotification() {
        const btn = document.getElementById('btnSendNotification');
        const btnText = document.getElementById('btnSendText');
        const alertBox = document.getElementById('sendResultAlert');

        // Collect selected channels
        const checkedChannels = Array.from(document.querySelectorAll('.channel-chk:checked'))
            .map(cb => cb.value);

        if (checkedChannels.length === 0) {
            alert('Please select at least one channel / provider!');
            return;
        }

        // Parse Payload
        let payloadObj;
        try {
            payloadObj = JSON.parse(document.getElementById('payloadJsonInput').value);
        } catch (e) {
            alert('Payload must be valid JSON: ' + e.message);
            return;
        }

        const notifType = document.getElementById('notifTypeInput').value.trim() || 'ORDER_SHIPPED';
        const recipientUserId = document.getElementById('recipientUserId').value.trim() || 'cust-500';
        const recipientEmail = document.getElementById('recipientEmail').value.trim() || 'customer@example.com';
        const recipientPhone = document.getElementById('recipientPhone').value.trim() || '+15551234567';
        const recipientWebhook = document.getElementById('recipientWebhook').value.trim() || 'http://localhost:5022/api/webhooks/receive-test';

        const requestBody = {
            type: notifType,
            channels: checkedChannels,
            language: "en",
            priority: 1, // Normal
            recipient: {
                externalUserId: recipientUserId,
                email: recipientEmail,
                phone: recipientPhone,
                webhookUrl: recipientWebhook
            },
            payload: payloadObj
        };

        btn.disabled = true;
        btnText.textContent = 'Dispatching to Providers...';

        try {
            const resp = await fetch('/api/v1/notifications', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Tenant-Id': this.tenantId,
                    'X-Application-Id': this.applicationId
                },
                body: JSON.stringify(requestBody)
            });

            if (!resp.ok) {
                const errText = await resp.text();
                throw new Error(`API Error (${resp.status}): ${errText}`);
            }

            const data = await resp.json();
            this.lastNotificationId = data.notificationId;

            document.getElementById('lastNotificationId').textContent = data.notificationId;
            alertBox.classList.remove('d-none');

            // Switch to or refresh Delivery Tracker tab
            await this.refreshDeliveries();
            await this.refreshWebhooks();

        } catch (err) {
            console.error('Send error:', err);
            alert('Failed to send notification: ' + err.message);
        } finally {
            btn.disabled = false;
            btnText.textContent = 'Send Notification Across Channels';
        }
    }

    async refreshDeliveries() {
        if (!this.lastNotificationId) return;

        try {
            const resp = await fetch(`/api/v1/notifications/${this.lastNotificationId}/deliveries`, {
                headers: {
                    'X-Tenant-Id': this.tenantId,
                    'X-Application-Id': this.applicationId
                }
            });

            if (!resp.ok) return;

            const deliveries = await resp.json();
            document.getElementById('deliveryCount').textContent = deliveries.length;

            const tbody = document.getElementById('deliveriesTableBody');
            if (deliveries.length === 0) {
                tbody.innerHTML = `<tr><td colspan="6" class="text-center text-muted py-4">No deliveries created yet.</td></tr>`;
                return;
            }

            const channelBadge = (ch) => {
                const map = { 0: 'Email', 1: 'Sms', 2: 'WhatsApp', 3: 'Push', 4: 'InApp', 5: 'SignalR', 6: 'Voice', 7: 'Webhook' };
                const name = typeof ch === 'number' ? map[ch] || ch : ch;
                const cls = `badge-${String(name).toLowerCase()}`;
                return `<span class="badge ${cls}">${name}</span>`;
            };

            const statusBadge = (st) => {
                const map = { 0: 'Pending', 1: 'Queued', 2: 'Processing', 3: 'Sent', 4: 'Delivered', 5: 'Retrying', 6: 'Failed', 7: 'DeadLettered' };
                const name = typeof st === 'number' ? map[st] || st : st;
                let bg = 'bg-secondary';
                if (name === 'Sent' || name === 'Delivered') bg = 'bg-success';
                else if (name === 'Processing' || name === 'Queued') bg = 'bg-primary';
                else if (name === 'Retrying') bg = 'bg-warning text-dark';
                else if (name === 'Failed' || name === 'DeadLettered') bg = 'bg-danger';
                return `<span class="badge ${bg}">${name}</span>`;
            };

            tbody.innerHTML = deliveries.map(d => {
                const lastAttempt = d.attempts && d.attempts.length > 0 ? d.attempts[d.attempts.length - 1] : null;
                const providerMsgId = lastAttempt?.providerMessageId || '-';
                return `
                    <tr>
                        <td>${channelBadge(d.channel)}</td>
                        <td class="font-monospace small text-truncate" style="max-width: 160px;" title="${this.escapeHtml(d.recipientAddress || '')}">
                            ${this.escapeHtml(d.recipientAddress || '-')}
                        </td>
                        <td>${statusBadge(d.status)}</td>
                        <td><span class="badge bg-light text-dark border">${d.attemptCount}</span></td>
                        <td class="font-monospace small text-truncate" style="max-width: 180px;">${this.escapeHtml(providerMsgId)}</td>
                        <td class="small text-muted">${new Date(d.createdAt).toLocaleTimeString()}</td>
                    </tr>
                `;
            }).join('');

        } catch (e) {
            console.warn('Delivery status refresh error:', e);
        }
    }

    async refreshWebhooks() {
        try {
            const resp = await fetch('/api/webhooks/received');
            if (!resp.ok) return;

            const events = await resp.json();
            document.getElementById('webhookCount').textContent = events.length;

            const empty = document.getElementById('webhooksEmpty');
            const list = document.getElementById('webhooksList');

            if (events.length === 0) {
                if (empty) empty.style.display = 'block';
                return;
            }
            if (empty) empty.style.display = 'none';

            list.innerHTML = events.map(e => `
                <div class="notification-item">
                    <div class="d-flex justify-content-between align-items-start">
                        <div>
                            <span class="badge badge-webhook me-1">Webhook</span>
                            <span class="badge ${e.isSignatureValid ? 'bg-success' : 'bg-danger'} me-1">
                                ${e.isSignatureValid ? 'HMAC-SHA256 Valid' : 'Invalid Signature'}
                            </span>
                            <strong class="text-dark">Event ID: ${this.escapeHtml(e.eventId)}</strong>
                        </div>
                        <small class="text-muted font-monospace">${new Date(e.receivedAt).toLocaleTimeString()}</small>
                    </div>
                    <div class="mt-2">
                        <small class="text-muted d-block">Signature: <code>${this.escapeHtml(e.signatureHeader || 'none')}</code></small>
                    </div>
                    <div class="mt-2">
                        <pre class="json-view mb-0">${this.escapeHtml(JSON.stringify(e.payload, null, 2))}</pre>
                    </div>
                </div>
            `).join('');

        } catch (e) {
            console.warn('Webhook history refresh error:', e);
        }
    }

    pollDeliveries() {
        // Poll every 3 seconds if active
        setInterval(() => {
            if (this.lastNotificationId) {
                this.refreshDeliveries();
            }
            this.refreshWebhooks();
        }, 3000);
    }

    bindUiEvents() {
        document.getElementById('btnSendNotification').addEventListener('click', () => this.sendNotification());

        document.getElementById('btnToggleConn').addEventListener('click', async () => {
            if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
                await this.hubConnection.stop();
                this.updateConnectionStatus('disconnected');
            } else {
                await this.startSignalR();
            }
        });

        document.getElementById('soundToggle').addEventListener('change', (e) => {
            this.soundEnabled = e.target.checked;
        });

        document.getElementById('btnClearSignalR').addEventListener('click', () => {
            const list = document.getElementById('signalrList');
            list.innerHTML = `
                <div class="text-center text-muted py-5" id="signalrEmpty">
                    <i class="bi bi-bell-slash fs-1 d-block mb-2 text-secondary"></i>
                    Stream cleared. Ready for next incoming notification.
                </div>
            `;
            this.signalrCount = 0;
            this.unreadCount = 0;
            this.updateBadges();
        });

        document.getElementById('btnRefreshDeliveries').addEventListener('click', () => this.refreshDeliveries());
        document.getElementById('btnRefreshWebhooks').addEventListener('click', () => this.refreshWebhooks());

        document.getElementById('btnClearWebhooks').addEventListener('click', async () => {
            await fetch('/api/webhooks/clear', { method: 'POST' });
            await this.refreshWebhooks();
        });

        document.getElementById('btnResetPayload').addEventListener('click', () => {
            document.getElementById('payloadJsonInput').value = JSON.stringify({
                customerName: "Jane Doe",
                orderId: "ORD-" + Math.floor(10000 + Math.random() * 90000),
                carrier: "DHL Express",
                trackingUrl: "https://track.dhl.com/package"
            }, null, 2);
        });
    }

    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = String(text);
        return div.innerHTML;
    }
}

// Instantiate on DOM load
window.addEventListener('DOMContentLoaded', () => {
    window.app = new NotificationGatewayApp();
});
