const PATHS = require('./paths');
module.exports = {
  modules: [
    PATHS.app, 
    'node_modules',
    PATHS.modules,
    PATHS.styles,
  ],
  extensions: [
    '.ts',
    '.tsx',
    '.js',
    '.jsx',
    '.scss',
  ],
  alias: PATHS,
};
