/**
 * UtilityService.js – Common reusable helper methods.
 */
(function () {
    'use strict';
    angular.module('PETApp.services')
        .service('UtilityService', ['$q', '$rootScope', 'APP_CONST', function ($q, $rootScope, APP_CONST) {
            var self = this;

            /** Generates a PET reference number e.g. PET-2026-00042 */
            self.generatePETRef = function (id) {
                return 'PET-' + new Date().getFullYear() + '-' + String(id).padStart(5, '0');
            };

            /** Formats a number as AED currency string */
            self.formatAED = function (value) {
                if (value === null || value === undefined || isNaN(value)) return 'AED 0.00';
                return 'AED ' + parseFloat(value).toLocaleString('en-AE',
                    { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            };

            /** Formats an ISO date string to DD/MM/YYYY */
            self.formatDate = function (isoDate) {
                if (!isoDate) return '';
                var d = new Date(isoDate);
                if (isNaN(d)) return '';
                var dd = String(d.getDate()).padStart(2, '0');
                var mm = String(d.getMonth() + 1).padStart(2, '0');
                return dd + '/' + mm + '/' + d.getFullYear();
            };

            /** Calculates PET line item totals */
            self.calcLine = function (line) {
                line.FCYAmount    = (parseFloat(line.Quantity) || 0) * (parseFloat(line.UnitPrice) || 0);
                line.LCYAmount    = line.FCYAmount * (parseFloat(line.ExchangeRate) || 1);
                line.FinalAmountLCY = line.LCYAmount * (1 + (parseFloat(line.Contingency) || 0) / 100);
                return line;
            };

            /** Deep clone an object */
            self.clone = function (obj) { return JSON.parse(JSON.stringify(obj)); };

            /** Returns true if two dates are within N days of each other */
            self.isLongPending = function (submittedDate, days) {
                if (!submittedDate) return false;
                var diff = (new Date() - new Date(submittedDate)) / (1000 * 60 * 60 * 24);
                return diff > (days || 14);
            };

            /**
             * Shows the global ConfirmDialog and returns a promise.
             * Resolves if the user clicks OK, rejects if they click Cancel.
             * @param {string} message   Body text
             * @param {string} [title]   Dialog title (default "Confirm")
             * @param {string} [okLabel] OK button label (default "Confirm")
             */
            self.confirm = function (message, title, okLabel) {
                var deferred = $q.defer();
                $rootScope.confirmDialog = {
                    visible:     true,
                    message:     message,
                    title:       title   || 'Confirm',
                    okLabel:     okLabel || 'Confirm',
                    cancelLabel: 'Cancel',
                    ok: function () {
                        $rootScope.confirmDialog.visible = false;
                        deferred.resolve();
                    },
                    cancel: function () {
                        $rootScope.confirmDialog.visible = false;
                        deferred.reject();
                    }
                };
                return deferred.promise;
            };
        }]);
}());
