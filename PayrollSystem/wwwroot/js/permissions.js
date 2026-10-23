(function (window) {
    'use strict';

    function normalizePermission(permission) {
        return (permission || '').trim().toUpperCase();
    }

    function getAuth() {
        return window.appAuth || { isPrivileged: false, permissions: [] };
    }

    function getPermissions() {
        const auth = getAuth();
        return (auth.permissions || []).map(normalizePermission);
    }

    function hasPermissionToken(tokens, permission) {
        const required = normalizePermission(permission);
        if (!required) {
            return false;
        }

        if (tokens.includes(required)) {
            return true;
        }

        if (tokens.some(function (token) { return token.indexOf(required + ':') === 0; })) {
            return true;
        }

        if (required.indexOf(':') === -1) {
            return tokens.some(function (token) {
                return token.endsWith(':' + required);
            });
        }

        return false;
    }

    window.appPermissions = {
        isPrivileged: function () {
            return !!getAuth().isPrivileged;
        },
        has: function (permission) {
            if (this.isPrivileged()) {
                return true;
            }

            return hasPermissionToken(getPermissions(), permission);
        },
        hasAny: function () {
            const permissions = Array.prototype.slice.call(arguments);
            return permissions.some(function (permission) {
                return window.appPermissions.has(permission);
            });
        }
    };
})(window);
