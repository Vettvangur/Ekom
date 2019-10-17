var path = require('path');
var webpack = require('webpack');

var settings = {
  devtool: 'eval-source-map',
  watch: true,
  entry: 'js/app',
  output: {
    path: path.join(__dirname, '../Ekom.Site/Content/js'),
    filename: 'app.js'
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        exclude: /node_modules/,
        loader: 'awesome-typescript-loader',
      }
    ]
  },
  resolve: {
    modules: [
      path.resolve(__dirname, 'js'),
      path.resolve(__dirname),
      'node_modules'
    ],
    extensions: [".webpack.js", ".web.js", ".ts", ".tsx", ".js", ".jsx"],
    alias: {
    }
  }
};

module.exports = function (env) {

  if (env && env.prod) {
    settings = Object.assign(settings, {
      devtool: '',
      output: {
        path: path.join(__dirname, '../Ekom.Site/Content/js'),
        filename: 'app.min.js'
      },
      plugins: [
        new webpack.DefinePlugin({
          'process.env': {
            // This has effect on the react lib size 
            'NODE_ENV': JSON.stringify('production'),
          }
        }),
        new webpack.optimize.UglifyJsPlugin({})
      ]
    });
  }

  return settings;
};
