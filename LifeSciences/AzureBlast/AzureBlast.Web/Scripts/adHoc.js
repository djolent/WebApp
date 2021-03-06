
var BlastPortal = BlastPortal || {};

(function (namespace) {
    'use strict';

    namespace.dates = {
        formatDate: function (date) {
            
            if (date === null || date === undefined) {
                return "";
            }

            if (date.getFullYear() === 1970) {
                return "";
            }

            return "" +
                namespace.strings.pad(date.getHours(), 2) + ":" +
                namespace.strings.pad(date.getMinutes(), 2) + ":" +
                namespace.strings.pad(date.getSeconds(), 2) + " " +
                namespace.strings.pad(date.getDate(), 2) + "/" +
                namespace.strings.pad(date.getMonth(), 2) + "/" +
                date.getFullYear();
        }
    }

    namespace.strings = {
        pad: function (n, width, z) {
            z = z || '0';
            n = n + '';

            return n.length >= width ? n : new Array(width - n.length + 1).join(z) + n;
        }
    };

}(BlastPortal));
