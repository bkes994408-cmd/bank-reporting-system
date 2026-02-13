<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">📅</span>
        查詢當月應申報報表及狀態
      </h1>
      <p class="page-description">查詢當月份應申報的申報表狀態</p>
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
          <label class="form-label required">申報民國年</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="searchForm.applyYear"
            placeholder="民國年，如: 113"
            maxlength="3"
          />
        </div>
        <div class="form-group">
          <label class="form-label">申報月份</label>
          <select class="form-select" v-model="searchForm.applyMonth">
            <option value="">全部</option>
            <option value="01">01月</option>
            <option value="02">02月</option>
            <option value="03">03月</option>
            <option value="04">04月</option>
            <option value="05">05月</option>
            <option value="06">06月</option>
            <option value="07">07月</option>
            <option value="08">08月</option>
            <option value="09">09月</option>
            <option value="10">10月</option>
            <option value="11">11月</option>
            <option value="12">12月</option>
          </select>
        </div>
      </div>

      <div class="btn-group">
        <button class="btn btn-primary" @click="searchReports" :disabled="loading">
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

    <div v-else-if="monthlyReports.length > 0" class="card">
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
            <tr v-for="report in monthlyReports" :key="report.transactionId || report.no">
              <td>{{ report.transactionId || '-' }}</td>
              <td>{{ report.reportId }}</td>
              <td>{{ report.period }}</td>
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
              <td>{{ report.dueTime }}</td>
              <td>{{ report.declarationTime || '-' }}</td>
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
import { ref, reactive, onMounted } from 'vue'
import { getMonthlyReports } from '../services/api'

const loading = ref(false)
const searched = ref(false)
const monthlyReports = ref([])
const showErrorModal = ref(false)
const currentErrors = ref([])

const searchForm = reactive({
  bankCode: '',
  applyYear: '',
  applyMonth: ''
})

// Set default year and month
onMounted(() => {
  const now = new Date()
  const year = now.getFullYear() - 1911 // Convert to ROC year
  const month = String(now.getMonth() + 1).padStart(2, '0')
  searchForm.applyYear = String(year)
  searchForm.applyMonth = month
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
  if (!searchForm.bankCode || !searchForm.applyYear) {
    alert('請填寫必填欄位')
    return
  }

  loading.value = true
  searched.value = true

  try {
    const response = await getMonthlyReports({
      bankCode: searchForm.bankCode,
      applyYear: searchForm.applyYear,
      applyMonth: searchForm.applyMonth || undefined
    })

    if (response.code === '0000' && response.payload) {
      monthlyReports.value = response.payload.reports || []
    } else {
      monthlyReports.value = []
    }
  } catch (error) {
    console.error('Search failed:', error)
    monthlyReports.value = []
  } finally {
    loading.value = false
  }
}

const resetSearch = () => {
  searchForm.bankCode = ''
  const now = new Date()
  searchForm.applyYear = String(now.getFullYear() - 1911)
  searchForm.applyMonth = String(now.getMonth() + 1).padStart(2, '0')
  monthlyReports.value = []
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
