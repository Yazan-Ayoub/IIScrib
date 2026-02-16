// IIScribe Web App
class IIScribeApp {
    constructor() {
        this.apiBase = 'http://localhost:5000/api';
        this.currentDeploymentId = null;
        this.init();
    }

    init() {
        this.setupNavigation();
        this.setupForms();
        this.loadDashboard();
        this.setupEventListeners();
        
        console.log('🚀 IIScribe initialized');
    }

    setupNavigation() {
        const navItems = document.querySelectorAll('.nav-item');
        
        navItems.forEach(item => {
            item.addEventListener('click', (e) => {
                e.preventDefault();
                
                // Update active state
                navItems.forEach(ni => ni.classList.remove('active'));
                item.classList.add('active');
                
                // Show corresponding page
                const pageName = item.dataset.page;
                this.showPage(pageName);
            });
        });
    }

    showPage(pageName) {
        const pages = document.querySelectorAll('.page');
        pages.forEach(page => page.classList.remove('active'));
        
        const activePage = document.getElementById(`${pageName}-page`);
        if (activePage) {
            activePage.classList.add('active');
            
            // Load page data
            switch(pageName) {
                case 'dashboard':
                    this.loadDashboard();
                    break;
                case 'deployments':
                    this.loadDeployments();
                    break;
                case 'profiles':
                    this.loadProfiles();
                    break;
                case 'sites':
                    this.loadSites();
                    break;
            }
        }
    }

    setupForms() {
        // Deploy form
        const deployForm = document.getElementById('deploy-form');
        if (deployForm) {
            deployForm.addEventListener('submit', (e) => {
                e.preventDefault();
                this.handleDeploy();
            });
        }

        // Database toggle
        const enableDatabase = document.getElementById('enable-database');
        const databaseConfig = document.getElementById('database-config');
        if (enableDatabase && databaseConfig) {
            enableDatabase.addEventListener('change', (e) => {
                databaseConfig.style.display = e.target.checked ? 'block' : 'none';
            });
        }

        // SSL toggle
        const enableSsl = document.getElementById('enable-ssl');
        const sslConfig = document.getElementById('ssl-config');
        if (enableSsl && sslConfig) {
            enableSsl.addEventListener('change', (e) => {
                sslConfig.style.display = e.target.checked ? 'block' : 'none';
            });
        }
    }

    setupEventListeners() {
        // Filter deployments
        const filterStatus = document.getElementById('filter-status');
        const filterEnvironment = document.getElementById('filter-environment');
        
        if (filterStatus) {
            filterStatus.addEventListener('change', () => this.loadDeployments());
        }
        
        if (filterEnvironment) {
            filterEnvironment.addEventListener('change', () => this.loadDeployments());
        }
    }

    async loadDashboard() {
        try {
            // Load mock data for demo
            const stats = this.getMockStats();
            
            // Update stats
            document.getElementById('total-deployments').textContent = stats.totalDeployments;
            document.getElementById('active-sites').textContent = stats.activeSites;
            document.getElementById('success-rate').textContent = stats.successRate + '%';
            document.getElementById('certs-expiring').textContent = stats.certsExpiring;
            
            // Load recent deployments
            this.loadRecentDeployments();
            
        } catch (error) {
            console.error('Error loading dashboard:', error);
            this.showError('Failed to load dashboard data');
        }
    }

    async loadRecentDeployments() {
        const tbody = document.querySelector('#recent-deployments-table tbody');
        if (!tbody) return;
        
        const deployments = this.getMockDeployments().slice(0, 5);
        
        tbody.innerHTML = deployments.map(dep => `
            <tr>
                <td>${dep.name}</td>
                <td><span class="badge info">${dep.environment}</span></td>
                <td><span class="badge ${this.getStatusClass(dep.status)}">${dep.status}</span></td>
                <td>${dep.duration}s</td>
                <td>${this.formatDate(dep.createdAt)}</td>
                <td>
                    <button class="btn btn-secondary" onclick="app.viewDeployment('${dep.id}')">View</button>
                </td>
            </tr>
        `).join('');
    }

