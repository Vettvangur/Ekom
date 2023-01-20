import * as React from 'react';
import * as ReactDOM from 'react-dom';

interface Props {
	portalId?: string;
  portalClass?: string;
	elementType?: string;

  /**
   * Create a react root container under the target id or class
   */
  createContainerInTarget?: boolean;
}

/**
 * Simple portal as demonstrated by Dan Abromov
 * http://stackoverflow.com/a/28823590/5663961
 */
class Portal extends React.Component<Props> {
  _portalElement: Element = null;

  componentDidMount() {

    var {
      portalId,
      portalClass,
      elementType,
      createContainerInTarget,
    } = this.props;
    
    var portal = portalId 
            && document.getElementById(portalId) 
            || portalClass 
            && document.getElementsByClassName(portalClass)[0];

		// Create and append to body
    if (!portal) {
      portal = document.createElement(elementType || 'div');
      portal.id = portalId;
      document.body.appendChild(portal);
    }
    else if (createContainerInTarget) {
      var target = portal;
      portal = document.createElement(elementType || 'div');

      if (portalId)
        portal.id = portalId;
      target.appendChild(portal);
    }

    this._portalElement = portal;
    this.componentDidUpdate();
  }

  componentWillUnmount() {

    try {
      document.body.removeChild(this._portalElement);
    }
    catch(ex) {
      // console.log('Unable to remove child from document: \n' + ex);
    }
  }

  componentDidUpdate() {

    ReactDOM.render(<div>{this.props.children}</div>, this._portalElement);
  }

  render() { return null; }
}

export default Portal;