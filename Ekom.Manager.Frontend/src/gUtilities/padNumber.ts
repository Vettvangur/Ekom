/**
 * Pads an integer with leading zeros
 * https://stackoverflow.com/questions/2998784/how-to-output-integers-with-leading-zeros-in-javascript
 * 
 * @param num Number to pad with zeros
 * @param size length of target number
 */
export default function pad(num: number, size: number) {
    var s = "000000000" + num;
    return s.substr(s.length-size);
}