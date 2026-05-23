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
    },

    _dismissId: 0,
    _dismissHandlers: {},
    bindLightDismiss: function (triggerEl, ignoreSelector, dotNetRef, methodName) {
        const id = ++this._dismissId;
        const self = this;
        const handler = function (e) {
            const target = e.target;
            if (triggerEl && triggerEl.contains && triggerEl.contains(target)) return;
            if (ignoreSelector && target.closest && target.closest(ignoreSelector)) return;
            dotNetRef.invokeMethodAsync(methodName);
        };
        // Defer attach so the click that opened the dropdown doesn't immediately close it.
        setTimeout(function () { document.addEventListener('mousedown', handler); }, 0);
        this._dismissHandlers[id] = handler;
        return id;
    },
    unbindLightDismiss: function (id) {
        const handler = this._dismissHandlers[id];
        if (handler) {
            document.removeEventListener('mousedown', handler);
            delete this._dismissHandlers[id];
        }
    },

    initTextareaAutoResize: function (el) {
        if (!el) return;
        const resize = function () {
            el.style.height = 'auto';
            el.style.height = el.scrollHeight + 'px';
        };
        el.addEventListener('input', resize);
    },

    resetTextareaHeight: function (el) {
        if (!el) return;
        el.style.height = '';
    },

    _baseTitle: null,
    setTabTitlePrefix: function (prefix) {
        if (!this._baseTitle) this._baseTitle = document.title;
        document.title = prefix + (this._baseTitle || 'Sovrant');
    },
    clearTabTitlePrefix: function () {
        if (this._baseTitle) {
            document.title = this._baseTitle;
            this._baseTitle = null;
        }
    }
};

// Apply saved theme on load
(function () {
    const saved = localStorage.getItem('sovrant-theme');
    if (saved) {
        document.documentElement.setAttribute('data-theme', saved);
    }
})();
