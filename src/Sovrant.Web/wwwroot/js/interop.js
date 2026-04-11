window.sovrantInterop = {
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },

    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('sovrant-theme', theme);
    },

    getTheme: function () {
        return localStorage.getItem('sovrant-theme') || 'dark';
    }
};

// Apply saved theme on load
(function () {
    const saved = localStorage.getItem('sovrant-theme');
    if (saved) {
        document.documentElement.setAttribute('data-theme', saved);
    }
})();
