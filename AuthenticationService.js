/**
 * AuthenticationService.js
 * Handles current user retrieval, role resolution and route-level authorization.
 */
(function () {
    'use strict';

    angular.module('PETApp.services')
        .service('AuthenticationService', ['$q', '$http', 'SharePointService', 'CRUDService', 'APP_CONST',
            function ($q, $http, SharePointService, CRUDService, APP_CONST) {

                var self         = this;
                var _userPromise = null;   // Cache to avoid repeat calls

                /**
                 * Returns the current user with their application roles resolved.
                 * Result is cached for the session lifetime.
                 * @returns {Promise<{id, title, email, loginName, roles[]}>}
                 */
                self.getCurrentUser = function () {
                    if (_userPromise) return _userPromise;

                    _userPromise = SharePointService.getCurrentUser()
                        .then(function (spUser) {
                            return self.getUserRoles(spUser.Id)
                                .then(function (roles) {
                                    return angular.extend(spUser, { roles: roles });
                                });
                        })
                        .catch(function (err) {
                            _userPromise = null;
                            return $q.reject(err);
                        });

                    return _userPromise;
                };

                /**
                 * Returns the application roles assigned to a user login name.
                 */
                // userId is the numeric SharePoint user Id (spUser.Id).
                // Filtering by UserRefId (the hidden integer FK column) avoids the
                // $expand+$select-on-sub-field requirement that causes a 400 on SP on-prem.
                self.getUserRoles = function (userId) {
                    return CRUDService.getByFilter(
                        APP_CONST.LISTS.ROLE_MANAGEMENT,
                        'UserRefId eq ' + userId + ' and IsActive eq 1',
                        { select: 'Id,AppRole,Department' }
                    ).then(function (items) {
                        return items.map(function (i) { return i.AppRole; });
                    });
                };

                /**
                 * Checks whether the current user has a specific application role.
                 */
                self.hasRole = function (role) {
                    return self.getCurrentUser().then(function (user) {
                        return user.roles && user.roles.indexOf(role) !== -1;
                    });
                };

                /**
                 * Checks whether the current user has any of the provided roles.
                 */
                self.hasAnyRole = function (roles) {
                    return self.getCurrentUser().then(function (user) {
                        if (!user.roles) return false;
                        return roles.some(function (r) {
                            return user.roles.indexOf(r) !== -1;
                        });
                    });
                };

                /**
                 * Used in route resolve to ensure the user is authenticated before entering a route.
                 */
                self.ensureAuthenticated = function () {
                    return self.getCurrentUser().then(function (user) {
                        if (!user || !user.Id) return $q.reject('unauthenticated');
                        return user;
                    });
                };

                /**
                 * Clears the cached user (e.g. after role assignment changes).
                 */
                self.clearCache = function () {
                    _userPromise = null;
                };

                /**
                 * Returns true if the supplied role is in the administrator set.
                 */
                self.isAdmin = function () {
                    return self.hasRole(APP_CONST.ROLES.ADMINISTRATOR);
                };
            }
        ]);

}());
