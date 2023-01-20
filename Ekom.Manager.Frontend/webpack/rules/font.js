const path = require('path');
const PATHS = require('../paths');

const CURRENT_WORKING_DIR = process.cwd();
module.exports = ({
  production = false,
} = {}) => {

  return {
    test: /\.(woff(2)?|ttf|eot|svg)(\?v=\d+\.\d+\.\d+)?$/,
    exclude: [PATHS.icons],
    use: [{
      loader: require.resolve('file-loader'),
      options: {
        name: 'fonts/[name].[ext]',
      },
    }],
  };
};
