module.exports = () => ({
  plugins: [
    require('postcss-cssnext'),
    require('cssnano')({
      preset: 'default',
    }),
  ],
});
