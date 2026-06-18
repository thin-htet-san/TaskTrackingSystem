window.firebaseMessagingConfig = {
    vapidKey: "BP8VcUO8HousJEE2s8yG7jBnL3tRfZOyK2cNMYX93SIdxefSytuOheHkfkjN8MVOu4GGhyKbb3OaAADoLlsZxqs"
};

window.getFirebaseVapidKey = function () {
    return window.firebaseMessagingConfig?.vapidKey ?? "";
};
