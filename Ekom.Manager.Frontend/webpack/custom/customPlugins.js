const webpack = require('webpack');


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
      new ForkTsCheckerWebpackPlugin(),
      new BundleAnalyzerPlugin({
        analyzerPort: 9999
      }),
    ]
  }
  if (production) {
    return [
    ];
  }
  return [];
}
