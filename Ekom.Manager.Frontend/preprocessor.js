// Copyright 2004-present Facebook. All Rights Reserved.

const tsc = require('typescript');
const tsConfig = require('./tsconfig.json');
const babelJest = require('babel-jest');

module.exports = {
  process(src, path) {
		const isTypeScript = path.endsWith('.ts') || path.endsWith('.tsx');
		const isJavaScript = path.endsWith('.js') || path.endsWith('.jsx');

		if (isTypeScript) {
			src = tsc.transpile(
				src,
				tsConfig.compilerOptions,
				path,
				[]
			);
		}

		if (isJavaScript || isTypeScript) {
			src = babelJest.process(src, isJavaScript ? path : 'file.js');
		}

		return src;
  },
};
