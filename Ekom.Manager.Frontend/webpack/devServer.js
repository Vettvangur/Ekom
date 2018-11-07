const webpack = require('webpack');
const path = require('path');

module.exports = {
  contentBase: path.resolve(__dirname, '../build'),
  compress: true,
  port: 3777,
  hot: true,
  open: false,
  headers: {
    'Access-Control-Allow-Origin': '*'
  }
}
