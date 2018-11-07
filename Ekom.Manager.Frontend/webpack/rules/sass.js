const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const postcssCssnext = require('postcss-cssnext');
const cssnano = require('cssnano');
const postcssReporter = require('postcss-reporter');
const PATHS = require('../paths');

module.exports = ({
  production = false,
} = {}) => {

  // localIdentName set the formula for CSS Modules.
  // [name]__[local]
  const localIdentName = 'localIdentName=[name]__[local]___[hash:base64:5]';

  return {
    test: (input) => {
      const isScssModule = /\.m\.scss$/.test(input);
      const isRegularScss = /\.scss$/.test(input);
      return isRegularScss && !isScssModule;
    },
    include: [
      PATHS.app,
      PATHS.styles,
    ],
    use: [
      MiniCssExtractPlugin.loader,
      {
        loader: require.resolve('css-loader'),
        options: {
          localIdentName,
          sourceMap: true,
          modules: true,
          namedExport: true,
          importLoaders: 1
        }
      },
      {
        loader: 'postcss-loader',
        options: {
          ident: 'postcss',
          plugins: [
            postcssCssnext({
              browsers: ['> 1%', 'last 2 versions']
            }),
            cssnano({
              preset: 'default'
            }),
            postcssReporter({
              clearMessages: true
            })
          ]
        }
      },
      {
        loader: require.resolve('sass-loader'),
      }
    ],
  };
};
