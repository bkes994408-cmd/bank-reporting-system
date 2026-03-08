<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">📤</span>
        申報上傳
      </h1>
      <p class="page-description">提供金融機構上傳申報表資料</p>
    </div>

    <div v-if="message" :class="['alert', `alert-${message.type}`]">
      {{ message.type === 'success' ? '✅' : '❌' }} {{ message.text }}
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">📝 基本資料</h2>
        <button class="btn btn-outline" @click="loadPreviousData">
          📂 載入填報資料
        </button>
      </div>
      
      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">報表編號</label>
          <select class="form-select" v-model="form.reportId" @change="onReportChange">
            <option value="">請選擇報表</option>
            <option v-for="report in reportTypes" :key="report.id" :value="report.id">
              {{ report.id }} - {{ report.name }}
            </option>
          </select>
        </div>
      </div>
      
      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">金融機構代碼</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="form.bankCode"
            placeholder="7位數代碼，如: 0070000"
            maxlength="7"
          />
          <span class="form-hint">銀行3碼代碼+0000，例如第一銀行為0070000</span>
        </div>
        <div class="form-group">
          <label class="form-label required">金融機構名稱</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="form.bankName"
            placeholder="如: 第一銀行"
          />
        </div>
      </div>

      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">報表年度</label>
          <input 
            type="text" 
            class="form-input" 
            v-model="form.reportYear"
            placeholder="民國年，如: 113"
            maxlength="3"
          />
        </div>
        <div class="form-group">
          <label class="form-label required">報表月份/季度</label>
          <select class="form-select" v-model="form.reportMonth">
            <option value="">請選擇</option>
            <option value="01">01</option>
            <option value="02">02</option>
            <option value="03">03</option>
            <option value="04">04</option>
            <option value="05">05</option>
            <option value="06">06</option>
            <option value="07">07</option>
            <option value="08">08</option>
            <option value="09">09</option>
            <option value="10">10</option>
            <option value="11">11</option>
            <option value="12">12</option>
          </select>
          <span class="form-hint">月報表填01~12，季報表填01~04</span>
        </div>
      </div>
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">👤 聯絡人資訊</h2>
      </div>
      
      <h3 style="margin-bottom: 16px; font-size: 1rem; color: var(--text-secondary);">承辦人</h3>
      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">姓名</label>
          <input type="text" class="form-input" v-model="form.contractorName" />
        </div>
        <div class="form-group">
          <label class="form-label required">電話</label>
          <input type="text" class="form-input" v-model="form.contractorTel" />
        </div>
        <div class="form-group">
          <label class="form-label required">Email</label>
          <input type="email" class="form-input" v-model="form.contractorEmail" />
        </div>
      </div>

      <h3 style="margin: 24px 0 16px; font-size: 1rem; color: var(--text-secondary);">主管</h3>
      <div class="form-row">
        <div class="form-group">
          <label class="form-label required">姓名</label>
          <input type="text" class="form-input" v-model="form.managerName" />
        </div>
        <div class="form-group">
          <label class="form-label required">電話</label>
          <input type="text" class="form-input" v-model="form.managerTel" />
        </div>
        <div class="form-group">
          <label class="form-label required">Email</label>
          <input type="email" class="form-input" v-model="form.managerEmail" />
        </div>
      </div>
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">📁 上傳資料</h2>
      </div>
      
      <div class="form-row">
        <div class="form-group">
          <label class="form-label">上傳方式</label>
          <select class="form-select" v-model="uploadType">
            <option value="excel">Excel 申報表檔案</option>
            <option value="json">JSON 檔案</option>
          </select>
        </div>
      </div>

      <div class="file-upload" @click="triggerFileInput" @dragover.prevent @drop.prevent="handleDrop">
        <input 
          ref="fileInput" 
          type="file" 
          :accept="uploadType === 'excel' ? '.xls,.xlsx' : '.json'"
          @change="handleFileChange"
          style="display: none;"
        />
        <div class="file-upload-icon">{{ uploadType === 'excel' ? '📊' : '📄' }}</div>
        <div class="file-upload-text">
          <span v-if="selectedFile">已選擇: {{ selectedFile.name }}</span>
          <span v-else>點擊或拖曳檔案至此處上傳</span>
        </div>
        <div class="file-upload-hint">
          {{ uploadType === 'excel' ? '支援 .xls, .xlsx 格式' : '支援 .json 格式' }}
        </div>
      </div>
    </div>

    <div class="btn-group">
      <button 
        class="btn btn-primary" 
        @click="submitForm"
        :disabled="loading"
      >
        {{ loading ? '上傳中...' : '📤 確認上傳申報表' }}
      </button>
      <button class="btn btn-secondary" @click="resetForm">
        ↻ 清除
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { parseExcelWithContact, declare, getReportCatalog } from '../services/api'

