import { APIRequestContext, Locator, Page, expect } from '@playwright/test';
import { apiUrl } from '../playwright.config';

// All tests of a run share one database (and run in parallel), so every name must be unique
// or a locator could match a board/task created by another test.
export function uniqueName(prefix: string): string {
  return `${prefix} ${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
}

// REST via JSON transcoding: much faster than the UI for setup that is not under test.
export async function createBoardViaApi(request: APIRequestContext, name: string): Promise<number> {
  const response = await request.post(`${apiUrl}/v1/boards`, { data: { name } });
  expect(response.ok()).toBeTruthy();
  const board = (await response.json()) as { id: number };
  return board.id;
}

export async function createTaskViaApi(request: APIRequestContext, boardId: number, title: string): Promise<number> {
  const response = await request.post(`${apiUrl}/v1/boards/${boardId}/tasks`, { data: { title } });
  expect(response.ok()).toBeTruthy();
  const task = (await response.json()) as { id: number };
  return task.id;
}

// "Live" means the WatchBoard stream is open and the task list was resynced. The stream sends
// changes only (no snapshot), so anything done before that point would never reach this page.
export async function openBoard(page: Page, boardId: number): Promise<void> {
  await page.goto(`/boards/${boardId}`);
  await expect(page.getByRole('status')).toHaveText('Live');
}

export function column(page: Page, name: 'To do' | 'In progress' | 'Done'): Locator {
  return page.getByRole('region', { name });
}

export function taskCard(scope: Locator, title: string): Locator {
  // `has` is resolved relative to each card, so the inner locator must start from the page.
  return scope.locator('app-task-card').filter({ has: scope.page().getByRole('heading', { name: title, exact: true }) });
}

// locator.dragTo() does not move a CDK drag-drop card: CDK only starts a drag after a series of
// pointer moves past its 5px threshold, while dragTo jumps straight to the target.
export async function dragCard(page: Page, card: Locator, target: Locator): Promise<void> {
  const from = await card.boundingBox();
  const to = await target.boundingBox();
  if (!from || !to) throw new Error('Card or target column is not visible');

  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
  await page.mouse.down();
  await page.mouse.move(to.x + to.width / 2, to.y + 80, { steps: 20 });
  await page.mouse.up();
}
