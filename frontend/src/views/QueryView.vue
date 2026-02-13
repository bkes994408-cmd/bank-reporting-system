<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">🔍</span>
        申報表查詢
      </h1>
      <p class="page-description">查詢已申報資料（僅能查詢本機申報之報表）</p>
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🔍 查詢條件</h2>
      </div>

      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">金融機構代碼</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="searchForm.bankCode"
            placeholder="7位數代碼"
            maxlength="7"
          />
        </div>
        <div class="form-group">
          <label class="form-label">申報起始日期</label>
          <input 
            type="date" 
            class="form-input" 
            v-model="searchForm.startDate"
          />
        </div>
        <div class="form-group">
          <label class="form-label">申報結束日期</label>
          <input 
            type="date" 
            class="form-input" 
            v-model="searchForm.endDate"
          />
        </div>
      </div>

      <div class="form-group">
        <label class="form-label">報表編號</label>
        <div style="display: flex; flex-wrap: wrap; gap: 12px; margin-top: 8px;">
          <label 
            v-for="report in reportTypes" 
            :key="report"
            style="display: flex; align-items: center; gap: 6px; cursor: pointer;"
          >
            <input type="checkbox" :value="report" v-model="searchForm.reportIds" />
            {{ report }}
          </label>
        </div>
      </div>

      <div class="btn-group">
        <button class="btn btn-primary" @click="searchReports">
          🔍 查詢
        </button>
        <button class="btn btn-secondary" @click="resetSearch">
          ↻ 清除
        </button>
      </div>
    </div>

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="queryResults.length > 0" class="card">
      <div class="card-header">
        <h2 class="card-title">📋 查詢清單</h2>
      </div>

      <div class="table-container">
        <table class="table">
          <thead>
            <tr>
              <th>申報表名稱</th>
              <th>報表年度</th>
              <th>報表月份</th>
              <th>申報狀態</th>
              <th>錯誤訊息</th>
              <th>申報時間</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="report in queryResults" :key="report.requestId || report.no">
              <td>{{ report.reportId }}</td>
              <td>{{ report.year }}</td>
              <td>{{ report.month }}</td>
              <td>
                <span :class="getStatusClass(report.statusType)">
                  {{ report.status }}
                </span>
              </td>
              <td>
                <button 
                  v-if="report.errors && report.errors.length"
                  class="btn btn-danger"
                  style="padding: 4px 12px; font-size: 0.8rem;"
                  @click="showErrors(report.errors)"
                >
                  顯示錯誤
                </button>
                <span v-else>-</span>
              </td>
              <td>{{ report.declarationTime || '-' }}</td>
              <td>
                <div style="display: flex; gap: 8px;">
                  <button 
                    class="btn btn-outline"
                    style="padding: 4px 12px; font-size: 0.8rem;"
                    @click="viewReport(report)"
                  >
                    顯示資料
                  </button>
                  <button 
                    class="btn btn-secondary"
                    style="padding: 4px 12px; font-size: 0.8rem;"
                    @click="exportReport(report)"
                  >
                    匯出
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-else-if="searched" class="empty-state">
      <div class="empty-state-icon">📭</div>
      <div class="empty-state-text">查無資料</div>
      <div class="empty-state-hint">請調整查詢條件後重新查詢</div>
    </div>

    <!-- Report Detail Modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">📄 申報資料</h3>
          <button class="modal-close" @click="closeModal">×</button>
        </div>
        <div class="modal-body">
          <div v-if="selectedReport">
            <p><strong>申報時間:</strong> {{ selectedReport.declarationTime }}</p>
            <p><strong>報表編號:</strong> {{ selectedReport.reportId }}</p>
            <hr style="margin: 16px 0;" />
            <pre style="background: #f1f5f9; padding: 16px; border-radius: 8px; overflow-x: auto; font-size: 0.85rem;">{{ JSON.stringify(selectedReport, null, 2) }}</pre>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeModal">關閉</button>
        </div>
      </div>
    </div>

    <!-- Error Modal -->
    <div v-if="showErrorModal" class="modal-overlay" @click.self="closeErrorModal">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">❌ 錯誤訊息</h3>
          <button class="modal-close" @click="closeErrorModal">×</button>
        </div>
        <div class="modal-body">
          <div class="alert alert-danger" v-for="(error, idx) in currentErrors" :key="idx">
            {{ error }}
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="closeErrorModal">關閉</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { getDeclareResult } from '../services/api'

const loading = ref(false)
const searched = ref(false)
const queryResults = ref([])
const showModal = ref(false)
const showErrorModal = ref(false)
const selectedReport = ref(null)
const currentErrors = ref([])

const reportTypes = [
  'AI302', 'AI330', 'AI335', 'AI341', 'AI345', 'AI346',
  'AI370', 'AI372', 'AI395', 'AI397', 'AI501', 'AI505',
  'AI515', 'AI520', 'AI555', 'AI560', 'AI812', 'AI813',
  'AI814', 'AI823', 'AI863'
]

const searchForm = reactive({
  bankCode: '',
  startDate: '',
  endDate: '',
  reportIds: []
})

const getStatusClass = (statusType) => {
  const statusMap = {
    '00': 'badge badge-secondary',
    '01': 'badge badge-success',
    '10': 'badge badge-success',
    '21': 'badge badge-danger',
    '22': 'badge badge-danger',
    '23': 'badge badge-danger'
  }
  return statusMap[statusType] || 'badge badge-info'
}

const searchReports = async () => {
  if (!searchForm.bankCode) {
    alert('請輸入金融機構代碼')
    return
  }

  loading.value = true
  searched.value = true

  try {
    // In a real implementation, this would query from local storage or cache
    // For now, we'll simulate with some sample data
    queryResults.value = []
    
    // This is a placeholder - in reality, you'd have local cache of submissions
    const savedData = localStorage.getItem('submissionHistory')
    if (savedData) {
      const history = JSON.parse(savedData)
      queryResults.value = history.filter(item => {
        if (item.bankCode !== searchForm.bankCode) return false
        if (searchForm.reportIds.length && !searchForm.reportIds.includes(item.reportId)) return false
        return true
      })
    }
  } catch (error) {
    console.error('Search failed:', error)
  } finally {
    loading.value = false
  }
}

const resetSearch = () => {
  searchForm.bankCode = ''
  searchForm.startDate = ''
  searchForm.endDate = ''
  searchForm.reportIds = []
  queryResults.value = []
  searched.value = false
}

const viewReport = (report) => {
  selectedReport.value = report
  showModal.value = true
}

const closeModal = () => {
  showModal.value = false
  selectedReport.value = null
}

const showErrors = (errors) => {
  currentErrors.value = errors
  showErrorModal.value = true
}

const closeErrorModal = () => {
  showErrorModal.value = false
  currentErrors.value = []
}

const exportReport = (report) => {
  const dataStr = JSON.stringify(report, null, 2)
  const dataBlob = new Blob([dataStr], { type: 'application/json' })
  const url = URL.createObjectURL(dataBlob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${report.reportId}_${report.year}_${report.month}.json`
  link.click()
  URL.revokeObjectURL(url)
}
</script>