const loading = ref(false)
const message = ref(null)
const fileInput = ref(null)
const selectedFile = ref(null)
const uploadType = ref('excel')

const reportTypes = ref([])

const form = reactive({
  requestId: '',
  bankCode: '',
  bankName: '',
  reportYear: '',
  reportMonth: '',
  reportId: '',
  contractorName: '',
  contractorTel: '',
  contractorEmail: '',
  managerName: '',
  managerTel: '',
  managerEmail: ''
})

const onReportChange = () => {
  // Generate requestId based on bankCode
  if (form.bankCode) {
    form.requestId = `${form.bankCode}-${Date.now()}`
  }
}

const triggerFileInput = () => {
  fileInput.value?.click()
}

const handleFileChange = (e) => {
  const file = e.target.files?.[0]
  if (file) {
    selectedFile.value = file
  }
}

const handleDrop = (e) => {
  const file = e.dataTransfer.files?.[0]
  if (file) {
    selectedFile.value = file
  }
}

const loadPreviousData = () => {
  const savedData = localStorage.getItem('lastUploadData')
  if (savedData) {
    const data = JSON.parse(savedData)
    Object.assign(form, data)
    message.value = { type: 'success', text: '已載入上次填報資料' }
  } else {
    message.value = { type: 'warning', text: '沒有找到歷史填報資料' }
  }
}

const validateForm = () => {
  const required = [
    'bankCode', 'bankName', 'reportYear', 'reportMonth', 'reportId',
    'contractorName', 'contractorTel', 'contractorEmail',
    'managerName', 'managerTel', 'managerEmail'
  ]
  
  for (const field of required) {
    if (!form[field]) {
      return false
    }
  }
  
  if (!selectedFile.value && uploadType.value !== 'json') {
    return false
  }
  
  return true
}

const submitForm = async () => {
  if (!validateForm()) {
    message.value = { type: 'danger', text: '請填寫所有必填欄位並上傳檔案' }
    return
  }

  loading.value = true
  message.value = null

  try {
    if (uploadType.value === 'excel') {
      // Upload Excel file with contact info
      const formData = new FormData()
      formData.append('uploadFile', selectedFile.value)
      formData.append('bankCode', form.bankCode)
      formData.append('bankName', form.bankName)
      formData.append('reportYear', form.reportYear)
      formData.append('reportMonth', form.reportMonth)
      formData.append('reportId', form.reportId)
      formData.append('contractorName', form.contractorName)
      formData.append('contractorTel', form.contractorTel)
      formData.append('contractorEmail', form.contractorEmail)
      formData.append('managerName', form.managerName)
      formData.append('managerTel', form.managerTel)
      formData.append('managerEmail', form.managerEmail)

      const parseResponse = await parseExcelWithContact(formData)
      
      if (parseResponse.code === '0000') {
        // Submit the declaration
        const declareResponse = await declare(parseResponse.payload)
        
        if (declareResponse.code === '0000') {
          message.value = { type: 'success', text: '上傳成功！交易編號: ' + (declareResponse.payload?.transactionId || '') }
          // Save form data for future use
          localStorage.setItem('lastUploadData', JSON.stringify({
            bankCode: form.bankCode,
            bankName: form.bankName,
            contractorName: form.contractorName,
            contractorTel: form.contractorTel,
            contractorEmail: form.contractorEmail,
            managerName: form.managerName,
            managerTel: form.managerTel,
            managerEmail: form.managerEmail
          }))
        } else {
          message.value = { type: 'danger', text: '上傳失敗: ' + declareResponse.msg }
        }
      } else {
        message.value = { type: 'danger', text: 'Excel轉換失敗: ' + parseResponse.msg }
      }
    } else {
      // Upload JSON file directly
      const reader = new FileReader()
      reader.onload = async (e) => {
        try {
          const jsonData = JSON.parse(e.target.result)
          const declareData = {
            ...form,
            requestId: form.requestId || `${form.bankCode}-${Date.now()}`,
            report: jsonData
          }
          
          const response = await declare(declareData)
          if (response.code === '0000') {
            message.value = { type: 'success', text: '上傳成功！' }
          } else {
            message.value = { type: 'danger', text: '上傳失敗: ' + response.msg }
          }
        } catch (parseError) {
          message.value = { type: 'danger', text: 'JSON格式錯誤' }
        }
        loading.value = false
      }
      reader.readAsText(selectedFile.value)
      return
    }
  } catch (error) {
    message.value = { type: 'danger', text: '上傳失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const resetForm = () => {
  Object.keys(form).forEach(key => {
    form[key] = ''
  })
  selectedFile.value = null
  message.value = null
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

onMounted(async () => {
  try {
    const response = await getReportCatalog()
    if (response.code === '0000' && response.payload?.items) {
      reportTypes.value = response.payload.items
    }
  } catch (error) {
    console.error('Failed to load report catalog:', error)
  }
})
</script>