    async loadDeployments() {
        const tbody = document.querySelector('#deployments-table tbody');
        if (!tbody) return;
        
        const filterStatus = document.getElementById('filter-status')?.value || '';
        const filterEnvironment = document.getElementById('filter-environment')?.value || '';
        
        let deployments = this.getMockDeployments();
        
        // Apply filters
        if (filterStatus) {
            deployments = deployments.filter(d => d.status === filterStatus);
        }
        if (filterEnvironment) {
            deployments = deployments.filter(d => d.environment === filterEnvironment);
        }
        
        tbody.innerHTML = deployments.map(dep => `
            <tr>
                <td><code>${dep.id.substring(0, 8)}</code></td>
                <td>${dep.name}</td>
                <td><span class="badge info">${dep.environment}</span></td>
                <td>${dep.target}</td>
                <td><span class="badge ${this.getStatusClass(dep.status)}">${dep.status}</span></td>
                <td>${dep.duration}s</td>
                <td>${this.formatDate(dep.createdAt)}</td>
                <td>
                    <button class="btn btn-secondary" onclick="app.viewDeployment('${dep.id}')">
                        View
                    </button>
                    ${dep.status === 'Success' ? `
                        <button class="btn btn-danger" onclick="app.rollbackDeployment('${dep.id}')">
                            Rollback
                        </button>
                    ` : ''}
                </td>
            </tr>
        `).join('');
    }

    async loadProfiles() {
        const grid = document.getElementById('profiles-grid');
        if (!grid) return;
        
        const profiles = this.getMockProfiles();
        
        grid.innerHTML = profiles.map(profile => `
            <div class="profile-card" onclick="app.useProfile('${profile.id}')">
                <div class="profile-header">
                    <div class="profile-title">${profile.name}</div>
                    <div class="profile-actions">
                        <button class="btn btn-secondary" onclick="event.stopPropagation(); app.editProfile('${profile.id}')">
                            Edit
                        </button>
                    </div>
                </div>
                <div class="profile-description">${profile.description}</div>
                <div class="profile-meta">
                    <span class="badge info">${profile.environment}</span>
                    <span class="badge info">${profile.target}</span>
                    <span class="badge success">${profile.deploymentCount} deployments</span>
                </div>
            </div>
        `).join('');
    }

    async loadSites() {
        const grid = document.getElementById('sites-grid');
        if (!grid) return;
        
        const sites = this.getMockSites();
        
        grid.innerHTML = sites.map(site => `
            <div class="site-card">
                <div class="site-header">
                    <div class="site-name">${site.name}</div>
                    <div class="site-status">
                        <span class="status-dot ${site.isRunning ? 'running' : 'stopped'}"></span>
                        <span>${site.isRunning ? 'Running' : 'Stopped'}</span>
                    </div>
                </div>
                <a href="${site.url}" class="site-url" target="_blank">${site.url}</a>
                <div class="site-meta">
                    <div><strong>App Pool:</strong> ${site.appPoolName}</div>
                    <div><strong>Environment:</strong> ${site.environment}</div>
                    <div><strong>Last Deployed:</strong> ${this.formatDate(site.lastDeployed)}</div>
                </div>
                <div class="site-actions">
                    ${site.isRunning ? `
                        <button class="btn btn-danger" onclick="app.stopSite('${site.name}')">Stop</button>
                    ` : `
                        <button class="btn btn-success" onclick="app.startSite('${site.name}')">Start</button>
                    `}
                    <button class="btn btn-secondary" onclick="app.browseSite('${site.url}')">Browse</button>
                </div>
            </div>
        `).join('');
    }

