export default function createKeyboardEvent(
  callback, 
  keycode : string, 
  ctrl? : boolean, 
  alt? : boolean, 
  root?) : (e) => void {

    var keyup = function (e) {

        if ((ctrl && e.ctrlKey || !ctrl) &&
            (alt && e.altKey || !alt) &&
            e.keyCode == keycode) {

                callback();
                e.preventDefault();
        }
    };

    if (root === undefined) root = document;

    root.addEventListener('keydown', keyup);

    // Return the callback as we need to reference it when removing the event listener
    return keyup;
};
