window.ttsFirebase = (() => {
    let initialized = false;

    function getConfig() {
        return window.ttsFirebaseConfig || {};
    }

    function ensureInitialized() {
        if (initialized) {
            return;
        }

        if (typeof firebase === "undefined") {
            throw new Error("Firebase SDK is not loaded.");
        }

        const config = getConfig();
        if (!config.apiKey || !config.projectId || !config.messagingSenderId || !config.appId) {
            throw new Error("Firebase web config is incomplete.");
        }

        firebase.initializeApp({
            apiKey: config.apiKey,
            authDomain: config.authDomain,
            projectId: config.projectId,
            messagingSenderId: config.messagingSenderId,
            appId: config.appId
        });

        initialized = true;
    }

    async function getRegistrationToken() {
        if (!("Notification" in window) || !("serviceWorker" in navigator)) {
            return null;
        }

        const config = getConfig();
        if (!config.vapidKey) {
            throw new Error("Firebase VAPID key is missing.");
        }

        ensureInitialized();

        const permission = await Notification.requestPermission();
        if (permission !== "granted") {
            return null;
        }

        const registration = await navigator.serviceWorker.register("/firebase-messaging-sw.js");
        const messaging = firebase.messaging();
        const token = await messaging.getToken({
            vapidKey: config.vapidKey,
            serviceWorkerRegistration: registration
        });

        return token || null;
    }

    async function areNotificationsEnabled() {
        if (!("Notification" in window) || !("serviceWorker" in navigator)) {
            return false;
        }
        if (Notification.permission !== "granted") {
            return false;
        }
        try {
            const registration = await navigator.serviceWorker.getRegistration("/firebase-messaging-sw.js");
            if (!registration) {
                return false;
            }
            const messaging = firebase.messaging();
            const config = getConfig();
            const token = await messaging.getToken({
                vapidKey: config.vapidKey,
                serviceWorkerRegistration: registration
            });
            return !!token;
        } catch (e) {
            return false;
        }
    }

    async function disableNotifications() {
        if (!("Notification" in window) || !("serviceWorker" in navigator)) {
            return;
        }
        try {
            const registration = await navigator.serviceWorker.getRegistration("/firebase-messaging-sw.js");
            if (registration) {
                const messaging = firebase.messaging();
                await messaging.deleteToken();
            }
        } catch (e) {
            console.error("Error during disableNotifications:", e);
        }
    }

    return {
        getRegistrationToken,
        areNotificationsEnabled,
        disableNotifications
    };
})();