    async handleDeploy() {
        const form = document.getElementById('deploy-form');
        const progressContainer = document.getElementById('deployment-progress');
        
        // Get form data
        const deployData = {
            applicationPath: document.getElementById('app-path').value,
            domainName: document.getElementById('domain-name').value,
            httpPort: parseInt(document.getElementById('http-port').value),
            httpsPort: parseInt(document.getElementById('https-port').value),
            environment: document.getElementById('environment').value,
            target: document.getElementById('target').value,
            strategy: document.getElementById('strategy').value,
            runHealthChecks: true,
            sendNotifications: false
        };

        // Add database config if enabled
        if (document.getElementById('enable-database').checked) {
            deployData.databaseConfig = {
                provider: document.getElementById('db-provider').value,
                databaseName: document.getElementById('db-name').value,
                deploymentMode: 'Migrate',
                backupBeforeDeployment: document.getElementById('db-backup').checked,
                autoRollbackOnFailure: true
            };
        }

        // Add SSL config if enabled
        if (document.getElementById('enable-ssl').checked) {
            deployData.sslConfig = {
                certificateType: document.getElementById('cert-type').value,
                validityDays: 365,
                autoRenew: true
            };
        }

        // Hide form, show progress
        form.style.display = 'none';
        progressContainer.classList.remove('hidden');

        try {
            // Simulate deployment process
            await this.simulateDeployment(deployData);
            
            this.showSuccess('Deployment completed successfully!');
            
            // Reset after 3 seconds
            setTimeout(() => {
                form.style.display = 'block';
                progressContainer.classList.add('hidden');
                form.reset();
                this.resetProgress();
            }, 3000);
            
        } catch (error) {
            console.error('Deployment error:', error);
            this.showError('Deployment failed: ' + error.message);
            
            form.style.display = 'block';
            progressContainer.classList.add('hidden');
        }
    }

    async simulateDeployment(data) {
        const steps = [
            { step: 1, name: 'Discovery', message: 'Analyzing application...', duration: 1000 },
            { step: 2, name: 'Database', message: 'Running migrations...', duration: 2000 },
            { step: 3, name: 'SSL', message: 'Generating certificate...', duration: 1000 },
            { step: 4, name: 'Deploy', message: 'Deploying to IIS...', duration: 2000 },
            { step: 5, name: 'Health Check', message: 'Running health checks...', duration: 1000 }
        ];

        for (const step of steps) {
            this.updateProgress(step.step, step.message, 'active');
            this.addLog(step.message, 'info');
            
            await this.delay(step.duration);
            
            this.updateProgress(step.step, 'Completed ✓', 'completed');
            this.addLog(`${step.name} completed successfully`, 'success');
        }

        // Update overall progress
        this.setProgressBar(100);
    }

    updateProgress(stepNumber, status, state) {
        const step = document.querySelector(`.progress-step[data-step="${stepNumber}"]`);
        if (!step) return;
        
        step.classList.remove('active', 'completed');
        if (state) {
            step.classList.add(state);
        }
        
        const statusEl = step.querySelector('.step-status');
        if (statusEl) {
            statusEl.textContent = status;
        }
        
        // Calculate progress percentage
        const progress = (stepNumber / 5) * 100;
        this.setProgressBar(progress);
    }

    setProgressBar(percent) {
        const fill = document.querySelector('.progress-fill');
        const percentText = document.querySelector('.progress-percent');
        
        if (fill) fill.style.width = `${percent}%`;
        if (percentText) percentText.textContent = `${Math.round(percent)}%`;
    }

    addLog(message, type = 'info') {
        const logContent = document.getElementById('deployment-log-content');
        if (!logContent) return;
        
        const timestamp = new Date().toLocaleTimeString();
        const logEntry = document.createElement('div');
        logEntry.className = `log-entry ${type}`;
        logEntry.textContent = `[${timestamp}] ${message}`;
        
        logContent.appendChild(logEntry);
        logContent.scrollTop = logContent.scrollHeight;
    }

    resetProgress() {
        const steps = document.querySelectorAll('.progress-step');
        steps.forEach(step => {
            step.classList.remove('active', 'completed');
            const status = step.querySelector('.step-status');
            if (status) status.textContent = 'Waiting...';
        });
        
        this.setProgressBar(0);
        
        const logContent = document.getElementById('deployment-log-content');
        if (logContent) logContent.innerHTML = '';
    }

    viewDeployment(id) {
        alert(`Viewing deployment: ${id}\n\nIn a production app, this would show detailed deployment information.`);
    }

    async rollbackDeployment(id) {
        if (!confirm('Are you sure you want to rollback this deployment?')) {
            return;
        }
        
        this.showSuccess('Rollback initiated for deployment ' + id.substring(0, 8));
        
        // In production, call: await this.api('POST', `/deployments/${id}/rollback`);
    }

    useProfile(id) {
        this.showSuccess('Profile loaded! Fill in the application path to deploy.');
        this.showPage('deploy');
    }

    editProfile(id) {
        alert('Edit profile: ' + id);
    }

