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
        this.auth = this.loadAuth();

        this.init();
    }

    async init() {
        this.bindUiEvents();
        this.updateAuthUi();
        await this.startSignalR();
        this.pollDeliveries();
    }

    loadAuth() {
        try {
            const raw = localStorage.getItem('ng_auth');
            return raw ? JSON.parse(raw) : null;
        } catch {
            return null;
        }
    }

    saveAuth(authObj) {
        this.auth = authObj;
        if (authObj) localStorage.setItem('ng_auth', JSON.stringify(authObj));
        else localStorage.removeItem('ng_auth');
        this.updateAuthUi();
    }

    getAuthHeaders(includeJson = false) {
        const headers = {
            'X-Tenant-Id': this.tenantId,
            'X-Application-Id': this.applicationId
        };
        if (includeJson) headers['Content-Type'] = 'application/json';
        if (this.auth?.token) headers['Authorization'] = `Bearer ${this.auth.token}`;
        return headers;
    }

    updateAuthUi() {
        const btnOpen = document.getElementById('btnOpenAuthModal');
        const userBadge = document.getElementById('authUserBadge');
        const adminTabLi = document.getElementById('tab-admin-li');
        const providersTabLi = document.getElementById('tab-providers-li');

        if (this.auth && this.auth.token) {
            btnOpen.classList.add('d-none');
            userBadge.classList.remove('d-none');
            document.getElementById('authUserName').textContent = this.auth.fullName || this.auth.email;
            const roleEl = document.getElementById('authUserRole');
            roleEl.textContent = this.auth.role;
            roleEl.className = this.auth.role === 'Admin' ? 'badge bg-danger me-2' : 'badge bg-info text-dark me-2';

            // Every authenticated user (Admin & Customer) can view & save their own Provider Settings
            providersTabLi.classList.remove('d-none');
            this.loadProviderSettings();

            if (this.auth.role === 'Admin') {
                adminTabLi.classList.remove('d-none');
                this.loadAdminDashboard();
            } else {
                adminTabLi.classList.add('d-none');
                if (document.getElementById('tab-admin').classList.contains('active')) {
                    bootstrap.Tab.getOrCreateInstance(document.getElementById('tab-signalr')).show();
                }
            }
        } else {
            btnOpen.classList.remove('d-none');
            userBadge.classList.add('d-none');
            adminTabLi.classList.add('d-none');
            providersTabLi.classList.add('d-none');
            if (document.getElementById('tab-admin').classList.contains('active') ||
                document.getElementById('tab-providers').classList.contains('active')) {
                bootstrap.Tab.getOrCreateInstance(document.getElementById('tab-signalr')).show();
            }
        }
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
                headers: this.getAuthHeaders(true),
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
                headers: this.getAuthHeaders(false)
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

        document.getElementById('btnLoginSubmit').addEventListener('click', () => this.login());
        document.getElementById('btnRegisterSubmit').addEventListener('click', () => this.register());
        document.getElementById('btnLogout').addEventListener('click', () => this.saveAuth(null));
        document.getElementById('btnAdminApplyFilters').addEventListener('click', () => this.loadAdminDashboard());
        document.getElementById('btnAdminResetFilters').addEventListener('click', () => {
            document.getElementById('adminFilterSearch').value = '';
            document.getElementById('adminFilterRole').value = '';
            document.getElementById('adminFilterChannel').value = '';
            document.getElementById('adminFilterStatus').value = '';
            this.loadAdminDashboard();
        });
        document.getElementById('tab-admin').addEventListener('shown.bs.tab', () => this.loadAdminDashboard());
        document.getElementById('tab-providers').addEventListener('shown.bs.tab', () => this.loadProviderSettings());
        document.getElementById('btnRefreshProviders').addEventListener('click', () => this.loadProviderSettings());

        document.querySelectorAll('.btn-toggle-secret').forEach(btn => {
            btn.addEventListener('click', () => {
                const input = document.getElementById(btn.dataset.target);
                if (!input) return;
                const isPwd = input.type === 'password';
                input.type = isPwd ? 'text' : 'password';
                btn.innerHTML = isPwd ? '<i class="bi bi-eye-slash"></i>' : '<i class="bi bi-eye"></i>';
            });
        });

        document.getElementById('btnSaveSmtp').addEventListener('click', () => this.saveProviderConfig('SMTP', 'Email', {
            Host: document.getElementById('cfgSmtpHost').value.trim(),
            Port: parseInt(document.getElementById('cfgSmtpPort').value, 10) || 587,
            UseSsl: document.getElementById('cfgSmtpUseSsl').checked,
            FromEmail: document.getElementById('cfgSmtpFromEmail').value.trim(),
            FromName: document.getElementById('cfgSmtpFromName').value.trim(),
            Username: document.getElementById('cfgSmtpUsername').value.trim(),
            Password: document.getElementById('cfgSmtpPassword').value
        }));

        document.getElementById('btnSaveSms').addEventListener('click', () => this.saveProviderConfig('SMS', 'Sms', {
            ApiKey: document.getElementById('cfgSmsApiKey').value.trim(),
            ApiSecret: document.getElementById('cfgSmsApiSecret').value.trim(),
            SenderId: document.getElementById('cfgSmsSenderId').value.trim() || 'NotifGate'
        }));

        document.getElementById('btnSaveWhatsApp').addEventListener('click', () => this.saveProviderConfig('WHATSAPP', 'WhatsApp', {
            IdInstance: document.getElementById('cfgWaIdInstance').value.trim(),
            ApiToken: document.getElementById('cfgWaApiToken').value.trim()
        }));

        document.getElementById('btnSaveWebhook').addEventListener('click', () => this.saveProviderConfig('WEBHOOK', 'Webhook', {
            SecretKey: document.getElementById('cfgWebhookSecret').value.trim()
        }));
    }

    async loadProviderSettings() {
        if (!this.auth?.token) return;
        try {
            const resp = await fetch('/api/v1/provider-configurations', { headers: this.getAuthHeaders(false) });
            if (!resp.ok) return;
            const list = await resp.json();

            const getProp = (obj, name) => {
                if (!obj) return '';
                const key = Object.keys(obj).find(k => k.toLowerCase() === name.toLowerCase());
                return key ? (obj[key] ?? '') : '';
            };

            const setStatus = (elId, isConfigured) => {
                const el = document.getElementById(elId);
                if (!el) return;
                el.textContent = isConfigured ? 'Saved' : 'Not Configured';
                el.className = isConfigured
                    ? 'badge bg-success-subtle text-success'
                    : 'badge bg-secondary-subtle text-secondary';
            };

            const smtpItem = list.find(x => x.channel === 'Email');
            const smtp = smtpItem?.configuration;
            document.getElementById('cfgSmtpHost').value = smtp ? getProp(smtp, 'Host') : '';
            document.getElementById('cfgSmtpPort').value = smtp ? (getProp(smtp, 'Port') || '') : '';
            document.getElementById('cfgSmtpUseSsl').checked = smtp ? Boolean(getProp(smtp, 'UseSsl')) : false;
            document.getElementById('cfgSmtpFromEmail').value = smtp ? getProp(smtp, 'FromEmail') : '';
            document.getElementById('cfgSmtpFromName').value = smtp ? getProp(smtp, 'FromName') : '';
            document.getElementById('cfgSmtpUsername').value = smtp ? getProp(smtp, 'Username') : '';
            document.getElementById('cfgSmtpPassword').value = smtp ? getProp(smtp, 'Password') : '';
            setStatus('badgeStatusEmail', Boolean(smtpItem?.isConfigured));

            const smsItem = list.find(x => x.channel === 'Sms');
            const sms = smsItem?.configuration;
            document.getElementById('cfgSmsApiKey').value = sms ? getProp(sms, 'ApiKey') : '';
            document.getElementById('cfgSmsApiSecret').value = sms ? getProp(sms, 'ApiSecret') : '';
            document.getElementById('cfgSmsSenderId').value = sms ? getProp(sms, 'SenderId') : '';
            setStatus('badgeStatusSms', Boolean(smsItem?.isConfigured));

            const waItem = list.find(x => x.channel === 'WhatsApp');
            const wa = waItem?.configuration;
            document.getElementById('cfgWaIdInstance').value = wa ? getProp(wa, 'IdInstance') : '';
            document.getElementById('cfgWaApiToken').value = wa ? getProp(wa, 'ApiToken') : '';
            setStatus('badgeStatusWhatsApp', Boolean(waItem?.isConfigured));

            const whItem = list.find(x => x.channel === 'Webhook');
            const wh = whItem?.configuration;
            document.getElementById('cfgWebhookSecret').value = wh ? getProp(wh, 'SecretKey') : '';
            setStatus('badgeStatusWebhook', Boolean(whItem?.isConfigured));
        } catch (err) {
            console.warn('Provider settings load error:', err);
        }
    }

    async saveProviderConfig(providerCode, channel, configuration) {
        const toast = document.getElementById('providerSaveToast');
        const toastText = document.getElementById('providerSaveToastText');
        try {
            const resp = await fetch('/api/v1/provider-configurations/upsert', {
                method: 'PUT',
                headers: this.getAuthHeaders(true),
                body: JSON.stringify({ providerCode, channel, configuration, isPrimary: true, priority: 1 })
            });
            const data = await resp.json();
            if (!resp.ok) throw new Error(data.message || 'Save failed');

            toastText.textContent = data.message || `${channel} configuration saved!`;
            toast.className = 'alert alert-success py-2 small mb-3';
            toast.classList.remove('d-none');
            setTimeout(() => toast.classList.add('d-none'), 4000);
            await this.loadProviderSettings();
        } catch (err) {
            toastText.textContent = err.message;
            toast.className = 'alert alert-danger py-2 small mb-3';
            toast.classList.remove('d-none');
        }
    }

    async login() {
        const errEl = document.getElementById('loginErrorAlert');
        errEl.classList.add('d-none');
        const email = document.getElementById('loginEmail').value.trim();
        const password = document.getElementById('loginPassword').value;

        try {
            const resp = await fetch('/api/v1/auth/login', {
                method: 'POST',
                headers: this.getAuthHeaders(true),
                body: JSON.stringify({ email, password })
            });
            const data = await resp.json();
            if (!resp.ok) throw new Error(data.message || 'Login failed');
            this.saveAuth(data);
            bootstrap.Modal.getInstance(document.getElementById('authModal'))?.hide();
        } catch (err) {
            errEl.textContent = err.message;
            errEl.classList.remove('d-none');
        }
    }

    async register() {
        const errEl = document.getElementById('registerErrorAlert');
        errEl.classList.add('d-none');
        const fullName = document.getElementById('regFullName').value.trim();
        const email = document.getElementById('regEmail').value.trim();
        const password = document.getElementById('regPassword').value;
        const role = document.getElementById('regRole').value;

        try {
            const resp = await fetch('/api/v1/auth/register', {
                method: 'POST',
                headers: this.getAuthHeaders(true),
                body: JSON.stringify({ fullName, email, password, role })
            });
            const data = await resp.json();
            if (!resp.ok) throw new Error(data.message || 'Registration failed');
            this.saveAuth(data);
            bootstrap.Modal.getInstance(document.getElementById('authModal'))?.hide();
        } catch (err) {
            errEl.textContent = err.message;
            errEl.classList.remove('d-none');
        }
    }

    async loadAdminDashboard() {
        if (this.auth?.role !== 'Admin') return;

        const params = new URLSearchParams();
        const search = document.getElementById('adminFilterSearch')?.value.trim();
        const role = document.getElementById('adminFilterRole')?.value;
        const channel = document.getElementById('adminFilterChannel')?.value;
        const status = document.getElementById('adminFilterStatus')?.value;
        if (search) params.set('search', search);
        if (role) params.set('role', role);
        if (channel) params.set('channel', channel);
        if (status) params.set('status', status);

        try {
            const resp = await fetch(`/api/v1/admin/dashboard?${params.toString()}`, {
                headers: this.getAuthHeaders(false)
            });
            if (!resp.ok) return;
            const data = await resp.json();

            const s = data.summary;
            document.getElementById('kpiTotalUsers').textContent = s.totalUsers;
            document.getElementById('kpiAdminCount').textContent = s.adminCount;
            document.getElementById('kpiCustomerCount').textContent = s.customerCount;
            document.getElementById('kpiNotifTypes').textContent = s.totalNotificationTypes;
            document.getElementById('kpiTemplates').textContent = s.totalTemplates;
            document.getElementById('kpiSentDeliveries').textContent = s.sentDeliveries;
            document.getElementById('kpiTotalDeliveries').textContent = s.totalDeliveries;
            document.getElementById('kpiFailedDeliveries').textContent = s.failedDeliveries;
            document.getElementById('kpiTotalNotifications').textContent = s.totalNotifications;

            document.getElementById('adminUsersBody').innerHTML = data.users.length === 0
                ? '<tr><td colspan="4" class="text-center text-muted py-3">No users match filters.</td></tr>'
                : data.users.map(u => `
                    <tr>
                        <td>${this.escapeHtml(u.fullName)}</td>
                        <td class="font-monospace">${this.escapeHtml(u.email)}</td>
                        <td><span class="badge ${u.role === 'Admin' ? 'bg-danger' : 'bg-info text-dark'}">${this.escapeHtml(u.role)}</span></td>
                        <td class="text-muted">${new Date(u.createdAt).toLocaleDateString()}</td>
                    </tr>`).join('');

            document.getElementById('adminTypesBody').innerHTML = data.notificationTypes.length === 0
                ? '<tr><td colspan="3" class="text-center text-muted py-3">No types match filters.</td></tr>'
                : data.notificationTypes.map(t => `
                    <tr>
                        <td class="font-monospace fw-semibold">${this.escapeHtml(t.code)}</td>
                        <td>${(t.allowedChannels || []).map(c => `<span class="badge bg-light text-dark border me-1">${this.escapeHtml(c)}</span>`).join('')}</td>
                        <td><span class="badge bg-primary-subtle text-primary">${t.templateCount}</span></td>
                    </tr>`).join('');

            document.getElementById('adminDeliveriesBody').innerHTML = data.recentDeliveries.length === 0
                ? '<tr><td colspan="6" class="text-center text-muted py-3">No deliveries match filters.</td></tr>'
                : data.recentDeliveries.map(d => `
                    <tr>
                        <td class="font-monospace">${this.escapeHtml(d.notificationType)}</td>
                        <td><span class="badge badge-${String(d.channel).toLowerCase()}">${this.escapeHtml(d.channel)}</span></td>
                        <td class="font-monospace text-truncate" style="max-width:150px;">${this.escapeHtml(d.recipientAddress)}</td>
                        <td><span class="badge ${d.status === 'Sent' || d.status === 'Delivered' ? 'bg-success' : d.status === 'Failed' ? 'bg-danger' : 'bg-secondary'}">${this.escapeHtml(d.status)}</span></td>
                        <td>${d.attemptCount}</td>
                        <td class="text-muted">${new Date(d.createdAt).toLocaleTimeString()}</td>
                    </tr>`).join('');
        } catch (err) {
            console.warn('Admin dashboard load error:', err);
        }
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
