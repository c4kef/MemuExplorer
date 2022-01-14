/* Utilities */

var RANDOM = function() {};

function _randomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function _randomHex(len) {
    var hex = '0123456789abcdef';
    var output = '';
    for (var i = 0; i < len; ++i) {
        output += hex.charAt(Math.floor(Math.random() * hex.length));
    }
    return output;
}

function _pad(n, width) {
    n = n + "";
    return n.length >= width ? n : new Array(width - n.length + 1).join("0") + n;
}

function _randomPaddedInt(length) {
    return _pad(_randomInt(0, Math.pow(10, length)), length);
}

function _luhn_getcheck(code) {
    code = String(code).concat("0");
    var len = code.length;
    var parity = len % 2;
    var sum = 0;
    for (var i = len - 1; i >= 0; i--) {
        var d = parseInt(code.charAt(i))
        if (i % 2 == parity) {
            d *= 2;
        }
        if (d > 9) {
            d -= 9;
        }
        sum += d;
    }
    var checksum = sum % 10;
    return checksum == 0 ? 0 : 10 - checksum;
}

function _luhn_verify(code) {
    code = String(code);
    var len = code.length;
    var parity = len % 2;
    var sum = 0;
    for (var i = len - 1; i >= 0; i--) {
        var d = parseInt(code.charAt(i))
        if (i % 2 == parity) {
            d *= 2;
        }
        if (d > 9) {
            d -= 9;
        }
        sum += d;
    }
    return sum % 10 == 0;
}


/* Spoofing functions */

function spoofAndroidID(android_id) {
    if (android_id == RANDOM) {
        android_id = _randomHex(16);
    } else if (android_id !== null) {
        android_id = String(android_id).toLowerCase();
        if (! android_id.match(/^[0-9a-f]{16}$/)) {
            throw new Error("Invalid Android ID value");
        }
    }
    var ss = Java.use("android.provider.Settings$Secure");
    ss.getString.overload("android.content.ContentResolver", "java.lang.String").implementation = function(context, param) {
        if (param == ss.ANDROID_ID.value) {
            return android_id;
        } else {
            return this.getString(context, param);
        }
    }
}

function spoofPhone(phone) {
    if (phone === RANDOM) {
        phone = _randomPaddedInt(10);
    } else if (phone !== null) {
        phone = String(phone);
        if (! phone.match(/^[0-9]{1,15}$/)) {
            throw new Error("Invalid phone number");
        }
    }
    var tm = Java.use("android.telephony.TelephonyManager");
    tm.getLine1Number.overload().implementation = function() {
        return phone;
    }
}

function spoofIMEI(imei) {
    if (imei === RANDOM) {
        imei = _randomPaddedInt(14);
        imei = imei.concat(_luhn_getcheck(imei));
    } else if (imei !== null) {
        imei = String(imei);
        if (! imei.match(/^[0-9]{15}$/)) {
            throw new Error("Invalid IMEI value");
        }
        if (! _luhn_verify(imei)) {
            console.warn("IMEI has an invalid check digit");
        }
    }
    var tm = Java.use("android.telephony.TelephonyManager");
    tm.getDeviceId.overload().implementation = function() {
        return imei;
    }
    tm.getDeviceId.overload("int").implementation = function(slotIndex) {
        return imei;
    }
    tm.getImei.overload().implementation = function() {
        return imei;
    }
    tm.getImei.overload("int").implementation = function(slotIndex) {
        return imei;
    }
}

function spoofIMSI(imsi) {
    if (imsi == RANDOM) {
        imsi = _randomPaddedInt(15);
    } else if (imsi !== null) {
        imsi = String(imsi);
        if (! imsi.match(/^[0-9]{14,15}$/)) {
            throw new Error("Invalid IMSI value");
        }
    }
    var tm = Java.use("android.telephony.TelephonyManager");
    tm.getSubscriberId.overload().implementation = function() {
        return imsi;
    }
}

function spoofICCID(iccid) {
    if (iccid == RANDOM) {
        iccid = "89".concat(_randomPaddedInt(16));
        iccid = iccid.concat(_luhn_getcheck(iccid));
    } else if (iccid !== null) {
        iccid = String(iccid);
        if (! iccid.match(/^[0-9]{19,20}$/)) {
            throw new Error("Invalid ICCID value");
        }
        if (! _luhn_verify(iccid)) {
            console.warn("ICCID has an invalid check digit");
        }
    }
    var tm = Java.use("android.telephony.TelephonyManager");
    tm.getSimSerialNumber.overload().implementation = function() {
        return iccid;
    }
}

function spoofMAC(mac) {
    if (mac == RANDOM) {
        mac = [];
        for (var i = 0; i < 6; i++) {
            mac.push(_randomInt(0, 255));
        }
        mac = Java.array("byte", mac);
    } else if (mac !== null) {
        var mac_str = String(mac).toUpperCase();
        if (! mac_str.match(/^([0-9A-F]{2}:){5}[0-9A-F]{2}$/)) {
            throw new Error("Invalid MAC address value");
        }
        mac = [];
        var mac_arr = mac_str.split(":");
        for (var i = 0; i < 6; i++) {
            mac.push(parseInt(mac_arr[i], 16));
        }
        mac = Java.array("byte", mac);
    }
    var ni = Java.use("java.net.NetworkInterface");
    ni.getHardwareAddress.overload().implementation = function() {
        return mac;
    }
}

function hideGSFID() {
    var cr = Java.use("android.content.ContentResolver");
    cr.query.overload("android.net.Uri", "[Ljava.lang.String;", "java.lang.String", "[Ljava.lang.String;", "java.lang.String", "android.os.CancellationSignal").implementation = function(uri, projection, selection, selectionArgs, sortOrder, cancellationSignal) {
        var qres = this.query(uri, projection, selection, selectionArgs, sortOrder, cancellationSignal);
        if (uri.toString() == "content://com.google.android.gsf.gservices") {
            qres = null;
        }
        return qres;
    }
    cr.query.overload("android.net.Uri", "[Ljava.lang.String;", "java.lang.String", "[Ljava.lang.String;", "java.lang.String").implementation = function(uri, projection, selection, selectionArgs, sortOrder) {
        var qres = this.query(uri, projection, selection, selectionArgs, sortOrder);
        if (uri.toString() == "content://com.google.android.gsf.gservices") {
            qres = null;
        }
        return qres;
    }
}

Java.perform(function () {
    spoofMAC(RANDOM);
    spoofICCID(RANDOM);
    spoofIMSI(RANDOM);
    spoofAndroidID(RANDOM);
    spoofIMEI(RANDOM);
    spoofPhone(RANDOM);
    hideGSFID();
});
