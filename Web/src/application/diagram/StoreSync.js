export const rootCanvasId = 'root'

export default class StoreSync {

    store = null
    local = null
    remote = null
    isSyncEnabled = false

    constructor(store) {
        this.store = store
        this.local = store.local
        this.remote = store.remote
    }

    setHandlers(setError, setProgress, setSyncMode) {
        this.setError = setError
        this.setProgress = setProgress
        this.setSyncMode = setSyncMode
    }


    async initialize() {
        console.log('initialize')
        let sync = this.local.getSync()
        let didConnect = false

        console.log('sync', sync)
        if (sync.isConnecting) {
            console.log('Connecting after a previous login')
            // A previous login triggered reload and now we should call connect
            this.local.updateSync({ isConnecting: false })
            const connectData = await this.remote.connect()
            didConnect = true
            console.log('connected', connectData)
            sync = this.local.updateSync({ token: connectData.token })
        }

        if (!sync.token) {
            console.log('No sync token, sync is disabled')
            this.isSyncEnabled = false
            this.setSyncMode(false)
            return
        }

        if (!didConnect) {
            // Checking connection with api (but only if not already connected)
            await this.remote.check()
        }

        this.remote.setToken(sync.token)
        this.isSyncEnabled = true
        this.setSyncMode(true)
        console.log('Sync is enabled')

        await this.syncDiagrams()
    }

    async login(provider) {
        console.log('Login with', provider)
        this.local.updateSync({ isConnecting: true, provider: provider })

        try {
            // Checking if user already is logged in with the specified provider
            const user = await this.remote.getCurrentUser()
            if (user?.clientPrincipal?.identityProvider === provider) {
                // User is logged in, lets just reload site (no need to login again)
                console.log('Still logged in with', provider)
                window.location.reload()
            }

        } catch (error) {
            // Failed to check current user, lets ignore that and login
        }

        // Login for the specified id provider
        if (provider === 'Local') {
            // Local (dev), just reload
            window.location.reload()
        } else if (provider === 'Google') {
            window.location.href = `/.auth/login/google`;
        } else if (provider === 'Microsoft') {
            window.location.href = `/.auth/login/aad`;
        } else if (provider === 'Facebook') {
            window.location.href = `/.auth/login/facebook`;
        } else if (provider === 'GitHub') {
            window.location.href = `/.auth/login/github`;
        } else {
            this.local.updateSync({ isConnecting: false, provider: null })
            throw new Error('Unsupported identity provider ' + provider)
        }
    }


    async disableCloudSync() {
        if (!this.isSyncEnabled) {
            return
        }
        this.setProgress(true)
        try {
            console.log('Disable cloud sync')
            this.isSyncEnabled = false
            this.local.updateSync({ token: null, isConnecting: false, provider: null })
            this.remote.setToken(null)
            window.location.href = `/.auth/logout`;
        } catch (error) {
            this.setError('Failed to disable cloud sync')
        } finally {
            this.setProgress(false)
        }
    }


    async openDiagramRootCanvas(diagramId) {
        if (!this.isSyncEnabled) {
            return null
        }
        // Try to get diagram from remote server and cache locally
        const diagram = await this.remote.getDiagram(diagramId)
        this.local.writeDiagram(diagram)

        try {
            // Now read the root canvas from local store
            console.log('gettin ', diagramId)
            const d = this.local.readCanvas(diagramId, rootCanvasId)
            console.log('got', d)
            return d
        } catch (error) {
            console.trace('error', error)
            throw error
        }

    }

    async newDiagram(diagram) {
        if (!this.isSyncEnabled) {
            return

        }
        // Sync with remote server
        const diagramInfo = await this.remote.newDiagram(diagram)
        this.local.writeDiagramInfo(diagramInfo)
    }

    setCanvas(canvas) {
        if (!this.isSyncEnabled) {
            return
        }

        // Sync with remote server
        this.remote.setCanvas(canvas)
            .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
            .catch(error => this.setError('Failed to sync canvas change'))
    }

    async deleteDiagram(diagramId) {
        if (!this.isSyncEnabled) {
            return
        }

        await this.remote.deleteDiagram(diagramId)
        await this.syncDiagrams()
    }

    setDiagramName(diagramId, name) {
        if (!this.isSyncEnabled) {
            return
        }

        this.remote.updateDiagram({ diagramInfo: { diagramId: diagramId, name: name } })
            .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
            .catch(error => this.setError('Failed to sync name change'))
    }

    async uploadDiagrams(diagrams) {
        if (!this.isSyncEnabled) {
            return false
        }

        // Store all read diagram
        await this.remote.uploadDiagrams(diagrams)
        await this.syncDiagrams()
        return true
    }

    async downloadAllDiagrams() {
        if (!this.isSyncEnabled) {
            return null
        }
        return await this.remote.downloadAllDiagrams()
    }

    async clearRemoteData() {
        this.setProgress(true)
        try {
            if (!this.isSyncEnabled) {
                this.setError('Cloud sync not enabled, cannot clear remote data')
                return false
            }
            await this.remote.clearAllData()
            return true
        } catch (error) {
            this.setError('Failed to clear remote data, ' + error.message)
            return false
        } finally {
            this.setProgress(false)
        }
    }

    async syncDiagrams() {
        if (!this.isSyncEnabled) {
            return
        }

        console.log('Syncing')
        const currentId = this.store.getMostResentDiagramId()

        // Get all remote server diagrams data and write to local store
        const remoteInfos = await this.remote.getAllDiagramsData()
        remoteInfos.forEach(data => this.local.writeDiagramInfo(data))

        // Get local diagram infos to check if to be deleted or published
        const localInfos = this.local.readAllDiagramsInfos()

        for (let i = 0; i < localInfos.length; i++) {
            const localInfo = localInfos[i];
            const isRemote = remoteInfos.find(remoteInfo => remoteInfo.diagramId === localInfo.diagramId)
            if (!isRemote) {
                // The local info is not a remote info
                console.log('local', localInfo)
                if (localInfo.etag) {
                    // The local info was a remote, but no longer
                    if (currentId === localInfo.diagramId) {
                        // Is the current diagram, lets re-add the diagram to remote
                        const diagram = this.local.readDiagram(localInfo.diagramId)
                        if (diagram) {
                            console.log('push', diagram)
                            const newInfo = await this.remote.newDiagram(diagram)

                            // Update local diagram info
                            this.local.writeDiagramInfo(newInfo)
                        }
                    } else {
                        // Local info can just be deleted since it was deleted on the server
                        console.log('Remove', localInfo.diagramId)
                        this.local.removeDiagram(localInfo.diagramId)
                    }
                } else {
                    // The local info never pushed, lets push to remote
                    const diagram = this.local.readDiagram(localInfo.diagramId)
                    if (diagram) {
                        console.log('push', diagram)
                        const newInfo = await this.remote.newDiagram(diagram)

                        // Update local diagram info
                        this.local.writeDiagramInfo(newInfo)
                    }
                }
            }
        }
    }

}