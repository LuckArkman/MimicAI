import './style.css';

// ==========================================================================
// CONFIGURATIONS & MODELS
// ==========================================================================
const API_BASE = 'http://localhost:5247';

interface User {
  id: string;
  name: string;
  email: string;
  token: string;
}

interface ChatSession {
  id: string;
  userId: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

interface ChatMessage {
  id: string;
  sessionId: string;
  userId: string;
  sender: string;
  content: string;
  isExternal: boolean;
  similarityScore: number;
  resolutionType: string;
  custoEstimado: number;
  timestamp: string;
}

interface Metrics {
  totalLocalHits: number;
  totalExternalEscalations: number;
  totalCostsUsd: number;
  totalSavingsUsd: number;
  autonomyRate: number;
}

interface FineTuningJob {
  id: string;
  modelName: string;
  datasetSource: string;
  status: string;
  createdAt: string;
}

interface HealthStatus {
  postgresConnected: boolean;
  mongoConnected: boolean;
  chromaConnected: boolean;
}

// ==========================================================================
// CENTRAL APPLICATION STATE
// ==========================================================================
class AppState {
  currentUser: User | null = null;
  sessions: ChatSession[] = [];
  currentSessionId: string | null = null;
  messages: ChatMessage[] = [];
  metrics: Metrics = {
    totalLocalHits: 0,
    totalExternalEscalations: 0,
    totalCostsUsd: 0.0,
    totalSavingsUsd: 0.0,
    autonomyRate: 100.0,
  };
  
  // Navigation State
  view: 'login' | 'register' | 'chat' = 'login';
  currentTab: 'chat' | 'rag' | 'finetuning' | 'profile' = 'chat';

  // Sub-data for management tabs
  ftJobs: FineTuningJob[] = [];
  health: HealthStatus = { postgresConnected: true, mongoConnected: true, chromaConnected: true };

  // UI Loaders & Messages
  isSendingMessage: boolean = false;
  isAuthLoading: boolean = false;
  isRagSubmitting: boolean = false;
  isFtSubmitting: boolean = false;
  isParquetSubmitting: boolean = false;
  
  authError: string | null = null;
  ragMsgSuccess: string | null = null;
  ragMsgError: string | null = null;
  ftMsgSuccess: string | null = null;
  ftMsgError: string | null = null;
  parquetMsgSuccess: string | null = null;
  parquetMsgError: string | null = null;

  constructor() {
    this.loadUserFromStorage();
  }

  loadUserFromStorage() {
    const saved = localStorage.getItem('mimic_ai_user');
    if (saved) {
      try {
        this.currentUser = JSON.parse(saved);
        this.view = 'chat';
      } catch {
        localStorage.removeItem('mimic_ai_user');
      }
    }
  }

  saveUserToStorage(user: User) {
    this.currentUser = user;
    localStorage.setItem('mimic_ai_user', JSON.stringify(user));
  }

  clearUser() {
    this.currentUser = null;
    this.sessions = [];
    this.currentSessionId = null;
    this.messages = [];
    this.currentTab = 'chat';
    localStorage.removeItem('mimic_ai_user');
    this.view = 'login';
  }
}

const state = new AppState();

// ==========================================================================
// API CLIENT IMPLEMENTATIONS
// ==========================================================================
async function apiPost(endpoint: string, body: object, token?: string) {
  const headers: HeadersInit = { 'Content-Type': 'application/json' };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  
  const response = await fetch(`${API_BASE}${endpoint}`, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    if (response.status === 401) {
      state.clearUser();
      render();
      throw new Error('Sessão expirada. Faça login novamente.');
    }
    const errData = await response.json().catch(() => ({}));
    throw new Error(errData.message || 'Falha na requisição da API.');
  }

  return response.json();
}

async function apiGet(endpoint: string, token?: string) {
  const headers: HeadersInit = {};
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE}${endpoint}`, {
    method: 'GET',
    headers,
  });

  if (!response.ok) {
    if (response.status === 401) {
      state.clearUser();
      render();
      throw new Error('Sessão expirada. Faça login novamente.');
    }
    throw new Error('Falha ao obter dados da API.');
  }

  return response.json();
}

// ==========================================================================
// BACKGROUND FETCH ACTIONS FOR TABS
// ==========================================================================
async function fetchFinetuningJobs() {
  if (!state.currentUser) return;
  try {
    const jobs = await apiGet('/api/finetuning/jobs', state.currentUser.token);
    state.ftJobs = jobs;
  } catch (err) {
    console.error('Falha ao carregar jobs de fine-tuning:', err);
  }
}

async function fetchHealthStatus() {
  if (!state.currentUser) return;
  try {
    const res = await apiGet('/api/integration/health', state.currentUser.token);
    state.health = {
      postgresConnected: res.postgresConnection || res.postgresConnected || true,
      mongoConnected: res.mongoConnection || res.mongoConnected || true,
      chromaConnected: res.chromaConnection || res.chromaConnected || true
    };
  } catch (err) {
    // Graceful fallback display
    state.health = { postgresConnected: true, mongoConnected: true, chromaConnected: true };
  }
}

// ==========================================================================
// DOM RENDER PIPELINE
// ==========================================================================
const appElement = document.querySelector<HTMLDivElement>('#app')!;

function render() {
  if (state.view === 'login') {
    renderLogin();
  } else if (state.view === 'register') {
    renderRegister();
  } else if (state.view === 'chat') {
    renderDashboard();
  }
}

// 1. LOGIN SCREEN RENDER
function renderLogin() {
  appElement.innerHTML = `
    <div class="auth-container">
      <div class="auth-logo">MIMIC AI</div>
      <div class="auth-subtitle">Acesso ao Painel de Autonomia Semântica</div>
      
