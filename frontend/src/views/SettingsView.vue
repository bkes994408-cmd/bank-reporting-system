<template>
  <div class="slide-up">
    <div class="page-header">
      <h1 class="page-title">
        <span class="page-title-icon">⚙️</span>
        設定
      </h1>
      <p class="page-description">設定代理程式版本、Authtoken及金鑰資訊</p>
    </div>

    <div v-if="message" :class="['alert', `alert-${message.type}`]">
      {{ message.type === 'success' ? '✅' : '❌' }} {{ message.text }}
    </div>

    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🛡️ 目前測試角色</h2>
      </div>
      <div class="form-group">
        <label class="form-label">Active Role（會帶入 X-Role header）</label>
        <select v-model="activeRole" @change="persistRole" class="form-input">
          <option value="admin">admin</option>
          <option value="superadmin">superadmin</option>
          <option value="reporter">reporter</option>
          <option value="viewer">viewer</option>
        </select>
      </div>
    </div>

    <!-- Version Card -->
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">📦 代理程式版本</h2>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label class="form-label">當前版本</label>
          <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px; font-family: monospace;">
            {{ agentInfo.version || '載入中...' }}
          </div>
        </div>
        <div class="form-group">
          <label class="form-label">最新版本</label>
          <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px; font-family: monospace;">
            {{ versionInfo.latestVersion || '載入中...' }}
          </div>
        </div>
      </div>
      <button class="btn btn-outline" @click="checkVersion" :disabled="loading">
        🔄 檢查版本
      </button>
    </div>

    <!-- Token Card -->
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🔑 Authtoken</h2>
      </div>
      <div class="form-group">
        <label class="form-label">當前Token狀態</label>
        <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px;">
          {{ agentInfo.token ? '已設定' : '未設定' }}
        </div>
      </div>
      <button class="btn btn-primary" @click="showTokenModal = true">
        🔄 更新Token
      </button>
    </div>

    <!-- Key Card -->
    <div class="card">
      <div class="card-header">
        <h2 class="card-title">🗝️ 金鑰</h2>
      </div>
      <div class="form-group">
        <label class="form-label">金鑰狀態</label>
        <div style="padding: 12px 16px; background: var(--bg-color); border-radius: 8px;">
          {{ agentInfo.key || '未匯入' }}
        </div>
      </div>
      <div class="btn-group">
        <button class="btn btn-primary" @click="showKeyModal = true">
          📥 匯入金鑰
        </button>
        <button class="btn btn-success" @click="validateKeysAction" :disabled="loading">
          ✅ 金鑰和Authtoken測試
        </button>
      </div>
    </div>

    <!-- Token Modal -->
    <div v-if="showTokenModal" class="modal-overlay" @click.self="showTokenModal = false">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">🔄 更新Token</h3>
          <button class="modal-close" @click="showTokenModal = false">×</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label class="form-label required">Authtoken</label>
            <textarea 
              class="form-textarea" 
              v-model="tokenForm.token"
              rows="5"
              placeholder="請輸入Authtoken"
            ></textarea>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showTokenModal = false">取消</button>
          <button class="btn btn-primary" @click="submitToken" :disabled="loading">
            {{ loading ? '處理中...' : '確認送出' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Key Modal -->
    <div v-if="showKeyModal" class="modal-overlay" @click.self="showKeyModal = false">
      <div class="modal">
        <div class="modal-header">
          <h3 class="modal-title">📥 匯入金鑰</h3>
          <button class="modal-close" @click="showKeyModal = false">×</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label class="form-label required">金鑰A</label>
            <textarea 
              class="form-textarea" 
              v-model="keyForm.keyA"
              rows="3"
              placeholder="請輸入金鑰A"
            ></textarea>
          </div>
          <div class="form-group">
            <label class="form-label required">金鑰B</label>
            <textarea 
              class="form-textarea" 
              v-model="keyForm.keyB"
              rows="3"
              placeholder="請輸入金鑰B"
            ></textarea>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showKeyModal = false">取消</button>
          <button class="btn btn-primary" @click="submitKeys" :disabled="loading">
            {{ loading ? '處理中...' : '確認送出' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { checkVersion as checkVersionApi, getInfo, updateToken, importKeys, validateKeys } from '../services/api'

const loading = ref(false)
const message = ref(null)
const showTokenModal = ref(false)
const showKeyModal = ref(false)
const activeRole = ref('reporter')

const agentInfo = reactive({
  version: '',
  token: '',
  key: ''
})

const versionInfo = reactive({
  version: '',
  latestVersion: ''
})

const tokenForm = reactive({
  token: ''
})

const keyForm = reactive({
  keyA: '',
  keyB: ''
})

const fetchInfo = async () => {
  try {
    const response = await getInfo()
    if (response.code === '0000' && response.payload) {
      agentInfo.version = response.payload.version
      agentInfo.token = response.payload.token
      agentInfo.key = response.payload.key
    }
  } catch (error) {
    console.error('Failed to fetch info:', error)
  }
}

const checkVersion = async () => {
  loading.value = true
  try {
    const response = await checkVersionApi()
    if (response.code === '0000' && response.payload) {
      versionInfo.version = response.payload.version
      versionInfo.latestVersion = response.payload.latestVersion
      
      if (versionInfo.version === versionInfo.latestVersion) {
        message.value = { type: 'success', text: '已是最新版本' }
      } else {
        message.value = { type: 'info', text: '有新版本可用' }
      }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '檢查版本失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const submitToken = async () => {
  if (!tokenForm.token) {
    message.value = { type: 'danger', text: '請輸入Token' }
    return
  }

  loading.value = true
  try {
    const response = await updateToken({ token: tokenForm.token })
    if (response.code === '0000') {
      message.value = { type: 'success', text: 'Token更新成功' }
      showTokenModal.value = false
      tokenForm.token = ''
      fetchInfo()
    } else {
      message.value = { type: 'danger', text: '更新失敗: ' + response.msg }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '更新失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const submitKeys = async () => {
  if (!keyForm.keyA || !keyForm.keyB) {
    message.value = { type: 'danger', text: '請輸入完整金鑰' }
    return
  }

  loading.value = true
  try {
    const response = await importKeys({
      keyA: keyForm.keyA,
      keyB: keyForm.keyB
    })
    if (response.code === '0000') {
      message.value = { type: 'success', text: '金鑰匯入成功' }
      showKeyModal.value = false
      keyForm.keyA = ''
      keyForm.keyB = ''
      fetchInfo()
    } else {
      message.value = { type: 'danger', text: '匯入失敗: ' + response.msg }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '匯入失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const validateKeysAction = async () => {
  loading.value = true
  try {
    const response = await validateKeys()
    if (response.code === '0000') {
      message.value = { type: 'success', text: '金鑰測試通過！金鑰和Token驗證成功。' }
    } else {
      message.value = { type: 'danger', text: '驗證失敗: ' + response.msg }
    }
  } catch (error) {
    message.value = { type: 'danger', text: '驗證失敗: ' + error.message }
  } finally {
    loading.value = false
  }
}

const persistRole = () => {
  localStorage.setItem('activeRole', activeRole.value)
  message.value = { type: 'success', text: `已切換測試角色為 ${activeRole.value}` }
}

onMounted(() => {
  activeRole.value = localStorage.getItem('activeRole') || 'reporter'
  fetchInfo()
  checkVersion()
})
</script>
