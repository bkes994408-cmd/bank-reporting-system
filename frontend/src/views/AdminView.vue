<template>
  <div class="page-container">
    <div class="page-header">
      <h1>後臺管理</h1>
      <p>帳號與角色管理（MVP）</p>
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
      <h3>使用者列表</h3>
      <table class="data-table">
        <thead>
          <tr>
            <th>Username</th>
            <th>Display Name</th>
            <th>Roles</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="u in users" :key="u.username">
            <td>{{ u.username }}</td>
            <td>{{ u.displayName }}</td>
            <td>{{ (u.roles || []).join(', ') }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { createAdminUser, getAdminUsers } from '../services/api'

const users = ref([])
const newUser = ref({ username: '', displayName: '', roles: 'viewer' })

const loadUsers = async () => {
  const res = await getAdminUsers()
  users.value = res?.payload?.users || []
}

const createUser = async () => {
  if (!newUser.value.username.trim()) return

  await createAdminUser({
    username: newUser.value.username,
    displayName: newUser.value.displayName,
    roles: newUser.value.roles.split(',').map(x => x.trim()).filter(Boolean)
  })

  newUser.value = { username: '', displayName: '', roles: 'viewer' }
  await loadUsers()
}

onMounted(loadUsers)
</script>