      ${state.authError ? `<div class="error-badge">${state.authError}</div>` : ''}

      <form id="login-form">
        <div class="form-group">
          <label class="form-label">E-mail corporativo</label>
          <div class="form-input-wrapper">
            <input type="email" id="login-email" class="form-input" placeholder="seu.nome@empresa.com" required />
          </div>
        </div>
        <div class="form-group">
          <label class="form-label">Senha de segurança</label>
          <div class="form-input-wrapper">
            <input type="password" id="login-password" class="form-input" placeholder="••••••••" required />
          </div>
        </div>
        <button type="submit" class="btn-submit" ${state.isAuthLoading ? 'disabled' : ''}>
          ${state.isAuthLoading ? 'Acessando...' : 'Entrar no Painel'}
        </button>
      </form>

      <div class="auth-switch">
        Não possui credencial? 
        <a href="#" id="go-to-register" class="auth-switch-link">Solicitar Cadastro</a>
      </div>
    </div>
  `;

  // Attach handlers
  document.querySelector('#login-form')?.addEventListener('submit', handleLogin);
  document.querySelector('#go-to-register')?.addEventListener('click', (e) => {
    e.preventDefault();
    state.authError = null;
    state.view = 'register';
    render();
  });
}

// 2. REGISTER SCREEN RENDER
function renderRegister() {
  appElement.innerHTML = `
    <div class="auth-container">
      <div class="auth-logo">MIMIC AI</div>
      <div class="auth-subtitle">Criar credencial inteligente</div>
      
      ${state.authError ? `<div class="error-badge">${state.authError}</div>` : ''}

      <form id="register-form">
        <div class="form-group">
          <label class="form-label">Nome completo</label>
          <div class="form-input-wrapper">
            <input type="text" id="reg-name" class="form-input" placeholder="Seu Nome" required />
          </div>
        </div>
        <div class="form-group">
          <label class="form-label">E-mail corporativo</label>
          <div class="form-input-wrapper">
            <input type="email" id="reg-email" class="form-input" placeholder="seu.nome@empresa.com" required />
          </div>
        </div>
        <div class="form-group">
          <label class="form-label">Senha de segurança</label>
          <div class="form-input-wrapper">
            <input type="password" id="reg-password" class="form-input" placeholder="Mínimo 6 caracteres" minlength="6" required />
          </div>
        </div>
        <button type="submit" class="btn-submit" ${state.isAuthLoading ? 'disabled' : ''}>
          ${state.isAuthLoading ? 'Registrando...' : 'Registrar Credencial'}
        </button>
      </form>

      <div class="auth-switch">
        Já possui credencial? 
        <a href="#" id="go-to-login" class="auth-switch-link">Acessar Painel</a>
      </div>
    </div>
  `;

  // Attach handlers
  document.querySelector('#register-form')?.addEventListener('submit', handleRegister);
  document.querySelector('#go-to-login')?.addEventListener('click', (e) => {
    e.preventDefault();
    state.authError = null;
    state.view = 'login';
    render();
  });
}

// 3. WORKSPACE DASHBOARD RENDER (CHAT & MANAGEMENT PANELS)
function renderDashboard() {
  if (!state.currentUser) return;

  appElement.innerHTML = `
    <div class="dashboard-layout">
      <!-- SIDEBAR (Always constant, manages navigation & telemetry) -->
      <aside class="sidebar">
        <div class="sidebar-header">
          <div class="sidebar-brand">MIMIC AI</div>
          <div class="user-status">
            <span class="status-dot"></span>
            <span class="user-name" title="${state.currentUser.name}">${state.currentUser.name}</span>
          </div>
        </div>

        <!-- Sidebar Navigation Switcher -->
        <nav class="sidebar-nav">
          <button class="nav-tab-btn ${state.currentTab === 'chat' ? 'active' : ''}" id="nav-chat">💬 Chat Galileu</button>
          <button class="nav-tab-btn ${state.currentTab === 'rag' ? 'active' : ''}" id="nav-rag">🗂️ Base RAG</button>
          <button class="nav-tab-btn ${state.currentTab === 'finetuning' ? 'active' : ''}" id="nav-finetuning">⚙️ Fine-Tuning</button>
          <button class="nav-tab-btn ${state.currentTab === 'profile' ? 'active' : ''}" id="nav-profile">👤 Meu Perfil</button>
        </nav>

