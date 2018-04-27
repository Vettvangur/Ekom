const webpack = require('webpack');
const merge = require('webpack-merge');
const path = require('path');

const common = require('./webpack.common.js');

module.exports = merge(common(true), {
  mode: 'production',
  // This is the output webpack. [name] is the key from the entry object.
  output: {
    filename: 'js/[name].min.js',
    path: path.join(__dirname, '../dist'),
  },

  plugins: [
  ],
});
