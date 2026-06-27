/**
 * app.js – Falcon PET AngularJS Application Module
 * Declares the root module and its dependencies.
 * Loaded first in index.html; all other scripts depend on this.
 */
(function () {
    'use strict';

    angular.module('PETApp', [
        'ngRoute',          // Client-side routing
        'ngResource',       // REST resource support
        'ngSanitize',       // Safe HTML binding

        // Sub-modules (declared in their own files)
        'PETApp.constants',
        'PETApp.config',
        'PETApp.factories',
        'PETApp.services',
        'PETApp.directives',
        'PETApp.filters',
        'PETApp.controllers'
    ]);

    // ── Sub-module declarations ─────────────────────────────────────────────
    angular.module('PETApp.constants',   []);
    angular.module('PETApp.config',      []);
    angular.module('PETApp.factories',   []);
    angular.module('PETApp.services',    []);
    angular.module('PETApp.directives',  []);
    angular.module('PETApp.filters',     []);
    angular.module('PETApp.controllers', []);

    /**
     * Root run block – hide the initial loader once AngularJS has bootstrapped
     * and the first route has been resolved.
     *
     * NOTE: AuthenticationService and NotificationService are obtained lazily via
     * $injector rather than declared as direct run-block dependencies.  If either
     * service file failed to load (e.g. a 404 during dynamic script loading),
     * direct injection would crash the entire Angular bootstrap.  Lazy access
     * lets us degrade gracefully and still show the page.
     */
    angular.module('PETApp')
        .run(['$rootScope', '$location', '$injector',
            function ($rootScope, $location, $injector) {

                // Remove the pre-Angular loader
                var loader = document.getElementById('appLoader');
                if (loader) loader.parentNode.removeChild(loader);

                // Global loading flag (used by the overlay spinner)
                $rootScope.loading = false;

                // Initialise confirm dialog state so ng-show never throws on first render
                $rootScope.confirmDialog = { visible: false };

                // Current user stored on $rootScope for access in all controllers
                $rootScope.currentUser  = null;
                $rootScope.userRoles    = [];
                $rootScope.isAuthorized = false;

                // Lazy-get services so a missing file doesn't crash bootstrap
                function getAuth()   { return $injector.has('AuthenticationService')  ? $injector.get('AuthenticationService')  : null; }
                function getNotify() { return $injector.has('NotificationService')    ? $injector.get('NotificationService')    : null; }

                // Load current user on startup
                var AuthenticationService = getAuth();
                if (AuthenticationService) {
                    AuthenticationService.getCurrentUser()
                        .then(function (user) {
                            $rootScope.currentUser  = user;
                            $rootScope.userRoles    = user.roles || [];
                            $rootScope.isAuthorized = user.roles && user.roles.length > 0;
                        })
                        .catch(function () {
                            var NotificationService = getNotify();
                            if (NotificationService) {
                                NotificationService.error('Unable to authenticate user.');
                            }
                            $location.path('/home');
                        });
                }

                // ── Route change events ──────────────────────────────────────
                $rootScope.$on('$routeChangeStart', function (event, next) {
                    $rootScope.loading = true;

                    // Role-based access check
                    if (next && next.$$route && next.$$route.requiredRoles) {
                        var allowed = next.$$route.requiredRoles;
                        var hasRole = allowed.some(function (r) {
                            return $rootScope.userRoles.indexOf(r) !== -1;
                        });
                        if (!hasRole) {
                            event.preventDefault();
                            var NotificationService = getNotify();
                            if (NotificationService) {
                                NotificationService.warning('You do not have permission to access this page.');
                            }
                            $location.path('/dashboard');
                        }
                    }
                });

                $rootScope.$on('$routeChangeSuccess', function () {
                    $rootScope.loading = false;
                });

                $rootScope.$on('$routeChangeError', function () {
                    $rootScope.loading = false;
                    var NotificationService = getNotify();
                    if (NotificationService) {
                        NotificationService.error('Failed to load the requested page.');
                    }
                });

                // ── Global helper available in all templates ─────────────────
                $rootScope.hasRole = function (role) {
                    return $rootScope.userRoles && $rootScope.userRoles.indexOf(role) !== -1;
                };

                $rootScope.hasAnyRole = function (roles) {
                    if (!$rootScope.userRoles || !roles) return false;
                    return roles.some(function (r) {
                        return $rootScope.userRoles.indexOf(r) !== -1;
                    });
                };
            }
        ]);

}());
