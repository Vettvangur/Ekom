const typescript = require('./typescript');
const sass = require('./sass');
const font = require('./font');
const svgInReact = require('./svgInReact');
// const css = require('./css');

module.exports = ({
  production = false
} = {}) => (
  [
    typescript({
      production
    }),
    sass({
      production
    }),
    font(),
    svgInReact(),
    // css({
    //   production
    // }),
  ]
);
