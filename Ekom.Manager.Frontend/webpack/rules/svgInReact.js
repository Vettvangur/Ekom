const PATHS = require('../paths');

module.exports = () => ({
  test: /\.svg$/,
  loaders: ['babel-loader', 'svg-to-jsx-loader'],
});