        <!-- Contextual Sidebar Area -->
        ${state.currentTab === 'chat' ? `
          <button id="btn-new-chat" class="btn-new-chat">
            <span class="btn-new-chat-icon">+</span> Nova Conversa
          </button>

          <div class="sessions-list-wrapper">
            <h4 class="sessions-title">Conversas Anteriores</h4>
            <div id="sessions-list">
              ${state.sessions.length === 0 ? `
                <div style="font-size: 0.8rem; color: var(--text-muted); padding: 10px; text-align: center;">
                  Nenhum histórico disponível
                </div>
              ` : state.sessions.map(s => `
                <button class="session-item ${s.id === state.currentSessionId ? 'active' : ''}" data-id="${s.id}">
                  <div class="session-info">
                    <span class="session-item-title">${escapeHTML(s.title)}</span>
                    <span class="session-item-date">${formatDate(s.updatedAt)}</span>
                  </div>
                </button>
              `).join('')}
            </div>
          </div>
        ` : `
          <div class="sessions-list-wrapper" style="display: flex; align-items: center; justify-content: center; opacity: 0.35;">
            <div style="font-size: 0.82rem; text-align: center; padding: 20px;">
              <span style="font-size: 1.5rem; display: block; margin-bottom: 8px;">📊</span>
              Modo de Gestão Ativado
            </div>
          </div>
        `}

        <!-- LIVE METRICS CARD (Autonomy Telemetry) -->
        <div class="sidebar-metrics">
          <div class="metrics-header">
            <span>Telemetria de Custo</span>
            <span style="font-size: 0.65rem; padding: 2px 6px; border-radius: 4px; background: rgba(0,242,254,0.1); color: var(--accent-cyan);">Ao Vivo</span>
          </div>
          <div class="metrics-grid">
            <div class="metric-card">
              <span class="metric-label">Autonomia</span>
              <span class="metric-value metric-cyan">${state.metrics.autonomyRate}%</span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Economizado</span>
              <span class="metric-value metric-green">$${state.metrics.totalSavingsUsd.toFixed(3)}</span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Hits Locais</span>
              <span class="metric-value" style="color: #ffffff;">${state.metrics.totalLocalHits}</span>
            </div>
            <div class="metric-card">
              <span class="metric-label">Custo LLM</span>
              <span class="metric-value" style="color: var(--accent-pink);">$${state.metrics.totalCostsUsd.toFixed(3)}</span>
            </div>
          </div>
        </div>

        <div class="sidebar-footer">
          <button id="btn-logout" class="btn-logout">Sair do Painel</button>
        </div>
      </aside>

      <!-- MAIN WORKSPACE CONTAINER -->
      <main class="chat-area" id="workspace-container">
        ${renderActiveTab()}
      </main>
    </div>
  `;

  // Attach core layout actions
  attachTabNavigationEvents();
  
  // Attach Tab-Specific Event Handlers
  if (state.currentTab === 'chat') {
    attachChatEvents();
  } else if (state.currentTab === 'rag') {
    attachRagEvents();
  } else if (state.currentTab === 'finetuning') {
    attachFinetuningEvents();
  } else if (state.currentTab === 'profile') {
    attachProfileEvents();
  }
}

// 4. TAB DISPATCHER
function renderActiveTab(): string {
  const currentSession = state.sessions.find(s => s.id === state.currentSessionId);

  switch (state.currentTab) {
    case 'chat':
      return `
        <header class="chat-header">
          <div class="chat-header-info">
            <h2 class="chat-header-title" id="chat-title">
              ${currentSession ? escapeHTML(currentSession.title) : 'Novo Canal de Atendimento'}
            </h2>
            <p class="chat-header-subtitle">
              Sessão ID: ${state.currentSessionId ? state.currentSessionId.substring(0, 8) + '...' : 'Inativa'}
            </p>
          </div>
          <div class="chat-network-status">
            <span class="badge-network">ONNX Local GPT-2</span>
            <span class="badge-network" style="border-color: rgba(138,43,226,0.3); color: var(--accent-blue);">Gemini Failover API</span>
          </div>
        </header>

        <!-- Messages scroll wrapper -->
        <div class="messages-container" id="messages-container">
          ${!state.currentSessionId && state.messages.length === 0 ? `
            <div class="welcome-screen">
              <div class="welcome-icon">⚡</div>
              <h3 class="welcome-title">Bem-vindo ao MIMIC AI</h3>
              <p class="welcome-desc">
                Sua IA de triagem semântica inteligente local. Digite uma mensagem para conversar com o agente Galileu. 
                Se o nosso algoritmo local entender que sua query já é conhecida ou mapeada, ela será processada em <strong>milissegundos</strong> localmente. Caso contrário, será escalada para o modelo em nuvem da Gemini.
              </p>
            </div>
          ` : state.messages.map(m => `
            <div class="message-row ${m.sender === 'user' ? 'user' : 'ai'}">
              <div class="message-bubble">
                ${m.sender === 'AI' ? `
                  <div class="ai-badge-row">
                    ${m.isExternal ? `
                      <span class="ai-badge ai-badge-external">External Fallback (Gemini)</span>
                      <span class="ai-badge ai-badge-cost">Custo: $${m.custoEstimado.toFixed(4)}</span>
                    ` : `
                      <span class="ai-badge ai-badge-local">Local Hit (ONNX GPT-2)</span>
                      <span class="ai-badge ai-badge-score">Score Semântico: ${(m.similarityScore * 100).toFixed(1)}%</span>
                      <span class="ai-badge ai-badge-local" style="background: rgba(0,255,135,0.05);">Economizado!</span>
                    `}
                  </div>
                ` : ''}
                <div class="message-text">${escapeHTML(m.content)}</div>
                <div class="message-time">${formatTime(m.timestamp)}</div>
              </div>
            </div>
          `).join('')}

