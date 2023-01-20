import * as React from 'react';

interface Props {
    ok?     : string
    cancel? : string;

    // Handle OK is mandatory
    handleOK(e? : React.FormEvent<HTMLAnchorElement>)      : void;
    handleCancel?(e? : React.FormEvent<HTMLAnchorElement>) : void;

    h1?: string;
    h2?: string;
    h3?: string;
    h4?: string;
    h5?: string;
    h6?: string;
    
    divisor? : string;
}

export function cancel() {

    this.setState({ modal: false });
}

class ModalWindow extends React.Component<Props> {

  noClick(e) {
    e.preventDefault();
  }

  render() {

    for (var x = 1; x <= 3; ++x) {
        if (this.props['h' + x]) {
            var largeHeadingType = 'h' + x;
            var largeHeadingText = this.props['h' + x];
        }
    }

    for (var x = 4; x <= 6; ++x) {
        if (this.props['h' + x]) {
            var smallHeadingType = 'h' + x;
            var smallHeadingText = this.props['h' + x];
        }
    }

    if (this.props.cancel)
        var cancelBtn =
            (<a className="button light-gray" onClick={this.props.handleCancel}>
                {this.props.cancel}
            </a>);

    return (
      <div className="modal-window-container" onClick={this.noClick}>
        <article className="modal-window center">
          {largeHeadingType ? React.createElement(largeHeadingType, null, largeHeadingText) : null}
          {smallHeadingType ? React.createElement(smallHeadingType, null, smallHeadingText) : null}
          <div className="button-container">
            {cancelBtn}
            {this.props.divisor ? <p>{this.props.divisor}</p> : null}
            <a className="button primary" onClick={this.props.handleOK}>
              {this.props.ok ? this.props.ok : 'OK'}
            </a>
          </div>
        </article>
      </div>);
  }
}

export default ModalWindow;