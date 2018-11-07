const path = require('path');
const CURRENT_WORKING_DIR = process.cwd();

module.exports = {
  app: path.resolve(CURRENT_WORKING_DIR, 'src'),
  styles: path.resolve(CURRENT_WORKING_DIR, 'styles'),
  components: path.resolve(CURRENT_WORKING_DIR, 'src/components'),
  containers: path.resolve(CURRENT_WORKING_DIR, 'src/containers'),
  models: path.resolve(CURRENT_WORKING_DIR, 'src/models'),
  routing: path.resolve(CURRENT_WORKING_DIR, 'src/routing'),
  stores: path.resolve(CURRENT_WORKING_DIR, 'src/stores'),
  utilities: path.resolve(CURRENT_WORKING_DIR, 'src/utilities'),
  gUtilities: path.resolve(CURRENT_WORKING_DIR, 'src/gUtilities'),
  public: path.resolve(CURRENT_WORKING_DIR, 'build'),
  modules: path.resolve(CURRENT_WORKING_DIR, 'node_modules')
};
