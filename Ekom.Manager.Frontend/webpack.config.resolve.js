const path = require('path');

module.exports = {
  alias: {
    components: path.resolve('./src/components'),
    containers: path.resolve('./src/containers'),
    utilites: path.resolve('./src/utilities'),
    services: path.resolve('./src/services'),
    routes: path.resolve('./src/routes'),
    stores: path.resolve('./src/stores'),
  },
  extensions: [
    '.webpack.js',
    '.web.js',
    '.js',
    '.jsx',
    '.scss',
  ],
  modules: [
    path.resolve(__dirname),
    path.resolve(__dirname, 'src'),
    path.resolve(__dirname, 'node_modules'),
    path.resolve(__dirname, 'src/styles'),
    'node_modules',
  ],
};
