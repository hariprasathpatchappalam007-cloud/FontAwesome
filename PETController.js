/**
 * PETController.js
 * Manages the PET Workflow form – tab-based layout matching PetWorkflow.aspx.
 * Tabs: PET Registration | Project Details | Project Sizing | PET Approval | CSV Import
 *
 * Budget source is loaded from CAPEX Master or OPEX Master depending on selected type.
 * Line items match the PET Form.csv columns and dbo.PetLineItem SQL schema.
 */
(function () {
    'use strict';
    angular.module('PETApp.controllers')
        .controller('PETController', [
            '$routeParams', '$location', '$q',
            'CRUDService', 'WorkflowService', 'LookupService',
            'ValidationService', 'NotificationService',
            'CSVService', 'UtilityService', 'APP_CONST',
            function ($routeParams, $location, $q,
                      CRUDService, WorkflowService, LookupService,
                      ValidationService, NotificationService,
                      CSVService, UtilityService, APP_CONST) {

                var vm    = this;
                var LISTS = APP_CONST.LISTS;
                var isNew = !$routeParams.id;

                // -- Tabs ----------------------------------------------------------
                vm.activeTab = 'pet';
                vm.setTab    = function (t) { vm.activeTab = t; };

                vm.stepClass = function (n) {
                    var cur = _currentStep();
                    if (n < cur)   return 'done';
                    if (n === cur) return 'active';
                    return '';
                };

                function _currentStep() {
                    var s = vm.pet.PETStatus;
                    if (s === 'Draft' || s === 'Sent Back' || s === 'SentBack') return 1;
                    if (s === 'Submitted' || s === 'PendingReview')              return 2;
                    if (s === 'PendingApproval')                                 return 3;
                    if (s === 'Approved'  || s === 'Rejected')                   return 4;
                    return 1;
                }

                // -- State ---------------------------------------------------------
                vm.loading          = false;
                vm.saving           = false;
                vm.isNew            = isNew;
                vm.pet              = _emptyPET();
                vm.validationErrors = [];
                vm.workflowHistory  = [];

                // Project picker
                vm.projectList      = [];
                vm.projectSearch    = '';
                vm.filteredProjects = [];
                vm.showProjectDrop  = false;
                vm.selectedProject  = null;

                // Budget
                vm.typeSourceList   = [];
                vm.budgetDetail     = null;

                // People
                vm.reviewers        = [];
                vm.approvers        = [];

                // Master data for line items
                vm.vendors          = [];
                vm.glList           = [];
                vm.currencies       = ['AED','USD','EUR','GBP','INR','SGD','CHF','JPY'];

                // Line items
                vm.lineItems        = [];
                vm.totals           = { count: 0, totalLCY: 0, totalFinal: 0 };
                vm.lineModal        = { show: false, isNew: true, line: {} };

                // CSV Import
                vm.csvFile          = null;
                vm.csvRows          = [];
                vm.csvErrors        = [];
                vm.importRunning    = false;
                vm.importProgress   = 0;
                vm.importResult     = null;

                // -- Init ----------------------------------------------------------
                vm.$onInit = function () {
                    var promises = [
                        LookupService.getReviewers().then(function (r) { vm.reviewers = r; }),
                        LookupService.getApprovers().then(function (a) { vm.approvers = a; }),
                        LookupService.getVendorList().then(function (v) { vm.vendors   = v; }),
                        LookupService.getGLList().then(function (g)     { vm.glList    = g; }),
                        _loadProjectDetails()
                    ];

                    if (!isNew) {
                        vm.loading = true;
                        $q.all(promises).then(function () {
                            return CRUDService.getById(LISTS.PET_PROJECTS, $routeParams.id, {
                                select: 'Id,Title,PETRefNo,PETStatus,ProjectType,BudgetSourceCode,' +
                                        'JIRAProjectKey,ProjectDetailsId,ReviewerUserId,ApproverUserId,' +
                                        'RequestedAmountAED,Remarks,Version,' +
                                        'Requestor/Title,Requestor/EMail',
                                expand: 'Requestor'
                            });
                        }).then(function (pet) {
                            vm.pet = pet;
                            if (pet.JIRAProjectKey) {
                                vm.projectSearch = pet.Title || pet.JIRAProjectKey;
                            }
                            if (pet.ProjectType) {
                                _loadTypeSourceList(pet.ProjectType);
                            }
                            if (pet.BudgetSourceCode) {
                                _loadBudgetDetail(pet.ProjectType, pet.BudgetSourceCode);
                            }
                            return _loadLineItems(pet.Id);
                        }).then(function () {
                            return WorkflowService.getWorkflowHistory($routeParams.id);
                        }).then(function (h) {
                            vm.workflowHistory = h;
                        }).catch(function () {
                            NotificationService.error('Failed to load PET.');
                        }).finally(function () { vm.loading = false; });
                    } else {
                        $q.all(promises).catch(angular.noop);
                    }
                };

                // -- Project picker ------------------------------------------------
                function _loadProjectDetails() {
                    return CRUDService.getAll(LISTS.PROJECT_DETAILS, {
                        select: 'Id,Title,JiraKey,ProjectName,ProjectKey,Manager,' +
                                'Platform,TechLead,JiraStatus,ActivityRagStatus,IsActive',
                        filter: 'IsActive eq 1',
                        orderby: 'Title asc',
                        top: 2000
                    }).then(function (items) {
                        vm.projectList = items;
                    }).catch(angular.noop);
                }

                vm.onProjectSearch = function () {
                    var q = (vm.projectSearch || '').toLowerCase().trim();
                    if (!q) { vm.filteredProjects = []; vm.showProjectDrop = false; return; }
                    vm.filteredProjects = vm.projectList.filter(function (p) {
                        return (p.Title      && p.Title.toLowerCase().indexOf(q)       >= 0) ||
                               (p.JiraKey    && p.JiraKey.toLowerCase().indexOf(q)    >= 0) ||
                               (p.ProjectKey && p.ProjectKey.toLowerCase().indexOf(q) >= 0);
                    }).slice(0, 20);
                    vm.showProjectDrop = vm.filteredProjects.length > 0;
                };

                vm.selectProject = function (p) {
                    vm.selectedProject      = p;
                    vm.projectSearch        = p.Title || p.JiraKey;
                    vm.pet.Title            = p.Title || p.JiraKey;
                    vm.pet.JIRAProjectKey   = p.JiraKey || '';
                    vm.pet.ProjectDetailsId = p.Id;
                    vm.pet.ProjectManager   = p.Manager || '';
                    vm.showProjectDrop      = false;
                };

                vm.clearProject = function () {
                    vm.selectedProject      = null;
                    vm.projectSearch        = '';
                    vm.pet.Title            = '';
                    vm.pet.JIRAProjectKey   = '';
                    vm.pet.ProjectDetailsId = null;
                    vm.pet.ProjectManager   = '';
                    vm.filteredProjects     = [];
                    vm.showProjectDrop      = false;
                };

                // -- Type / Budget source ------------------------------------------
                vm.onTypeChange = function () {
                    vm.budgetDetail         = null;
                    vm.pet.BudgetSourceCode = '';
                    vm.typeSourceList       = [];
                    _loadTypeSourceList(vm.pet.ProjectType);
                };

                function _loadTypeSourceList(type) {
                    if (type === 'CAPEX') {
                        LookupService.getCapexList().then(function (list) {
                            vm.typeSourceList = list.map(function (c) {
                                return { Code: c.CAPEXCode, Name: c.CAPEXName,
                                         Available: c.AvailableAmount, Net: c.NetBalance };
                            });
                        });
                    } else if (type === 'OPEX') {
                        LookupService.getOpexList().then(function (list) {
                            vm.typeSourceList = list.map(function (o) {
                                return { Code: o.OPEXCode, Name: o.OPEXName,
                                         Available: o.AvailableAmount, Net: o.NetBalance };
                            });
                        });
                    }
                }

                vm.onBudgetSourceChange = function () {
                    _loadBudgetDetail(vm.pet.ProjectType, vm.pet.BudgetSourceCode);
                };

                function _loadBudgetDetail(type, code) {
                    if (!type || !code) { vm.budgetDetail = null; return; }
                    var fetch = type === 'CAPEX'
                        ? LookupService.getCapexDetail(code)
                        : LookupService.getOpexDetail(code);
                    fetch.then(function (d) { vm.budgetDetail = d; }).catch(angular.noop);
                }

                vm.fmt = function (n) {
                    if (n === null || n === undefined || isNaN(n)) return '0.00';
                    return parseFloat(n).toLocaleString('en-AE',
                        { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                };

                // -- Line items ----------------------------------------------------
                function _loadLineItems(petId) {
                    return CRUDService.getAll(LISTS.PROJECT_SIZING, {
                        select: 'Id,LineItemNo,Department,ExpHead,Topic,VendorName,Description,' +
                                'CostType,UnitType,Quantity,UnitPrice,Currency,ExchangeRate,' +
                                'FCYAmount,LCYAmount,Contingency,FinalAmountLCY,YearlyRecurrence,' +
                                'GLNumber,Comments',
                        filter: 'PETProjectId eq ' + petId,
                        orderby: 'LineItemNo asc'
                    }).then(function (items) {
                        vm.lineItems = items;
                        _calcTotals();
                    });
                }

                function _calcTotals() {
                    var t = { count: vm.lineItems.length, totalLCY: 0, totalFinal: 0 };
                    vm.lineItems.forEach(function (l) {
                        t.totalLCY   += parseFloat(l.LCYAmount      || 0);
                        t.totalFinal += parseFloat(l.FinalAmountLCY || 0);
                    });
                    vm.totals = t;
                    vm.pet.RequestedAmountAED = t.totalFinal;
                }

                vm.calcLine = function (line) {
                    var units = parseFloat(line.Quantity     || 0);
                    var price = parseFloat(line.UnitPrice    || 0);
                    var rate  = parseFloat(line.ExchangeRate || 1);
                    var cont  = parseFloat(line.Contingency  || 0) / 100;
                    line.FCYAmount      = units * price;
                    line.LCYAmount      = line.FCYAmount * rate;
                    line.FinalAmountLCY = line.LCYAmount * (1 + cont);
                };

                vm.openLineModal = function (line) {
                    if (line) {
                        vm.lineModal = { show: true, isNew: false,
                            line: angular.copy(line), orig: line };
                    } else {
                        vm.lineModal = {
                            show: true, isNew: true,
                            line: {
                                LineItemNo:       vm.lineItems.length + 1,
                                Department:       '',
                                ExpHead:          vm.pet.ProjectType || 'CAPEX',
                                Topic:            '',
                                VendorName:       '',
                                Description:      '',
                                CostType:         '',
                                UnitType:         'Man Days',
                                Quantity:         1,
                                UnitPrice:        0,
                                Currency:         'AED',
                                ExchangeRate:     1,
                                FCYAmount:        0,
                                LCYAmount:        0,
                                Contingency:      0,
                                FinalAmountLCY:   0,
                                YearlyRecurrence: 1,
                                GLNumber:         '',
                                Comments:         ''
                            }
                        };
                    }
                };

                vm.closeLineModal = function () { vm.lineModal.show = false; };

                vm.saveLine = function () {
                    var line = vm.lineModal.line;
                    vm.calcLine(line);
                    if (vm.lineModal.isNew) {
                        CRUDService.insert(LISTS.PROJECT_SIZING,
                            angular.extend({ PETProjectId: vm.pet.Id }, line)
                        ).then(function (saved) {
                            vm.lineItems.push(angular.extend({}, line, { Id: saved.Id }));
                            _calcTotals();
                            vm.lineModal.show = false;
                            NotificationService.success('Line item added.');
                        }).catch(function (e) {
                            NotificationService.error('Failed to add line: ' + _errMsg(e));
                        });
                    } else {
                        CRUDService.update(LISTS.PROJECT_SIZING, line.Id, line).then(function () {
                            angular.extend(vm.lineModal.orig, line);
                            _calcTotals();
                            vm.lineModal.show = false;
                            NotificationService.success('Line item updated.');
                        }).catch(function (e) {
                            NotificationService.error('Failed to update line: ' + _errMsg(e));
                        });
                    }
                };

                vm.deleteLine = function (line) {
                    UtilityService.confirm('Delete line #' + line.LineItemNo + '?', 'Delete Line')
                        .then(function () {
                            CRUDService.delete(LISTS.PROJECT_SIZING, line.Id).then(function () {
                                var idx = vm.lineItems.indexOf(line);
                                if (idx >= 0) vm.lineItems.splice(idx, 1);
                                _calcTotals();
                                NotificationService.success('Line deleted.');
                            });
                        });
                };

                // -- CRUD ----------------------------------------------------------
                vm.save = function (submit) {
                    vm.validationErrors = _validate(vm.pet);
                    if (vm.validationErrors.length) { vm.setTab('pet'); return; }

                    vm.saving = true;
                    var payload = _preparePayload(vm.pet);
                    var promise = isNew
                        ? CRUDService.insert(LISTS.PET_PROJECTS, payload)
                        : CRUDService.update(LISTS.PET_PROJECTS, vm.pet.Id, payload);

                    promise.then(function (saved) {
                        if (isNew && saved) {
                            vm.pet.Id = saved.Id;
                            isNew     = false;
                            vm.isNew  = false;
                        }
                        if (submit) {
                            return WorkflowService.submit(
                                vm.pet.Id, vm.pet.ReviewerUserId, vm.pet.ApproverUserId);
                        }
                    }).then(function () {
                        NotificationService.success(submit ? 'PET submitted for review.' : 'PET saved as draft.');
                        $location.path('/dashboard');
                    }).catch(function (e) {
                        NotificationService.error('Save failed: ' + _errMsg(e));
                    }).finally(function () { vm.saving = false; });
                };

                vm.recall = function () {
                    UtilityService.confirm('Recall this PET submission?', 'Recall PET').then(function () {
                        WorkflowService.recall(vm.pet.Id, 'Recalled by requestor').then(function () {
                            NotificationService.success('PET recalled.');
                            vm.$onInit();
                        });
                    });
                };

                vm.cancel = function () {
                    UtilityService.confirm('Cancel this PET? This cannot be undone.',
                        'Cancel PET', 'Yes, Cancel').then(function () {
                        WorkflowService.cancel(vm.pet.Id, 'Cancelled by requestor').then(function () {
                            NotificationService.success('PET cancelled.');
                            $location.path('/dashboard');
                        });
                    });
                };

                // -- CSV Import ----------------------------------------------------
                vm.onCSVFileChange = function (files) {
                    if (!files || !files.length) return;
                    vm.csvFile      = files[0];
                    vm.csvRows      = [];
                    vm.csvErrors    = [];
                    vm.importResult = null;
                    CSVService.parseFile(vm.csvFile).then(function (result) {
                        var mapped   = _mapCSVRows(result.data);
                        vm.csvRows   = mapped.valid;
                        vm.csvErrors = mapped.invalid;
                    });
                };

                function _mapCSVRows(rows) {
                    var valid = [], invalid = [];
                    rows.forEach(function (r, i) {
                        if (!r['Exp. Head'] && !r['Topic']) return;
                        var line = {
                            LineItemNo:       i + 1,
                            Department:       r['Department']        || '',
                            ExpHead:          r['Exp. Head']         || 'CAPEX',
                            Topic:            r['Topic']             || '',
                            VendorName:       r['Vendor']            || '',
                            Description:      r['Description']       || '',
                            CostType:         r['Cost Type']         || '',
                            UnitType:         r['Unit Type']         || '',
                            Quantity:         _num(r['Unit(s)']),
                            UnitPrice:        _num(r['Unit Price']),
                            Currency:         r['Base CY']           || 'AED',
                            ExchangeRate:     1,
                            FCYAmount:        _num(r['Amt. FCY']),
                            LCYAmount:        _num(r['Amt. LCY']),
                            Contingency:      _num(r['Cont. %']),
                            FinalAmountLCY:   _num(r['Final Amt LCY']),
                            YearlyRecurrence: _num(r['Yearly Recurrence']) || 1,
                            GLNumber:         '',
                            Comments:         ''
                        };
                        if (!line.Topic && !line.Description) {
                            invalid.push({ row: i + 2, reason: 'Missing Topic/Description', data: r });
                        } else {
                            valid.push(line);
                        }
                    });
                    return { valid: valid, invalid: invalid };
                }

                vm.runImport = function () {
                    if (!vm.csvRows.length) { NotificationService.warning('No valid rows to import.'); return; }
                    if (!vm.pet.Id) { NotificationService.warning('Save the PET header first.'); return; }

                    vm.importRunning  = true;
                    vm.importProgress = 0;
                    var done = 0, failed = 0;

                    var chain = $q.resolve();
                    vm.csvRows.forEach(function (line) {
                        chain = chain.then(function () {
                            return CRUDService.insert(LISTS.PROJECT_SIZING,
                                angular.extend({ PETProjectId: vm.pet.Id }, line)
                            ).then(function () { done++; })
                             .catch(function ()  { failed++; });
                        }).then(function () {
                            vm.importProgress = Math.round(((done + failed) / vm.csvRows.length) * 100);
                        });
                    });

                    chain.finally(function () {
                        vm.importRunning = false;
                        vm.importResult  = { success: done, failed: failed };
                        _loadLineItems(vm.pet.Id);
                        NotificationService.success(
                            'Import complete: ' + done + ' added, ' + failed + ' failed.');
                    });
                };

                // -- Private helpers -----------------------------------------------
                function _emptyPET() {
                    return {
                        Title:              '',
                        JIRAProjectKey:     '',
                        ProjectDetailsId:   null,
                        ProjectManager:     '',
                        ProjectType:        '',
                        BudgetSourceCode:   '',
                        ReviewerUserId:     '',
                        ApproverUserId:     '',
                        Remarks:            '',
                        PETStatus:          'Draft',
                        RequestedAmountAED: 0,
                        Version:            1
                    };
                }

                function _preparePayload(pet) {
                    return {
                        Title:              pet.Title || pet.JIRAProjectKey,
                        JIRAProjectKey:     pet.JIRAProjectKey    || '',
                        ProjectDetailsId:   pet.ProjectDetailsId  || null,
                        ProjectType:        pet.ProjectType       || '',
                        BudgetSourceCode:   pet.BudgetSourceCode  || '',
                        ReviewerUserId:     pet.ReviewerUserId    || null,
                        ApproverUserId:     pet.ApproverUserId    || null,
                        Remarks:            pet.Remarks           || '',
                        PETStatus:          pet.PETStatus         || 'Draft',
                        RequestedAmountAED: parseFloat(pet.RequestedAmountAED) || 0
                    };
                }

                function _validate(pet) {
                    var e = [];
                    if (!pet.ProjectDetailsId)  e.push('Please select a project.');
                    if (!pet.ProjectType)        e.push('Please select CAPEX or OPEX.');
                    if (!pet.BudgetSourceCode)   e.push('Please select a budget source.');
                    if (!pet.ApproverUserId)     e.push('Please select an approver.');
                    return e;
                }

                function _num(v) {
                    if (v === null || v === undefined || v === '') return 0;
                    return parseFloat(String(v).replace(/,/g, '')) || 0;
                }

                function _errMsg(e) {
                    return e && e.data && e.data.error
                        ? (e.data.error.message.value || e.data.error.message)
                        : String(e);
                }
            }
        ]);
}());
