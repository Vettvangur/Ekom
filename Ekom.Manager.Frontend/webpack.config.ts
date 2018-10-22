// tslint:disable:no-var-requires
// tslint:disable:no-console
/// <binding ProjectOpened='Watch - Development' />

const path = require('path');
const webpack = require('webpack');
const HardSourceWebpackPlugin = require('hard-source-webpack-plugin');
const { CheckerPlugin } = require('awesome-typescript-loader');
const BrowserSyncPlugin = require('browser-sync-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

var plugins = [
  function() {
    this.plugin('watch-run', function(watching, callback) {
      console.log('Begin compile at ' + new Date());
      callback();
    });
  },
  new CheckerPlugin(),
  new BrowserSyncPlugin({
    host: 'ekom.localhost.vettvangur.is',
    port: 3012,
    files: ['../Ekom.Site/Views/EkomManager/**/*.cshtml', 'scss/**/*.scss'],
  }),
  new MiniCssExtractPlugin({
    filename: `../css/[name].css`,
    chunkFilename: '[id].css',
  }),
  new webpack.WatchIgnorePlugin([/s?css\.d\.ts$/]),
];

const babelPresets = ['react', ['env', { modules: false }]];

var settings = {
  mode: 'development',
  devtool: 'eval-source-map',
  watch: true,
  entry: {
    app: 'src/index',
  },
  output: {
    path: path.join(__dirname, '../Ekom.Site/App_Plugins/EkomManager/js'),
    filename: '[name].js',
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        exclude: /node_modules/,
        loader: 'awesome-typescript-loader',
        options: {
          useCache: true,
          useBabel: true,
          babelOptions: {
            babelrc: false /* Important line */,
            presets: babelPresets,
          },
        },
      },
      {
        test: /\.jsx?$/,
        exclude: /node_modules/,
        loader: 'babel-loader',
        options: {
          presets: babelPresets,
        },
      },
      {
        test: /\.s?css$/,
        include: [
          path.resolve(__dirname, 'styles'),
          path.resolve(__dirname, 'src'),
        ],
        exclude: [/node_modules/],
        use: [
          MiniCssExtractPlugin.loader,
          {
            loader: 'typings-for-css-modules-loader',
            options: {
              importLoaders: 1,
              // To enable CSS Modules set modules to true
              modules: true,
              namedExport: true, //
              // localIdentName set the formula for CSS Modules.
              // In production classes are set to random hash to save bytes.
              // localIdentName: isProd ? '[hash:base64:5]' : '[name]__[local]',
              localIdentName: '[name]__[local]',
            },
          },
          ///cSettings for PostCSS are in the file postcss.config.js
          'postcss-loader',
          'sass-loader',
        ],
      },
      {
        test: /\.svg$/,
        loaders: ['babel-loader', 'svg-to-jsx-loader'],
      },
      {
        test: /\.(woff(2)?|ttf|eot|svg)(\?v=\d+\.\d+\.\d+)?$/,
        use: [{
          loader: 'file-loader',
          options: {
            name: '[name].[ext]',
            publicPath: '../assets/fonts',
            outputPath: '/assets/fonts/',
          },
        }],
      },
    ],
  },
  plugins,
  resolve: {
    modules: [
      path.resolve(__dirname, 'src'),
      path.resolve(__dirname),
      'node_modules',
      path.resolve(__dirname),
      'styles',
      path.resolve(__dirname),
      'assets',
    ],
    extensions: [
      '.webpack.js',
      '.web.js',
      '.ts',
      '.tsx',
      '.js',
      '.jsx',
      '.scss',
    ],
    alias: {},
  },
};

module.exports = function(env) {
  if (env && env.prod) {
    // Don't typecheck when building production files
    Object.assign(settings.module.rules[0].options, {
      forceIsolatedModules: true,
      transpileOnly: true,
      useTranspileModule: true,
    });

    Object.assign(settings, {
      mode: 'production',
      watch: false,
      devtool: 'source-map',
      output: {
        path: path.join(__dirname, '../Ekom.Site/App_Plugins/EkomManager/js'),
        filename: '[name].min.js',
      },
      plugins: plugins,
    });
  }

  return settings;
};