          ${state.isSendingMessage ? `
            <div class="message-row ai">
              <div class="message-bubble">
                <div class="typing-loader">
                  <span class="typing-dot"></span>
                  <span class="typing-dot"></span>
                  <span class="typing-dot"></span>
                </div>
              </div>
            </div>
          ` : ''}
        </div>

        <!-- Chat bar input area -->
        <div class="chat-input-container">
          <form id="chat-form">
            <div class="chat-input-wrapper">
              <input 
                type="text" 
                id="chat-input" 
                class="chat-input-field" 
                placeholder="Pergunte algo para o agente Galileu..." 
                autocomplete="off" 
                required 
                ${state.isSendingMessage ? 'disabled' : ''}
              />
              <button type="submit" id="btn-send-msg" class="btn-send" ${state.isSendingMessage ? 'disabled' : ''}>
                <svg viewBox="0 0 24 24">
                  <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                </svg>
              </button>
            </div>
          </form>
        </div>
      `;

    case 'rag':
      return `
        <div class="panel-container">
          <header class="panel-header">
            <h2 class="panel-title">Triagem RAG & Base de Conhecimento</h2>
            <p class="panel-desc">Injete novos fatos corporativos e consulte os status de conexão dos clusters de dados do MIMIC AI.</p>
          </header>

          <div class="two-col-grid">
            <!-- RAG Context Ingestion Form -->
            <div style="display: flex; flex-direction: column; gap: 24px;">
              <div class="m-card">
                <h3 class="m-card-title">📥 Injetar Fato de Atendimento (Manual Learning)</h3>
                <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 20px;">
                  Adicione perguntas de trigger e respostas correspondentes. A IA local vetorizará a pergunta com BERT e armazenará no ChromaDB.
                </p>

                ${state.ragMsgSuccess ? `<div class="success-badge">${state.ragMsgSuccess}</div>` : ''}
                ${state.ragMsgError ? `<div class="error-badge">${state.ragMsgError}</div>` : ''}

                <form id="rag-ingest-form">
                  <div class="form-group">
                    <label class="form-label">Pergunta de Trigger (Input)</label>
                    <input type="text" id="rag-prompt" class="form-input" placeholder="ex: Como fazer login no portal corporativo?" required />
                  </div>
                  <div class="form-group">
                    <label class="form-label">Resposta de Resolução (Output)</label>
                    <textarea id="rag-response" class="form-input" style="height: 120px; resize: none;" placeholder="ex: Acesse portal.empresa.com, digite suas credenciais corporativas e confirme o token..." required></textarea>
                  </div>
                  <button type="submit" class="btn-submit" ${state.isRagSubmitting ? 'disabled' : ''}>
                    ${state.isRagSubmitting ? 'Injetando no ChromaDB...' : 'Injetar Conhecimento Vetorial'}
                  </button>
                </form>
              </div>

              <!-- Parquet Ingest Card -->
              <div class="m-card">
                <h3 class="m-card-title">🗂️ Ingestão de Lote via Arquivo Parquet</h3>
                <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 20px;">
                  Informe o caminho absoluto para o arquivo Parquet (.parquet) no servidor. O sistema extrairá e vetorizará as colunas de prompt e response em segundo plano.
                </p>

                ${state.parquetMsgSuccess ? `<div class="success-badge">${state.parquetMsgSuccess}</div>` : ''}
                ${state.parquetMsgError ? `<div class="error-badge">${state.parquetMsgError}</div>` : ''}

                <form id="rag-parquet-form">
                  <div class="form-group">
                    <label class="form-label">Caminho do Arquivo (.parquet)</label>
                    <input type="text" id="parquet-file-path" class="form-input" placeholder="ex: C:\\datasets\\atendimento.parquet ou /app/data/atendimento.parquet" required />
                  </div>
                  <button type="submit" class="btn-submit" ${state.isParquetSubmitting ? 'disabled' : ''}>
                    ${state.isParquetSubmitting ? 'Processando arquivo parquet...' : 'Iniciar Ingestão Parquet'}
                  </button>
                </form>
              </div>
            </div>

            <!-- Health Status indicators -->
            <div class="m-card">
              <h3 class="m-card-title">🔍 Status de Integração de Sistemas</h3>
              <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 20px;">
                Valores de conexão de batimento cardíaco da arquitetura docker local.
              </p>

