import { BrowserContext, expect, test } from '@playwright/test';
import { column, createBoardViaApi, dragCard, openBoard, taskCard, uniqueName } from './support';

test('should show changes from one user to another without reload', async ({ browser, request }) => {
  const boardId = await createBoardViaApi(request, uniqueName('E2E live'));
  const title = uniqueName('Live task');

  // Two contexts = two independent users (no shared storage, separate connections).
  const contexts: BrowserContext[] = [];
  try {
    contexts.push(await browser.newContext(), await browser.newContext());
    const [pageA, pageB] = await Promise.all(contexts.map((context) => context.newPage()));

    // Both must be "Live" first: WatchBoard sends changes only, so B would miss anything
    // A does before B's stream is subscribed.
    await Promise.all([openBoard(pageA, boardId), openBoard(pageB, boardId)]);

    await pageA.getByRole('button', { name: 'New task' }).click();
    const dialog = pageA.getByRole('dialog');
    await dialog.getByLabel('Title').fill(title);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await expect(dialog).toBeHidden();

    await expect(taskCard(column(pageB, 'To do'), title)).toBeVisible();

    await dragCard(pageA, taskCard(column(pageA, 'To do'), title), column(pageA, 'Done'));
    await expect(taskCard(column(pageA, 'Done'), title)).toBeVisible();

    await expect(taskCard(column(pageB, 'Done'), title)).toBeVisible();
    await expect(taskCard(column(pageB, 'To do'), title)).toHaveCount(0);
  } finally {
    await Promise.all(contexts.map((context) => context.close()));
  }
});
