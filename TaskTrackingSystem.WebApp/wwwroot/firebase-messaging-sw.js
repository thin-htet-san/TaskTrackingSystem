// Shim 'window' for Firebase compat SDK: service workers use 'self', not 'window'
self.window = self;

importScripts("/firebase-app-compat.js");
importScripts("/firebase-messaging-compat.js");

const ttsFirebaseConfig = {
    apiKey: "AIzaSyAsudRlYNKctjvctfr4Vb84wWodUYSv5NQ",
    authDomain: "ttsys-726b5.firebaseapp.com",
    projectId: "ttsys-726b5",
    storageBucket: "ttsys-726b5.firebasestorage.app",
    messagingSenderId: "517093039925",
    appId: "1:517093039925:web:08df7153a8f05ceb82b67c",
    measurementId: "G-0TVHXFR6WB",
    vapidKey: "BP8VcUO8HousJEE2s8yG7jBnL3tRfZOyK2cNMYX93SIdxefSytuOheHkfkjN8MVOu4GGhyKbb3OaAADoLlsZxqs"
};

firebase.initializeApp({
    apiKey: ttsFirebaseConfig.apiKey,
    authDomain: ttsFirebaseConfig.authDomain,
    projectId: ttsFirebaseConfig.projectId,
    messagingSenderId: ttsFirebaseConfig.messagingSenderId,
    appId: ttsFirebaseConfig.appId
});

const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
    const title = payload?.notification?.title || "Taskify";
    const body = payload?.notification?.body || "You have a new notification.";

    self.registration.showNotification(title, {
        body,
        icon: "/favicon.png"
    });
});
