(function () {
    const storageKey = "tts-theme";

    function normalize(theme) {
        return theme === "dark" || theme === "light" ? theme : "light";
    }

    function currentTheme() {
        return normalize(
            document.documentElement.dataset.theme ||
            (document.documentElement.classList.contains("dark") ? "dark" : "light")
        );
    }

    function setTheme(theme) {
        const normalized = normalize(theme);
        const isDark = normalized === "dark";
        const root = document.documentElement;

        root.classList.toggle("dark", isDark);
        root.dataset.theme = normalized;

        if (document.body) {
            document.body.classList.toggle("dark", isDark);
            document.body.dataset.theme = normalized;
        }

        try {
            localStorage.setItem(storageKey, normalized);
        } catch {
        }

        updateControls();
        return isDark;
    }

    function updateControls() {
        const isDark = currentTheme() === "dark";
        const title = isDark ? "Switch to light mode" : "Switch to dark mode";
        const iconName = isDark ? "sun" : "moon";

        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            button.setAttribute("aria-pressed", String(isDark));
            button.setAttribute("aria-label", title);
            button.setAttribute("title", title);

            const icon = button.querySelector("[data-lucide]");
            if (icon) {
                icon.setAttribute("data-lucide", iconName);
            }
        });

        if (window.lucide) {
            window.lucide.createIcons();
        }
    }

    function initTheme() {
        let storedTheme = "light";
        try {
            storedTheme = normalize(localStorage.getItem(storageKey));
        } catch {
        }

        setTheme(storedTheme);
    }

    window.themePreference = {
        apply: setTheme,
        init: initTheme,
        toggle() {
            return setTheme(currentTheme() === "dark" ? "light" : "dark");
        }
    };

    window.updateTaskifyThemeControls = updateControls;
    window.toggleTaskifyThemeButton = function () {
        window.themePreference.toggle();
        return false;
    };

    if (!window.__taskifyThemeToggleBound) {
        window.__taskifyThemeToggleBound = true;
        document.addEventListener("click", event => {
            const target = event.target instanceof Element ? event.target : event.target?.parentElement;
            const button = target?.closest("[data-theme-toggle]");
            if (!button) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            window.themePreference.toggle();
        }, true);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initTheme, { once: true });
    } else {
        initTheme();
    }

    window.addEventListener("pageshow", updateControls);
})();
