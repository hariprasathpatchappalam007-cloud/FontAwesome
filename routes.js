/**
 * routes.js – ngRoute client-side routing configuration
 * Maps URL hash paths to templates and controllers.
 * requiredRoles restricts access; checked in the $routeChangeStart handler in app.js.
 */
(function () {
    'use strict';

    angular.module('PETApp')
        .config(['$routeProvider', 'APP_CONST',
            function ($routeProvider, APP_CONST) {

                var ROLES = APP_CONST.ROLES;

                $routeProvider

                    // ── Home / Landing ────────────────────────────────────────
                    .when('/home', {
                        templateUrl:   'Templates/Home.html',
                        controller:    'HomeController',
                        controllerAs:  'vm',
                        resolve:       { auth: authResolve }
                    })

                    // ── Dashboard ─────────────────────────────────────────────
                    .when('/dashboard', {
                        templateUrl:   'Templates/PET/Dashboard.html',
                        controller:    'DashboardController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.PET_APPROVER,
                                        ROLES.CAPEX_APPROVER, ROLES.OPEX_APPROVER,
                                        ROLES.REVIEWER, ROLES.REQUESTOR],
                        resolve:       { auth: authResolve }
                    })

                    // ── PET Workflow ──────────────────────────────────────────
                    .when('/pet/new', {
                        templateUrl:   'Templates/PET/ProjectDetails.html',
                        controller:    'PETController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.REQUESTOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/pet/edit/:id', {
                        templateUrl:   'Templates/PET/ProjectDetails.html',
                        controller:    'PETController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.REQUESTOR, ROLES.REVIEWER],
                        resolve:       { auth: authResolve }
                    })
                    .when('/pet/sizing/:id', {
                        templateUrl:   'Templates/PET/ProjectSizing.html',
                        controller:    'ProjectSizingController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.REQUESTOR, ROLES.REVIEWER],
                        resolve:       { auth: authResolve }
                    })
                    .when('/pet/approval/:id', {
                        templateUrl:   'Templates/PET/PETApproval.html',
                        controller:    'ApprovalController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.PET_APPROVER,
                                        ROLES.CAPEX_APPROVER, ROLES.OPEX_APPROVER, ROLES.REVIEWER],
                        resolve:       { auth: authResolve }
                    })
                    .when('/pet/import', {
                        templateUrl:   'Templates/PET/CSVImport.html',
                        controller:    'PETController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.REQUESTOR],
                        resolve:       { auth: authResolve }
                    })

                    // ── My Approvals ──────────────────────────────────────────
                    .when('/myapprovals', {
                        templateUrl:   'Templates/Approvals/MyApprovals.html',
                        controller:    'MyApprovalController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR, ROLES.PET_APPROVER,
                                        ROLES.CAPEX_APPROVER, ROLES.OPEX_APPROVER, ROLES.REVIEWER],
                        resolve:       { auth: authResolve }
                    })

                    // ── Masters ───────────────────────────────────────────────
                    .when('/masters/capex', {
                        templateUrl:   'Templates/Masters/CAPEX.html',
                        controller:    'CAPEXController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/masters/opex', {
                        templateUrl:   'Templates/Masters/OPEX.html',
                        controller:    'OPEXController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/masters/vendor', {
                        templateUrl:   'Templates/Masters/Vendor.html',
                        controller:    'VendorController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/masters/gl', {
                        templateUrl:   'Templates/Masters/GLMaster.html',
                        controller:    'GLController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })

                    // ── Administration ────────────────────────────────────────
                    .when('/admin/roles', {
                        templateUrl:   'Templates/Admin/RoleManagement.html',
                        controller:    'RoleController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/admin/emaillogs', {
                        templateUrl:   'Templates/Admin/EmailLog.html',
                        controller:    'EmailLogController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })
                    .when('/admin/jirasync', {
                        templateUrl:   'Templates/Admin/JiraSync.html',
                        controller:    'JiraSyncController',
                        controllerAs:  'vm',
                        requiredRoles: [ROLES.ADMINISTRATOR],
                        resolve:       { auth: authResolve }
                    })

                    // ── Default / Not found ───────────────────────────────────
                    .otherwise({ redirectTo: '/home' });

                // ── Auth resolve factory ─────────────────────────────────────
                function authResolve() {
                    return ['AuthenticationService', function (AuthenticationService) {
                        return AuthenticationService.ensureAuthenticated();
                    }];
                }
            }
        ]);

}());
