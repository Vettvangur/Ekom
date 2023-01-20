import queryString  from './queryString';
import spawnCallout from './spawnCallout';

var querystring = queryString();

if (querystring['success']) {

    spawnCallout({ message: 0 });
}