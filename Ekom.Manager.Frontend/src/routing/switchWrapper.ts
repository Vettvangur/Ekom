/**
 * SwitchWrapper removes the render prop and leaves transition props used by CSSTransition?
 */
const SwitchWrapper = ({ render, ...rest }) => render(rest);

export default SwitchWrapper;
