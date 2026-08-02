// Theme management via JS interop.
// Manages localStorage persistence and dynamic loading of the
// Ant Design Blazor dark-theme CSS link.

window.themeManager = {
    _darkCssId: 'ant-design-blazor-dark',
    _darkCssHref: '_content/AntDesign/css/ant-design-blazor.dark.css',

    getStoredTheme: function () {
        return localStorage.getItem('theme') || 'light';
    },

    setStoredTheme: function (theme) {
        localStorage.setItem('theme', theme);
    },

    applyTheme: function (theme) {
        var existingLink = document.getElementById(this._darkCssId);

        if (theme === 'dark') {
            if (!existingLink) {
                var link = document.createElement('link');
                link.id = this._darkCssId;
                link.rel = 'stylesheet';
                link.href = this._darkCssHref;
                document.head.appendChild(link);
            }
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            if (existingLink) {
                existingLink.remove();
            }
            document.documentElement.setAttribute('data-theme', 'light');
        }
    },

    initTheme: function () {
        var theme = this.getStoredTheme();
        this.applyTheme(theme);
        return theme;
    }
};