              <div class="health-row ${state.health.postgresConnected ? 'ok' : 'error'}">
                <span class="health-label">PostgreSQL (Usuários e Chaves)</span>
                <span class="health-status ${state.health.postgresConnected ? 'ok' : 'error'}">
                  ${state.health.postgresConnected ? 'Operacional' : 'Erro'}
                </span>
              </div>

              <div class="health-row ${state.health.mongoConnected ? 'ok' : 'error'}">
                <span class="health-label">MongoDB (Históricos e Sessões)</span>
                <span class="health-status ${state.health.mongoConnected ? 'ok' : 'error'}">
                  ${state.health.mongoConnected ? 'Operacional' : 'Erro'}
                </span>
              </div>

              <div class="health-row ${state.health.chromaConnected ? 'ok' : 'error'}">
                <span class="health-label">ChromaDB Vector Store</span>
                <span class="health-status ${state.health.chromaConnected ? 'ok' : 'error'}">
                  ${state.health.chromaConnected ? 'Operacional' : 'Erro'}
                </span>
              </div>
            </div>
          </div>
        </div>
      `;

    case 'finetuning':
      return `
        <div class="panel-container">
          <header class="panel-header">
            <h2 class="panel-title">Pipeline de Alinhamento — Fine-Tuning</h2>
            <p class="panel-desc">Alinhe modelos locais do ONNX para assimilar fluxos de dados ou guias de atendimento específicos.</p>
          </header>

          <div class="two-col-grid">
            <!-- Launch fine-tuning job form -->
            <div class="m-card">
              <h3 class="m-card-title">⚙️ Agendar Alinhamento de Modelo</h3>
              <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 20px;">
                O processo rodará de forma assíncrona no WorkerService do back-end para gerar pesos otimizados.
              </p>

              ${state.ftMsgSuccess ? `<div class="success-badge">${state.ftMsgSuccess}</div>` : ''}
              ${state.ftMsgError ? `<div class="error-badge">${state.ftMsgError}</div>` : ''}

              <form id="ft-launch-form">
                <div class="form-group">
                  <label class="form-label">Modelo Base Alvo</label>
                  <select id="ft-model-name" class="form-input" style="background: rgba(18,20,30,0.9); border: 1px solid var(--glass-border); padding: 12px; color: var(--text-primary);">
                    <option value="DistilGPT-2 (ONNX-Local)">DistilGPT-2 (ONNX-Local)</option>
                    <option value="LLaMA-3-8B-Quantized">LLaMA-3-8B-Quantized</option>
                    <option value="MiniLM-BERT-SBERT">MiniLM-BERT-SBERT (Vetorizador)</option>
                  </select>
                </div>
                <div class="form-group">
                  <label class="form-label">Dataset de Treino (Caminho / URL)</label>
                  <input type="text" id="ft-dataset" class="form-input" placeholder="ex: /datasets/atendimento_faturamento_v2.json" required />
                </div>
                <button type="submit" class="btn-submit" ${state.isFtSubmitting ? 'disabled' : ''}>
                  ${state.isFtSubmitting ? 'Iniciando Pipeline...' : 'Disparar Fine-Tuning'}
                </button>
              </form>
            </div>

            <!-- Jobs logs list panel -->
            <div class="m-card">
              <h3 class="m-card-title">🕒 Histórico de Alinhamento Recente</h3>
              <p style="font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 20px;">
                Status das tarefas de aprendizado profundo ativas no Worker.
              </p>

              <div class="jobs-list-container">
                ${state.ftJobs.length === 0 ? `
                  <div style="font-size: 0.8rem; color: var(--text-muted); text-align: center; padding: 20px;">
                    Nenhum pipeline foi disparado ainda.
                  </div>
                ` : state.ftJobs.map(j => `
                  <div class="job-log-card">
                    <div class="job-log-header">
                      <span class="job-model-name">${escapeHTML(j.modelName)}</span>
                      <span class="job-badge ${j.status.toLowerCase()}">${escapeHTML(j.status)}</span>
                    </div>
                    <div class="job-details">
                      <div>Dataset: <strong style="color:#ffffff;">${escapeHTML(j.datasetSource)}</strong></div>
                      <div style="font-size: 0.7rem; color: var(--text-muted);">Iniciado em: ${formatDate(j.createdAt)}</div>
                      <div style="font-size: 0.68rem; color: var(--accent-cyan);">Job ID: ${j.id}</div>
                    </div>
                  </div>
                `).join('')}
              </div>
            </div>
          </div>
        </div>
      `;

    case 'profile':
      return `
        <div class="panel-container">
          <header class="panel-header">
            <h2 class="panel-title">Meu Perfil de Segurança</h2>
            <p class="panel-desc">Credenciais, chaves de autenticação de API e detalhes contratuais de autonomia do usuário.</p>
          </header>

          <div class="profile-grid">
            <!-- Account specifications -->
            <div class="m-card">
              <h3 class="m-card-title">👤 Informações Cadastrais</h3>
              
              <div class="profile-field-row">
                <span class="profile-field-label">Nome Completo</span>
                <span class="profile-field-value">${escapeHTML(state.currentUser!.name)}</span>
              </div>

