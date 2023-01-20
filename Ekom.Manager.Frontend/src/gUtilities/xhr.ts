/*
 * XHR Wrapper
 * Gunnar Már Óttarsson 2017
 */

export interface P1 {
    type?: string;
    method?: string;
    url: string;
    responseType?: XMLHttpRequestResponseType;
    callback?(xhrObj : XMLHttpRequest | XhrLike): () => void;
    contentType?: string;
    defaultContentType?: boolean;
}

interface XhrLike {
    status : number;
    response : any;
}

export default function constructXhr(p1 : P1 | string, 
                    callback : (xhrObj : XMLHttpRequest | XhrLike) => void) : XMLHttpRequest {

    var xhr = new XMLHttpRequest();

    // Wrapper function that encapsulates the consumers onload callback
    // Used to fix older IE incompatibility with xhr.responseType = 'json'
    var onloadWrapper = function(e : Event) {

        var xhr = e.target as XMLHttpRequest;
        

            // Determine if browser supports responseType param
        if ((xhr.responseType !== (p1 as P1).responseType && xhr.responseType !== 'json')) {

            var newXhr : XhrLike = {
                response: JSON.parse(xhr.response),
                status: xhr.status
            };

            // If the callback parameter was left out it must be in the P1 obj
            callback ? callback(newXhr) : (p1 as P1).callback(newXhr);
        }
        else
            callback ? callback(xhr) : (p1 as P1).callback(xhr);
    };

    if (typeof p1 === 'object') {
      
      var type = p1.type || p1.method || 'POST';

      xhr.open(type, p1.url);
      xhr.responseType = p1.responseType || 'json';

      // Use the default request header when transmitting multipart MIME encoded data
      // Usage: Specify the contentType explicitly with p1.contentType
      // set no flags to use our default of application/x-www-form-urlencoded
      // set p1.defaultContentType to use XHR default of multipart/form-data
      // when transmitting files
      if (!p1.defaultContentType)
          xhr.setRequestHeader('Content-type', p1.contentType ||
												'application/x-www-form-urlencoded');
    }
    else {
		xhr.open('POST', p1);
		xhr.responseType = 'json';
		xhr.setRequestHeader('Content-type', 'application/x-www-form-urlencoded');
    }

	xhr.onload = onloadWrapper;

	return xhr;
};

// Demo onload callback
/*
	var myCallback = xhr => {
		if (xhr.status >= 200 && xhr.status < 300) {
			console.log('success');
		}
		else console.log('error');
		AlwaysRunThis();
	};
*/

// Simple example usage - send application/x-www-form-urlencoded with POST
/*
	var formData = 'param1=' + encodeURIComponent(paramStr1) + '&param2=' + encodeURIComponent(paramStr2);
	var constructXhr = require('utilities/xhr.ts');
	var xhr = constructXhr('/umbraco/surface/test/action', myCallback);
	xhr.send(formData);
*/

// Example usage - send a js obj
/*
	var myObj = { prop1: 'a', prop2: 1 };
	var constructXhr = require('utilities/xhr.ts');
	var xhr = constructXhr({
		type: 'POST',
		url: '/umbraco/surface/test/action',
		responseType: 'json',
		contentType: 'application/json',
	}, myCallback);
	xhr.send(JSON.stringify(myObj));
*/