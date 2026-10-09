import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const pkg = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
assert.equal(pkg.name, 'com.breadpack.breadlingo.i2');
assert.equal(pkg.license, 'MIT');
assert.match(pkg.version, /^\d+\.\d+\.\d+$/);
assert(!pkg.dependencies?.['com.unity.localization']);
for (const value of Object.values(pkg.dependencies ?? {})) assert.match(value, /^\d+\.\d+\.\d+$/);
const editor = JSON.parse(await readFile(path.join(root, 'Editor/BreadLingo.I2.Editor.asmdef'), 'utf8'));
assert.deepEqual(editor.includePlatforms, ['Editor']);
assert.deepEqual(editor.references, []);
const forbidden = /(?:\.env(?:\.|$)|credentials\.json|token\.json|I2Languages\.prefab|language\.csv|\.dll$|\.pdb$)/i;
async function inspect(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.name === '.git' || entry.name === 'artifacts' || entry.name === 'node_modules') continue;
    const file = path.join(directory, entry.name);
    assert(!forbidden.test(entry.name), `Private/vendor/binary file is forbidden: ${entry.name}`);
    if (entry.isDirectory()) await inspect(file);
    else if (/\.(cs|json|md|mjs|ya?ml)$/.test(entry.name)) {
      const text = await readFile(file, 'utf8');
      assert(!/(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----|[A-Z]:[\\/]Projects[\\/]DevilsBook)/.test(text), `Private data in ${entry.name}`);
    }
  }
}
await inspect(root);
console.log(`Package source boundary checks passed: ${pkg.name}@${pkg.version}`);
