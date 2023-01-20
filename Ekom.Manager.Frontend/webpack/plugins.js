const webpack = require('webpack');
const BundleAnalyzerPlugin = require('webpack-bundle-analyzer').BundleAnalyzerPlugin;
const MiniCssExtractPlugin = require('mini-css-extract-plugin');


module.exports = ({
  production = false
} = {}) => {
  const bannerOptions = {
    raw: true,
    banner: 'require("source-map-support").install();'
  };
  const compress = {
    warnings: false
  };
  const compileTimeConstantForMinification = {
    __PRODUCTION__: JSON.stringify(production)
  };

  if (!production) {
    return [
      new webpack.HotModuleReplacementPlugin(),
      new webpack.NamedModulesPlugin(),
      new MiniCssExtractPlugin({
        filename: `css/[name].styles.css`,
        chunkFilename: '[id].css',
      }),
      new BundleAnalyzerPlugin({
        analyzerPort: 9999
      }),
    ];
  }
  if (production) {
    return [
      new webpack.NamedModulesPlugin(),
      new MiniCssExtractPlugin({
        filename: `css/[name].min.css`,
        chunkFilename: '[id].css',
      }),
      new webpack.DefinePlugin({
        'process.env.PRODUCTION': JSON.stringify(process.env.NODE_ENV === 'production'),
      }),
    ];
  }
  return [];
}
