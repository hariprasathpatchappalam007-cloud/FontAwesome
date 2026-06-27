/**
 * config.js – AngularJS provider configuration
 * Runs in the config phase before any controllers or services are created.
 */
(function () {
    'use strict';

    angular.module('PETApp.config')
        .config(['$httpProvider', '$locationProvider', '$compileProvider',
            function ($httpProvider, $locationProvider, $compileProvider) {

                // ── CSRF / Request Digest headers for SharePoint REST ────────
                $httpProvider.defaults.headers.common['Accept']       = 'application/json;odata=verbose';
                $httpProvider.defaults.headers.common['Content-Type'] = 'application/json;odata=verbose';

                // ── Disable debug info in production for performance ─────────
                // Comment out during development if needed
                $compileProvider.debugInfoEnabled(true);

                // ── Whitelist blob: URLs used by the FileUpload directive ────
                $compileProvider.aHrefSanitizationWhitelist(
                    /^\s*(https?|ftp|mailto|tel|file|blob|javascript):/
                );

                // ── Hash-bang routing (required for SharePoint CEWP) ────────
                // Use $locationProvider.html5Mode(false) to keep hash-based routing
                $locationProvider.hashPrefix('!');
            }
        ]);

    /**
     * HTTP interceptor – injects the SharePoint request digest on every
     * non-GET REST call, shows the global loading spinner, and handles
     * 401/403 responses gracefully.
     */
    angular.module('PETApp.config')
        .factory('SPHttpInterceptor', ['$q', '$rootScope', '$injector',
            function ($q, $rootScope, $injector) {

                var digestCache = null;
                var digestExpiry = null;

                function getDigest() {
                    if (digestCache && digestExpiry && new Date() < digestExpiry) {
                        return $q.resolve(digestCache);
                    }
                    var $http = $injector.get('$http');
                    return $http.post('/_api/contextinfo', {}).then(function (res) {
                        digestCache  = res.data.d.GetContextWebInformation.FormDigestValue;
                        digestExpiry = new Date(
                            new Date().getTime() +
                            (res.data.d.GetContextWebInformation.FormDigestTimeoutSeconds - 30) * 1000
                        );
                        return digestCache;
                    });
                }

                return {
                    request: function (config) {
                        // ── Fix relative template / resource URLs ────────────
                        // When served via CEWP, Angular's $http resolves relative
                        // URLs against the SitePages page URL, not SiteAssets/PET.
                        // Prefix any relative URL with _PET_BASE so ng-include
                        // partials, route templateUrls, and similar requests always
                        // load from the correct SiteAssets/PET location.
                        //
                        // Safe-guards — do NOT prefix:
                        //   /absolute/paths          (starts with /)
                        //   http(s)://...  or  //... (absolute or protocol-relative)
                        //   data:  blob:  etc.       (any URI scheme with colon)
                        if (config.url &&
                            config.url.charAt(0) !== '/' &&
                            !/^(https?:)?\/\//.test(config.url) &&
                            !/^[a-zA-Z][a-zA-Z0-9+\-.]*:/.test(config.url)) {
                            config.url = (window._PET_BASE || '') + config.url;
                        }

                        if (/\/_api\//i.test(config.url) &&
                            config.method && config.method.toUpperCase() !== 'GET') {
                            return getDigest().then(function (digest) {
                                config.headers['X-RequestDigest'] = digest;
                                return config;
                            });
                        }
                        return config;
                    },

                    responseError: function (rejection) {
                        if (rejection.status === 401 || rejection.status === 403) {
                            var NotificationService = $injector.get('NotificationService');
                            NotificationService.error('Session expired or insufficient permissions. Please refresh the page.');
                        }
                        return $q.reject(rejection);
                    }
                };
            }
        ])
        .config(['$httpProvider', function ($httpProvider) {
            $httpProvider.interceptors.push('SPHttpInterceptor');
        }]);

}());
