import { expect, test } from '@playwright/test';
import { column, createBoardViaApi, dragCard, openBoard, taskCard, uniqueName } from './support';

test('should create a task and move it to Done persistently', async ({ page, request }) => {
  const boardId = await createBoardViaApi(request, uniqueName('E2E tasks'));
  const title = uniqueName('Task');

  await openBoard(page, boardId);

  await page.getByRole('button', { name: 'New task' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Title').fill(title);
  await dialog.getByRole('button', { name: 'Save' }).click();
  await expect(dialog).toBeHidden();

  const todo = column(page, 'To do');
  const done = column(page, 'Done');
  await expect(taskCard(todo, title)).toBeVisible();

  // The card moves optimistically, before the server has answered; reloading at that point
  // would abort the in-flight UpdateTaskItem call, so wait for its response first.
  const moved = page.waitForResponse((response) => response.url().endsWith('/taskboard.v1.TaskItemService/UpdateTaskItem'));
  await dragCard(page, taskCard(todo, title), done);
  await expect(taskCard(done, title)).toBeVisible();
  await expect(taskCard(todo, title)).toHaveCount(0);
  await moved;

  // Only a reload proves the server stored the new status, not just the optimistic update.
  await page.reload();
  await expect(page.getByRole('status')).toHaveText('Live');
  await expect(taskCard(done, title)).toBeVisible();
  await expect(taskCard(todo, title)).toHaveCount(0);
});
