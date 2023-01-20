const PATHS = require('../paths');

module.exports = ({
  production = false
} = {}) => {
  const createPresets = () => {
    const presets = [
      "@babel/preset-env", 
      "@babel/preset-typescript",
      "@babel/preset-react"
    ];
    return presets;
  };
  const presets = createPresets();

  const plugins = production ? [
    "@babel/transform-arrow-functions",
  ] : [
    "react-hot-loader/babel",
    "@babel/transform-arrow-functions",
  ];

  return {
    exclude: PATHS.modules,
    test: /\.(js|jsx|ts|tsx)?$/,
    use: [{
      loader: 'awesome-typescript-loader',
      options: {
        cacheDirectory: true,
        presets,
        plugins,
      },
    }, ],
  }
};
