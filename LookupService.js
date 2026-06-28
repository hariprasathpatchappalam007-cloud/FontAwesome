/**
 * LookupService.js
 * Retrieves master data lookup values for dropdowns and auto-complete.
 * Results are cached in CacheFactory to minimize REST calls.
 */
(function () {
    'use strict';

    angular.module('PETApp.services')
        .service('LookupService', ['$q', 'CRUDService', 'CacheFactory', 'APP_CONST',
            function ($q, CRUDService, CacheFactory, APP_CONST) {

                var self  = this;
                var LISTS = APP_CONST.LISTS;

                function cached(key, fetchFn) {
                    var cached = CacheFactory.get(key);
                    if (cached) return $q.resolve(cached);
                    return fetchFn().then(function (data) {
                        CacheFactory.set(key, data);
                        return data;
                    });
                }

                self.getCapexList = function () {
                    return cached('capex', function () {
                        return CRUDService.getByFilter(LISTS.CAPEX_MASTER, 'IsActive eq 1',
                            { select: 'Id,CAPEXCode,CAPEXName,BudgetedAmount,AvailableAmount,NetBalance', orderby: 'CAPEXName' });
                    });
                };

                self.getOpexList = function () {
                    return cached('opex', function () {
                        return CRUDService.getByFilter(LISTS.OPEX_MASTER, 'IsActive eq 1',
                            { select: 'Id,OPEXCode,OPEXName,BudgetedAmount,AvailableAmount,NetBalance', orderby: 'OPEXName' });
                    });
                };

                /** Load full budget detail for a specific CAPEX ID (after user selects budget source) */
                self.getCapexDetail = function (code) {
                    return CRUDService.getByFilter(LISTS.CAPEX_MASTER,
                        "CAPEXCode eq '" + code + "'",
                        { select: 'Id,CAPEXCode,CAPEXName,Description,BudgetedAmount,UtilizedAmount,' +
                                  'AvailableAmount,LockedAmount,BudgetAfterLocked,ClaimAmount,NetBalance,GLNumbers' }
                    ).then(function (items) { return items.length ? items[0] : null; });
                };

                /** Load full budget detail for a specific OPEX ID */
                self.getOpexDetail = function (code) {
                    return CRUDService.getByFilter(LISTS.OPEX_MASTER,
                        "OPEXCode eq '" + code + "'",
                        { select: 'Id,OPEXCode,OPEXName,Description,BudgetedAmount,UtilizedAmount,' +
                                  'AvailableAmount,LockedAmount,BudgetAfterLocked,ClaimAmount,NetBalance,Contracts' }
                    ).then(function (items) { return items.length ? items[0] : null; });
                };

                /** Users with Reviewer or Administrator role (for reviewer dropdown) */
                self.getReviewers = function () {
                    return cached('reviewers', function () {
                        return CRUDService.getByFilter(LISTS.ROLE_MANAGEMENT,
                            "(AppRole eq 'Reviewer' or AppRole eq 'Administrator') and IsActive eq 1",
                            { select: 'Id,AppRole,Department,UserRef/Id,UserRef/Title,UserRef/EMail',
                              expand: 'UserRef' }
                        ).then(function (items) {
                            return items.filter(function (i) { return i.UserRef; })
                                        .map(function (i) {
                                            return { UserId: i.UserRef.Id, Title: i.UserRef.Title,
                                                     Email: i.UserRef.EMail || '', Role: i.AppRole };
                                        });
                        });
                    });
                };

                /** Users with PET Approver / CAPEX Approver / OPEX Approver / Admin role */
                self.getApprovers = function () {
                    return cached('approvers', function () {
                        return CRUDService.getByFilter(LISTS.ROLE_MANAGEMENT,
                            "(AppRole eq 'PET Approver' or AppRole eq 'CAPEX Approver' or " +
                            "AppRole eq 'OPEX Approver' or AppRole eq 'Administrator') and IsActive eq 1",
                            { select: 'Id,AppRole,Department,UserRef/Id,UserRef/Title,UserRef/EMail',
                              expand: 'UserRef' }
                        ).then(function (items) {
                            return items.filter(function (i) { return i.UserRef; })
                                        .map(function (i) {
                                            return { UserId: i.UserRef.Id, Title: i.UserRef.Title,
                                                     Email: i.UserRef.EMail || '', Role: i.AppRole };
                                        });
                        });
                    });
                };

                self.getVendorList = function () {
                    return cached('vendors', function () {
                        return CRUDService.getByFilter(LISTS.VENDOR_MASTER, 'IsActive eq 1',
                            { select: 'Id,VendorCode,VendorName', orderby: 'VendorName' });
                    });
                };

                self.getGLList = function () {
                    return cached('gl', function () {
                        return CRUDService.getByFilter(LISTS.GL_MASTER, 'IsActive eq 1',
                            { select: 'Id,GLNumber,GLDescription,GLType', orderby: 'GLNumber' });
                    });
                };

                self.getBudgetSources = function () {
                    return cached('budgetSources', function () {
                        return CRUDService.getByFilter(LISTS.BUDGET_SOURCE, 'IsActive eq 1',
                            { select: 'Id,SourceCode,SourceName,FiscalYear', orderby: 'SourceName' });
                    });
                };

                self.getRoles = function () {
                    return cached('roles', function () {
                        return CRUDService.getByFilter(LISTS.ROLE_MANAGEMENT, 'IsActive eq 1',
                            { select: 'Id,AppRole,UserRef/Title,UserRef/EMail,Department',
                              expand: 'UserRef', orderby: 'AppRole' });
                    });
                };

                self.clearAll = function () {
                    ['capex', 'opex', 'vendors', 'gl', 'budgetSources', 'roles', 'reviewers', 'approvers']
                        .forEach(function (k) { CacheFactory.remove(k); });
                };
            }
        ]);

}());
