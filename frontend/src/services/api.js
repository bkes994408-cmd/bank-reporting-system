import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Request interceptor
api.interceptors.request.use(
  config => {
    // RBAC 測試用：允許前端切換角色（預設 reporter）
    const role = localStorage.getItem('activeRole') || 'reporter'
    config.headers['X-Role'] = role
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// Response interceptor
api.interceptors.response.use(
  response => response.data,
  error => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)

// Parsing APIs
export const parseExcel = (formData) => {
  return api.post('/parsing/excel', formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  })
}

export const parseExcelWithContact = (formData) => {
  return api.post('/parsing/excel-with-contact', formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  })
}

// Declare APIs
export const declare = (data) => {
  return api.post('/declare', data)
}

export const getDeclareResult = (data) => {
  return api.post('/declare/result', data)
}

// Reports APIs
export const getMonthlyReports = (data) => {
  return api.post('/reports', data)
}

export const getReportHistories = (data) => {
  return api.post('/reports/histories', data)
}

// Keys APIs
export const importKeys = (data) => {
  return api.post('/keys/import', data)
}

export const validateKeys = () => {
  return api.post('/keys/validate')
}

// Token APIs
export const updateToken = (data) => {
  return api.post('/token/update', data)
}

// News APIs
export const getNews = (data) => {
  return api.post('/news', data)
}

export const downloadAttachment = (data) => {
  return api.post('/news/attachments', data, {
    responseType: 'blob'
  })
}

// System APIs
export const checkVersion = () => {
  return api.get('/check-version')
}

export const getInfo = () => {
  return api.get('/info')
}

export const getSettings = () => {
  return api.get('/settings')
}

export const updateSettings = (data) => {
  return api.post('/settings', data)
}

// Admin APIs (MVP)
export const getAdminUsers = () => {
  return api.get('/admin/users')
}

export const createAdminUser = (data) => {
  return api.post('/admin/users', data)
}

export const getAdminRoles = () => {
  return api.get('/admin/roles')
}

export const updateAdminUserRoles = (username, roles) => {
  return api.put(`/admin/users/${encodeURIComponent(username)}/roles`, { roles })
}

export default api