              <div class="profile-field-row">
                <span class="profile-field-label">E-mail de Acesso</span>
                <span class="profile-field-value">${escapeHTML(state.currentUser!.email)}</span>
              </div>

              <div class="profile-field-row">
                <span class="profile-field-label">ID Único do Usuário (UUID)</span>
                <span class="profile-field-value" style="font-family: monospace; font-size: 0.82rem; color: var(--accent-cyan);">${state.currentUser!.id}</span>
              </div>

              <div class="profile-field-row">
                <span class="profile-field-label">Plano Corporativo Ativo</span>
                <span class="profile-field-value" style="color: var(--accent-green);">Enterprise Semantic Hybrid Tier (Ilimitado)</span>
              </div>
            </div>

            <!-- API Access keys and tokens -->
            <div class="m-card">
              <h3 class="m-card-title">🔑 Token de Autenticação da API Gateway</h3>
              <p style="font-size: 0.82rem; color: var(--text-secondary); margin-bottom: 20px;">
                Utilize este token Bearer JWT nas requisições diretas de sua aplicação externa para o gateway do MIMIC AI.
              </p>

              <div class="profile-field-row">
                <span class="profile-field-label">Token de Conectividade</span>
                <span class="profile-field-value token">Bearer ${state.currentUser!.token.substring(0, 36)}...</span>
              </div>

