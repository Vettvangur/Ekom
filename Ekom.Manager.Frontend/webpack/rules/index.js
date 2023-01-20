const typescript = require('./typescript');
const sass = require('./sass');
const font = require('./font');
const svgInReact = require('./svgInReact');
const svgSpriteLoader = require('./svgSpriteLoader');
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
    svgSpriteLoader(),
    font(),
    // css({
    //   production
    // }),
  ]
);
