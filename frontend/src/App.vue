<template>
  <div class="app-layout">
    <aside class="sidebar" :class="{ open: sidebarOpen }">
      <div class="sidebar-header">
        <div class="sidebar-logo">
          <div class="sidebar-logo-icon">🏦</div>
          <div>
            <div class="sidebar-title">銀行監理資料</div>
            <div class="sidebar-subtitle">數位申報系統</div>
          </div>
        </div>
      </div>
      
      <nav class="sidebar-nav">
        <div class="nav-section">
          <div class="nav-section-title">主要功能</div>
          <router-link to="/" class="nav-item" active-class="active">
            <span class="nav-item-icon">📋</span>
            <span>公告資訊</span>
          </router-link>
          <router-link to="/upload" class="nav-item" active-class="active">
            <span class="nav-item-icon">📤</span>
            <span>申報上傳</span>
          </router-link>
          <router-link to="/query" class="nav-item" active-class="active">
            <span class="nav-item-icon">🔍</span>
            <span>申報表查詢</span>
          </router-link>
          <router-link to="/history" class="nav-item" active-class="active">
            <span class="nav-item-icon">📊</span>
            <span>報表申報歷程</span>
          </router-link>
          <router-link to="/monthly" class="nav-item" active-class="active">
            <span class="nav-item-icon">📅</span>
            <span>當月應申報報表</span>
          </router-link>
        </div>
        
        <div class="nav-section">
          <div class="nav-section-title">系統設定</div>
          <router-link to="/settings" class="nav-item" active-class="active">
            <span class="nav-item-icon">⚙️</span>
            <span>設定</span>
          </router-link>
          <router-link to="/system" class="nav-item" active-class="active">
            <span class="nav-item-icon">🔧</span>
            <span>系統參數設定</span>
          </router-link>
        </div>
      </nav>
    </aside>
    
    <main class="main-content">
      <router-view />
    </main>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const sidebarOpen = ref(false)
</script>
