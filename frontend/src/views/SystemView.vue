<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">🔧</span>
        系統參數設定
      </h1>
      <p class="page-description">設定代理程式與申報平台API連結及自動更新時間</p>
    </div>

    <div v-if="message" :class="['alert', `alert-${message.type}`]">
      {{ message.type === 'success' ? '✅' : '❌' }} {{ message.text }}
    </div>

    <!-- API Server URL Card -->
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🌐 API伺服器連結</h2>
      </div>
      <div class="form-group">
        <label class="form-label">當前連結</label>
        <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px; font-family: monospace; word-break: break-all;">
          {{ settings.apiServerUrl || '載入中...' }}
        </div>
        <p class="form-hint" style="margin-top: 8px;">
          此為代理程式與申報平台主機進行通訊的目標網址
        </p>
      </div>
      <button class="btn btn-primary" @click="showUrlModal = true">
        ✏️ 編輯連結
      </button>
    </div>

    <!-- Auto Update Time Card -->
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">⏰ 自動更新時間</h2>
      </div>
      <div class="form-group">
        <label class="form-label">當前設定</label>
        <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px; font-family: monospace;">
          {{ settings.autoUpdateTime || '載入中...' }}
        </div>
        <p class="form-hint" style="margin-top: 8px;">
          系統會於此指定時間每日自動連線至伺服器檢查是否有最新版本
        </p>
      </div>
      <button class="btn btn-primary" @click="showTimeModal = true">
        ⏰ 設定更新時間
      </button>
    </div>

    <!-- Warning -->
    <div class="alert alert-warning">
      ⚠️ <strong>重要提醒：</strong>API伺服器連結為系統核心設定，輸入錯誤的連結將導致代理程式無法連線，所有申報功能將會中斷。
    </div>

    <!-- URL Modal -->
    <div v-if="showUrlModal" class="modal-overlay" @click.self="showUrlModal = false">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">✏️ 編輯API伺服器連結</h3>
          <button class="modal-close" @click="showUrlModal = false">×</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label class="form-label required">伺服器連結</label>
            <input 
              type="text" 
              class="form-input" 
              v-model="urlForm.apiServerUrl"
              placeholder="https://127.0.0.1:8005/APBSA"
            />
            <p class="form-hint" style="margin-top: 8px;">
              測試環境: https://172.31.201.171:9568/APBSG<br>
              正式環境: https://172.31.200.171:9568/APBSG
            </p>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showUrlModal = false">取消</button>
          <button class="btn btn-primary" @click="submitUrl" :disabled="loading">
            {{ loading ? '處理中...' : '確認送出' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Time Modal -->
    <div v-if="showTimeModal" class="modal-overlay" @click.self="showTimeModal = false">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">⏰ 設定自動更新時間</h3>
          <button class="modal-close" @click="showTimeModal = false">×</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label class="form-label required">更新時間</label>
            <input 
              type="time" 
              class="form-input" 
              v-model="timeForm.autoUpdateTime"
            />
            <p class="form-hint" style="margin-top: 8px;">
              建議設定在非工作時段，如凌晨時間
            </p>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showTimeModal = false">取消</button>
          <button class="btn btn-primary" @click="submitTime" :disabled="loading">
            {{ loading ? '處理中...' : '確認送出' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { getSettings, updateSettings } from '../services/api'

const loading = ref(false)
const message = ref(null)
const showUrlModal = ref(false)
const showTimeModal = ref(false)

const settings = reactive({
  apiServerUrl: '',
  autoUpdateTime: ''
})

const urlForm = reactive({
  apiServerUrl: ''
})

const timeForm = reactive({
  autoUpdateTime: ''
})

const fetchSettings = async () => {
  try {
    const response = await getSettings()
    if (response.code === '0000' && response.payload) {
      settings.apiServerUrl = response.payload.apiServerUrl
      settings.autoUpdateTime = response.payload.autoUpdateTime
    }
  } catch (error) {
    console.error('Failed to fetch settings:', error)
  }
}

const submitUrl = async () => {
  if (!urlForm.apiServerUrl) {
    message.value = { type: 'danger', text: '請輸入伺服器連結' }
    return
  }

  loading.value = true
  try {
    const response = await updateSettings({
      apiServerUrl: urlForm.apiServerUrl
    })
    if (response.code === '0000') {
      message.value = { type: 'success', text: '更新成功' }
      settings.apiServerUrl = urlForm.apiServerUrl
      showUrlModal.value = false
    } else {
      message.value = { type: 'danger', text: '更新失敗: ' + response.msg }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '更新失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const submitTime = async () => {
  if (!timeForm.autoUpdateTime) {
    message.value = { type: 'danger', text: '請選擇更新時間' }
    return
  }

  loading.value = true
  try {
    const response = await updateSettings({
      autoUpdateTime: timeForm.autoUpdateTime
    })
    if (response.code === '0000') {
      message.value = { type: 'success', text: '更新成功' }
      settings.autoUpdateTime = timeForm.autoUpdateTime
      showTimeModal.value = false
    } else {
      message.value = { type: 'danger', text: '更新失敗: ' + response.msg }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '更新失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchSettings()
})
</script>
