const merge = require('webpack-merge');
const webpack = require('webpack');
const path = require('path');
//const BrowserSyncPlugin = require('browser-sync-webpack-plugin');

const common = require('./webpack.common.js');

module.exports = merge(common(false), {
  devtool: 'eval-source-map',
  watch: true,
  mode: 'development',

  // This is the output webpack. [Name] is the key from the entry object.
  output: {
    filename: '[name].js',
    path: path.join(__dirname, '../Ekom.Site/App_Plugins/EkomManager'),
  },

  plugins: [
    new webpack.HotModuleReplacementPlugin()
  ],

  //plugins: [
    // This runs BrowserSync.
    //new BrowserSyncPlugin({
    //  host: 'ekom.localhost.vettvangur.is',
    //  notify: false,
    //}),
  //],
});
