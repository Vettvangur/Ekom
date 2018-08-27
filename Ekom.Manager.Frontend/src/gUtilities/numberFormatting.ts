/**
 * Number With Delimiter Helper
 * https://stackoverflow.com/questions/2901102/how-to-print-a-number-with-commas-as-thousands-separators-in-javascript?page=1&tab=votes#tab-top
 * 
 * @param num 
 * @param delimiter 
 */
export default function(num: number, delimiter?: string) {
  delimiter = delimiter || '.';
  
  return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, delimiter);
};
