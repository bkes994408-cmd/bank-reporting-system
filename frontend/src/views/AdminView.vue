<template>
  <div class="page-container">
    <div class="page-header">
      <h1>後臺管理</h1>
      <p>帳號與角色管理（MVP）</p>
    </div>

    <div v-if="message" :class="['alert', `alert-${message.type}`]" style="margin-bottom: 16px;">
      {{ message.type === 'success' ? '✅' : '❌' }} {{ message.text }}
    </div>

    <div class="card" style="margin-bottom: 16px;">
      <h3>新增使用者</h3>
      <div class="form-grid">
        <div class="form-group">
          <label>Username</label>
          <input v-model="newUser.username" placeholder="例如：alice" />
        </div>
        <div class="form-group">
          <label>Display Name</label>
          <input v-model="newUser.displayName" placeholder="例如：Alice" />
        </div>
        <div class="form-group">
          <label>Roles（逗號分隔）</label>
          <input v-model="newUser.roles" placeholder="admin,reporter" />
        </div>
      </div>
      <button class="btn btn-primary" @click="createUser">新增</button>
    </div>

    <div class="card">
      <h3>使用者列表（可直接編輯角色）</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Username</th>
            <th>Display Name</th>
            <th>Roles</th>
            <th>動作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in users" :key="u.username">
            <td>{{ u.username }}</td>
            <td>{{ u.displayName }}</td>
            <td>
              <input
                v-model="editableRoles[u.username]"
                placeholder="admin,reporter"
              />
            </td>
            <td>
              <button class="btn btn-outline" @click="saveRoles(u.username)">儲存角色</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { createAdminUser, getAdminUsers, updateAdminUserRoles } from '../services/api'

const users = ref([])
const message = ref(null)
const editableRoles = ref({})
const newUser = ref({ username: '', displayName: '', roles: 'viewer' })

const loadUsers = async () => {
  const res = await getAdminUsers()
  users.value = res?.payload?.users || []

  const map = {}
  users.value.forEach(u => {
    map[u.username] = (u.roles || []).join(', ')
  })
  editableRoles.value = map
}

const createUser = async () => {
  if (!newUser.value.username.trim()) return

  try {
    await createAdminUser({
      username: newUser.value.username,
      displayName: newUser.value.displayName,
      roles: newUser.value.roles.split(',').map(x => x.trim()).filter(Boolean)
    })

    message.value = { type: 'success', text: '使用者新增成功' }
    newUser.value = { username: '', displayName: '', roles: 'viewer' }
    await loadUsers()
  } catch (error) {
    message.value = { type: 'danger', text: `新增失敗：${error?.response?.data?.msg || error.message}` }
  }
}

const saveRoles = async (username) => {
  const roleText = editableRoles.value[username] || ''
  const roles = roleText.split(',').map(x => x.trim()).filter(Boolean)

  try {
    await updateAdminUserRoles(username, roles)
    message.value = { type: 'success', text: `${username} 角色更新成功` }
    await loadUsers()
  } catch (error) {
    message.value = { type: 'danger', text: `更新失敗：${error?.response?.data?.msg || error.message}` }
  }
}

onMounted(loadUsers)
</script>
