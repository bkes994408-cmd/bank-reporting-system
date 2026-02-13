<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">📊</span>
        查詢報表申報歷程
      </h1>
      <p class="page-description">查詢各申報表的申報歷程</p>
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
          <label class="form-label required">報表編號</label>
          <select class="form-select" v-model="searchForm.reportId">
            <option value="">請選擇報表</option>
            <option v-for="report in reportTypes" :key="report" :value="report">
              {{ report }}
            </option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label required">報表年度</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="searchForm.year"
            placeholder="民國年，如: 113"
            maxlength="3"
          />
        </div>
        <div class="form-group">
          <label class="form-label">報表申報歷程</label>
          <select class="form-select" v-model="searchForm.type">
            <option value="NEW">最新一筆</option>
            <option value="HIS">所有歷程</option>
          </select>
        </div>
      </div>

      <div class="btn-group">
        <button class="btn btn-primary" @click="searchHistory" :disabled="loading">
          {{ loading ? '查詢中...' : '🔍 查詢' }}
        </button>
        <button class="btn btn-secondary" @click="resetSearch">
          ↻ 清除
        </button>
      </div>
    </div>

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
    </div>

    <div v-else-if="historyResults.length > 0" class="card">
      <div class="card-header">
        <h2 class="card-title">📋 查詢結果</h2>
      </div>

      <div class="table-container">
        <table class="table">
          <thead>
            <tr>
              <th>訊息編號</th>
              <th>報表編號</th>
              <th>填報週期</th>
              <th>報表年度</th>
              <th>報表月份</th>
              <th>申報狀態</th>
              <th>錯誤訊息</th>
              <th>申報截止時間</th>
              <th>最後上傳申報時間</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="history in historyResults" :key="history.transactionId || history.no">
              <td>{{ history.transactionId || '-' }}</td>
              <td>{{ history.reportId }}</td>
              <td>{{ history.period }}</td>
              <td>{{ history.year }}</td>
              <td>{{ history.month }}</td>
              <td>
                <span :class="getStatusClass(history.statusType)">
                  {{ history.status }}
                </span>
              </td>
              <td>
                <button 
                  v-if="history.errors && history.errors.length"
                  class="btn btn-danger"
                  style="padding: 4px 12px; font-size: 0.8rem;"
                  @click="showErrors(history.errors)"
                >
                  顯示錯誤
                </button>
                <span v-else>-</span>
              </td>
              <td>{{ history.dueTime }}</td>
              <td>{{ history.declarationTime || '-' }}</td>
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
import { getReportHistories } from '../services/api'

const loading = ref(false)
const searched = ref(false)
const historyResults = ref([])
const showErrorModal = ref(false)
const currentErrors = ref([])

const reportTypes = [
  'AI302', 'AI330', 'AI335', 'AI341', 'AI345', 'AI346',
  'AI370', 'AI372', 'AI395', 'AI397', 'AI501', 'AI505',
  'AI515', 'AI520', 'AI555', 'AI560', 'AI812', 'AI813',
  'AI814', 'AI823', 'AI863'
]

const searchForm = reactive({
  bankCode: '',
  reportId: '',
  year: '',
  type: 'NEW'
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

const searchHistory = async () => {
  if (!searchForm.bankCode || !searchForm.reportId || !searchForm.year) {
    alert('請填寫所有必填欄位')
    return
  }

  loading.value = true
  searched.value = true

  try {
    const response = await getReportHistories({
      bankCode: searchForm.bankCode,
      reportId: searchForm.reportId,
      year: searchForm.year,
      type: searchForm.type
    })

    if (response.code === '0000' && response.payload) {
      historyResults.value = response.payload.reports || []
    } else {
      historyResults.value = []
    }
  } catch (error) {
    console.error('Search failed:', error)
    historyResults.value = []
  } finally {
    loading.value = false
  }
}

const resetSearch = () => {
  searchForm.bankCode = ''
  searchForm.reportId = ''
  searchForm.year = ''
  searchForm.type = 'NEW'
  historyResults.value = []
  searched.value = false
}

const showErrors = (errors) => {
  currentErrors.value = errors
  showErrorModal.value = true
}

const closeErrorModal = () => {
  showErrorModal.value = false
  currentErrors.value = []
}
</script>
