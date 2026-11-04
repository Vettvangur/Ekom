import { copyFile } from 'node:fs/promises';

await copyFile(
  new URL('../../App_Plugins/Ekom/dist/printing-section-view.element.js', import.meta.url),
  new URL('../../../Ekom.Web/App_Plugins/Ekom/Manager/libs/printing-section-view.element.js', import.meta.url),
);
