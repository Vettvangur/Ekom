const path = require('path');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const OptimizeCSSAssetsPlugin = require('optimize-css-assets-webpack-plugin');

module.exports = isProd => {
  return {
    // These are the files wepack imports. The keys are the known as [name] and
    // are the final filenames. The values are the path to each file.
    entry: {
      site: ['babel-polyfill', 'src/app']
    },

    module: {
      rules: [
        {
          // This transpiles the js and jsx files.
          test: /\.jsx?$/,
          exclude: /node_modules/,
          loader: 'babel-loader',
          options: {
            // Settings for babel-loder are in the file .babelrc
            babelrc: true,
          },
        },
        {
          test: /\.s?css$/,
          include: [
            path.resolve(__dirname, 'styles'),
            path.resolve(__dirname, 'src'),
          ],
          exclude: /node_modules/,
          use: [
            MiniCssExtractPlugin.loader,
            {
              loader: 'css-loader',
              options: {
                minimize: isProd,
                importLoaders: 1,
                // To enable CSS Modules set modules to true
                modules: true,

                // localIdentName set the formula for CSS Modules.
                // In production classes are set to random hash to save bytes.
                localIdentName: isProd ? '[hash:base64:6]' : '[name]__[local]',
              },
            },
            // Settings for PostCSS are in the file postcss.config.js
            'postcss-loader',
            'sass-loader',
          ],
        },
      ],
    },
    resolve: {
      modules: [
        path.resolve(__dirname),
        path.resolve(__dirname, 'src'),
        path.resolve(__dirname, 'node_modules'),
        path.resolve(__dirname, 'src/styles'),
      ],
      extensions: [
        '.webpack.js',
        '.web.js',
        '.js',
        '.jsx',
        '.scss',
      ],
      alias: {
        components: path.resolve('./src/components'),
        routes: path.resolve('./src/routes'),
        stores: path.resolve('./src/stores'),
      },
    },

    plugins: [

      new MiniCssExtractPlugin({
        filename: 'css/[name].css',
        chunkFilename: '[id].css',
      }),

      // CopyWebpackPlugin copies all the files from set folder to another. The
      // from path is relative to the entry point and to is relative to the outputb folder.
      new CopyWebpackPlugin([{ from: 'src/assets', to: 'assets' }]),
    ],
    optimization: {
      minimizer: [new OptimizeCSSAssetsPlugin({})],
    },
  };
};
