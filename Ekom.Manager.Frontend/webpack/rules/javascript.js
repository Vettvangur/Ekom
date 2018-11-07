const PATHS = require('../paths');

module.exports = ({
  production = false,
} = {}) => {
  const createPresets = () => {
    const presets = ['react', 'env'];
    return presets;
  };
  const presets = createPresets();

  const plugins = production ? [] : [];

  return {
    exclude: PATHS.modules,
    test: /\.js$|\.jsx$/,
    use: {
      loader: "babel-loader",
      options: {
        presets,
        plugins,
        cacheDirectory: true,
      }
    },
  };
};
