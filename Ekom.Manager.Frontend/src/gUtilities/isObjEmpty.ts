/* ========================
 *  Is Object Empty Helper
 * ======================== */

/**
 * True if empty
 */
export default function isObjEmpty(obj : Object) {

    if (!obj || obj && Object.keys(obj).length === 0 && obj.constructor === Object)
        return true;

    return false;
};