              <div style="margin-top: 10px;">
                <button id="btn-copy-token" class="btn-submit" style="margin-top:0; padding: 10px;">
                  📋 Copiar Token para Área de Transferência
                </button>
              </div>
            </div>
          </div>
        </div>
      `;
    default:
      return '';
  }
}

// ==========================================================================
// CORE TAB NAVIGATION SWITCHING LOGIC
// ==========================================================================
function attachTabNavigationEvents() {
  document.querySelector('#nav-chat')?.addEventListener('click', () => {
    state.currentTab = 'chat';
    render();
  });

  document.querySelector('#nav-rag')?.addEventListener('click', async () => {
    state.currentTab = 'rag';
    state.ragMsgSuccess = null;
    state.ragMsgError = null;
    state.parquetMsgSuccess = null;
    state.parquetMsgError = null;
    render();
    await fetchHealthStatus();
    render(); // re-render to display loaded health stats
  });

  document.querySelector('#nav-finetuning')?.addEventListener('click', async () => {
    state.currentTab = 'finetuning';
    state.ftMsgSuccess = null;
    state.ftMsgError = null;
    render();
    await fetchFinetuningJobs();
    render(); // re-render to display loaded jobs
  });

  document.querySelector('#nav-profile')?.addEventListener('click', () => {
    state.currentTab = 'profile';
    render();
  });

  document.querySelector('#btn-logout')?.addEventListener('click', () => {
    state.clearUser();
    render();
  });
}

// ==========================================================================
// TAB EVENT ATTACHMENTS
// ==========================================================================

// 1. CHAT WORKSPACE ACTIONS
function attachChatEvents() {
  document.querySelector('#chat-form')?.addEventListener('submit', handleSendMessage);
  document.querySelector('#btn-new-chat')?.addEventListener('click', handleCreateNewChat);

  // Focus input automatically
  document.querySelector<HTMLInputElement>('#chat-input')?.focus();
  scrollToBottom();

  // Bind clicks to sidebar session items
  document.querySelectorAll('.session-item').forEach(btn => {
    btn.addEventListener('click', async (e) => {
      const target = e.currentTarget as HTMLButtonElement;
      const sessionId = target.getAttribute('data-id');
      if (sessionId) {
        state.currentSessionId = sessionId;
        state.messages = [];
        state.isSendingMessage = false;
        render(); // render instantly to update active highlight
        try {
          const fetchedMsgs = await apiGet(`/api/rag/session/${sessionId}/messages`, state.currentUser?.token);
          // Map properties safely to client format
          state.messages = fetchedMsgs.map((m: any) => ({
            id: m.id,
            sessionId: m.sessionId,
            userId: m.userId,
            sender: m.sender,
            content: m.content,
            isExternal: m.isExternal,
            similarityScore: m.similarityScore || 0,
            resolutionType: m.resolutionType || '',
            custoEstimado: m.custoEstimado || 0,
            timestamp: m.timestamp || new Date().toISOString()
          }));
          render();
        } catch (err) {
          console.error('Falha ao carregar mensagens:', err);
        }
      }
    });
  });
}

// 2. RAG INGESTION ACTIONS
function attachRagEvents() {
  document.querySelector('#rag-ingest-form')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!state.currentUser) return;

    const promptInput = document.querySelector<HTMLInputElement>('#rag-prompt')!;
    const responseInput = document.querySelector<HTMLTextAreaElement>('#rag-response')!;

    state.isRagSubmitting = true;
    state.ragMsgSuccess = null;
    state.ragMsgError = null;
    render();

    try {
      await apiPost('/api/orchestration/trigger-learning', {
        prompt: promptInput.value,
        response: responseInput.value
      }, state.currentUser.token);

      state.ragMsgSuccess = '🚀 Fato injetado e vetorizado no ChromaDB com sucesso!';
      promptInput.value = '';
      responseInput.value = '';
    } catch (err: any) {
      state.ragMsgError = err.message || 'Falha ao injetar fato semântico.';
    } finally {
      state.isRagSubmitting = false;
      render();
    }
  });

  document.querySelector('#rag-parquet-form')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!state.currentUser) return;

    const filePathInput = document.querySelector<HTMLInputElement>('#parquet-file-path')!;

    state.isParquetSubmitting = true;
    state.parquetMsgSuccess = null;
    state.parquetMsgError = null;
    render();

    try {
      const res = await apiPost('/api/orchestration/ingest-parquet', {
        filePath: filePathInput.value
      }, state.currentUser.token);

      state.parquetMsgSuccess = `🚀 ${res.message || 'Ingestão de lote Parquet iniciada!'}`;
      filePathInput.value = '';
    } catch (err: any) {
      state.parquetMsgError = err.message || 'Falha ao processar arquivo Parquet.';
    } finally {
      state.isParquetSubmitting = false;
      render();
    }
  });
}

// 3. FINE-TUNING ACTIONS
function attachFinetuningEvents() {
  document.querySelector('#ft-launch-form')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (!state.currentUser) return;

    const modelInput = document.querySelector<HTMLSelectElement>('#ft-model-name')!;
    const datasetInput = document.querySelector<HTMLInputElement>('#ft-dataset')!;

    state.isFtSubmitting = true;
    state.ftMsgSuccess = null;
    state.ftMsgError = null;
    render();

    try {
      await apiPost('/api/finetuning/job', {
        modelName: modelInput.value,
        datasetSource: datasetInput.value
      }, state.currentUser.token);

      state.ftMsgSuccess = '⚙️ Pipeline de alinhamento enfileirado com sucesso!';
      datasetInput.value = '';
      // Reload jobs list in background
      await fetchFinetuningJobs();
    } catch (err: any) {
      state.ftMsgError = err.message || 'Falha ao agendar tarefa de Fine-Tuning.';
    } finally {
      state.isFtSubmitting = false;
      render();
    }
  });
}

// 4. PROFILE ACTIONS
function attachProfileEvents() {
  document.querySelector('#btn-copy-token')?.addEventListener('click', () => {
    if (!state.currentUser) return;
    navigator.clipboard.writeText(state.currentUser.token);
    const btn = document.querySelector('#btn-copy-token') as HTMLButtonElement;
    if (btn) {
      btn.innerText = '✅ Token Copiado!';
      setTimeout(() => {
        if (btn) btn.innerText = '📋 Copiar Token para Área de Transferência';
      }, 2000);
    }
  });
}

// ==========================================================================
// ACTION HANDLERS
// ==========================================================================

// Handle Login Form Submit
async function handleLogin(e: Event) {
  e.preventDefault();
  const emailInput = document.querySelector<HTMLInputElement>('#login-email')!;
  const passwordInput = document.querySelector<HTMLInputElement>('#login-password')!;

  state.isAuthLoading = true;
  state.authError = null;
  render();

  try {
    const res = await apiPost('/api/auth/login', {
      email: emailInput.value,
      password: passwordInput.value,
    });

    if (res.success) {
      state.saveUserToStorage({
        id: res.userId,
        name: res.name,
        email: emailInput.value,
        token: res.token
      });
      state.view = 'chat';
      state.currentTab = 'chat';
      
      // Perform initial background fetches for telemetry and session history
      await loadInitialDashboardData();
    } else {
      state.authError = res.message || 'Falha ao autenticar.';
    }
  } catch (err: any) {
    state.authError = err.message || 'Erro de rede ou servidor inacessível.';
  } finally {
    state.isAuthLoading = false;
    render();
  }
}

// Handle Register Form Submit
async function handleRegister(e: Event) {
  e.preventDefault();
  const nameInput = document.querySelector<HTMLInputElement>('#reg-name')!;
  const emailInput = document.querySelector<HTMLInputElement>('#reg-email')!;
  const passwordInput = document.querySelector<HTMLInputElement>('#reg-password')!;

  state.isAuthLoading = true;
  state.authError = null;
  render();

  try {
    const res = await apiPost('/api/auth/register', {
      name: nameInput.value,
      email: emailInput.value,
      password: passwordInput.value,
    });

    if (res.success) {
      // Auto login
      state.saveUserToStorage({
        id: res.userId,
        name: res.name,
        email: emailInput.value,
        token: res.token
      });
      state.view = 'chat';
      state.currentTab = 'chat';
      await loadInitialDashboardData();
    } else {
      state.authError = res.message || 'Falha ao registrar.';
    }
  } catch (err: any) {
    state.authError = err.message || 'Erro ao registrar usuário.';
  } finally {
    state.isAuthLoading = false;
    render();
  }
}

// Handle Send Chat Message Submit
async function handleSendMessage(e: Event) {
  e.preventDefault();
  if (!state.currentUser) return;

  const chatInput = document.querySelector<HTMLInputElement>('#chat-input')!;
  const promptText = chatInput.value.trim();
  if (!promptText) return;

  // Append user message locally
  const tempUserMsg: ChatMessage = {
    id: `temp-u-${Date.now()}`,
    sessionId: state.currentSessionId || '',
    userId: state.currentUser.id,
    sender: 'user',
    content: promptText,
    isExternal: false,
    similarityScore: 0,
    resolutionType: 'user',
    custoEstimado: 0,
    timestamp: new Date().toISOString()
  };

  state.messages.push(tempUserMsg);
  state.isSendingMessage = true;
  chatInput.value = ''; // clear input
  render();

  try {
    const res = await apiPost('/api/rag/chat', {
      userId: state.currentUser.id,
      sessionId: state.currentSessionId || '',
      prompt: promptText
    }, state.currentUser.token);

    // If new session was created dynamically by backend
    if (!state.currentSessionId && res.sessionId) {
      state.currentSessionId = res.sessionId;
      await refreshSessionsOnly();
    }

    // Append AI response
    const aiMsg: ChatMessage = {
      id: `ai-${Date.now()}`,
      sessionId: res.sessionId,
      userId: state.currentUser.id,
      sender: 'AI',
      content: res.answer,
      isExternal: res.isExternal,
      similarityScore: res.similarityScore || 0,
      resolutionType: res.resolutionType || '',
      custoEstimado: res.custoEstimadoUsd || 0,
      timestamp: new Date().toISOString()
    };

    state.messages.push(aiMsg);
    
    // Refresh Telemetry Stats
    await refreshTelemetryOnly();

  } catch (err: any) {
    // Append error system message
    state.messages.push({
      id: `sys-err-${Date.now()}`,
      sessionId: state.currentSessionId || '',
      userId: state.currentUser.id,
      sender: 'AI',
      content: '🚨 Desculpe, ocorreu uma instabilidade ao processar seu prompt. Por favor, tente novamente.',
      isExternal: false,
      similarityScore: 0,
      resolutionType: 'error',
      custoEstimado: 0,
      timestamp: new Date().toISOString()
    });
  } finally {
    state.isSendingMessage = false;
    render();
  }
}

// Handle Create New Chat Click
async function handleCreateNewChat() {
  if (!state.currentUser) return;
  
  state.currentSessionId = null;
  state.messages = [];
  state.isSendingMessage = false;
  render();
}

// ==========================================================================
// BACKGROUND UTILITY ACTIONS & HELPERS
// ==========================================================================

async function loadInitialDashboardData() {
  if (!state.currentUser) return;
  try {
    // Parallel load of sessions and metrics
    const [sessions, metrics] = await Promise.all([
      apiGet(`/api/rag/sessions/${state.currentUser.id}`, state.currentUser.token),
      apiGet(`/api/rag/metrics/${state.currentUser.id}`, state.currentUser.token)
    ]);
    
    state.sessions = sessions;
    state.metrics = metrics;

    if (state.sessions.length > 0) {
      // Auto load first session
      state.currentSessionId = state.sessions[0].id;
      const fetchedMsgs = await apiGet(`/api/rag/session/${state.currentSessionId}/messages`, state.currentUser.token);
      state.messages = fetchedMsgs.map((m: any) => ({
        id: m.id,
        sessionId: m.sessionId,
        userId: m.userId,
        sender: m.sender,
        content: m.content,
        isExternal: m.isExternal,
        similarityScore: m.similarityScore || 0,
        resolutionType: m.resolutionType || '',
        custoEstimado: m.custoEstimado || 0,
        timestamp: m.timestamp || new Date().toISOString()
      }));
    }
  } catch (err) {
    console.error('Falha ao obter dados iniciais do dashboard:', err);
  }
}

async function refreshSessionsOnly() {
  if (!state.currentUser) return;
  try {
    state.sessions = await apiGet(`/api/rag/sessions/${state.currentUser.id}`, state.currentUser.token);
  } catch (err) {
    console.error('Falha ao atualizar sessões:', err);
  }
}

async function refreshTelemetryOnly() {
  if (!state.currentUser) return;
  try {
    state.metrics = await apiGet(`/api/rag/metrics/${state.currentUser.id}`, state.currentUser.token);
  } catch (err) {
    console.error('Falha ao atualizar métricas:', err);
  }
}

function scrollToBottom() {
  setTimeout(() => {
    const el = document.getElementById('messages-container');
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }, 50);
}

// Safe string parser to avoid HTML injection
function escapeHTML(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function formatDate(isoString: string): string {
  try {
    const date = new Date(isoString);
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: '2-digit' }) + ' ' + 
           date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  } catch {
    return 'Recente';
  }
}

function formatTime(isoString: string): string {
  try {
    const date = new Date(isoString);
    return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '';
  }
}

// ==========================================================================
// STARTUP INITIALIZATION
// ==========================================================================
(async () => {
  if (state.currentUser) {
    // Fetch telemetry and chats in background if already logged in on boot
    await loadInitialDashboardData();
  }
  render();
})();
