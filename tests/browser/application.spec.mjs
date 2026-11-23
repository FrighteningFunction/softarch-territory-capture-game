// Real application acceptance checks. Never point this project at BrowserHarness.
// C's read-only DOM hooks are documented in README.md next to these tests.
import { test, expect } from '@playwright/test';

const key = 'territory-capture.save.v1';
async function start(page, kinds) {
  await page.goto('/');
  await page.getByTestId('player-count').selectOption(String(kinds.length));
  for (let i = 0; i < kinds.length; i++)
    await page.getByTestId(`player-kind-${i}`).selectOption(kinds[i]);
  await page.getByTestId('start-game').click();
  await expect(page.getByTestId('cell')).toHaveCount(120);
}
async function boardState(page) {
  const board = page.getByTestId('board');
  return {
    player: await board.getAttribute('data-current-player-id'),
    turn: await board.getAttribute('data-turn-number'),
    cells: await page.getByTestId('cell').evaluateAll(cells => cells.map(cell => ({
      id: Number(cell.dataset.cellId), color: Number(cell.dataset.color),
      ownerId: cell.dataset.ownerId === '' ? null : Number(cell.dataset.ownerId)
    })).sort((a, b) => a.id - b.id)),
    scores: await page.getByTestId('player-score').evaluateAll(players => players.map(player => ({
      id: Number(player.dataset.playerId), score: Number(player.dataset.score)
    })).sort((a, b) => a.id - b.id))
  };
}

test('real human move updates the board, scores and turn', async ({ page }) => {
  await start(page, ['Human', 'Human']);
  await page.getByTestId('choose-color').and(page.locator(':enabled')).first().click();
  await expect(page.getByTestId('board')).toHaveAttribute('data-turn-number', '1');
  await expect(page.getByTestId('board')).toHaveAttribute('data-current-player-id', '1');
  const state = await boardState(page);
  expect(state.cells.map(cell => cell.id)).toEqual(Array.from({ length: 120 }, (_, id) => id));
  expect(state.scores).toHaveLength(2);
  for (const player of state.scores)
    expect(player.score).toBe(state.cells.filter(cell => cell.ownerId === player.id).length);
});

test('computer turns run automatically and hand control back to the human', async ({ page }) => {
  await start(page, ['Human', 'Computer', 'Computer']);
  await page.getByTestId('choose-color').and(page.locator(':enabled')).first().click();
  await expect(page.getByTestId('board')).toHaveAttribute('data-turn-number', '3');
  await expect(page.getByTestId('board')).toHaveAttribute('data-current-player-id', '0');
  await expect(page.getByTestId('save-game')).toBeEnabled();
});

test('real save/reload/resume restores every cell, score and the next player', async ({ page }) => {
  await start(page, ['Human', 'Human']);
  await page.getByTestId('choose-color').and(page.locator(':enabled')).first().click();
  await expect(page.getByTestId('board')).toHaveAttribute('data-turn-number', '1');
  const before = await boardState(page);
  await page.getByTestId('save-game').click();
  await expect(page.getByTestId('operation-status')).toHaveAttribute('data-result', 'success');
  const saved = await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key);
  expect(saved.version).toBe(1);
  expect(saved.state.cells).toEqual(before.cells);
  expect(String(saved.state.currentPlayerIndex)).toBe(before.player);
  expect(String(saved.state.turnNumber)).toBe(before.turn);
  await page.reload();
  await page.getByTestId('resume-game').click();
  await expect(page.getByTestId('cell')).toHaveCount(120);
  expect(await boardState(page)).toEqual(before);
  expect(await page.evaluate(key => JSON.parse(localStorage.getItem(key)), key)).toEqual(saved);
});
