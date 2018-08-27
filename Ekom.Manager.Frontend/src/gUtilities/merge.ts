/**
 * Merges two string arrays, eliminating duplicates
 * http://stackoverflow.com/a/28631880
 */
export function mergeStringArrays(a: string[], b: string[]) {
    var hash = {}, i;
    for (i=0; i<a.length; i++) {
        hash[a[i]]=true;
    } 
    for (i=0; i<b.length; i++) {
        hash[b[i]]=true;
    } 
    return Object.keys(hash);
}

/**
 * Merges two arrays of objects, eliminating duplicates
 * http://stackoverflow.com/a/25120770
 */
var merge = function(array1: any[], array2: any[]) {
    var array3 = [];
    var arr = array1.concat(array2);
    var len = arr.length;
    var assoc = {};

    while(len--) {
        var itm = arr[len];

        if(!assoc[itm]) { // Eliminate the indexOf call
            array3.unshift(itm);
            assoc[itm] = true;
        }
    }

    return array3;
};

export default merge;