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
    },

    copyCode: function (button) {
        const wrapper = button.closest('.code-block-wrapper');
        if (!wrapper) return;
        const codeEl = wrapper.querySelector('pre code');
        if (!codeEl) return;
        navigator.clipboard.writeText(codeEl.textContent).then(function () {
            button.textContent = 'Copied!';
            setTimeout(function () { button.textContent = 'Copy'; }, 1500);
        });
    },

    registerCtrlK: function (dotNetRef) {
        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('OnCtrlK');
            }
        });
    },

    getElementRect: function (element) {
        if (!element) return null;
        const r = element.getBoundingClientRect();
        return { top: r.top, left: r.left, width: r.width, height: r.height, bottom: r.bottom, right: r.right };
    }
};

// Apply saved theme on load
(function () {
    const saved = localStorage.getItem('sovrant-theme');
    if (saved) {
        document.documentElement.setAttribute('data-theme', saved);
    }
})();
