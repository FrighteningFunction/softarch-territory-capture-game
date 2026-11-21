import { test, expect } from '@playwright/test';

const key = 'territory-capture.save.v1';
const snapshot = page => page.getByTestId('snapshot').textContent().then(JSON.parse);
test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await expect(page.getByTestId('status')).toHaveText('Ready');
});

test('real WASM strategy evaluates legal simulations', async ({ page }) => {
  await page.getByRole('button', { name: 'Evaluate AI', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('AI:Orange');
});

test('save, overwrite, reload and restore every field through real JS interop', async ({ page }) => {
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('None');
  await page.getByRole('button', { name: 'Fixture move', exact: true }).click();
  const expected = await snapshot(page);
  expect(expected.state.turnNumber).toBe(1);
  expect(expected.state.currentPlayerIndex).toBe(1);
  expect(expected.state.cells.filter(cell => cell.ownerId === 0)).toHaveLength(5);
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('None');
  expect(await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).toEqual(expected);
  await page.reload();
  await expect(page.getByTestId('status')).toHaveText('Ready');
  expect((await snapshot(page)).state.turnNumber).toBe(0);
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('None');
  expect(await snapshot(page)).toEqual(expected);
});

for (const [name, json, error] of [
  ['missing', null, 'Missing'], ['corrupt', '{bad', 'InvalidData'],
  ['future version', '{"version":2}', 'UnsupportedVersion']
]) {
  test(`${name} save returns a typed error without installing partial state`, async ({ page }) => {
    const before = await snapshot(page);
    await page.evaluate(({ key, json }) => {
      if (json === null) localStorage.removeItem(key); else localStorage.setItem(key, json);
    }, { key, json });
    await page.getByRole('button', { name: 'Load', exact: true }).click();
    await expect(page.getByTestId('status')).toHaveText(error);
    expect(await snapshot(page)).toEqual(before);
  });
}

test('quota failure preserves previous save and reports failure', async ({ page }) => {
  // Interop caches resolved functions. Install a controllable wrapper before the
  // first call so the second call fails through the same cached function.
  await page.addInitScript(() => {
    const original = Storage.prototype.setItem;
    Storage.prototype.setItem = function (...args) {
      if (globalThis.storageWriteBlocked)
        throw new DOMException('Test quota failure', 'QuotaExceededError');
      return original.apply(this, args);
    };
  });
  await page.reload();
  await expect(page.getByTestId('status')).toHaveText('Ready');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('None');
  const before = await page.evaluate(key => localStorage.getItem(key), key);
  await page.getByRole('button', { name: 'Fixture move', exact: true }).click();
  await page.evaluate(() => { globalThis.storageWriteBlocked = true; });
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('Unavailable');
  expect(await page.evaluate(key => localStorage.getItem(key), key)).toBe(before);
});

test('disabled storage returns unavailable', async ({ page }) => {
  await page.evaluate(() => {
    Storage.prototype.getItem = () => { throw new DOMException('Test denied storage', 'SecurityError'); };
  });
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await expect(page.getByTestId('status')).toHaveText('Unavailable');
});
