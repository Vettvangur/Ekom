/// <binding ProjectOpened='default' />
var gulp = require('gulp'),
  sass = require('gulp-sass'),
  autoprefixer = require('gulp-autoprefixer'),
  cssnano = require('gulp-cssnano'),
  header = require('gulp-header'),
  rename = require('gulp-rename');

var sassPaths = [
  'scss/**/*.scss',
  'node_modules/foundation-sites/scss'],

  sassWatch = [
    'scss/**/*.scss',
    'node_modules/foundation-sites/scss/**/*.scss'];

var css_destPath = '../Ekom.Site/Content/css';

gulp.task('sass', function () {
  return gulp.src('scss/app.scss')
    .pipe(sass({
      includePaths: sassPaths,
      errLogToConsole: true
    })
      .on('error', sass.logError))
    .pipe(autoprefixer({
      browsers: ['> 1%', 'last 2 versions', 'ie >= 9']
    }))
    .pipe(header('\ufeff'))
    .pipe(gulp.dest(css_destPath))
    .pipe(cssnano())
    .pipe(rename({ extname: '.min.css' }))
    .pipe(header('\ufeff'))
    .pipe(gulp.dest(css_destPath));
});

gulp.task('default', ['sass'], function () {
  gulp.watch(sassWatch, ['sass']);
});
