interface Settings {
  timeout?: number;
  error?: boolean;
  message: string | number;
}

export default function spawnCallout(obj : Settings) {

    var messages = [
      'Aðgerð heppnaðist',
      'Villa í þjóni'
    ];

    if (obj.timeout)
      var timeout = obj.timeout;
    else if (obj.error)
      var timeout = 8000;
    else
      var timeout = 4000;

    var calloutContainer = document.getElementById('alert-container');

    //var div = document.createElement('div');

    // If supplied with a number we choose a string from our messages array 
    // corresponding to supplied number
    calloutContainer.innerText = isNaN(obj.message as number) ? obj.message : messages[obj.message];

    calloutContainer.classList.add('animation');
    calloutContainer.classList.add(obj.error ? 'error' : 'success');

    // We stagger the css classes as to retrigger the animation
    setTimeout(() => calloutContainer.classList.remove('animation', (obj.error ? 'error' : 'success')), timeout - 310);

    // In the end we remove the callout
    //setTimeout(function() {
    //    calloutContainer.innerHTML = "";
    //}, timeout);
};

// Demo: 

// spawncallout({ message: 0 });
// spawncallout({ message: 1, error: true });
// spawncallout({ message: 'YoSup', error: true, timeout: 10000 });