    stopSite(name) {
        this.showSuccess(`Site ${name} stopped`);
        setTimeout(() => this.loadSites(), 500);
    }

    startSite(name) {
        this.showSuccess(`Site ${name} started`);
        setTimeout(() => this.loadSites(), 500);
    }

    browseSite(url) {
        window.open(url, '_blank');
    }

    // API helper
    async api(method, endpoint, data = null) {
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json'
            }
        };
        
        if (data) {
            options.body = JSON.stringify(data);
        }
        
        const response = await fetch(this.apiBase + endpoint, options);
        
        if (!response.ok) {
            throw new Error(`API error: ${response.statusText}`);
        }
        
        return response.json();
    }

    // Utility methods
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    formatDate(date) {
        return new Date(date).toLocaleString();
    }

    getStatusClass(status) {
        const map = {
            'Success': 'success',
            'Failed': 'danger',
            'InProgress': 'warning',
            'Pending': 'info',
            'RolledBack': 'warning'
        };
        return map[status] || 'info';
    }

    showSuccess(message) {
        this.showNotification(message, 'success');
    }

    showError(message) {
        this.showNotification(message, 'error');
    }

    showNotification(message, type) {
        // Simple alert for now - in production, use a toast library
        alert(message);
    }

    // Mock data for demonstration
    getMockStats() {
        return {
            totalDeployments: 247,
            activeSites: 15,
            successRate: 94.7,
            certsExpiring: 2
        };
    }

    getMockDeployments() {
        return [
            {
                id: 'dep-001-' + Date.now(),
                name: 'MyWebApp',
                environment: 'Production',
                target: 'LocalIIS',
                status: 'Success',
                duration: 47,
                createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000)
            },
            {
                id: 'dep-002-' + Date.now(),
                name: 'WebAPI',
                environment: 'Staging',
                target: 'AzureVM',
                status: 'Success',
                duration: 62,
                createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000)
            },
            {
                id: 'dep-003-' + Date.now(),
                name: 'Dashboard',
                environment: 'Development',
                target: 'LocalIIS',
                status: 'Failed',
                duration: 15,
                createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000)
            },
            {
                id: 'dep-004-' + Date.now(),
                name: 'MobileAPI',
                environment: 'Production',
                target: 'AzureVM',
                status: 'Success',
                duration: 89,
                createdAt: new Date(Date.now() - 48 * 60 * 60 * 1000)
            },
            {
                id: 'dep-005-' + Date.now(),
                name: 'AdminPortal',
                environment: 'Development',
                target: 'LocalIIS',
                status: 'InProgress',
                duration: 0,
                createdAt: new Date()
            }
        ];
    }

    getMockProfiles() {
        return [
            {
                id: 'prof-001',
                name: 'Production Template',
                description: 'Standard production deployment with blue-green strategy',
                environment: 'Production',
                target: 'AzureVM',
                deploymentCount: 45
            },
            {
                id: 'prof-002',
                name: 'Development Quick Deploy',
                description: 'Fast deployment for local development',
                environment: 'Development',
                target: 'LocalIIS',
                deploymentCount: 123
            },
            {
                id: 'prof-003',
                name: 'Staging Validation',
                description: 'Staging environment with health checks',
                environment: 'Staging',
                target: 'AzureVM',
                deploymentCount: 67
            }
        ];
    }

    getMockSites() {
        return [
            {
                name: 'MyWebApp',
                url: 'https://mywebapp.local',
                isRunning: true,
                appPoolName: 'MyWebApp_Pool',
                environment: 'Production',
                lastDeployed: new Date(Date.now() - 2 * 60 * 60 * 1000)
            },
            {
                name: 'WebAPI',
                url: 'https://webapi.local',
                isRunning: true,
                appPoolName: 'WebAPI_Pool',
                environment: 'Development',
                lastDeployed: new Date(Date.now() - 5 * 60 * 60 * 1000)
            },
            {
                name: 'Dashboard',
                url: 'https://dashboard.local',
                isRunning: false,
                appPoolName: 'Dashboard_Pool',
                environment: 'Development',
                lastDeployed: new Date(Date.now() - 24 * 60 * 60 * 1000)
            }
        ];
    }
}

// Initialize app when DOM is ready
let app;
document.addEventListener('DOMContentLoaded', () => {
    app = new IIScribeApp();
});
