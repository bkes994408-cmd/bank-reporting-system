import { test, expect } from '@playwright/test'

test('主流程：進入申報上傳頁並觸發必填驗證提示', async ({ page }) => {
  await page.route('**/api/news', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        code: '0000',
        msg: 'ok',
        payload: {
          content: [],
          totalPages: 0
        }
      })
    })
  })

  await page.goto('/')
  await expect(page.getByRole('heading', { name: '公告資訊' })).toBeVisible()

  await page.getByRole('link', { name: '📤 申報上傳' }).click()
  await expect(page).toHaveURL(/\/upload$/)
  await expect(page.getByRole('heading', { name: '申報上傳' })).toBeVisible()

  await page.getByRole('button', { name: '📤 確認上傳申報表' }).click()
  await expect(page.getByText('請填寫所有必填欄位並上傳檔案')).toBeVisible()
})
