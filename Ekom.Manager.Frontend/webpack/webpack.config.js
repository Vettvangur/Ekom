const PATHS = require('./paths');
const rules = require('./rules');
const plugins = require('./plugins');
const resolve = require('./resolve');
const devServer = require('./devServer');
const path = require('path');

module.exports = (env = {}) => {
  const isProduction = process.env.NODE_ENV === 'production';
  const isBrowser = env.browser;

  console.log(`Running webpack in ${process.env.NODE_ENV} mode on ${isBrowser ? 'browser': 'server'}`);
  
  const node = {
    __dirname: true,
    __filename: true
  };

  const prodConfig = {
    mode: 'production',
    devtool: 'cheap-module-source-map',
    context: PATHS.app,
    entry: {
      app: ['../src/index']
    },
    node,
    output: {
      path: PATHS.build,
      filename: 'js/[name].min.js',
      chunkFilename: '[name].[chunkhash:6].js', // for code splitting. will work without but useful to set
      publicPath: PATHS.build
    },
    module: {
      rules: rules({
        production: true,
      })
    },
    resolve,
    plugins: plugins({
      production: true
    })
  };

  const devConfig = {
    mode: 'development',
    devtool: 'eval-source-map',
    watch: true,
    context: PATHS.app,
    target: 'web',
    entry: {
      app: [
        'react-hot-loader/patch',
        '../src/index'
      ]
    },
    output: {
      path: PATHS.public,
      filename: '[name].js',
      //publicPath: PATHS.public
      publicPath: 'http://localhost:3777/'
    },
    module: {
      rules: rules({
        production: false
      })
    },
    resolve,
    plugins: plugins({
      production: false
    }),
    devServer
  };

  const configuration = isProduction ? prodConfig : devConfig;

  return configuration;
};
