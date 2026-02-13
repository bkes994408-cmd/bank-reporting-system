<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">📋</span>
        公告資訊
      </h1>
      <p class="page-description">查詢聯徵中心發布的公告及下載附件檔案</p>
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🔍 搜尋公告</h2>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label class="form-label">關鍵字搜尋</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="searchForm.keyword"
            placeholder="輸入公告標題或內容關鍵字"
          />
        </div>
        <div class="form-group">
          <label class="form-label">排序方式</label>
          <select class="form-select" v-model="searchForm.sort">
            <option value="DESC">依發布日期降冪排序</option>
            <option value="ASC">依發布日期升冪排序</option>
          </select>
        </div>
      </div>
      <div class="btn-group">
        <button class="btn btn-primary" @click="fetchNews">
          🔍 查詢
        </button>
        <button class="btn btn-secondary" @click="resetSearch">
          ↻ 重置
        </button>
      </div>
    </div>

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="newsList.length === 0" class="empty-state">
      <div class="empty-state-icon">📭</div>
      <div class="empty-state-text">目前沒有公告</div>
      <div class="empty-state-hint">請稍後再查詢</div>
    </div>

    <div v-else>
      <div 
        v-for="news in newsList" 
        :key="news.id" 
        class="news-card"
      >
        <div class="news-card-header">
          <div class="news-card-title">
            <span v-if="news.type === 'TOP'" class="badge badge-danger">置頂</span>
            {{ news.title }}
          </div>
          <div class="news-card-date">{{ news.startDate }}</div>
        </div>
        <div class="news-card-content" v-html="formatContent(news.content)"></div>
        
        <div v-if="news.tagIds && news.tagIds.length" style="margin-top: 12px;">
          <span 
            v-for="tag in news.tagIds" 
            :key="tag.reportId"
            class="badge badge-info"
            style="margin-right: 8px;"
          >
            {{ tag.reportId }}
          </span>
        </div>

        <div v-if="news.attachments && news.attachments.length" class="news-card-attachments">
          <strong>📎 附件:</strong>
          <a 
            v-for="attachment in news.attachments"
            :key="attachment.url"
            href="#"
            class="attachment-link"
            @click.prevent="downloadFile(attachment)"
          >
            📄 {{ attachment.name }} ({{ formatFileSize(attachment.size) }})
          </a>
        </div>
      </div>

      <div class="pagination">
        <button 
          class="pagination-btn"
          :disabled="currentPage === 0"
          @click="changePage(currentPage - 1)"
        >
          ◀
        </button>
        <button 
          v-for="page in totalPages"
          :key="page"
          class="pagination-btn"
          :class="{ active: currentPage === page - 1 }"
          @click="changePage(page - 1)"
        >
          {{ page }}
        </button>
        <button 
          class="pagination-btn"
          :disabled="currentPage >= totalPages - 1"
          @click="changePage(currentPage + 1)"
        >
          ▶
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { getNews, downloadAttachment } from '../services/api'

const loading = ref(false)
const newsList = ref([])
const currentPage = ref(0)
const totalPages = ref(0)
const pageSize = ref(10)

const searchForm = reactive({
  keyword: '',
  sort: 'DESC'
})

const fetchNews = async () => {
  loading.value = true
  try {
    const response = await getNews({
      pageNumber: currentPage.value,
      pageSize: pageSize.value,
      keyword: searchForm.keyword || undefined,
      sort: searchForm.sort
    })
    if (response.code === '0000' && response.payload) {
      newsList.value = response.payload.content || []
      totalPages.value = response.payload.totalPages || 0
    }
  } catch (error) {
    console.error('Failed to fetch news:', error)
  } finally {
    loading.value = false
  }
}

const resetSearch = () => {
  searchForm.keyword = ''
  searchForm.sort = 'DESC'
  currentPage.value = 0
  fetchNews()
}

const changePage = (page) => {
  currentPage.value = page
  fetchNews()
}

const formatContent = (content) => {
  return content?.replace(/\n/g, '<br>') || ''
}

const formatFileSize = (bytes) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

const downloadFile = async (attachment) => {
  try {
    const response = await downloadAttachment({
      url: attachment.url,
      name: attachment.name,
      type: attachment.type
    })
    
    const url = window.URL.createObjectURL(new Blob([response]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', attachment.name)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  } catch (error) {
    alert('下載失敗: ' + error.message)
  }
}

onMounted(() => {
  fetchNews()
})
</script>
