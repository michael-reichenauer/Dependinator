import Api from "./Api"
import StoreFiles from "./StoreFiles"
import StoreLocal from "./StoreLocal"
//import { delay } from '../../common/utils'

const rootCanvasId = 'root'


class Store {
    files = new StoreFiles()
    local = new StoreLocal()
    remote = new Api()

    setError = null
    setProgress = null
    isSyncEnabled = false

    isCloudSyncEnabled = () => this.isSyncEnabled
    isLocal = () => window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'


    setHandlers(setError, setProgress) {
        this.setError = setError
        this.setProgress = setProgress
        this.remote.setProgressHandler(setProgress)
    }


    async initialize() {
        console.log('initialize')
        let sync = this.local.getSync()
        let didConnect = false

        console.log('sync', sync)
        if (sync.isConnecting) {
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
            return
        }

        if (!didConnect) {
            await this.remote.check()
        }

        this.remote.setToken(sync.token)
        this.isSyncEnabled = true
        await this.syncDiagrams()
        console.log('Sync is enabled')
    }

    async openMostResentDiagramCanvas() {
        const diagramId = this.getMostResentDiagramId()
        if (!diagramId) {
            throw new Error('No resent diagram')
        }

        return this.openDiagramRootCanvas(diagramId)
    }


    async openDiagramRootCanvas(diagramId) {
        try {
            if (this.isSyncEnabled) {
                // Try to get diagram from remote server and cache locally
                const diagram = await this.remote.getDiagram(diagramId)
                this.local.writeDiagram(diagram)

                // Now read the root canvas from local store
                return this.local.readCanvas(diagramId, rootCanvasId)
            }

            // Local mode: read the root canvas from local store
            const canvas = this.local.readCanvas(diagramId, rootCanvasId)
            if (!canvas) {
                throw new Error('Diagram not found')
            }

            this.local.updateAccessedDiagram(canvas.diagramId)
            return canvas
        } catch (error) {
            this.local.removeDiagram(diagramId)
            throw error
        }
        finally {
            await this.syncDiagrams()
        }
    }


    login(provider) {
        this.local.updateSync({ isConnecting: true, provider: provider })
        if (provider === 'Local') {
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

    getMostResentDiagramId() {
        return this.getRecentDiagramInfos()[0]?.diagramId
    }

    async newDiagram(diagramId, name, canvas) {
        const diagram = {
            diagramInfo: { diagramId: diagramId, name: name, accessed: Date.now() },
            canvases: [canvas]
        }
        this.local.writeDiagram(diagram)

        if (this.isSyncEnabled) {
            // Sync with remote server
            const diagramInfo = await this.remote.newDiagram(diagram)
            this.local.writeDiagramInfo(diagramInfo)
        }
    }

    setCanvas(canvas) {
        this.local.writeCanvas(canvas)
        this.local.updateAccessedDiagram(canvas.diagramId)

        if (this.isSyncEnabled) {
            // Sync with remote server
            this.remote.setCanvas(canvas)
                .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
                .catch(error => this.setError('Failed to sync canvas change'))
        }
    }

    async deleteDiagram(diagramId) {
        this.local.removeDiagram(diagramId)

        console.log('Delete', diagramId)
        if (this.isSyncEnabled) {
            await this.remote.deleteDiagram(diagramId)
            await this.syncDiagrams()
        }
    }

    setDiagramName(diagramId, name) {
        this.local.updateDiagramInfo(diagramId, { name: name })

        if (this.isSyncEnabled) {
            this.remote.updateDiagram({ diagramInfo: { diagramId: diagramId, name: name } })
                .then(diagramInfo => this.local.writeDiagramInfo(diagramInfo))
                .catch(error => this.setError('Failed to sync name change'))
        }
    }

    getCanvas(diagramId, canvasId) {
        return this.local.readCanvas(diagramId, canvasId)
    }

    getRecentDiagramInfos() {
        return this.local.readAllDiagramsInfos()
            .sort((i1, i2) => i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0)
            .reverse()
    }

    async loadDiagramFromFile() {
        const file = await this.files.loadFile()

        if (this.isSyncEnabled) {
            // Store all read diagram
            await this.remote.uploadDiagrams(file.diagrams)
            await this.syncDiagrams()
        } else {
            file.diagrams.forEach(diagram => this.local.writeDiagram(diagram))
        }
        const firstDiagramId = file.diagrams[0]?.diagramInfo.diagramId
        if (!firstDiagramId) {
            throw new Error('No diagram in file')
        }
        return firstDiagramId
    }

    saveDiagramToFile(diagramId) {
        const diagram = this.local.readDiagram(diagramId)
        if (diagram == null) {
            return
        }

        const file = { diagrams: [diagram] }
        this.files.saveFile(`${diagram.diagramInfo.name}.json`, file)
    }

    async saveAllDiagramsToFile() {
        let diagrams = []
        if (this.isSyncEnabled) {
            diagrams = await this.remote.downloadAllDiagrams()
        } else {
            diagrams = this.local.readAllDiagrams()
        }

        const file = { diagrams: diagrams }
        this.files.saveFile(`diagrams.json`, file)
    }

    clearLocalData() {
        this.local.clearAllData()
    }


    async syncDiagrams() {
        console.log('Syncing')
        if (!this.isSyncEnabled) {
            console.log('Syncing not enabled')
            return
        }

        const currentId = this.getMostResentDiagramId()

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

    // For printing 
    getDiagram(diagramId) {
        return this.local.readDiagram(diagramId)
    }
}

export const store = new Store()

