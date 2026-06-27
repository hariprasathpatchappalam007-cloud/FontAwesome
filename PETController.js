/**
 * PETController.js
 * Manages PET Project Details form (create / edit / submit / recall / cancel).
 * Projects are loaded from the "Project Details" list (JIRA-synced).
 * Also handles CSV bulk import.
 */
(function () {
    'use strict';
    angular.module('PETApp.controllers')
        .controller('PETController', [
            '$routeParams', '$location', 'CRUDService', 'WorkflowService',
            'LookupService', 'ValidationService', 'NotificationService',
            'CSVService', 'AttachmentService', 'UtilityService', 'APP_CONST',
            function ($routeParams, $location, CRUDService, WorkflowService,
                      LookupService, ValidationService, NotificationService,
                      CSVService, AttachmentService, UtilityService, APP_CONST) {
                var vm     = this;
                var LISTS  = APP_CONST.LISTS;
                var isNew  = !$routeParams.id;

                // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                vm.loading          = false;
                vm.saving           = false;
                vm.isNew            = isNew;
                vm.pet              = _emptyPET();
                vm.validationErrors = [];
                vm.budgetSources    = [];
                vm.workflowHistory  = [];

                // Project Details (JIRA-synced) â€“ used for project selector
                vm.projectList       = [];
                vm.projectSearch     = '';
                vm.filteredProjects  = [];
                vm.showProjectDrop   = false;
                vm.selectedProject   = null;

                // CSV Import state
                vm.csvFile       = null;
                vm.csvRows       = [];
                vm.csvErrors     = [];
                vm.importRunning = false;
                vm.importResult  = null;

                // â”€â”€ Init â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                vm.$onInit = function () {
                    // Load budget sources
                    LookupService.getBudgetSources().then(function (list) { vm.budgetSources = list; });

                    // Load Project Details list (JIRA-synced projects)
                    _loadProjectDetails();

                    if (!isNew) {
                        vm.loading = true;
                        CRUDService.getById(LISTS.PET_PROJECTS, $routeParams.id, {
                            select: 'Id,PETRefNo,ProjectTitle,ProjectType,PETStatus,' +
                                    'RequestedAmountAED,BudgetSourceId,JIRAProjectKey,' +
                                    'ProjectDetailsId,Remarks,Version,' +
                                    'Requestor/Title,Requestor/EMail,Reviewer/Title',
                            expand: 'Requestor,Reviewer'
                        }).then(function (pet) {
                            vm.pet = pet;
                            // Restore the selected project display
                            if (pet.JIRAProjectKey) {
                                vm.projectSearch = pet.ProjectTitle || pet.JIRAProjectKey;
                            }
                            return WorkflowService.getWorkflowHistory(pet.Id);
                        }).then(function (h) {
                            vm.workflowHistory = h;
                        }).catch(function () {
                            NotificationService.error('Failed to load PET.');
                        }).finally(function () { vm.loading = false; });
                    }
                };

                // â”€â”€ Project Details lookup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                function _loadProjectDetails() {
                    CRUDService.getAll(LISTS.PROJECT_DETAILS, {
                        select: 'Id,Title,JiraKey,ProjectName,ProjectKey,Manager,JiraStatus,IsActive',
                        filter: "IsActive eq 1",
                        orderby: 'Title asc',
                        top: 2000
                    }).then(function (items) {
                        vm.projectList = items;
                    }).catch(function () {
                        NotificationService.warning('Could not load project list from Project Details.');
                    });
                }

                /** Called when user types in the project search box */
                vm.onProjectSearch = function () {
                    var q = (vm.projectSearch || '').toLowerCase().trim();
                    if (!q) {
                        vm.filteredProjects = [];
                        vm.showProjectDrop  = false;
                        return;
                    }
                    vm.filteredProjects = vm.projectList.filter(function (p) {
                        return (p.Title && p.Title.toLowerCase().indexOf(q) >= 0) ||
                               (p.JiraKey && p.JiraKey.toLowerCase().indexOf(q) >= 0) ||
                               (p.ProjectKey && p.ProjectKey.toLowerCase().indexOf(q) >= 0);
                    }).slice(0, 20);
                    vm.showProjectDrop = vm.filteredProjects.length > 0;
                };

                /** Called when user selects a project from the dropdown */
                vm.selectProject = function (project) {
                    vm.selectedProject       = project;
                    vm.projectSearch         = project.Title || project.JiraKey;
                    vm.pet.ProjectTitle      = project.Title || project.JiraKey;
                    vm.pet.JIRAProjectKey    = project.JiraKey || '';
                    vm.pet.ProjectDetailsId  = project.Id;
                    vm.pet.ProjectManager    = project.Manager || '';
                    vm.showProjectDrop       = false;
                };

                /** Clear the project selection */
                vm.clearProject = function () {
                    vm.selectedProject      = null;
                    vm.projectSearch        = '';
                    vm.pet.ProjectTitle     = '';
                    vm.pet.JIRAProjectKey   = '';
                    vm.pet.ProjectDetailsId = null;
                    vm.pet.ProjectManager   = '';
                    vm.filteredProjects     = [];
                    vm.showProjectDrop      = false;
                };

                // â”€â”€ CRUD â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                vm.save = function (submit) {
                    vm.validationErrors = ValidationService.validatePET(vm.pet);
                    if (vm.validationErrors.length) return;

                    vm.saving = true;
                    var promise = isNew
                        ? CRUDService.insert(LISTS.PET_PROJECTS, _preparePayload(vm.pet))
                        : CRUDService.update(LISTS.PET_PROJECTS, vm.pet.Id, _preparePayload(vm.pet));

                    promise.then(function (saved) {
                        if (submit) {
                            var petId = saved ? saved.Id : vm.pet.Id;
                            return WorkflowService.submit(petId, vm.pet.ReviewerLoginName);
                        }
                    }).then(function () {
                        NotificationService.success(isNew ? 'PET created.' : 'PET saved.');
                        $location.path('/dashboard');
                    }).catch(function (err) {
                        NotificationService.error('Save failed: ' + _errMsg(err));
                    }).finally(function () { vm.saving = false; });
                };

                vm.recall = function () {
                    WorkflowService.recall(vm.pet.Id, 'Recalled by requestor')
                        .then(function () { NotificationService.success('PET recalled.'); vm.$onInit(); });
                };

                vm.cancel = function () {
                    WorkflowService.cancel(vm.pet.Id, 'Cancelled by requestor')
                        .then(function () { NotificationService.success('PET cancelled.'); $location.path('/dashboard'); });
                };

                // â”€â”€ CSV Import â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                vm.onCSVFileChange = function (files) {
                    if (!files || !files.length) return;
                    vm.csvFile   = files[0];
                    vm.csvRows   = [];
                    vm.csvErrors = [];
                    vm.importResult = null;
                    CSVService.parseFile(vm.csvFile).then(function (result) {
                        var validation  = CSVService.validate(result.data);
                        vm.csvRows      = validation.valid;
                        vm.csvErrors    = validation.invalid;
                    });
                };

                vm.runImport = function () {
                    if (!vm.csvRows.length) { NotificationService.warning('No valid rows to import.'); return; }
                    vm.importRunning = true;
                    CSVService.importRows(vm.csvRows, function (done, total) {
                        vm.importProgress = Math.round((done / total) * 100);
                    }).then(function (result) {
                        vm.importResult  = result;
                        NotificationService.success('Import completed: ' + result.success + ' succeeded, ' + result.failed + ' failed.');
                    }).catch(function () {
                        NotificationService.error('Import failed.');
                    }).finally(function () { vm.importRunning = false; });
                };

                // â”€â”€ Private â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                function _emptyPET() {
                    return {
                        ProjectTitle:       '',
                        ProjectType:        '',
                        PETStatus:          'Draft',
                        RequestedAmountAED: 0,
                        BudgetSourceId:     null,
                        JIRAProjectKey:     '',
                        ProjectDetailsId:   null,
                        ProjectManager:     '',
                        Remarks:            '',
                        Version:            1
                    };
                }

                function _preparePayload(pet) {
                    return {
                        Title:              pet.ProjectTitle,
                        ProjectTitle:       pet.ProjectTitle,
                        ProjectType:        pet.ProjectType,
                        RequestedAmountAED: parseFloat(pet.RequestedAmountAED) || 0,
                        BudgetSourceId:     pet.BudgetSourceId || null,
                        JIRAProjectKey:     pet.JIRAProjectKey || '',
                        ProjectDetailsId:   pet.ProjectDetailsId || null,
                        Remarks:            pet.Remarks || '',
                        PETStatus:          pet.PETStatus || 'Draft'
                    };
                }

                function _errMsg(err) {
                    return err && err.data && err.data.error ? err.data.error.message : String(err);
                }
            }
        ]);
}());
