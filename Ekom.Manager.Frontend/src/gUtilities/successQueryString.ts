import queryString  from 'gUtilities/queryString';
import spawnCallout from 'gUtilities/spawnCallout';

var querystring = queryString();

if (querystring['success']) {

    spawnCallout({ message: 0 });
}