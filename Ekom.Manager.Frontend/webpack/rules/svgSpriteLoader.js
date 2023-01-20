const PATHS = require('../paths');

module.exports = () => ({
  test: /\.svg$/,
  use: [
    { loader: 'svg-sprite-loader', 
      options: {
        publicPath: '../../assets/fonts',
        extract: false,
      } 
    }
  ],
  include: [
    PATHS.icons,
  ]
});