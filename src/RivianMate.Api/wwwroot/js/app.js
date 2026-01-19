// RivianMate client-side utilities

// Password field visibility toggle
function togglePasswordVisibility(button) {
    const wrapper = button.closest('.password-input-wrapper');
    if (!wrapper) return;

    const input = wrapper.querySelector('input');
    const icon = button.querySelector('svg');

    if (input.type === 'password') {
        input.type = 'text';
        button.setAttribute('aria-label', 'Hide password');
        // Update icon to eye-off
        if (icon) {
            icon.innerHTML = '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line>';
        }
    } else {
        input.type = 'password';
        button.setAttribute('aria-label', 'Show password');
        // Update icon to eye
        if (icon) {
            icon.innerHTML = '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle>';
        }
    }
}

window.rivianMate = {
    // Get the user's IANA timezone identifier (e.g., "America/New_York")
    getTimeZone: function () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone;
        } catch (e) {
            console.warn('Could not detect timezone:', e);
            return 'UTC';
        }
    },

    // Get the timezone offset in minutes (e.g., -300 for EST)
    getTimeZoneOffset: function () {
        return new Date().getTimezoneOffset();
    },

    // Format a UTC date string to local time
    formatLocalTime: function (utcDateString, options) {
        try {
            const date = new Date(utcDateString);
            if (isNaN(date.getTime())) return utcDateString;

            const defaultOptions = {
                dateStyle: 'short',
                timeStyle: 'short'
            };

            return date.toLocaleString(undefined, options || defaultOptions);
        } catch (e) {
            console.warn('Could not format date:', e);
            return utcDateString;
        }
    },

    // Format relative time (e.g., "5 minutes ago")
    formatRelativeTime: function (utcDateString) {
        try {
            const date = new Date(utcDateString);
            if (isNaN(date.getTime())) return utcDateString;

            const now = new Date();
            const diffMs = now - date;
            const diffSec = Math.floor(diffMs / 1000);
            const diffMin = Math.floor(diffSec / 60);
            const diffHour = Math.floor(diffMin / 60);
            const diffDay = Math.floor(diffHour / 24);

            if (diffSec < 60) return 'just now';
            if (diffMin < 60) return diffMin === 1 ? '1 minute ago' : `${diffMin} minutes ago`;
            if (diffHour < 24) return diffHour === 1 ? '1 hour ago' : `${diffHour} hours ago`;
            if (diffDay < 7) return diffDay === 1 ? 'yesterday' : `${diffDay} days ago`;

            return date.toLocaleDateString();
        } catch (e) {
            console.warn('Could not format relative time:', e);
            return utcDateString;
        }
    },

    // LocalStorage helpers for persisting user preferences
    storage: {
        get: function (key) {
            try {
                return localStorage.getItem(key);
            } catch (e) {
                console.warn('Could not read from localStorage:', e);
                return null;
            }
        },
        set: function (key, value) {
            try {
                localStorage.setItem(key, value);
            } catch (e) {
                console.warn('Could not write to localStorage:', e);
            }
        },
        remove: function (key) {
            try {
                localStorage.removeItem(key);
            } catch (e) {
                console.warn('Could not remove from localStorage:', e);
            }
        }
    }
};
